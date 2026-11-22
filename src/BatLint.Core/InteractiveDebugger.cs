using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BatLint.Core
{
    /// <summary>
    /// 交互式批处理调试（方案 A：插桩 + stdin 驱动）。
    ///
    /// 原理：cmd.exe 没有行级钩子，但批处理是「同一进程逐行执行」的。所以生成一份插桩副本：
    ///   · 每个可执行语句前插入 call :__batdbg N
    ///   · 探针打印 @__BDBG__ N，然后 set /p 从 stdin 读一个命令字符
    ///   · IDE 持有 stdin 写端：想停就停，想看变量发 d（探针 dump 环境变量），
    ///     放行发 c，单步发 n，终止发 q
    /// 因为跑的还是同一个 cmd 进程，setlocal / 延迟展开 / %ERRORLEVEL% 全部保持原样。
    /// </summary>
    internal static class InteractiveDebugger
    {
        /// <summary> 探针输出的行标记 / Marker printed by the probe. </summary>
        public const string Marker = "@__BDBG__";

        /// <summary> 变量 dump 的结束标记 / End of a variable dump. </summary>
        public const string DumpEnd = "@__BDBGEND__";

        /// <summary> 用户中止时的退出码 / Exit code when the user aborts. </summary>
        public const int AbortExitCode = 199;

        private const string ProbeLabel = "__batdbg";

        /// <summary>
        /// 生成插桩副本。布局：跳过探针 → 探针定义 → 主脚本（带 call 探针）。
        /// 括号块内部、续行、标签行、注释行都不插探针（会破坏块结构或改变语义）。
        /// </summary>
        public static string BuildScript(string text, IReadOnlyCollection<int> breakpoints, bool probeEveryLine = true)
        {
            var lines = SplitLines(text, out _);
            var sb = new StringBuilder();

            sb.Append("@echo off\r\n");
            sb.Append("setlocal EnableExtensions\r\n");
            sb.Append("goto :__bdbg_main\r\n\r\n");
            sb.Append(ProbeStub());
            sb.Append(":__bdbg_main\r\n");

            var depth = 0;
            var inContinuation = false;
            var breaks = new HashSet<int>(breakpoints ?? (IReadOnlyCollection<int>)Array.Empty<int>());
            var lineNo = 0;

            foreach (var raw in lines)
            {
                lineNo++;
                var t = raw.Trim();

                var isComment = t.StartsWith("::") ||
                                (t.StartsWith("rem", StringComparison.OrdinalIgnoreCase) &&
                                 (t.Length == 3 || char.IsWhiteSpace(t[3])));
                var isLabel = t.StartsWith(":") && !t.StartsWith("::");
                var continues = t.EndsWith("^", StringComparison.Ordinal);

                var injectable = !isComment && !isLabel && t.Length > 0 && depth == 0 && !inContinuation;
                if (injectable && (probeEveryLine || breaks.Contains(lineNo)))
                    sb.Append("call :").Append(ProbeLabel).Append(' ').Append(lineNo).Append("\r\n");

                sb.Append(raw).Append("\r\n");

                inContinuation = continues;
                depth += DepthDelta(t);
                if (depth < 0) depth = 0;
            }

            sb.Append("\r\n:__bdbg_end\r\n");
            sb.Append("exit /b 0\r\n");
            return sb.ToString();
        }

        /// <summary> 探针本体（作为子过程放在脚本前面）/ The probe subroutine. </summary>
        private static string ProbeStub()
        {
            var sb = new StringBuilder();
            sb.Append(":").Append(ProbeLabel).Append("\r\n");
            sb.Append("set \"__bdbg_el=%ERRORLEVEL%\"\r\n");
            sb.Append("echo ").Append(Marker).Append(" %1\r\n");
            sb.Append(":").Append(ProbeLabel).Append("_wait\r\n");
            sb.Append("set \"__bdbg_cmd=\"\r\n");
            sb.Append("set /p \"__bdbg_cmd=\"\r\n");
            sb.Append("if not defined __bdbg_cmd goto :").Append(ProbeLabel).Append("_wait\r\n");
            sb.Append("if /i \"%__bdbg_cmd%\"==\"d\" ( set & echo ").Append(DumpEnd)
              .Append(" & goto :").Append(ProbeLabel).Append("_wait )\r\n");
            sb.Append("if /i \"%__bdbg_cmd%\"==\"q\" ( exit ").Append(AbortExitCode).Append(" )\r\n");
            sb.Append("exit /b %__bdbg_el%\r\n\r\n");
            return sb.ToString();
        }

        /// <summary> 解析探针标记行，取出原始行号 / Parse the marker line. </summary>
        public static bool TryParseMarker(string line, out int lineNumber)
        {
            lineNumber = 0;
            if (string.IsNullOrEmpty(line)) return false;

            var index = line.IndexOf(Marker, StringComparison.Ordinal);
            if (index < 0) return false;

            var rest = line.Substring(index + Marker.Length).TrimStart();
            var digits = new string(rest.TakeWhile(char.IsDigit).ToArray());
            return int.TryParse(digits, out lineNumber);
        }

        /// <summary> 解析环境变量 dump（name=value）/ Parse the dump. </summary>
        public static Dictionary<string, string> ParseVariables(IEnumerable<string> lines)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (line.StartsWith(DumpEnd, StringComparison.Ordinal)) break;

                var eq = line.IndexOf('=');
                if (eq <= 0) continue;

                map[line.Substring(0, eq)] = line.Substring(eq + 1);
            }
            return map;
        }

        /// <summary> 脚本里出现过的变量名（用于把 dump 收敛到关心范围）/ Variables referenced by the script. </summary>
        public static HashSet<string> ReferencedVariables(string text)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var lines = SplitLines(text, out _);

            foreach (var line in lines)
            {
                var i = 0;
                while (i < line.Length)
                {
                    var c = line[i];
                    if (c != '%' && c != '!') { i++; continue; }

                    var end = line.IndexOf(c, i + 1);
                    if (end < 0) break;

                    var inner = line.Substring(i + 1, end - i - 1).Trim();
                    if (inner.Length > 0 && (char.IsLetter(inner[0]) || inner[0] == '_'))
                    {
                        var colon = inner.IndexOf(':');
                        var name = colon > 0 ? inner.Substring(0, colon) : inner;
                        if (name.Length > 0 && name.All(ch => char.IsLetterOrDigit(ch) || ch == '_'))
                            names.Add(name);
                    }

                    i = end + 1;
                }
            }

            return names;
        }

        /// <summary> 统计引号外的括号深度变化 / Bracket depth delta outside quotes. </summary>
        private static int DepthDelta(string line)
        {
            var inQuote = false;
            var delta = 0;

            foreach (var c in line)
            {
                if (c == '"') { inQuote = !inQuote; continue; }
                if (inQuote) continue;
                if (c == '(') delta++;
                else if (c == ')') delta--;
            }

            return delta;
        }

        private static string[] SplitLines(string text, out int[] offsets)
            => ExtraRules.SplitLines(text, out offsets);
    }
}
