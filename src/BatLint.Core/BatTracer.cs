using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace BatLint.Core
{
    /// <summary> bat 调试注入器：插入执行轨迹与变量快照 / Injects trace markers and variable snapshots. </summary>
    internal static class BatTracer
    {
        private const string TracePrefix = "@echo [TRACE:";
        private const string VarsBegin = "@echo [VARS:";
        private const string VarsEnd = "@echo [ENDVARS]";

        /// <summary> 注入轨迹标记；括号块内部与续行不插 / Inject trace marks, skipping block bodies. </summary>
        public static List<string> Inject(IReadOnlyList<string> lines, IReadOnlyCollection<int> breakLines)
        {
            var result = new List<string>(lines.Count * 2);
            var vars = CollectVars(lines);
            var breaks = new HashSet<int>(breakLines);
            var depth = 0;
            var continued = false;

            for (var i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                var no = i + 1;
                var trimmed = line.TrimStart();

                var safe = depth == 0
                           && !continued
                           && trimmed.Length > 0
                           && !trimmed.StartsWith("(")
                           && !trimmed.StartsWith(")")
                           && !IsComment(trimmed)
                           && !trimmed.EndsWith("^");

                if (safe)
                {
                    result.Add($"{TracePrefix}{no}]");
                    if (breaks.Contains(no))
                    {
                        result.Add($"{VarsBegin}{no}]");
                        foreach (var v in vars) result.Add($"@set {v}");
                        result.Add(VarsEnd);
                    }
                }

                result.Add(line);
                depth += BareDelta(line);
                if (depth < 0) depth = 0;
                continued = trimmed.EndsWith("^");
            }

            return result;
        }

        /// <summary> 提取脚本中引用的变量名 / Collect variable names referenced in the script. </summary>
        private static IEnumerable<string> CollectVars(IReadOnlyList<string> lines)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var regex = new Regex(@"%([A-Za-z_][\w]*)%");

            foreach (var line in lines)
            {
                foreach (Match match in regex.Matches(line)) set.Add(match.Groups[1].Value);
            }

            return set;
        }

        /// <summary> 统计引号外的括号深度变化 / Bracket depth delta outside quotes. </summary>
        private static int BareDelta(string line)
        {
            var delta = 0;
            var inQuote = false;

            foreach (var c in line)
            {
                if (c == '"') inQuote = !inQuote;
                else if (!inQuote && c == '(') delta++;
                else if (!inQuote && c == ')') delta--;
            }

            return delta;
        }

        /// <summary> 是否注释行 / Whether the line is a comment. </summary>
        private static bool IsComment(string trimmed) =>
            trimmed.StartsWith("::") ||
            (trimmed.StartsWith("rem", StringComparison.OrdinalIgnoreCase) &&
             (trimmed.Length == 3 || char.IsWhiteSpace(trimmed[3])));

        /// <summary> 把原始输出整理成带行号与作用说明的可读文本 / Render output with line numbers and descriptions. </summary>
        public static string Format(string raw, IReadOnlyDictionary<int, string> descriptions = null)
        {
            var sb = new StringBuilder();
            var inVars = false;

            foreach (var line in raw.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
            {
                var t = line.Trim();

                if (t.StartsWith("[TRACE:", StringComparison.Ordinal))
                {
                    var no = t.Substring(7).TrimEnd(']');
                    sb.AppendLine();
                    sb.AppendLine($"───── 第 {no} 行 ─────");

                    if (descriptions != null && int.TryParse(no, out var n) && descriptions.TryGetValue(n, out var desc))
                        sb.AppendLine($"    ▸ {desc}");
                    continue;
                }

                if (t.StartsWith("[VARS:", StringComparison.Ordinal))
                {
                    inVars = true;
                    sb.AppendLine($"〔变量快照 @ {t.Substring(6).TrimEnd(']')}〕");
                    continue;
                }

                if (t.StartsWith("[ENDVARS]", StringComparison.Ordinal))
                {
                    inVars = false;
                    continue;
                }

                sb.AppendLine(inVars ? "    " + line : line);
            }

            return sb.ToString();
        }
    }
}
