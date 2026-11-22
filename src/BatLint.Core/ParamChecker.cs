using System;
using System.Collections.Generic;
using System.Linq;

namespace BatLint.Core
{
    /// <summary>
    /// 参数级检查（方案 B）：开关拼错 / 参数不够 / 开关冲突 / 子命令拼错。
    /// 只对 CommandSpecs 里登记过的命令生效，其它命令整行当不透明参数。
    /// </summary>
    internal static class ParamChecker
    {
        /// <summary> 一条参数级问题 / One parameter-level finding. </summary>
        internal sealed class Finding
        {
            public Finding(string rule, int offset, int length, string message, string suggestion = null)
            {
                Rule = rule;
                Offset = offset;
                Length = length;
                Message = message;
                Suggestion = suggestion;
            }

            public string Rule { get; }
            public int Offset { get; }
            public int Length { get; }
            public string Message { get; }

            /// <summary> 建议替换成的名字（开关/子命令），供 Ctrl+. 用 / Suggested replacement. </summary>
            public string Suggestion { get; }
        }

        /// <summary> 检查一行；返回参数级问题（偏移相对行首）。 </summary>
        public static List<Finding> Check(string line)
        {
            var findings = new List<Finding>();
            if (string.IsNullOrWhiteSpace(line)) return findings;

            var tokens = BatchLexer.Tokenize(line);
            if (tokens.Count == 0) return findings;

            // 只处理行首命令（& 之后的命令按各自处理）
            var commandIndex = 0;
            while (commandIndex < tokens.Count && tokens[commandIndex].Kind != BatchLexer.Kind.Word) commandIndex++;
            if (commandIndex >= tokens.Count) return findings;

            var commandToken = tokens[commandIndex];
            var command = BatchLexer.NormalizeCommand(commandToken.Text).ToLowerInvariant();
            if (!CommandSpecs.All.TryGetValue(command, out var spec)) return findings;

            var rest = tokens.Skip(commandIndex + 1).ToList();
            if (rest.Count == 0)
            {
                // 单独一行命令名：让 help/其它规则处理
                return findings;
            }

            // 二级子命令（reg / sc）
            var operandTokens = new List<BatchLexer.Token>();

            if (spec.SubCommands != null)
            {
                var subToken = rest.FirstOrDefault(t => t.Kind == BatchLexer.Kind.Word);
                if (subToken == null) return findings;

                var sub = subToken.Text.ToLowerInvariant();
                if (!spec.SubCommands.TryGetValue(sub, out var subSpec))
                {
                    var near = SpellSuggest.Best(sub, CommandSpecs.SubCommandNames(command));
                    var hint = near == null ? string.Empty : $"，是否想写 {near}？";
                    findings.Add(new Finding("R56", subToken.Start, subToken.Text.Length,
                        $"{command} 没有子命令 {sub}{hint}（cmd 会报「无效语法」或「未知命令」）", near));
                    return findings;
                }

                spec = subSpec;
                rest = rest.SkipWhile(t => !ReferenceEquals(t, subToken)).Skip(1).ToList();
            }

            // 逐个 token：开关 / 操作数（key= 后面可能跟一个值 token）
            var usedSwitches = new List<string>();

            for (var index = 0; index < rest.Count; index++)
            {
                var token = rest[index];
                if (token.Kind == BatchLexer.Kind.Operator) continue;
                if (token.Kind != BatchLexer.Kind.Word) continue;

                var text = token.Text;

                // sc 的 key=value 形式（值可以写在 = 后面，也可以空格分开）
                if (IsScOption(text, out var optionName, out var optionValue))
                {
                    var known = spec.Find(optionName);
                    if (known == null)
                    {
                        var near = SpellSuggest.Best(optionName, spec.SwitchNames);
                        var hint = near == null ? string.Empty : $"，是否想写 {near}=？";
                        findings.Add(new Finding("R53", token.Start, token.Text.Length,
                            $"{DisplayName(command, spec)} 没有 {optionName}= 这个选项{hint}", near));
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(optionValue))
                    {
                        var next = index + 1 < rest.Count ? rest[index + 1] : null;
                        if (next != null && next.Kind == BatchLexer.Kind.Word && !next.Text.StartsWith("/") && !next.Text.StartsWith("-"))
                            index++;                                        // 把值当成选项的值，不计入操作数
                        else
                            findings.Add(new Finding("R54", token.Start, token.Text.Length,
                                $"{optionName}= 后面缺少值（cmd 会报「选项值无效」）"));
                    }

                    usedSwitches.Add(known.Name);
                    continue;
                }

                // 常规 /switch（robocopy 也接受 -switch）
                if (text.Length > 1 && (text[0] == '/' || text[0] == '-'))
                {
                    var normalized = Normalize(text);
                    if (normalized.Length == 0) continue;

                    var known = spec.Find(normalized);
                    if (known == null)
                    {
                        var near = SpellSuggest.Best(normalized, spec.SwitchNames);
                        var hint = near == null ? string.Empty : $"，是否想写 /{near}？";
                        findings.Add(new Finding("R53", token.Start, token.Text.Length,
                            $"{DisplayName(command, spec)} 没有 /{normalized} 这个开关{hint}", near));
                        continue;
                    }

                    usedSwitches.Add(known.Name);
                    continue;
                }

                operandTokens.Add(token);
            }

            // 参数数量
            if (operandTokens.Count < spec.MinOperands)
                findings.Add(new Finding("R54", commandToken.Start, commandToken.Text.Length,
                    $"{DisplayName(command, spec)} 至少需要 {spec.MinOperands} 个参数（现在给了 {operandTokens.Count} 个）"));

            // 冲突开关
            if (spec.SubCommands == null && CommandSpecs.Conflicts != null)
            {
                foreach (var conflict in CommandSpecs.Conflicts)
                {
                    if (!conflict.Command.Equals(command, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!usedSwitches.Contains(conflict.A) || !usedSwitches.Contains(conflict.B)) continue;

                    findings.Add(new Finding("R55", commandToken.Start, commandToken.Text.Length,
                        $"{command} 的 {conflict.Message}"));
                }
            }

            return findings;
        }

        /// <summary> 归一化开关名：去掉前导 / 或 -，去掉尾随冒号，转小写。 </summary>
        private static string Normalize(string token)
        {
            // /-y 与 /y 是不同开关：只去掉 '/', 保留 '-'
            var s = token.StartsWith("/-", StringComparison.Ordinal) ? token.Substring(1) : token.TrimStart('/', '-');
            var colon = s.IndexOf(':');
            if (colon >= 0) s = s.Substring(0, colon);
            return s.Trim().ToLowerInvariant();
        }

        /// <summary> sc 的 key=value 选项 / sc's key=value option. </summary>
        private static bool IsScOption(string text, out string name, out string value)
        {
            name = null;
            value = null;

            var eq = text.IndexOf('=');
            if (eq <= 0) return false;

            var key = text.Substring(0, eq).Trim();
            if (key.Length == 0) return false;
            if (!key.All(c => char.IsLetterOrDigit(c) || c == '_')) return false;

            name = key.ToLowerInvariant();
            value = text.Substring(eq + 1).Trim();
            return true;
        }

        private static string DisplayName(string command, CommandSpecs.CommandSpec spec)
            => spec.Name.Equals(command, StringComparison.OrdinalIgnoreCase) ? command : command + " " + spec.Name;
    }
}
