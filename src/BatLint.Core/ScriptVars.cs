using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace BatLint.Core
{
    /// <summary> 脚本内 set 定义的变量，供补全与路径展开使用 / Variables defined by set in the script. </summary>
    internal sealed class ScriptVars
    {
        /// <summary> set 定义行 / A set statement. </summary>
        private static readonly Regex SetLine = new Regex(
            @"^\s*@?set\s+(?:/[ap]\s+)?\""?([A-Za-z_]\w*)\s*=(.*)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary> %NAME% 引用 / A %NAME% reference. </summary>
        private static readonly Regex VarRef = new Regex(@"%([A-Za-z_]\w*)%", RegexOptions.Compiled);

        private readonly Dictionary<string, string> _values;
        private readonly List<KeyValuePair<string, string>> _byLength;

        private ScriptVars(List<string> names, Dictionary<string, string> values)
        {
            Names = names;
            _values = values;

            // 按值的长度降序，保证最长前缀先匹配；Key 存路径值，Value 存变量名
            _byLength = values
                .Where(p => p.Value.TrimEnd('\\').Length > 2)
                .OrderByDescending(p => p.Value.Length)
                .Select(p => new KeyValuePair<string, string>(p.Value, p.Key))
                .ToList();
        }

        /// <summary> 脚本里出现过的变量名（去重，保持出现顺序）/ Distinct variable names in source order. </summary>
        public IReadOnlyList<string> Names { get; }

        /// <summary> 解析脚本文本 / Parse the whole script. </summary>
        public static ScriptVars Parse(string text)
        {
            var names = new List<string>();
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrEmpty(text))
            {
                foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
                {
                    var match = SetLine.Match(line);
                    if (!match.Success) continue;

                    var name = match.Groups[1].Value;
                    if (seen.Add(name)) names.Add(name);

                    var value = match.Groups[2].Value.Trim().Trim('"');
                    if (value.Length == 0 || value.IndexOf('%') >= 0) continue;   // 含引用的值无法静态展开
                    values[name] = value;
                }
            }

            return new ScriptVars(names, values);
        }

        /// <summary> 把 %NAME% 展开为真实路径 / Expand %NAME% references into real text. </summary>
        public string Expand(string token) =>
            VarRef.Replace(token, m =>
            {
                var name = m.Groups[1].Value;
                if (_values.TryGetValue(name, out var value)) return value;

                var environment = Environment.GetEnvironmentVariable(name);
                return environment ?? m.Value;
            });

        /// <summary> 把候选路径还原成 %NAME%\xxx 形式 / Collapse a candidate path back into %NAME%\xxx form. </summary>
        public string Collapse(string path)
        {
            foreach (var pair in _byLength)
            {
                var value = pair.Key.TrimEnd('\\');
                if (value.Length == 0) continue;
                if (!path.StartsWith(value, StringComparison.OrdinalIgnoreCase)) continue;
                if (path.Length > value.Length && path[value.Length] != '\\') continue;

                return $"%{pair.Value}%{path.Substring(value.Length)}";
            }

            return path;
        }
    }
}
