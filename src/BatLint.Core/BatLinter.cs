using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace BatLint.Core
{
    /// <summary> 一处语法问题 / One lint finding. </summary>
    internal sealed class LintIssue
    {
        /// <summary> 行号（1 基）/ Line number, 1-based. </summary>
        public int Line { get; set; }

        /// <summary> 文档内起始偏移 / Start offset in the document. </summary>
        public int Start { get; set; }

        /// <summary> 波浪线长度 / Squiggle length. </summary>
        public int Length { get; set; }

        /// <summary> 中文说明 / Chinese message. </summary>
        public string Message { get; set; }

        /// <summary> 规则编号 / Rule id. </summary>
        public string Rule { get; set; }

        /// <summary> 问题定性，如 logicError / Kind of the issue, e.g. logicError. </summary>
        public string Kind => ErrorRules.KindOf(Rule);

        /// <summary> 形如 "logicError: 逻辑错误" / "<kind>: <label>". </summary>
        public string KindLabel => ErrorRules.Describe(Rule);

        /// <summary> 规则标题 / Short rule title. </summary>
        public string RuleTitle => ErrorRules.TitleOf(Rule);

        /// <summary> 修复建议（如最近的变量名/开关名），供 Ctrl+. 直接套用 / A concrete fix suggestion. </summary>
        public string Suggestion { get; set; }
    }

    /// <summary> bat 静态检查：可判定的常见错误 / Static checks for well-known batch mistakes. </summary>
    internal static class BatLinter
    {
        /// <summary> set 等号左侧带空格，如 set A = 1 / Space before the equals sign in set. </summary>
        internal static readonly Regex SetSpaced = new Regex(@"^set\s+[^""/][^=]*\s+=", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary> 引用 %NAME% 形式 / References of the %NAME% form. </summary>
        internal static readonly Regex PercentVar = new Regex(@"%([A-Za-z_]\w*)%", RegexOptions.Compiled);

        /// <summary> set 定义行 / A set statement. </summary>
        internal static readonly Regex SetDef = new Regex(
            @"^\s*@?set\s+(?:/[ap]\s+)?\""?([A-Za-z_]\w*)\s*=",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary> 进程环境变量名，用于判断引用是否有效 / Environment names used to validate references. </summary>
        private static readonly HashSet<string> EnvNames = new HashSet<string>(
            Environment.GetEnvironmentVariables().Keys.Cast<string>(),
            StringComparer.OrdinalIgnoreCase);

        /// <summary> cmd 内置动态变量 / Built-in dynamic variables. </summary>
        private static readonly HashSet<string> BuiltinNames = new HashSet<string>(
            CompletionSource.BuiltIn, StringComparer.OrdinalIgnoreCase);

        /// <summary> 引用 !NAME! 形式 / References of the !NAME! form. </summary>
        internal static readonly Regex BangVar = new Regex(@"![A-Za-z_]\w*!", RegexOptions.Compiled);

        /// <summary> for 里误用 %i / Single-percent loop variable. </summary>
        internal static readonly Regex SinglePercentLoop = new Regex(@"(?<!%)%[A-Za-z](?!%)", RegexOptions.Compiled);

        /// <summary> cd 到盘符根 / cd to a drive root. </summary>
        internal static readonly Regex CdDrive = new Regex(@"^cd\s+[A-Za-z]:", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary> goto 标签 / goto target. </summary>
        internal static readonly Regex GotoTarget = new Regex(@"^goto\s+:?(\w+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary> 行首直接调用另一个 bat / A bare .bat invocation at line start. </summary>
        internal static readonly Regex BareBatCall = new Regex(@"^[""]?[^""\s]+\.[Bb][Aa][Tt]\b", RegexOptions.Compiled);

        /// <summary> if %x%== 形式 / Unquoted if comparison. </summary>
        internal static readonly Regex IfUnquoted = new Regex(@"^if\s+(not\s+)?%\w+%==", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary> if 两侧都是字面量，如 if not 1==2（条件恒定）/ Both sides of if are literals. </summary>
        internal static readonly Regex IfConstCompare = new Regex(
            @"^@?if\s+(?:not\s+)?(?![%!])([\w.\-]+)\s*==\s*(?![%!])([\w.\-]+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary> 覆盖系统内置变量（自引用追加不算）/ Overwriting a built-in variable, excluding self-appends. </summary>
        internal static readonly Regex BuiltinSet = new Regex(
            @"^set\s+""?(PATH|TEMP|TMP|COMSPEC|SYSTEMROOT|WINDIR|USERPROFILE|APPDATA|PROGRAMFILES)=(?!%\1%)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);


        // ── R34~R45：常见写法问题 ──────────────────────────────────────

        /// <summary> cd "%~dp0" 但没带 /d / cd to %~dp0 without /d. </summary>
        internal static readonly Regex CdDp0NoD = new Regex(
            @"^cd\s+""?%~dp0", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary> 循环块里用单个 > 重定向（会每次清空）/ Single > inside a block. </summary>
        internal static readonly Regex SingleRedirect = new Regex(
            @"(?<![>\d&])>(?![>&])\s*""?([^""\s&|<>]+)", RegexOptions.Compiled);

        /// <summary> if exist 目标未加引号 / if exist with an unquoted target. </summary>
        internal static readonly Regex IfExistUnquoted = new Regex(
            @"^if\s+(?:not\s+)?exist\s+(?!"")(\S*[\\%])", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary> set /a 右侧是纯标识符 / set /a with a bare identifier on the right. </summary>
        internal static readonly Regex SetAValue = new Regex(
            @"^set\s+/a\s+([A-Za-z_]\w*)\s*=\s*([A-Za-z_]\w*)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary> 无引号的 %1 比较 / Unquoted %1 comparison. </summary>
        internal static readonly Regex IfArgUnquoted = new Regex(
            @"^if\s+(?:not\s+)?%[\d*](?!%)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary> echo %VAR% 独占一行 / echo of a lone variable. </summary>
        internal static readonly Regex EchoLoneVar = new Regex(
            @"^echo\s+%\w+%\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary> start 后跟未加引号的路径 / start with an unquoted path. </summary>
        internal static readonly Regex StartUnquotedPath = new Regex(
            @"^start\s+(?:""[^""]*""\s+)?(?!"")(\S*[\\%])", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary> exit 未带 /b / exit without /b. </summary>
        internal static readonly Regex ExitNoB = new Regex(
            @"^exit(?!\s*/b)(\s+.*)?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary> copy/move/xcopy 路径未加引号 / unquoted paths for copy-like commands. </summary>
        internal static readonly Regex CopyUnquoted = new Regex(
            @"^(copy|move|xcopy)\s+(?!"")(\S*[\\%])", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary> 首行 @echo off / The @echo off line. </summary>
        internal static readonly Regex EchoOffLine = new Regex(
            @"^@?echo\s+off\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // ── R14~R31：第二批规则 ──────────────────────────────────────────

        /// <summary> mkdir 的参数像文件名 / mkdir with a file-like argument. </summary>
        internal static readonly Regex MkdirFileLike = new Regex(
            @"^(?:mkdir|md)\s+(""?)([^""\r\n]*?\.[A-Za-z0-9]{1,5})\1\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary> 外部程序调用，破坏零依赖前提 / External program calls. </summary>
        internal static readonly Regex ExternalCall = new Regex(
            @"(?:^|[&|(]\s*|\bstart\s+""?\s*)(powershell|pwsh|python|py|node|npm|curl|wget|mshta|cscript|wscript|bitsadmin)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary> for /f 未指定 delims 或 tokens / for /f without delims or tokens. </summary>
        internal static readonly Regex ForFNoDelims = new Regex(
            @"^for\s+/f\s+(?!.*(?:delims|tokens)=)(?:""[^""]*""\s+)?%%?[A-Za-z]",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary> if 后面直接跟 & / if followed by an ampersand. </summary>
        internal static readonly Regex IfWithAmp = new Regex(@"^if\b[^(]*&\s", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary> start "xxx.exe" 把文件名当成了窗口标题 / start with a quoted program as title. </summary>
        internal static readonly Regex StartQuotedExe = new Regex(
            @"^start\s+""[^""]*\.(?:exe|com|bat|cmd|msi)""\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary> if errorlevel 0 恒为真 / errorlevel 0 is always true. </summary>
        internal static readonly Regex ErrorlevelZero = new Regex(@"^if\s+errorlevel\s+0\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary> 递归删除且带通配或变量 / Recursive delete with wildcard or variable. </summary>
        internal static readonly Regex DangerousDelete = new Regex(
            @"^(?:del|erase|rd|rmdir)\s+(?=[^|]*\/(?:s|q))[^|]*(?:\*|%[^%]+%)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary> reg add 缺 /f，会弹确认框 / reg add without /f. </summary>
        internal static readonly Regex RegAddNoForce = new Regex(
            @"^reg\s+add\b(?![^|]*\/f\b)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary> for 列表里的通配符被引号包住 / Wildcard inside quotes in a for list. </summary>
        internal static readonly Regex ForQuotedWildcard = new Regex(
            @"^for\s+(?:/d\s+|/r\s+[^ ]*\s+)?%%?[A-Za-z]\s+in\s+\(\s*""[^""]*[*?]",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary> call :label 目标 / call :label target. </summary>
        internal static readonly Regex CallLabel = new Regex(@"^call\s+:(\w+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary> PATH 拼接缺少分号 / PATH concatenation without a semicolon. </summary>
        internal static readonly Regex PathConcatNoSemicolon = new Regex(
            @"^set\s+""?PATH=%PATH%[^;""\s]", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary> echo 与重定向符之间的空格会被写进文件，可在 do/&amp; 之后 / Spaces before the redirect land in the file. </summary>
        internal static readonly Regex EchoSpaceBeforeRedirect = new Regex(
            @"\becho\b[^>|&]*[^\s>]\s+>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary> for ... do 同行使用 %VAR% / %VAR% on the same line as for ... do. </summary>
        internal static readonly Regex ForDoPercentVar = new Regex(
            @"^for\b[^|]*\bdo\b[^|]*%[A-Za-z_]\w*%", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary> del 通配符缺 /q，会等 Y/N 确认 / del with a wildcard and no /q. </summary>
        internal static readonly Regex DelWildcardNoQuiet = new Regex(
            @"^(?:del|erase)\s+(?![^|]*\/q\b)[^|]*[*?]", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary> %~dp0 一类宏未加引号 / Unquoted %~dp0 style macros. </summary>
        internal static readonly Regex UnquotedScriptPath = new Regex(
            "(?<!\")%~[A-Za-z]*(?:dp|nx|n|x|f|p)0", RegexOptions.Compiled);

        /// <summary> echo 里用了反斜杠转义（cmd 不认），可在 do/&amp; 之后 / Backslash escapes inside echo, also after do or &amp;. </summary>
        internal static readonly Regex EchoBackslashEscape = new Regex(
            @"\becho\s+[^|>&]*\\[ntr](?:\s|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary> 全量检查 / Run all rules over the whole document. </summary>
        public static List<LintIssue> Check(string text)
        {
            var issues = new List<LintIssue>();
            if (string.IsNullOrEmpty(text)) return issues;

            // 关键：偏移必须相对「传入的原始文本」计算。
            // 之前先做 CRLF→LF 归一化再累加 +1，遇到 CRLF 文件会每行差 1 字节，
            // 导致波浪线、悬停、Ctrl+. 修复整体错位。
            string[] lines;
            int[] offsets;
            SplitLines(text, out lines, out offsets);

            var labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var labelLines = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var defined = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var delayed = false;
            var firstPushdLine = 0;
            var pushdCount = 0;
            var popdCount = 0;

            for (var i = 0; i < lines.Length; i++)
            {
                var t = lines[i].TrimStart();
                if (t.StartsWith(":") && !t.StartsWith("::"))
                {
                    var name = t.Substring(1).Split(new[] { ' ', '\t' }, 2)[0];
                    if (name.Length == 0) continue;
                    if (labelLines.ContainsKey(name))
                        Add(issues, offsets[i], 0, t, i + 1, $"标签 :{name} 重复定义", "R11");
                    else
                    {
                        labelLines[name] = i + 1;
                        labels.Add(name);
                    }
                }

                var def = SetDef.Match(t);
                if (def.Success)
                {
                    defined.Add(def.Groups[1].Value);
                    DefinedNames.Add(def.Groups[1].Value);
                }

                if (Regex.IsMatch(t, @"^setlocal\b.*enabledelayedexpansion", RegexOptions.IgnoreCase)) delayed = true;

                if (t.StartsWith("pushd", StringComparison.OrdinalIgnoreCase)) { pushdCount++; if (firstPushdLine == 0) firstPushdLine = i + 1; }
                else if (t.StartsWith("popd", StringComparison.OrdinalIgnoreCase)) popdCount++;
            }

            // R34：首行缺少 @echo off（跳过空行与注释行）
            var firstLine = lines.FirstOrDefault(l => l.TrimStart().Length > 0);
            if (firstLine != null
                && !EchoOffLine.IsMatch(firstLine.TrimStart())
                && !firstLine.TrimStart().StartsWith("::", StringComparison.Ordinal)
                && !firstLine.TrimStart().StartsWith("rem", StringComparison.OrdinalIgnoreCase))
                Add(issues, 0, 0, firstLine.TrimStart(), 1, "脚本首行缺少 @echo off，每条命令都会回显；建议首行写 @echo off", "R34");

            // R36：pushd / popd 数量不一致
            if (pushdCount != popdCount && firstPushdLine > 0)
                Add(issues, offsets[firstPushdLine - 1], 0, lines[firstPushdLine - 1].TrimStart(), firstPushdLine,
                    $"pushd 与 popd 数量不一致（pushd {pushdCount} 次 / popd {popdCount} 次），目录栈会残留", "R36");

            var depth = 0;

            for (var i = 0; i < lines.Length; i++)
            {
                var raw = lines[i];
                var t = raw.TrimStart();
                var indent = raw.Length - t.Length;
                var no = i + 1;

                if (t.Length == 0) continue;

                // 单遍特征扫描：后面每条规则先查位掩码，命中才跑正则
                var feat = FastScan.Features(t);
                var isComment = t.StartsWith("::")
                                || (t.StartsWith("rem", StringComparison.OrdinalIgnoreCase)
                                    && (t.Length == 3 || char.IsWhiteSpace(t[3])));

                // R9：:: 注释在括号块内会导致语法错误
                if (t.StartsWith("::") && depth > 0)
                    Add(issues, offsets[i], indent, t, no, "括号块内不能用 :: 注释，会报「此时不应有 xxx」，改用 REM", "R9");

                if (!isComment)
                {
                    if ((feat & FastScan.F.Set) != 0 && SetSpaced.IsMatch(t))
                        Add(issues, offsets[i], indent, t, no, "set 赋值时等号两侧不能有空格，应写成 set \"A=1\"", "R1");

                    if (depth > 0 && (feat & FastScan.F.Percent) != 0 && PercentVar.IsMatch(t) && !t.StartsWith("for", StringComparison.OrdinalIgnoreCase))
                        Add(issues, offsets[i], indent, t, no, "括号块内 %VAR% 在解析时就已展开，取不到新值，改用 !VAR!", "R2");

                    if (!delayed && (feat & FastScan.F.Bang) != 0 && BangVar.IsMatch(t))
                        Add(issues, offsets[i], indent, t, no, "使用了 !VAR! 但未开启延迟展开，需先 setlocal enabledelayedexpansion", "R3");

                    if ((feat & FastScan.F.Label) != 0 && t.StartsWith("cd", StringComparison.OrdinalIgnoreCase) && CdDrive.IsMatch(t) && !t.StartsWith("cd /d", StringComparison.OrdinalIgnoreCase))
                        Add(issues, offsets[i], indent, t, no, "跨盘切换目录必须用 cd /d，否则只显示目录不切换", "R4");

                    var go = (feat & FastScan.F.Goto) != 0 ? GotoTarget.Match(t) : Match.Empty;
                    if (go.Success)
                    {
                        var target = go.Groups[1].Value;
                        if (!target.Equals("eof", StringComparison.OrdinalIgnoreCase) && !labels.Contains(target))
                        {
                            var near = SpellSuggest.Best(target, labels);
                            var message = near == null
                                ? $"goto 的目标标签 :{target} 不存在，脚本会静默中断"
                                : $"goto 的目标标签 :{target} 不存在（是否想写 :{near}？），脚本会静默中断";

                            var targetStart = offsets[i] + indent + go.Groups[1].Index;
                            var colonBefore = targetStart > 0 && text[targetStart - 1] == ':';
                            if (colonBefore) { targetStart--; }
                            AddAt(issues, targetStart, target.Length + (colonBefore ? 1 : 0), no, message, "R5", near);
                        }
                    }

                    var callLabel = (feat & FastScan.F.Call) != 0 ? CallLabel.Match(t) : Match.Empty;
                    if (callLabel.Success)
                    {
                        var target = callLabel.Groups[1].Value;
                        if (!target.Equals("eof", StringComparison.OrdinalIgnoreCase) && !labels.Contains(target))
                        {
                            var near = SpellSuggest.Best(target, labels);
                            var message = near == null
                                ? $"call :{target} 的子过程不存在，调用会失败并继续往下执行"
                                : $"call :{target} 的子过程不存在（是否想写 :{near}？），调用会失败并继续往下执行";

                            var callStart = offsets[i] + indent + callLabel.Groups[1].Index;
                            var callColon = callStart > 0 && text[callStart - 1] == ':';
                            if (callColon) { callStart--; }
                            AddAt(issues, callStart, target.Length + (callColon ? 1 : 0), no, message, "R26", near);
                        }
                    }

                    if (BareBatCall.IsMatch(t)
                        && !t.StartsWith("call", StringComparison.OrdinalIgnoreCase)
                        && !t.StartsWith("start", StringComparison.OrdinalIgnoreCase))
                        Add(issues, offsets[i], indent, t, no, "调用另一个 bat 必须加 call，否则当前脚本会提前结束", "R6");

                    if (t.StartsWith("for", StringComparison.OrdinalIgnoreCase) && SinglePercentLoop.IsMatch(t))
                        Add(issues, offsets[i], indent, t, no, "for 的循环变量在脚本中要写 %%i，单个 % 只在命令行有效", "R7");

                    var constCmp = IfConstCompare.Match(t);
                    if (constCmp.Success)
                        AddAt(issues, offsets[i] + indent + constCmp.Index, constCmp.Length, no,
                            $"条件两侧都是字面量（{constCmp.Groups[1].Value}=={constCmp.Groups[2].Value}），结果恒定；要比变量请写 %a%==%b%", "R33");

                    if ((feat & FastScan.F.If) != 0 && IfUnquoted.IsMatch(t))
                        Add(issues, offsets[i], indent, t, no, "if 比较的两侧要加引号，否则变量为空时会变成语法错误", "R8");

                    if (BuiltinSet.IsMatch(t))
                        Add(issues, offsets[i], indent, t, no, "不要覆盖 PATH/TEMP 等系统变量，会影响后续命令与子进程", "R12");

                    if ((feat & FastScan.F.Percent) != 0)
                    foreach (Match m in PercentVar.Matches(t))
                    {
                        var name = m.Groups[1].Value;
                        if (defined.Contains(name) || EnvNames.Contains(name) || BuiltinNames.Contains(name)) continue;

                        var suggestion = Closest(name, defined);
                        var message = suggestion == null
                            ? $"变量 %{name}% 未定义，运行时会被当成空串展开"
                            : $"变量 %{name}% 未定义，是否想用 %{suggestion}%？（当前会展开成空串）";

                        AddAt(issues, offsets[i] + indent + m.Index, m.Length, no, message, "R13", suggestion);
                    }

                    // 按特征分桶：只调用“该行可能触发”的规则组
                    if ((feat & (FastScan.F.Copy | FastScan.F.Move | FastScan.F.Xcopy |
                                  FastScan.F.Robocopy | FastScan.F.Reg | FastScan.F.Sc)) != 0)
                        CheckParameters(issues, t, offsets[i], indent, no);

                    if ((feat & (FastScan.F.Percent | FastScan.F.IfExist | FastScan.F.SetA |
                                  FastScan.F.Redirect | FastScan.F.Echo | FastScan.F.ErrorLevel |
                                  FastScan.F.Exit | FastScan.F.Start | FastScan.F.Copy |
                                  FastScan.F.Move | FastScan.F.Set)) != 0)
                        CheckBlockRules(issues, t, offsets[i], indent, no, depth);

                    if ((feat & (FastScan.F.If | FastScan.F.For | FastScan.F.Set)) != 0)
                        CheckGrammar(issues, t, offsets[i], indent, no);

                    CheckLineRules(issues, t, offsets[i], indent, no, depth, feat);
                }

                depth += DepthDelta(t);
                if (depth < 0) depth = 0;
            }

            // 第二批规则（R58~R69）：独立文件，自带特征扫描
            try
            {
                foreach (var extra in ExtraRules.Check(text, lines, offsets))
                {
                    if (issues.Any(x => x.Rule == extra.Rule && x.Line == extra.Line)) continue;
                    issues.Add(extra);
                }
            }
            catch
            {
                // 规则自身异常不影响其它规则
            }

            // 第三批规则（R70~R79）
            try
            {
                foreach (var extra in ExtraRules2.Check(text, lines, offsets))
                {
                    if (issues.Any(x => x.Rule == extra.Rule && x.Line == extra.Line)) continue;
                    issues.Add(extra);
                }
            }
            catch
            {
                // 规则自身异常不影响其它规则
            }

            // 第四批规则（R83~R122）
            try
            {
                foreach (var extra in ExtraRules3.Check(text, lines, offsets))
                {
                    if (issues.Any(x => x.Rule == extra.Rule && x.Line == extra.Line)) continue;
                    issues.Add(extra);
                }
            }
            catch
            {
                // 规则自身异常不影响其它规则
            }

            MergeParserDiagnostics(issues, text);

            return issues;
        }

        /// <summary> 并入结构级诊断（R10/R49/R57），同规则同行时以解析器版本为准。 </summary>
        private static void MergeParserDiagnostics(List<LintIssue> issues, string text)
        {
            List<BatchParser.Diagnostic> parsed;
            try
            {
                parsed = BatchParser.Parse(text);
            }
            catch
            {
                return;                     // 解析器自身异常不影响其它规则
            }

            if (parsed.Count == 0) return;

            foreach (var d in parsed)
            {
                // 同规则同行：去掉正则版的粗略结果，换解析器的精确结果
                issues.RemoveAll(x => x.Rule == d.Rule && x.Line == d.Line);
                issues.Add(new LintIssue
                {
                    Line = d.Line,
                    Start = d.Start,
                    Length = Math.Max(1, d.Length),
                    Message = d.Message,
                    Rule = d.Rule
                });
            }
        }

        /// <summary>
        /// 增量通道入口：只检查 [fromLine, toLine] 这几行的“行内规则”，
        /// 跨行状态（括号深度、延迟展开、已定义变量）从文件开头顺序推出来（纯整数运算，很便宜）。
        /// 返回的偏移是相对整篇文本的绝对偏移，可直接用于 Scintilla 的 IndicatorFillRange。
        /// </summary>
        internal static List<LintIssue> CheckLines(string text, int fromLine, int toLine)
        {
            var issues = new List<LintIssue>();
            if (string.IsNullOrEmpty(text)) return issues;

            SplitLines(text, out var lines, out var offsets);
            if (fromLine < 0) fromLine = 0;
            if (toLine >= lines.Length) toLine = lines.Length - 1;
            if (fromLine > toLine) return issues;

            var defined = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var delayed = false;
            var depth = 0;

            // 前缀扫描：只维护三个标量状态，成本 O(行数) 且不碰正则
            for (var i = 0; i < fromLine; i++)
            {
                var t0 = lines[i].Trim();
                if (t0.Length == 0) continue;

                var def = SetDef.Match(t0);
                if (def.Success) defined.Add(def.Groups[1].Value);
                if (t0.StartsWith("setlocal", StringComparison.OrdinalIgnoreCase) &&
                    t0.IndexOf("enabledelayedexpansion", StringComparison.OrdinalIgnoreCase) >= 0) delayed = true;

                depth += DepthDelta(t0);
                if (depth < 0) depth = 0;
            }

            for (var i = fromLine; i <= toLine; i++)
            {
                var raw = lines[i];
                var t = raw.Trim();
                if (t.Length == 0) { continue; }

                var indent = raw.Length - raw.TrimStart().Length;
                var no = i + 1;
                var feat = FastScan.Features(t);

                var isComment = t.StartsWith("::") ||
                                (t.StartsWith("rem", StringComparison.OrdinalIgnoreCase) &&
                                 (t.Length == 3 || char.IsWhiteSpace(t[3])));

                if (!isComment)
                {
                    if ((feat & FastScan.F.Set) != 0 && SetSpaced.IsMatch(t))
                        Add(issues, offsets[i], indent, t, no, "set 赋值时等号两侧不能有空格，应写成 set \"A=1\"", "R1");

                    if (depth > 0 && (feat & FastScan.F.Percent) != 0 && PercentVar.IsMatch(t) &&
                        !t.StartsWith("for", StringComparison.OrdinalIgnoreCase))
                        Add(issues, offsets[i], indent, t, no, "括号块内 %VAR% 在解析时就已展开，取不到新值，改用 !VAR!", "R2");

                    if (!delayed && (feat & FastScan.F.Bang) != 0 && BangVar.IsMatch(t))
                        Add(issues, offsets[i], indent, t, no, "使用了 !VAR! 但未开启延迟展开，需先 setlocal enabledelayedexpansion", "R3");

                    if ((feat & FastScan.F.If) != 0 && IfUnquoted.IsMatch(t))
                        Add(issues, offsets[i], indent, t, no, "if 比较的两侧要加引号，否则变量为空时会变成语法错误", "R8");

                    if (BuiltinSet.IsMatch(t))
                        Add(issues, offsets[i], indent, t, no, "不要覆盖 PATH/TEMP 等系统变量，会影响后续命令与子进程", "R12");

                    CheckUnknownCommand(issues, t, offsets[i], indent, no);

                    if ((feat & (FastScan.F.Copy | FastScan.F.Move | FastScan.F.Xcopy |
                                  FastScan.F.Robocopy | FastScan.F.Reg | FastScan.F.Sc)) != 0)
                        CheckParameters(issues, t, offsets[i], indent, no);

                    if ((feat & (FastScan.F.Percent | FastScan.F.IfExist | FastScan.F.SetA |
                                  FastScan.F.Redirect | FastScan.F.Echo | FastScan.F.ErrorLevel |
                                  FastScan.F.Exit | FastScan.F.Start | FastScan.F.Copy |
                                  FastScan.F.Move | FastScan.F.Set)) != 0)
                        CheckBlockRules(issues, t, offsets[i], indent, no, depth);

                    if ((feat & (FastScan.F.If | FastScan.F.For | FastScan.F.Set)) != 0)
                        CheckGrammar(issues, t, offsets[i], indent, no);

                    CheckLineRules(issues, t, offsets[i], indent, no, depth, feat);
                }

                depth += DepthDelta(t);
                if (depth < 0) depth = 0;
            }

            // 第二批规则里的“行内”部分（R58~R65、R68/R69 之外的都行内）
            ExtraRules.CheckLines(issues, text, fromLine, toLine);
            ExtraRules2.CheckLines(issues, text, fromLine, toLine);

            return issues;
        }

        /// <summary> R53~R56：参数级检查（开关/操作数/子命令）/ Parameter-level checks. </summary>
        private static void CheckParameters(List<LintIssue> issues, string t, int lineStart, int indent, int no)
        {
            // 规则自身出错不应该拖垮整次检查
            try
            {
                foreach (var f in ParamChecker.Check(t))
                    AddAt(issues, lineStart + indent + f.Offset, Math.Max(1, f.Length), no, f.Message, f.Rule, f.Suggestion);
            }
            catch
            {
                // 忽略：参数级检查失败时保持其它规则可用
            }
        }

        /// <summary> 行首命令名 / The command token at the start of the line. </summary>
        internal static readonly Regex FirstToken = new Regex(@"^@?\s*([A-Za-z_][\w\-]*)", RegexOptions.Compiled);

        /// <summary> R52：命令名不存在（PATH 上也没有），但很像某个内置命令时给建议。 </summary>
        private static void CheckUnknownCommand(List<LintIssue> issues, string t, int lineStart, int indent, int no)
        {
            var m = FirstToken.Match(t);
            if (!m.Success) return;

            var name = m.Groups[1].Value;
            if (name.Length < 3) return;

            var commands = BatCommands.Commands;
            if (commands.Any(c => c.Equals(name, StringComparison.OrdinalIgnoreCase))) return;   // 是已知命令
            if (PathProbe.Exists(name)) return;                  // PATH 上真有这个程序，交给 R15 管

            var near = SpellSuggest.Best(name, commands);
            if (near == null) return;                            // 差太远就不猜

            AddAt(issues, lineStart + indent + m.Groups[1].Index, name.Length, no,
                $"命令 {name} 不存在（不是内置命令，PATH 上也找不到），是否想写 {near}？", "R52", near);
        }

        /// <summary> R46~R51：固定语法形态 + 相似度匹配 / Shape checks with similarity fallback. </summary>
        internal static void CheckGrammar(List<LintIssue> issues, string t, int lineStart, int indent, int no)
        {
            var verdicts = new[] { Grammar.CheckIf(t), Grammar.CheckFor(t), Grammar.CheckSet(t) };
            foreach (var v in verdicts)
            {
                if (v == null) continue;
                AddAt(issues, lineStart + indent, t.TrimEnd().Length, no, v.Message, v.Rule);
            }
        }

        /// <summary> R35、R37~R45：需要括号深度或变量表的行内规则 / Rules that need block depth or the variable table. </summary>
        internal static void CheckBlockRules(List<LintIssue> issues, string t, int lineStart, int indent, int no, int depth)
        {
            // R35：cd "%~dp0" 缺 /d
            if (CdDp0NoD.IsMatch(t) && !t.StartsWith("cd /d", StringComparison.OrdinalIgnoreCase))
                AddAt(issues, lineStart + indent, t.Length, no,
                    "cd \"%~dp0\" 缺 /d：从别的盘启动时只显示目录不切换；应写 cd /d \"%~dp0\"", "R35");

            // R37：循环块内用单个 > 会每次清空文件
            if (depth > 0)
            {
                foreach (Match m in SingleRedirect.Matches(t))
                {
                    var target = m.Groups[1].Value;
                    if (target.Equals("nul", StringComparison.OrdinalIgnoreCase) ||
                        target.Equals("con", StringComparison.OrdinalIgnoreCase)) continue;
                    AddAt(issues, lineStart + indent + m.Index, m.Length, no,
                        $"括号块内用 > 会在每次循环时清空 {target}；追加请用 >>", "R37");
                    break;
                }
            }

            // R38：if exist 目标没加引号
            if (IfExistUnquoted.IsMatch(t))
                AddAt(issues, lineStart + indent, t.Length, no,
                    "if exist 的目标没加引号，路径含空格时判断会失效；应写 if exist \"%F%\"", "R38");

            // R39：set /a 右侧不是数字
            var setA = SetAValue.Match(t);
            if (setA.Success)
            {
                var right = setA.Groups[2].Value;
                if (!IsDefinedVar(right, t))
                    AddAt(issues, lineStart + indent + setA.Groups[2].Index, right.Length, no,
                        $"set /a 只能算整数，右侧 {right} 既不是数字也不是已定义变量（会按 0 处理）", "R39");
            }

            // R40：无引号的 %1 比较
            if (IfArgUnquoted.IsMatch(t) && t.Contains("=="))
                AddAt(issues, lineStart + indent, t.Length, no,
                    "无引号的 %1 在参数为空时会变成 if ==，直接语法错误；应写 if \"%1\"==\"值\"", "R40");

            // R41：echo %VAR% 变量为空时输出回显状态
            if (EchoLoneVar.IsMatch(t))
                AddAt(issues, lineStart + indent, t.Length, no,
                    "echo %VAR% 在变量为空时会输出「ECHO 处于打开状态。」；改用 echo(%VAR%", "R41");

            // R42：start 的路径没加引号
            if (StartUnquotedPath.IsMatch(t))
                AddAt(issues, lineStart + indent, t.Length, no,
                    "start 的路径没加引号，含空格时会被拆成标题+命令；应写 start \"\" \"路径\"", "R42");

            // R43：括号块内的 %ERRORLEVEL%
            if (depth > 0 && t.IndexOf("%ERRORLEVEL%", StringComparison.OrdinalIgnoreCase) >= 0)
                AddAt(issues, lineStart + indent, t.Length, no,
                    "括号块内的 %ERRORLEVEL% 在解析时就已展开，拿到的是旧值；改用 !ERRORLEVEL! 或把判断移出块", "R43");

            // R44：exit 不带 /b
            if (ExitNoB.IsMatch(t))
                AddAt(issues, lineStart + indent, t.Length, no,
                    "exit 不带 /b 会关闭整个 cmd 窗口（被 call 时连调用方一起中断）；脚本内应写 exit /b", "R44");

            // R45：copy/move/xcopy 路径没加引号
            if (CopyUnquoted.IsMatch(t))
                AddAt(issues, lineStart + indent, t.Length, no,
                    $"{t.Split(' ')[0]} 的路径没加引号，含空格时会拆成多个参数；应写 \"...\"", "R45");
        }

        /// <summary> 标识符是否是已定义/环境变量 / Is the identifier a known variable? </summary>
        internal static bool IsDefinedVar(string name, string line)
        {
            if (EnvNames.Contains(name) || BuiltinNames.Contains(name)) return true;
            return Regex.IsMatch(line, @"(?:^|&)\s*set\s+""?\s*" + Regex.Escape(name) + "=", RegexOptions.IgnoreCase);
        }

        /// <summary> 逐行规则 R14~R25、R27~R31（不依赖跨行状态）/ Line-local rules that need no cross-line state. </summary>
        internal static void CheckLineRules(List<LintIssue> issues, string t, int lineStart, int indent, int no, int depth,
                                            FastScan.F feat2 = (FastScan.F)ulong.MaxValue)
        {
            if ((feat2 & FastScan.F.Mkdir) != 0 && MkdirFileLike.Match(t) is var mk && mk.Success)
                AddAt(issues, lineStart + indent, t.Length, no,
                    $"mkdir 只能建目录，这会建出名为 {mk.Groups[2].Value} 的文件夹；建文件请用 echo 内容>文件", "R14");

            foreach (Match m in ExternalCall.Matches(t))
                AddAt(issues, lineStart + indent + m.Index, m.Length, no,
                    $"调用了 {m.Groups[1].Value}，目标机器可能没有它，bat 的零依赖前提被破坏", "R15");

            if ((feat2 & FastScan.F.ForF) != 0 && ForFNoDelims.IsMatch(t))
                Add(issues, lineStart, indent, t, no, "for /f 默认按空格分词并丢弃行首空格；要读整行必须写 for /f \"delims=\"", "R16");

            if ((feat2 & (FastScan.F.If | FastScan.F.Amp)) != 0 && IfWithAmp.IsMatch(t))
                Add(issues, lineStart, indent, t, no, "if 后直接接 & 时，& 后面的命令会无条件执行；多条命令请用 if ... ( ... )", "R17");

            if (depth > 0 && t.StartsWith("goto", StringComparison.OrdinalIgnoreCase))
                Add(issues, lineStart, indent, t, no, "括号块内不要用 goto：块在执行前已整体解析，跳出会让括号计数错乱", "R18");

            if ((feat2 & FastScan.F.Pause) != 0 && t.Equals("pause", StringComparison.OrdinalIgnoreCase))
                Add(issues, lineStart, indent, t, no, "pause 会让无人值守的执行永久等待；确认脚本是给人双击用的", "R19");

            if ((feat2 & FastScan.F.Start) != 0 && StartQuotedExe.IsMatch(t))
                Add(issues, lineStart, indent, t, no, "start 的第一个引号参数是窗口标题，这样写不会启动程序；应写成 start \"\" \"app.exe\"", "R20");

            if ((feat2 & FastScan.F.ErrorLevel) != 0 && ErrorlevelZero.IsMatch(t))
                Add(issues, lineStart, indent, t, no, "if errorlevel 0 恒为真（语义是 >= 0）；判断成功请用 if not errorlevel 1", "R21");

            if ((feat2 & FastScan.F.Rd) != 0 && DangerousDelete.IsMatch(t))
                Add(issues, lineStart, indent, t, no, "递归删除且目标含通配符或变量，删错不可恢复；先 echo 出完整路径确认", "R22");

            if ((feat2 & FastScan.F.Reg) != 0 && RegAddNoForce.IsMatch(t))
                Add(issues, lineStart, indent, t, no, "reg add 缺少 /f，会弹确认框卡住无人值守的执行", "R23");

            if ((feat2 & FastScan.F.Quote) != 0 && OddQuotes(t))
                Add(issues, lineStart, indent, t, no, "引号数量为奇数，可能导致后续整段被当成字符串", "R24");

            if ((feat2 & FastScan.F.For) != 0 && ForQuotedWildcard.IsMatch(t))
                Add(issues, lineStart, indent, t, no, "通配符放在引号内不会被展开，for 只会拿到字面量", "R25");

            if ((feat2 & FastScan.F.PathVar) != 0 && PathConcatNoSemicolon.IsMatch(t))
                Add(issues, lineStart, indent, t, no, "PATH 拼接缺少分号，会把两个路径粘成一条无效路径", "R27");

            if ((feat2 & (FastScan.F.Echo | FastScan.F.Redirect)) != 0 && EchoSpaceBeforeRedirect.IsMatch(t))
                Add(issues, lineStart, indent, t, no, "重定向符前的空格会被写进文件内容；应写成 echo 内容>文件", "R28");

            if ((feat2 & (FastScan.F.For | FastScan.F.Percent)) != 0 && ForDoPercentVar.IsMatch(t))
                Add(issues, lineStart, indent, t, no, "for 同一行的 %VAR% 在循环开始前就展开，取不到更新值，改用 !VAR!", "R29");

            if ((feat2 & FastScan.F.Del) != 0 && DelWildcardNoQuiet.IsMatch(t))
                Add(issues, lineStart, indent, t, no, "del 通配符在部分情况下会等 Y/N 确认，无人值守会卡住；加 /q 并显式写路径", "R30");

            if (!t.StartsWith("echo", StringComparison.OrdinalIgnoreCase) && HasUnquotedMacro(t))
                Add(issues, lineStart, indent, t, no, "%~dp0 等宏没有加引号，脚本路径含空格时会被截断", "R31");

            if ((feat2 & FastScan.F.Echo) != 0 && EchoBackslashEscape.IsMatch(t))
                Add(issues, lineStart, indent, t, no, @"cmd 不认反斜杠转义，\n 会原样输出；echo 自带换行，追加用 >>", "R32");
        }

        /// <summary> 引号之外是否出现了 %~dp0 这类宏 / A path macro outside quotes. </summary>
        internal static bool HasUnquotedMacro(string line)
        {
            foreach (System.Text.RegularExpressions.Match m in UnquotedScriptPath.Matches(line))
            {
                var quotes = 0;
                for (var i = 0; i < m.Index; i++) if (line[i] == '"') quotes++;
                if (quotes % 2 == 0) return true;      // 偶数个引号 = 当前在引号外
            }
            return false;
        }

        /// <summary> 引号数为奇数（忽略 ^ 转义）/ Odd number of unescaped double quotes. </summary>
        internal static bool OddQuotes(string line)
        {
            var count = 0;

            for (var i = 0; i < line.Length; i++)
            {
                if (line[i] != '"') continue;
                if (i > 0 && line[i - 1] == '^') continue;   // ^" 是转义，不计入
                count++;
            }

            return count % 2 == 1;
        }

        /// <summary> 切行并给出每行在原文中的起始偏移（兼容 CRLF / LF）/ Split lines keeping real offsets. </summary>
        internal static void SplitLines(string text, out string[] lines, out int[] offsets)
        {
            var lineList = new List<string>();
            var offsetList = new List<int>();

            var start = 0;
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] != '\n') continue;

                var end = i;
                if (end > start && text[end - 1] == '\r') end--;      // CRLF 时去掉 \r

                lineList.Add(text.Substring(start, end - start));
                offsetList.Add(start);
                start = i + 1;
            }

            // 最后一行（文件末尾没有换行时）
            if (start <= text.Length)
            {
                var end = text.Length;
                if (end > start && text[end - 1] == '\r') end--;
                lineList.Add(text.Substring(start, end - start));
                offsetList.Add(start);
            }

            lines = lineList.ToArray();
            offsets = offsetList.ToArray();
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

        /// <summary> 记录一条问题，波浪线覆盖该行有效内容 / Record an issue covering the trimmed line. </summary>
        private static void Add(List<LintIssue> list, int lineStart, int indent, string trimmed, int number, string message, string rule,
                                string suggestion = null)
        {
            if (list.Count >= 500) return;

            var length = Math.Max(1, trimmed.TrimEnd().Length);
            AddAt(list, lineStart + indent, length, number, message, rule, suggestion);
        }

        /// <summary> 记录一条精确定位的问题 / Record an issue with an exact span. </summary>
        private static void AddAt(List<LintIssue> list, int start, int length, int number, string message, string rule,
                                  string suggestion = null)
        {
            if (list.Count >= 500) return;

            list.Add(new LintIssue
            {
                Line = number,
                Start = Math.Max(0, start),
                Length = Math.Max(1, length),
                Message = message,
                Rule = rule,
                Suggestion = suggestion
            });
        }

        /// <summary> 最近的候选名（内置命令 / 环境变量 / 脚本变量 / 额外池），来自 SpellSuggest。 </summary>
        private static string Closest(string name, IEnumerable<string> pool = null)
        {
            var candidates = new List<string>();
            candidates.AddRange(pool ?? Enumerable.Empty<string>());
            candidates.AddRange(BuiltinNames);
            candidates.AddRange(EnvNames);
            candidates.AddRange(DefinedNames);

            return SpellSuggest.Best(name.TrimStart('%', '!').TrimEnd('%', '!'), candidates);
        }

        /// <summary> 脚本里 set 定义过的变量名，供拼写建议使用 / Variables defined by set in this script. </summary>
        private static readonly HashSet<string> DefinedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }
}
