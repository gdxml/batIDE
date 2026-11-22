using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace BatLint.Core
{
    /// <summary>
    /// 第三批规则（R70~R79）。全部先用 cmd.exe 实测过行为：
    ///   z2  set A=x&amp;y        → 变量只拿到 x（赋值被 &amp; 截断）
    ///   z3  type nul &gt;         → exit 255（重定向缺目标）
    ///   z4  tokens=1 用 %%b     → 原样输出 %b（超范围不替换）
    ///   z5  if ""==""           → 恒真
    ///   z6  @echo off 后再加 @  → 冗余
    ///   z9  echo %%i（for 外）  → 输出 %i（不替换）
    ///   z11 goto :my label      → exit 1（标签不能带空格）
    ///   z12 del /s /q（无目标） → exit 1
    ///   z13 if exist nul ( )    → exit 255（空括号块）
    /// </summary>
    internal static class ExtraRules2
    {
        private const int MaxIssues = 500;

        /// <summary> for 的循环变量 / loop variable of a for statement. </summary>
        private static readonly Regex ForLoopVar = new Regex(@"\bfor\b[^\r\n]*?%%([A-Za-z])\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary> for 的 do 之后（循环体）/ body after do. </summary>
        private static readonly Regex ForBody = new Regex(@"\bdo\b(.*)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary> 单行 for + 单个 > 重定向（会反复清空同一个文件）/ single-line for with a single >. </summary>
        private static readonly Regex ForSingleRedirect = new Regex(@"[^>0-9]>[^>&|]", RegexOptions.Compiled);

        private static readonly Regex RedundantAt = new Regex(@"^@\s*[^@]", RegexOptions.Compiled);

        private static readonly Regex SetUnquotedAmp = new Regex(
            @"^@?set\s+(?!/[ap])(?!"")([A-Za-z_]\w*)=([^&|""]*)[&|]",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex RedirectNoTarget = new Regex(
            @"[^>0-9]>>?\s*$",
            RegexOptions.Compiled);

        private static readonly Regex ForFTokens = new Regex(
            @"^for\s+/f\s+""([^""]*tokens\s*=\s*([0-9,\-\* ]+)[^""]*)""\s+%%([A-Za-z])\s+in",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex EmptyQuotesCompare = new Regex(
            @"^if\s+(?:not\s+)?""""\s*==\s*""""",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex DoublePercentVar = new Regex(@"%%([A-Za-z])(?![%A-Za-z0-9_])", RegexOptions.Compiled);

        private static readonly Regex GotoWithSpace = new Regex(
            @"^goto\s+:([^&|\r\n]*\s+[^&|\r\n]*)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex DelNoTarget = new Regex(
            @"^(?:del|erase)(?:\s+/[a-zA-Z]+)*\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex EmptyBlock = new Regex(@"\(\s*\)", RegexOptions.Compiled);

        private static readonly Regex CdPercent = new Regex(@"%cd%\\", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex ForBlockStart = new Regex(@"^for\b.*\bdo\s*\(", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary> 跑第三批规则 / Run the third rule batch. </summary>
        public static List<LintIssue> Check(string text) => Check(text, null, null);

        /// <summary> 复用调用方已切好的行/偏移。 </summary>
        public static List<LintIssue> Check(string text, string[] preLines, int[] preOffsets)
        {
            var issues = new List<LintIssue>();
            if (string.IsNullOrEmpty(text)) return issues;

            int[] computed = null;
            var lines = preLines ?? SplitLines(text, out computed);
            var offsets = preOffsets ?? computed;
            var seenEchoOff = false;
            var forDepth = 0;

            for (var i = 0; i < lines.Length; i++)
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

                var startsForBlock = ForBlockStart.IsMatch(t);
                var forBody = forDepth > 0 || startsForBlock;

                if (!comment)
                {
                    var isEchoOff = (feat & FastScan.F.EchoOff) != 0 && t.TrimStart('@').TrimStart()
                        .StartsWith("echo off", StringComparison.OrdinalIgnoreCase);

                    // R70：@echo off 之后的冗余 @
                    if (seenEchoOff && RedundantAt.IsMatch(t) && !isEchoOff)
                        Add(issues, offsets[i], indent, t, no,
                            "@echo off 之后的行不必再加 @（回显已经关闭），多余的 @ 会降低可读性", "R70");

                    // R71：set 值含未转义 & / |，赋值被截断
                    if ((feat & FastScan.F.Set) != 0 && SetUnquotedAmp.IsMatch(t))
                        Add(issues, lines[i].StartsWith("@") ? offsets[i] : offsets[i], indent, t, no,
                            "set 的值里有未加引号的 & 或 |，赋值会在那里被截断（cmd 会把后半段当命令执行）；应写 set \"A=值\"", "R71");

                    // R72：重定向缺目标
                    if ((feat & FastScan.F.Redirect) != 0 && RedirectNoTarget.IsMatch(t))
                        Add(issues, offsets[i], indent, t, no,
                            "重定向符后面缺少目标文件；cmd 会报「命令语法不正确」", "R72");

                    // R73：for /f tokens=N 却用超范围的 %%变量
                    var tok = ForFTokens.Match(t);
                    if (tok.Success)
                    {
                        var maxToken = MaxTokenOf(tok.Groups[2].Value);
                        var loopVar = tok.Groups[3].Value[0];
                        var used = tok.Groups[3].Value[0] - 'a';
                        var offsetInLine = t.IndexOf("%%" + tok.Groups[3].Value, StringComparison.Ordinal);
                        if (offsetInLine >= 0)
                            CheckTokenRange(issues, t, offsets[i] + indent, no, maxToken, loopVar);
                    }

                    // R74：if ""=="" 恒真
                    if ((feat & FastScan.F.If) != 0 && EmptyQuotesCompare.IsMatch(t))
                        Add(issues, offsets[i], indent, t, no,
                            "两侧都是空引号，条件恒为真；应改成判断变量，如 if not \"%VAR%\"==\"\"", "R74");

                    // R75：%%x 用在 for 之外
                    if (!forBody && (feat & FastScan.F.DoublePercent) != 0 && DoublePercentVar.IsMatch(t)
                        && !t.StartsWith("for", StringComparison.OrdinalIgnoreCase))
                    {
                        var m = DoublePercentVar.Match(t);
                        AddAt(issues, offsets[i] + indent + m.Index, m.Length, no,
                            $"%%{m.Groups[1].Value} 只在 for 循环里有效，循环外不会替换（cmd 会原样输出 %{m.Groups[1].Value}）", "R75");
                    }

                    // R76：goto 目标含空格
                    if ((feat & FastScan.F.Goto) != 0 && GotoWithSpace.IsMatch(t))
                        Add(issues, offsets[i], indent, t, no,
                            "标签名不能带空格；goto 会把后半段当命令，cmd 直接报错", "R76");

                    // R77：del 缺目标
                    if ((feat & FastScan.F.Del) != 0 && DelNoTarget.IsMatch(t))
                        Add(issues, offsets[i], indent, t, no,
                            "del/erase 只有开关没有目标文件，cmd 会报错退出", "R77");

                    // R78：空括号块
                    if ((feat & FastScan.F.ParenOpen) != 0 && EmptyBlock.IsMatch(t) && !IsIfEmptyCompare(t))
                        Add(issues, offsets[i], indent, t, no,
                            "空的 ( ) 块是语法错误（cmd 报「此时不应有 )」）；要么填内容，要么删掉", "R78");

                    // R80：悬空 if（条件后面没有命令）—— cmd 报「命令语法不正确」，实测 exit 255
                    if ((feat & FastScan.F.If) != 0 && IsDanglingIf(t, out var ifCondition))
                        Add(issues, offsets[i], indent, t, no,
                            $"if 条件 `{ifCondition}` 后面没有命令，cmd 会直接报「命令语法不正确」（实测 exit 255）；" +
                            "要么把命令写在同一行，要么用 if ... ( 多行 )。下一行缩进的语句不会成为 if 的分支", "R80");

                    // R81：for 循环体里把循环变量写成了裸字母（cmd 会原样输出）
                    if ((feat & FastScan.F.For) != 0 && ForBodyMentionsBareLoopVar(t, out var bareVar))
                        Add(issues, offsets[i], indent, t, no,
                            $"循环体里出现了裸字母 {bareVar}，但循环变量是 %%{bareVar}；cmd 会原样输出 {bareVar}，应写 %%{bareVar}", "R81");

                    // R82：单行 for 里用 > 反复覆盖同一个文件
                    if ((feat & FastScan.F.For) != 0 && IsLoopOverwrite(t))
                        Add(issues, offsets[i], indent, t, no,
                            "循环内用单个 > 写文件，每轮都会清空上一次的内容；要累积请用 >>", "R82");

                    // R79：用 %CD% 拼路径
                    if ((feat & FastScan.F.Percent) != 0 && CdPercent.IsMatch(t))
                        Add(issues, lines[i].StartsWith("@") ? offsets[i] : offsets[i], indent, t, no,
                            "%CD% 是运行时目录，脚本被别处调用时会指错；拼脚本自身路径请用 %~dp0", "R79");

                    if (isEchoOff) seenEchoOff = true;
                }

                // 维护 for 块深度（用于 R75）
                if (startsForBlock) forDepth++;
                else if (forDepth > 0 && t.StartsWith(")", StringComparison.Ordinal)) forDepth--;
            }

            return issues.Take(MaxIssues).ToList();
        }

        /// <summary> 增量通道：只跑 [fromLine, toLine] 的行内规则（R70~R74、R76~R79；R75 需要 for 上下文，放全量） </summary>
        public static void CheckLines(List<LintIssue> issues, string text, int fromLine, int toLine)
        {
            var lines = SplitLines(text, out var offsets);
            if (fromLine < 0) fromLine = 0;
            if (toLine >= lines.Length) toLine = lines.Length - 1;

            var seenEchoOff = lines.Take(Math.Max(0, fromLine))
                .Any(l => l.TrimStart('@').TrimStart().StartsWith("echo off", StringComparison.OrdinalIgnoreCase));

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

                var isEchoOff = (feat & FastScan.F.EchoOff) != 0 &&
                                t.TrimStart('@').TrimStart().StartsWith("echo off", StringComparison.OrdinalIgnoreCase);

                if (seenEchoOff && RedundantAt.IsMatch(t) && !isEchoOff)
                    Add(issues, offsets[i], indent, t, no, "@echo off 之后的行不必再加 @（回显已经关闭），多余的 @ 会降低可读性", "R70");

                if ((feat & FastScan.F.Set) != 0 && SetUnquotedAmp.IsMatch(t))
                    Add(issues, offsets[i], indent, t, no, "set 的值里有未加引号的 & 或 |，赋值会在那里被截断；应写 set \"A=值\"", "R71");

                if ((feat & FastScan.F.Redirect) != 0 && RedirectNoTarget.IsMatch(t))
                    Add(issues, offsets[i], indent, t, no, "重定向符后面缺少目标文件；cmd 会报「命令语法不正确」", "R72");

                var tok = ForFTokens.Match(t);
                if (tok.Success)
                {
                    var maxToken = MaxTokenOf(tok.Groups[2].Value);
                    CheckTokenRange(issues, t, offsets[i] + indent, no, maxToken, tok.Groups[3].Value[0]);
                }

                if ((feat & FastScan.F.If) != 0 && EmptyQuotesCompare.IsMatch(t))
                    Add(issues, offsets[i], indent, t, no, "两侧都是空引号，条件恒为真；应改成判断变量，如 if not \"%VAR%\"==\"\"", "R74");

                if ((feat & FastScan.F.Goto) != 0 && GotoWithSpace.IsMatch(t))
                    Add(issues, offsets[i], indent, t, no, "标签名不能带空格；goto 会把后半段当命令，cmd 直接报错", "R76");

                if ((feat & FastScan.F.Del) != 0 && DelNoTarget.IsMatch(t))
                    Add(issues, offsets[i], indent, t, no, "del/erase 只有开关没有目标文件，cmd 会报错退出", "R77");

                if ((feat & FastScan.F.ParenOpen) != 0 && EmptyBlock.IsMatch(t) && !IsIfEmptyCompare(t))
                    Add(issues, offsets[i], indent, t, no, "空的 ( ) 块是语法错误（cmd 报「此时不应有 )」）；要么填内容，要么删掉", "R78");

                if ((feat & FastScan.F.Percent) != 0 && CdPercent.IsMatch(t))
                    Add(issues, offsets[i], indent, t, no, "%CD% 是运行时目录，脚本被别处调用时会指错；拼脚本自身路径请用 %~dp0", "R79");

                if (isEchoOff) seenEchoOff = true;
            }
        }

        /// <summary> 解析 tokens= 里的最大 token 序号 / Largest token index. </summary>
        private static int MaxTokenOf(string spec)
        {
            var max = 1;
            foreach (var part in spec.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var p = part.Trim();
                if (p.Length == 0) continue;

                var dash = p.IndexOf('-');
                if (dash > 0 && int.TryParse(p.Substring(dash + 1), out var high)) max = Math.Max(max, high);
                else if (int.TryParse(p, out var single)) max = Math.Max(max, single);
            }
            return max;
        }

        /// <summary> for /f 里用了超出 tokens 范围的循环变量 / loop var beyond tokens. </summary>
        private static void CheckTokenRange(List<LintIssue> issues, string line, int lineStart, int no, int maxToken, char loopVar)
        {
            // %%a 对应第 1 个 token，%%b 对应第 2 个……超出范围不会替换
            var offset = 0;
            while (true)
            {
                var idx = line.IndexOf("%%", offset, StringComparison.Ordinal);
                if (idx < 0) break;
                offset = idx + 2;
                if (offset >= line.Length) break;

                var c = line[offset];
                if (c < 'a' || c > 'z') continue;

                var position = c - 'a' + 1;
                if (position <= maxToken) continue;

                issues.Add(new LintIssue
                {
                    Line = no,
                    Start = lineStart + idx,
                    Length = 3,
                    Message = $"tokens 只声明了前 {maxToken} 个字段，%%{c} 不会被替换（cmd 会原样输出 %{c}）；请扩大 tokens 或改用 %%{loopVar}",
                    Rule = "R73"
                });
                return;
            }
        }

        /// <summary> 判断 if 是否悬空（条件之后没有任何命令）。条件形态与 Grammar 保持一致。 </summary>
        private static bool IsDanglingIf(string line, out string condition)
        {
            condition = string.Empty;
            var t = line.TrimStart();
            if (!t.StartsWith("if", StringComparison.OrdinalIgnoreCase)) return false;
            if (t.Length > 2 && !char.IsWhiteSpace(t[2])) return false;

            var rest = t.Substring(2).TrimStart();
            if (rest.StartsWith("/i", StringComparison.OrdinalIgnoreCase)) rest = rest.Substring(2).TrimStart();
            if (rest.StartsWith("not", StringComparison.OrdinalIgnoreCase)) rest = rest.Substring(3).TrimStart();

            string afterCondition;
            var m = Regex.Match(rest, @"^(?:exist|defined|errorlevel)\s+\S+", RegexOptions.IgnoreCase);
            if (m.Success) afterCondition = rest.Substring(m.Length);
            else
            {
                var eq = rest.IndexOf("==", StringComparison.Ordinal);
                var op = Regex.Match(rest, @"\s(?:equ|neq|lss|leq|gtr|geq)\s", RegexOptions.IgnoreCase);

                if (eq >= 0)
                {
                    var right = rest.Substring(eq + 2).TrimStart();
                    var token = right.Split(new[] { ' ', '	' }, 2);
                    afterCondition = token.Length > 1 ? token[1] : string.Empty;
                }
                else if (op.Success)
                {
                    var right = rest.Substring(op.Index + op.Length).TrimStart();
                    var token = right.Split(new[] { ' ', '	' }, 2);
                    afterCondition = token.Length > 1 ? token[1] : string.Empty;
                }
                else
                {
                    return false;                       // 形态不认识，交给 Grammar 报
                }
            }

            condition = rest.Trim();
            return afterCondition.Trim().Length == 0;
        }

        /// <summary> 循环体里出现裸字母（漏写 %%）。 </summary>
        private static bool ForBodyMentionsBareLoopVar(string line, out char loopVar)
        {
            loopVar = ' ';

            var varMatch = ForLoopVar.Match(line);
            if (!varMatch.Success) return false;

            var bodyMatch = ForBody.Match(line);
            if (!bodyMatch.Success) return false;

            var body = bodyMatch.Groups[1].Value;
            var letter = varMatch.Groups[1].Value[0];
            if (body.IndexOf("%%" + letter, StringComparison.OrdinalIgnoreCase) >= 0) return false;

            var words = body.Split(new[] { ' ', '	', '(', ')', '=', ',', '&', '|' }, StringSplitOptions.RemoveEmptyEntries);
            if (!words.Any(w => w.Equals(letter.ToString(), StringComparison.OrdinalIgnoreCase))) return false;

            loopVar = letter;
            return true;
        }

        /// <summary> 单行 for 里用 > 覆盖同一个文件（排除 >nul 与 >>）。 </summary>
        private static bool IsLoopOverwrite(string line)
        {
            var bodyMatch = ForBody.Match(line);
            if (!bodyMatch.Success) return false;

            var body = bodyMatch.Groups[1].Value;
            foreach (Match m in ForSingleRedirect.Matches(body))
            {
                var rest = body.Substring(m.Index + 2).TrimStart();
                if (rest.StartsWith(">", StringComparison.Ordinal)) continue;      // >>
                if (rest.StartsWith("nul", StringComparison.OrdinalIgnoreCase)) continue;
                return true;
            }
            return false;
        }

        private static bool IsIfEmptyCompare(string line)
            => line.IndexOf("\"\"==\"\"", StringComparison.Ordinal) >= 0;

        private static void Add(List<LintIssue> list, int lineStart, int indent, string trimmed, int number, string message, string rule)
            => AddAt(list, lineStart + indent, Math.Max(1, trimmed.Length), number, message, rule);

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

        internal static string[] SplitLines(string text, out int[] offsets)
            => ExtraRules.SplitLines(text, out offsets);
    }
}
