using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace BatLint.Core
{
    /// <summary> 一处外部程序依赖 / One external program dependency. </summary>
    internal sealed class Dependency
    {
        /// <summary> 程序名 / Program name. </summary>
        public string Name { get; set; }

        /// <summary> 行号（1 基）/ Line number, 1-based. </summary>
        public int Line { get; set; }
    }

    /// <summary> 扫描 bat 里调用的外部程序：这些会让「零依赖」承诺失效 / Scan external program calls that break the zero-dependency promise. </summary>
    internal static class DependencyScanner
    {
        /// <summary> 行首或命令分隔符后的程序名 / Program name after a line start or command separator. </summary>
        private static readonly Regex Call = new Regex(
            @"(?:^|[&|(]\s*|\bstart\s+)""?\s*(powershell|pwsh|python|python3|py|node|npm|npx|curl|wget|mshta|cscript|wscript|bitsadmin|java|dotnet)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary> 按行扫描，返回出现过的外部程序及其行号 / Scan line by line and report every external program. </summary>
        public static List<Dependency> Scan(string text)
        {
            var result = new List<Dependency>();
            if (string.IsNullOrEmpty(text)) return result;

            var lines = text.Replace("\r\n", "\n").Split('\n');
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < lines.Length; i++)
            {
                var t = lines[i].TrimStart();
                if (t.Length == 0 || t.StartsWith("::") || t.StartsWith("rem", StringComparison.OrdinalIgnoreCase)) continue;

                foreach (Match m in Call.Matches(t))
                {
                    var name = m.Groups[1].Value.ToLowerInvariant();
                    if (!seen.Add(name)) continue;
                    result.Add(new Dependency { Name = name, Line = i + 1 });
                }
            }

            return result;
        }
    }
}
