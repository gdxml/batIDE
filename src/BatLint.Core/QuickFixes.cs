using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace BatLint.Core
{
    /// <summary>
    /// 快速修复（S4）：把「诊断」变成「文本编辑」，供 Ctrl+. 使用。
    /// 设计照 Roslyn 的 CodeFix：
    ///   · 只对已有诊断生成修复，且修复内容确定（不猜）
    ///   · 一个修复 = 标题 + 一组编辑（Start/Length/NewText）
    ///   · 生成失败就返回空列表，绝不产出“可能对”的编辑
    /// </summary>
    internal static class QuickFixes
    {
        internal sealed class Edit
        {
            public Edit(int start, int length, string newText)
            {
                Start = start;
                Length = length;
                NewText = newText;
            }

            public int Start { get; }
            public int Length { get; }
            public string NewText { get; }

            public override string ToString() => $"[{Start},{Start + Length}) → \"{NewText}\"";
        }

        internal sealed class Fix
        {
            public Fix(string title, params Edit[] edits)
            {
                Title = title;
                Edits = edits.ToList();
            }

            public string Title { get; }
            public List<Edit> Edits { get; }
        }

        /// <summary> 为一条诊断生成可用的修复。 </summary>
        public static List<Fix> For(string text, LintIssue issue)
        {
            var fixes = new List<Fix>();
            if (text == null || issue == null) return fixes;

            var line = LineOf(text, issue.Line);
            var span = SafeSpan(text, issue);

            switch (issue.Rule)
            {
                case "R10":                                     // 括号没闭合
                    fixes.Add(new Fix("在文件末尾补上 )", new Edit(text.Length, 0, Environment.NewLine + ")")));
                    break;

                case "R49":                                     // for 缺 do
                    if (span.Length <= 0) break;
                    fixes.Add(new Fix("插入 do 关键字", new Edit(issue.Start, 0, " do")));
                    break;

                case "R47":                                     // 单个 = / 赋值符
                {
                    // 诊断跨度可能是整行：在行内定位那个 = 或 -= / += / *= / /=
                    var region = text.Substring(span.Start, Math.Max(0, span.Length));
                    var opMatch = Regex.Match(region, @"[-+*/]=(?!=)");
                    if (opMatch.Success)
                        fixes.Add(new Fix("把赋值符改成 ==", new Edit(span.Start + opMatch.Index, 2, "==")));
                    else
                    {
                        var eq = Regex.Match(region, @"(?<![=<>!])=(?!=)");
                        if (eq.Success)
                            fixes.Add(new Fix("改成 ==", new Edit(span.Start + eq.Index, 1, "==")));
                    }
                    break;
                }

                case "R34":                                     // 缺 @echo off
                    fixes.Add(new Fix("在首行插入 @echo off", new Edit(0, 0, "@echo off" + Environment.NewLine)));
                    break;

                case "R50":                                     // set 缺少 =
                {
                    var m = Regex.Match(line, @"^(\s*@?set\s+)(""?)([A-Za-z_]\w*)\2(\s+)");
                    if (m.Success)
                    {
                        var at = StartOfLine(text, issue.Line) + m.Length - 1;   // 那个空格
                        fixes.Add(new Fix("补上 = 号", new Edit(at, 1, "=")));
                    }
                    break;
                }

                case "R41":                                     // echo %VAR%
                {
                    var index = text.IndexOf("echo ", issue.Start, StringComparison.OrdinalIgnoreCase);
                    if (index >= 0 && index <= issue.Start + Math.Max(1, issue.Length))
                        fixes.Add(new Fix("改用 echo(%VAR%", new Edit(index + 4, 1, "(")));
                    break;
                }

                case "R8":                                      // if 比较缺引号
                {
                    var m = Regex.Match(line, @"%\w+%==");
                    if (m.Success)
                    {
                        var start = StartOfLine(text, issue.Line) + m.Index;
                        var left = text.Substring(start, m.Length - 2);
                        fixes.Add(new Fix("给左侧操作数加引号",
                            new Edit(start, 0, "\""),
                            new Edit(start + left.Length, 0, "\"")));
                    }
                    break;
                }
            }

            // 带「建议名」的诊断：直接把名字换掉
            if (!string.IsNullOrEmpty(issue.Suggestion) && span.Length > 0)
            {
                var old = text.Substring(span.Start, span.Length);
                var replacement = BuildReplacement(issue.Rule, old, issue.Suggestion);
                if (replacement != null && replacement != old)
                    fixes.Add(new Fix($"改成 {replacement}", new Edit(span.Start, span.Length, replacement)));
            }

            return fixes;
        }

        /// <summary> 把建议名套用回原写法（保留 / % ! 等包裹）。 </summary>
        private static string BuildReplacement(string rule, string old, string suggestion)
        {
            switch (rule)
            {
                case "R13":                                     // %NAME% → %SUGGEST%
                    if (old.StartsWith("%") && old.EndsWith("%"))
                        return "%" + suggestion + "%";
                    if (old.StartsWith("!") && old.EndsWith("!"))
                        return "!" + suggestion + "!";
                    return "%" + suggestion + "%";

                case "R53":                                     // /sw → /suggest
                    return old.StartsWith("-", StringComparison.Ordinal) ? "-" + suggestion : "/" + suggestion;

                case "R5":
                case "R26":                                     // :label → :suggest
                    return old.StartsWith(":", StringComparison.Ordinal) ? ":" + suggestion : suggestion;

                default:                                        // 命令名 / 子命令
                    return suggestion;
            }
        }

        private static (int Start, int Length) SafeSpan(string text, LintIssue issue)
        {
            var start = Math.Max(0, Math.Min(issue.Start, text.Length));
            var length = Math.Max(0, Math.Min(issue.Length, text.Length - start));
            return (start, length);
        }

        private static int StartOfLine(string text, int lineNumber)
        {
            var line = LineOf(text, lineNumber);
            if (string.IsNullOrEmpty(line)) return 0;

            var index = text.IndexOf(line, StringComparison.Ordinal);
            return index < 0 ? 0 : index;
        }

        private static string LineOf(string text, int lineNumber)
        {
            if (lineNumber <= 0) return string.Empty;
            var lines = text.Replace("\r\n", "\n").Split('\n');
            return lineNumber - 1 < lines.Length ? lines[lineNumber - 1] : string.Empty;
        }
    }
}
