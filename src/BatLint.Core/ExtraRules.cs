using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace BatLint.Core
{
    /// <summary>
    /// 第二批规则（R58~R69），独立成文件便于维护：
    /// 行内规则 + 少量需要跨行视野的规则（子过程结尾、未引用标签、setlocal 配对、参数守卫）。
    /// 全部先过 FastScan 的单遍特征掩码，再决定是否跑正则。
    /// </summary>
    internal static class ExtraRules
    {
        private const int MaxIssues = 500;

        // ── 行内 ──
        private static readonly Regex SetThenPercent = new Regex(
            @"^@?set\s+""?([A-Za-z_]\w*)=([^&|]*)[&|].*%\1%",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex ForLZeroStep = new Regex(
            @"^for\s+/l\s+%%?[A-Za-z]\s+in\s*\(\s*[^,]+,\s*0\s*,",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex SetADivZero = new Regex(
            @"^set\s+/a\s+[^=]+=\s*[^/]*/\s*0(?!\d)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex BadSubstring = new Regex(
            @"%([A-Za-z_]\w*):\s*,\s*-?\d+\s*%",
            RegexOptions.Compiled);

        private static readonly Regex EchoOffNoAt = new Regex(
            @"^echo\s+off\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex ErrorLevelStringCompare = new Regex(
            @"^if\s+(?:not\s+)?%errorlevel%\s*==\s*-?\d",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex DoubleRedirect = new Regex(
            @"[^>0-9]>[^>&|][^>]*[^>0-9]>[^>&|]",
            RegexOptions.Compiled);

        private static readonly Regex IfExistWildcard = new Regex(
            @"^if\s+(?:not\s+)?exist\s+""?[^""]*[*?]",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex UsesArg = new Regex(@"%(?:~[a-zA-Z*]+)?[1-9]", RegexOptions.Compiled);

        private static readonly Regex ArgGuard = new Regex(
            @"^(?:if\s+(?:not\s+)?""?%~?1""?\s*==|if\s+(?:not\s+)?defined\s+%~?1|if\s+""?%~?1""?\s*(?:equ|neq|gtr|lss|geq|leq))",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex LabelLine = new Regex(@"^:([A-Za-z0-9_\-.]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex CallTarget = new Regex(@"call\s+:([A-Za-z0-9_\-.]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex GotoTarget = new Regex(@"goto\s+:?([A-Za-z0-9_\-.]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex SubExit = new Regex(@"^exit\s*/b|^goto\s+:eof", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary> 跑一遍第二批规则 / Run the second rule batch. </summary>
        public static List<LintIssue> Check(string text) => Check(text, null, null);

        /// <summary> 复用调用方已切好的行/偏移，避免重复 SplitLines。 </summary>
        public static List<LintIssue> Check(string text, string[] preLines, int[] preOffsets)
        {
            var issues = new List<LintIssue>();
            if (string.IsNullOrEmpty(text)) return issues;

            int[] computed = null;
            var lines = preLines ?? SplitLines(text, out computed);
            var offsets = preOffsets ?? computed;

            var setlocalCount = 0;
            var endlocalCount = 0;
            var usesArg = false;
            var hasArgGuard = false;
            var labels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);   // label → line index
            var referenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var called = new HashSet<string>(StringComparer.OrdinalIgnoreCase);          // call :label 目标

            // 先扫一遍：标签表、引用表、setlocal 计数、参数守卫
            for (var i = 0; i < lines.Length; i++)
            {
                var t = lines[i].Trim();

                if (t.Length == 0) continue;

                if (t.StartsWith("setlocal", StringComparison.OrdinalIgnoreCase)) setlocalCount++;
                if (t.StartsWith("endlocal", StringComparison.OrdinalIgnoreCase)) endlocalCount++;

                if (UsesArg.IsMatch(t)) usesArg = true;
                if (ArgGuard.IsMatch(t)) hasArgGuard = true;

                if (t.StartsWith(":") && !t.StartsWith("::"))
                {
                    var m = LabelLine.Match(t);
                    if (m.Success) labels[m.Groups[1].Value] = i;
                }
                else
                {
                    foreach (Match m in GotoTarget.Matches(t)) referenced.Add(m.Groups[1].Value);
                    foreach (Match m in CallTarget.Matches(t))
                    {
                        referenced.Add(m.Groups[1].Value);
                        called.Add(m.Groups[1].Value);
                    }
                }
            }

            for (var i = 0; i < lines.Length; i++)
            {
                var raw = lines[i];
                var t = raw.Trim();
                if (t.Length == 0) continue;

                var no = i + 1;
                var feat = FastScan.Features(t);
                var comment = t.StartsWith("::") ||
                              (t.StartsWith("rem", StringComparison.OrdinalIgnoreCase) &&
                               (t.Length == 3 || char.IsWhiteSpace(t[3])));

                // ── 子过程结尾缺 exit /b（R68）
                if (!comment && t.StartsWith(":") && !t.StartsWith("::"))
                {
                    var m = LabelLine.Match(t);
                    if (m.Success && called.Contains(m.Groups[1].Value))
                    {
                        var terminated = false;
                        for (var j = i + 1; j < lines.Length; j++)
                        {
                            var body = lines[j].Trim();
                            if (body.StartsWith(":", StringComparison.Ordinal)) break;
                            if (SubExit.IsMatch(body)) { terminated = true; break; }
                        }

                        if (!terminated)
                            Add(issues, offsets[i], 0, t, no,
                                $"子过程 {t} 结尾没有 exit /b（或 goto :eof），执行完会继续往下跑进后面的代码", "R68");
                    }
                }

                if (comment) continue;

                // ── R58：同行 set 后立刻用 %VAR%
                if ((feat & FastScan.F.Set) != 0 && SetThenPercent.IsMatch(t))
                    Add(issues, offsets[i], 0, t, no,
                        "同一行里 set 之后马上用 %VAR% 取不到新值（整行先展开再执行）；改用 !VAR! 或拆成两行", "R58");

                // ── R59：for /l 步长为 0
                if ((feat & FastScan.F.ForL) != 0 && ForLZeroStep.IsMatch(t))
                    Add(issues, offsets[i], 0, t, no,
                        "for /l 的步长为 0，循环永远不会结束；步长应为正/负整数", "R59");

                // ── R60：set /a 除以 0
                if ((feat & FastScan.F.SetA) != 0 && SetADivZero.IsMatch(t))
                    Add(issues, offsets[i], 0, t, no,
                        "set /a 里除以 0 会报「除数为零」并中断该行；先判断除数再除", "R60");

                // ── R61：变量子串语法
                if ((feat & FastScan.F.Percent) != 0)
                {
                    foreach (Match m in BadSubstring.Matches(t))
                        AddAt(issues, offsets[i] + raw.IndexOf(t, StringComparison.Ordinal) + m.Index, m.Length, no,
                            $"变量子串语法错误 %{m.Groups[1].Value}:…%：冒号后要先给起始下标，例如 %{m.Groups[1].Value}:~0,3%", "R61");
                }

                // ── R62：echo off 缺 @
                if ((feat & FastScan.F.Echo) != 0 && EchoOffNoAt.IsMatch(t))
                    Add(issues, offsets[i], 0, t, no,
                        "echo off 前面缺 @，这一行本身会被回显；应写 @echo off", "R62");

                // ── R63：%ERRORLEVEL% 用 == 比数字
                if ((feat & FastScan.F.ErrorLevel) != 0 && ErrorLevelStringCompare.IsMatch(t))
                    Add(issues, offsets[i], 0, t, no,
                        "%ERRORLEVEL% 与数字用 == 是字符串比较，边界值会判错；应写 if errorlevel N 或 if %ERRORLEVEL% equ N", "R63");

                // ── R64：同一行两个 >
                if ((feat & FastScan.F.Redirect) != 0 && DoubleRedirect.IsMatch(t))
                    Add(issues, offsets[i], 0, t, no,
                        "同一行有两个 > 重定向，后一个会覆盖前一个的结果；要累加请用 >>", "R64");

                // ── R65：if exist 带通配符
                if ((feat & FastScan.F.IfExist) != 0 && IfExistWildcard.IsMatch(t))
                    Add(issues, offsets[i], 0, t, no,
                        "if exist 后面带通配符是按字面量判断的，结果不可靠；改用 for 遍历或 dir /b", "R65");
            }

            // ── R66：setlocal / endlocal 不配对
            if (setlocalCount > 0 && setlocalCount != endlocalCount)
                Add(issues, 0, 0, lines.Length > 0 ? lines[0].Trim() : string.Empty, 1,
                    $"setlocal 出现 {setlocalCount} 次、endlocal 出现 {endlocalCount} 次；作用域可能渗漏（脚本结束会隐式 endlocal，但中间的状态会带出去）", "R66");

            // ── R67：用了 %1 却没有参数守卫
            if (usesArg && !hasArgGuard)
                Add(issues, 0, 0, lines.Length > 0 ? lines[0].Trim() : string.Empty, 1,
                    "用了 %1 等参数但没有先检查参数；建议开头加 if \"%~1\"==\"\" ( echo 用法: %~nx0 参数 & exit /b 1 )", "R67");

            // ── R69：标签定义但没人引用
            foreach (var kv in labels)
            {
                if (kv.Key.Equals("eof", StringComparison.OrdinalIgnoreCase)) continue;
                if (referenced.Contains(kv.Key)) continue;

                var idx = kv.Value;
                Add(issues, offsets[idx], 0, lines[idx].Trim(), idx + 1,
                    $"标签 :{kv.Key} 定义了但没有任何 goto/call 引用（改名遗留的死代码？）", "R69");
            }

            return issues.Take(MaxIssues).ToList();
        }

        /// <summary> 增量通道：只检查 [fromLine, toLine] 的行内规则（不含跨行的标签/子过程/setlocal 统计）。 </summary>
        public static void CheckLines(List<LintIssue> issues, string text, int fromLine, int toLine)
        {
            var lines = SplitLines(text, out var offsets);
            if (fromLine < 0) fromLine = 0;
            if (toLine >= lines.Length) toLine = lines.Length - 1;

            for (var i = fromLine; i <= toLine && i < lines.Length; i++)
            {
                var raw = lines[i];
                var t = raw.Trim();
                if (t.Length == 0) continue;

                var no = i + 1;
                var indent = raw.Length - raw.TrimStart().Length;
                var feat = FastScan.Features(t);
                var comment = t.StartsWith("::") ||
                              (t.StartsWith("rem", StringComparison.OrdinalIgnoreCase) &&
                               (t.Length == 3 || char.IsWhiteSpace(t[3])));
                if (comment) continue;

                if ((feat & FastScan.F.Set) != 0 && SetThenPercent.IsMatch(t))
                    Add(issues, offsets[i], indent, t, no,
                        "同一行里 set 之后马上用 %VAR% 取不到新值（整行先展开再执行）；改用 !VAR! 或拆成两行", "R58");

                if ((feat & FastScan.F.ForL) != 0 && ForLZeroStep.IsMatch(t))
                    Add(issues, offsets[i], indent, t, no,
                        "for /l 的步长为 0，循环永远不会结束；步长应为正/负整数", "R59");

                if ((feat & FastScan.F.SetA) != 0 && SetADivZero.IsMatch(t))
                    Add(issues, offsets[i], indent, t, no,
                        "set /a 里除以 0 会报「除数为零」并中断该行；先判断除数再除", "R60");

                if ((feat & FastScan.F.Percent) != 0)
                {
                    foreach (Match m in BadSubstring.Matches(t))
                        AddAt(issues, offsets[i] + indent + m.Index, m.Length, no,
                            $"变量子串语法错误 %{m.Groups[1].Value}:…%：冒号后要先给起始下标，例如 %{m.Groups[1].Value}:~0,3%", "R61");
                }

                if ((feat & FastScan.F.Echo) != 0 && EchoOffNoAt.IsMatch(t))
                    Add(issues, offsets[i], indent, t, no,
                        "echo off 前面缺 @，这一行本身会被回显；应写 @echo off", "R62");

                if ((feat & FastScan.F.ErrorLevel) != 0 && ErrorLevelStringCompare.IsMatch(t))
                    Add(issues, offsets[i], indent, t, no,
                        "%ERRORLEVEL% 与数字用 == 是字符串比较，边界值会判错；应写 if errorlevel N 或 if %ERRORLEVEL% equ N", "R63");

                if ((feat & FastScan.F.Redirect) != 0 && DoubleRedirect.IsMatch(t))
                    Add(issues, offsets[i], indent, t, no,
                        "同一行有两个 > 重定向，后一个会覆盖前一个的结果；要累加请用 >>", "R64");

                if ((feat & FastScan.F.IfExist) != 0 && IfExistWildcard.IsMatch(t))
                    Add(issues, offsets[i], indent, t, no,
                        "if exist 后面带通配符是按字面量判断的，结果不可靠；改用 for 遍历或 dir /b", "R65");
            }
        }

        private static void Add(List<LintIssue> list, int lineStart, int indent, string trimmed, int number, string message, string rule)
            => AddAt(list, lineStart + indent, Math.Max(1, trimmed.TrimEnd().Length), number, message, rule);

        private static void AddAt(List<LintIssue> list, int start, int length, int number, string message, string rule)
        {
            if (list.Count >= MaxIssues) return;
            list.Add(new LintIssue
            {
                Line = number,
                Start = Math.Max(0, start),
                Length = Math.Max(1, length),
                Message = message,
                Rule = rule
            });
        }

        /// <summary> 切行并保留原始偏移（CRLF / LF 都支持）/ Split lines keeping real offsets. </summary>
        internal static string[] SplitLines(string text, out int[] offsets)
        {
            var lineList = new List<string>();
            var offsetList = new List<int>();

            var start = 0;
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] != '\n') continue;

                var end = i;
                if (end > start && text[end - 1] == '\r') end--;

                lineList.Add(text.Substring(start, end - start));
                offsetList.Add(start);
                start = i + 1;
            }

            if (start <= text.Length)
            {
                var end = text.Length;
                if (end > start && text[end - 1] == '\r') end--;
                lineList.Add(text.Substring(start, end - start));
                offsetList.Add(start);
            }

            offsets = offsetList.ToArray();
            return lineList.ToArray();
        }
    }
}
