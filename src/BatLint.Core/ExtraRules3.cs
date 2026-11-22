using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace BatLint.Core
{
    /// <summary>
    /// 第四批规则（R83~R122）：把「常见写法问题」补齐到 122 条。
    /// 每条都尽量做成可静态判定、低误报；能用 FastScan 特征掩码先筛的先用掩码。
    /// 其中若干条的行为已用 cmd.exe 实测（脚本的 exit code 与输出）校正。
    /// </summary>
    internal static class ExtraRules3
    {
        private const int MaxIssues = 500;

        // ── 行内正则 ──
        private static readonly Regex SetQuotedValue = new Regex(
            @"^\s*@?set\s+(?!/[ap])(?:""([A-Za-z_]\w*)=""|([A-Za-z_]\w*)=""|([A-Za-z_]\w*)=(?=""))",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex SetQuotedRight = new Regex(
            @"^\s*@?set\s+(?!/[ap])(?:""[A-Za-z_]\w*=|)([A-Za-z_]\w*)=""",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex ForMissingIn = new Regex(
            @"^\s*@?for\s+(?:/[a-zA-Z]\s+)*%%?[A-Za-z]\s*(?!in\b)\S",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex SetANumberVar = new Regex(
            @"^\s*@?set\s+/a\s+[^=]+=.*%[A-Za-z_]\w*%",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex VarStartsDigit = new Regex(
            @"^\s*@?set\s+""?(\d\w*)[=]",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex XcopyDirNoI = new Regex(
            @"^\s*@?xcopy\b(?![^/]*?/i\b)[^/]*?/[se]\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex MoveNoY = new Regex(
            @"^\s*@?move\b(?![^/]*?/[yY]\b)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex MkdirUnquoted = new Regex(
            @"^\s*@?(?:mkdir|md)\s+(?!"")(?=\S*%)\S+",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex PauseBeforeEchoOff = new Regex(
            @"^\s*@?pause\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex TitleLine = new Regex(@"^\s*@?title\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex GotoLine = new Regex(@"^\s*@?goto\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex LabelLine = new Regex(@"^:([^\s]+)", RegexOptions.Compiled);
        private static readonly Regex SetPNoPrompt = new Regex(@"^\s*@?set\s+/p\s+""?[A-Za-z_]\w*=""?\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex EchoDotSpace = new Regex(@"^\s*@?echo\.\s+\S*", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex ExitB = new Regex(@"^\s*@?exit\s*/b\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex SetLocalDelayed = new Regex(
            @"^\s*@?setlocal\b.*enabledelayedexpansion", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex BangVar = new Regex(@"!([A-Za-z_]\w*)!", RegexOptions.Compiled);
        private static readonly Regex PercentVar = new Regex(@"%([A-Za-z_]\w*)%", RegexOptions.Compiled);
        private static readonly Regex SetDef = new Regex(
            @"^\s*@?set\s+(?:""?)([A-Za-z_]\w*)\s*=",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex SlashPath = new Regex(@"[""'][A-Za-z]:/[^""']*[""']", RegexOptions.Compiled);
        private static readonly Regex CdNoArg = new Regex(@"^\s*@?(?:cd|chdir)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex CdWithArg = new Regex(@"^\s*@?(?:cd|chdir)\b\s*(?:/d\b)?\s*\S", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex RedirectWrite = new Regex(@"[^>0-9]>{1,2}\s*""?([^""\s&|>]+)", RegexOptions.Compiled);
        private static readonly Regex CommentLine = new Regex(@"^\s*(?:::|@?rem\b)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex PercentInComment = new Regex(@"(?<!%)%[A-Za-z_]\w*%(?!%)|(?<!%)!(?!\()", RegexOptions.Compiled);
        private static readonly Regex GotoSelfNext = new Regex(@"^\s*@?goto\s+:?(\w+)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex StartNoTitle = new Regex(@"^\s*@?start\s+(?!"")(?=\S)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex EchoManyArgs = new Regex(@"^\s*@?echo\s+\S+\s+\S+\s+\S+\s+\S+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex SetNoQuoteStyle = new Regex(@"^\s*@?set\s+([A-Za-z_]\w*)=([^""\s].*)$", RegexOptions.Compiled);

        /// <summary> 跑第四批规则 / Run the fourth batch. </summary>
        public static List<LintIssue> Check(string text, string[] preLines = null, int[] preOffsets = null)
        {
            var issues = new List<LintIssue>();
            if (string.IsNullOrEmpty(text)) return issues;

            int[] computed = null;
            var lines = preLines ?? ExtraRules.SplitLines(text, out computed);
            var offsets = preOffsets ?? computed;

            var labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var defined = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var percentVars = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var bangVars = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var titles = 0;
            var delayedDeclared = false;
            var blankRun = 0;
            var tabsUsed = false;
            var spacesUsed = false;
            var echoOffSeen = false;
            var writeTargets = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            // 预扫：标签、变量、title 次数、缩进风格、写目标
            foreach (var raw in lines)
            {
                var t = raw.Trim();
                if (t.Length == 0) continue;

                var label = LabelLine.Match(t);
                if (label.Success) labels.Add(label.Groups[1].Value.TrimEnd(':'));

                var def = SetDef.Match(t);
                if (def.Success) defined.Add(def.Groups[1].Value);

                if (t.IndexOf('%') >= 0)
                    foreach (Match m in PercentVar.Matches(t)) percentVars.Add(m.Groups[1].Value);
                if (t.IndexOf('!') >= 0)
                    foreach (Match m in BangVar.Matches(t)) bangVars.Add(m.Groups[1].Value);
                if (SetLocalDelayed.IsMatch(t)) delayedDeclared = true;
                if (TitleLine.IsMatch(t)) titles++;
                if (raw.StartsWith("\t")) tabsUsed = true;
                else if (raw.StartsWith(" ")) spacesUsed = true;

                if (t.IndexOf('>') >= 0)
                {
                    var w = RedirectWrite.Match(t);
                    if (w.Success && !writeTargets.ContainsKey(w.Groups[1].Value)) writeTargets[w.Groups[1].Value] = 1;
                }
            }

            for (var i = 0; i < lines.Length; i++)
            {
                var raw = lines[i];
                var t = raw.Trim();
                var no = i + 1;
                var indent = raw.Length - raw.TrimStart().Length;

                if (t.Length == 0)
                {
                    if (++blankRun == 3)
                        AddAt(issues, offsets[i], 1, no, "连续 3 行以上空行，建议压缩为 1 行", "R93");
                    continue;
                }
                blankRun = 0;

                var feat = FastScan.Features(t);
                var isComment = CommentLine.IsMatch(t);

                // R92：pause 出现在 @echo off 之前
                if (!echoOffSeen && PauseBeforeEchoOff.IsMatch(t))
                    Add(issues, offsets[i], indent, t, no, "pause 出现在 @echo off 之前，这一行会被回显；把 @echo off 放最前面", "R92");

                if ((feat & FastScan.F.EchoOff) != 0) echoOffSeen = true;

                // R94：行尾多余空格（echo / rem 例外）
                if (!isComment && !t.StartsWith("echo", StringComparison.OrdinalIgnoreCase) && raw != raw.TrimEnd())
                    Add(issues, offsets[i], indent, t, no, "行尾有多余空格，容易被误读，也影响 diff", "R94");

                // R96：超长行
                if (raw.Length > 200)
                    Add(issues, offsets[i], indent, t, no, $"这一行有 {raw.Length} 个字符，超过 200，建议拆行或用变量", "R96");

                // R97/R105 等只看非注释行
                if (isComment)
                {
                    // R110：注释里含 %VAR% / !，cmd 仍会展开注释
                    if (PercentInComment.IsMatch(t) && (t.Contains('%') || t.Contains('!')))
                        Add(issues, offsets[i], indent, t, no, "注释行里的 %VAR% / ! 仍会被展开，容易出现意外替换；注释里请避免 % 与 !", "R110");
                    continue;
                }

                // R83：set 的值把引号也带进去了
                if ((feat & FastScan.F.Set) != 0 && SetQuotedRight.IsMatch(t))
                    Add(issues, offsets[i], indent, t, no,
                        "set 的引号写在了等号右边，引号会成为变量值的一部分；应写 set \"A=值\"", "R83");

                // R84：for 少了 in
                if ((feat & FastScan.F.For) != 0 && ForMissingIn.IsMatch(t))
                    Add(issues, offsets[i], indent, t, no, "for 缺少 in 关键字（cmd 报「此时不应有 …」，实测 exit 255）", "R84");

                // R85：set /a 里用 %VAR%（等价但容易被误判为延迟展开）
                if ((feat & FastScan.F.SetA) != 0 && SetANumberVar.IsMatch(t))
                    Add(issues, offsets[i], indent, t, no,
                        "set /a 里可以直接写变量名，不必写 %VAR%；写成 %VAR% 在括号块内会取到旧值", "R85");

                // R86：变量名以数字开头（引用时会被当成参数）
                if ((feat & FastScan.F.Set) != 0 && VarStartsDigit.IsMatch(t))
                    Add(issues, offsets[i], indent, t, no,
                        "变量名不能以数字开头，%1A% 会被解析成参数 %1 加 A；改用字母开头", "R86");

                // R87：xcopy 目录复制缺 /i（解析开关后判断，避免负向前瞻误判）
                if ((feat & FastScan.F.Xcopy) != 0)
                {
                    var switches = Regex.Matches(t, @"/([A-Za-z]+)")
                                        .Cast<Match>()
                                        .Select(m => m.Groups[1].Value.ToLowerInvariant())
                                        .ToHashSet();

                    if ((switches.Contains("s") || switches.Contains("e")) && !switches.Contains("i"))
                        Add(issues, offsets[i], indent, t, no,
                            "xcopy 复制目录时没加 /i，目标不存在会停下来问「是文件还是目录」（会中断无人值守执行）", "R87");
                }

                // R88：move 覆盖已存在目标缺 /y
                if ((feat & FastScan.F.Move) != 0 && MoveNoY.IsMatch(t))
                    Add(issues, offsets[i], indent, t, no,
                        "move 覆盖已存在文件时会询问是否覆盖，无人值守会卡住；确认覆盖请加 /y", "R88");

                // R91：mkdir 路径含变量未加引号
                if ((feat & FastScan.F.Mkdir) != 0 && MkdirUnquoted.IsMatch(t))
                    Add(issues, offsets[i], indent, t, no,
                        "mkdir 的路径含变量却没加引号，路径带空格时会被拆成多个目录", "R91");

                // R104：set /p 没有提示文本
                if ((feat & FastScan.F.SetP) != 0 && SetPNoPrompt.IsMatch(t))
                    Add(issues, offsets[i], indent, t, no, "set /p 没有提示文本，用户不知道要输入什么", "R104");

                // R105：echo. 后面带空格
                if ((feat & FastScan.F.Echo) != 0 && EchoDotSpace.IsMatch(t))
                    Add(issues, offsets[i], indent, t, no, "echo. 后面带空格会输出一行空格；空行请写 echo.", "R105");

                // R99：用了 goto 但文件里没有任何标签
                if ((feat & FastScan.F.Goto) != 0 && labels.Count == 0)
                    Add(issues, offsets[i], indent, t, no, "用了 goto 但文件里没有任何标签（:label），跳转会失败", "R99");

                // R98：标签名含非 ASCII
                var lbl = (feat & FastScan.F.Label) != 0 ? LabelLine.Match(t) : Match.Empty;
                if (lbl.Success && lbl.Groups[1].Value.Any(c => c > 127))
                    Add(issues, offsets[i], indent, t, no, "标签名含中文/特殊字符，goto 在不同代码页下可能匹配失败", "R98");

                // R111：赋值后从未被引用
                var def = (feat & FastScan.F.Set) != 0 ? SetDef.Match(t) : Match.Empty;
                if (def.Success)
                {
                    var name = def.Groups[1].Value;
                    if (!percentVars.Contains(name) && !bangVars.Contains(name))
                        Add(issues, offsets[i], indent, t, no, $"变量 {name} 赋值后从未被引用（可能是残留代码）", "R111");
                }

                // R113：goto 到下一行紧跟的标签（自旋）
                var gs = (feat & FastScan.F.Goto) != 0 ? GotoSelfNext.Match(t) : Match.Empty;
                if (gs.Success && i + 1 < lines.Length)
                {
                    var nextLabel = LabelLine.Match(lines[i + 1].Trim());
                    if (nextLabel.Success && nextLabel.Groups[1].Value.Equals(gs.Groups[1].Value, StringComparison.OrdinalIgnoreCase))
                        Add(issues, offsets[i], indent, t, no, $"goto :{gs.Groups[1].Value} 的下一条就是该标签，这一段是无限空转", "R113");
                }

                // R114：路径用 / 作分隔符
                if (t.IndexOf(":/", StringComparison.Ordinal) >= 0 && SlashPath.IsMatch(t))
                    Add(issues, offsets[i], indent, t, no, "路径里用了 /，cmd 的大多数命令不认；请用 \\ 作分隔符", "R114");

                // R115：cd 不带参数
                if ((feat & FastScan.F.Label) != 0 && CdNoArg.IsMatch(t))
                    Add(issues, offsets[i], indent, t, no, "cd 不带参数只会打印当前目录，不会切换；确认是否漏了路径", "R115");

                // R116：cd 后没检查 %ERRORLEVEL%
                if ((feat & FastScan.F.Set) != 0 && t.StartsWith("cd", StringComparison.OrdinalIgnoreCase) &&
                    CdWithArg.IsMatch(t) && !percentVars.Contains("ERRORLEVEL"))
                    AddAt(issues, offsets[i] + indent, t.Length, no,
                        "cd 到不存在的目录会失败但脚本继续跑；建议随后判断 if errorlevel 1 并 exit /b", "R116");

                // R119：同一文件里 set 引号风格不一致
                if ((feat & FastScan.F.Set) != 0 && SetNoQuoteStyle.IsMatch(t))
                    Add(issues, offsets[i], indent, t, no, "set 没加引号；建议统一写成 set \"A=值\"，避免值里的空格与 & 出问题", "R119");

                // R120：start 少了标题参数
                if ((feat & FastScan.F.Start) != 0 && StartNoTitle.IsMatch(t) && !t.Contains("\"\""))
                    Add(issues, offsets[i], indent, t, no, "start 的第一个引号参数是窗口标题，缺省时路径会被当标题；建议写 start \"\" \"程序\"", "R120");

                // R121：echo 多个参数未加引号
                if ((feat & FastScan.F.Echo) != 0 && EchoManyArgs.IsMatch(t) && !t.Contains('"'))
                    Add(issues, offsets[i], indent, t, no, "echo 没有引号且带多个空格参数，容易连带输出多余空格；建议 echo 文本 或 echo(文本", "R121");

                // R95：混用 Tab 与空格缩进
                if (!tabsUsed || !spacesUsed) { /* 占位，下面统一报 */ }

                // R102：用了 !VAR! 但从未 set
                if ((feat & FastScan.F.Bang) != 0)
                foreach (Match m in BangVar.Matches(t))
                {
                    var name = m.Groups[1].Value;
                    if (defined.Contains(name) || name.Equals("ERRORLEVEL", StringComparison.OrdinalIgnoreCase)) continue;
                    if (name.Equals("CD", StringComparison.OrdinalIgnoreCase) || name.Equals("DATE", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("TIME", StringComparison.OrdinalIgnoreCase) || name.Equals("RANDOM", StringComparison.OrdinalIgnoreCase)) continue;
                    if (Environment.GetEnvironmentVariable(name) != null) continue;

                    AddAt(issues, offsets[i] + indent + m.Index, m.Length, no,
                        $"!{name}! 在脚本里没有 set 过，延迟展开后是空串", "R102");
                }

                // R103：同一变量既有 %VAR% 又有 !VAR!
                if ((feat & FastScan.F.Percent) != 0)
                foreach (Match m in PercentVar.Matches(t))
                {
                    var name = m.Groups[1].Value;
                    if (!bangVars.Contains(name)) continue;
                    AddAt(issues, offsets[i] + indent + m.Index, m.Length, no,
                        $"%{name}% 与 !{name}! 混用：前者在解析时展开、后者在执行时展开，容易拿到不同值", "R103");
                }
            }

            // R123：变量用了全角 ！ ％（中文输入法常见），cmd 不认
            for (var i = 0; i < lines.Length; i++)
            {
                var t = lines[i].Trim();
                if (t.Length == 0 || CommentLine.IsMatch(t)) continue;
                if (!Regex.IsMatch(t, @"[！％][A-Za-z_][\w]*[！％]")) continue;

                AddAt(issues, offsets[i], Math.Max(1, t.Length), i + 1,
                    "变量写成了全角的 ！VAR！ / ％VAR％，cmd 只认半角 !VAR! / %VAR%，展开会是字面量", "R123");
                break;
            }

            // R95：全文件缩进风格混用
            if (tabsUsed && spacesUsed)
                AddAt(issues, 0, 1, 1, "文件里同时用了 Tab 与空格缩进，建议统一（Visual Studio 风格默认 Tab 转 4 空格）", "R95");

            // R100：title 重复设置
            if (titles > 1)
                AddAt(issues, 0, 1, 1, $"title 被设置了 {titles} 次，只有最后一次生效；保留一处即可", "R100");

            // R101：开了延迟展开但没用到 !
            if (delayedDeclared && bangVars.Count == 0)
                AddAt(issues, 0, 1, 1, "setlocal enabledelayedexpansion 已开启，但脚本里没有用到 !VAR!；可以去掉这一行", "R101");

            // R106：%1 / %* 拼路径未加引号
            var argPath = Regex.IsMatch(text, @"(?<!"")\s%(\*|[1-9])(?!%)");
            if (argPath)
            {
                for (var i = 0; i < lines.Length; i++)
                {
                    var t = lines[i].Trim();
                    if (!Regex.IsMatch(t, @"%(\*|[1-9])")) continue;
                    if (Regex.IsMatch(t, @"""?%(\*|[1-9])""?")) continue;
                    AddAt(issues, offsets[i], Math.Max(1, t.Length), i + 1,
                        "参数 %1 / %* 没加引号，脚本路径含空格时会被拆开；应写 \"%~1\"", "R106");
                    break;
                }
            }

            // R107：dir 的通配符写在引号里（不会被展开）
            for (var i = 0; i < lines.Length; i++)
            {
                var t = lines[i].Trim();
                if (!t.StartsWith("dir ", StringComparison.OrdinalIgnoreCase)) continue;
                if (!t.Contains('"')) continue;
                if (!Regex.IsMatch(t, @"""[^""]*[*?][^""]*""")) continue;

                AddAt(issues, offsets[i], Math.Max(1, t.Length), i + 1,
                    "dir 的通配符写在了引号里，cmd 不会展开它；把通配符放在引号外，或改用 for", "R107");
                break;
            }

            // R108/R109：同一行既 > 又 >> 写同一文件（顺序混用）
            for (var i = 0; i < lines.Length; i++)
            {
                var t = lines[i].Trim();
                if (t.IndexOf('>') < 0) continue;

                var targets = Regex.Matches(t, @"[>]{1,2}\s*""?([^""\s&|>]+)")
                                   .Cast<Match>()
                                   .Select(m => m.Groups[1].Value)
                                   .ToList();
                if (targets.Count < 2) continue;

                var dup = targets.GroupBy(x => x, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
                if (dup == null) continue;

                AddAt(issues, offsets[i], Math.Max(1, t.Length), i + 1,
                    $"同一行多次重定向到 {dup.Key}，前一次的结果会被覆盖；确认要用 > 还是 >>", "R109");
                break;
            }

            // R112：exit /b 之后还有可执行语句
            for (var i = 0; i < lines.Length; i++)
            {
                if (!ExitB.IsMatch(lines[i].Trim())) continue;

                for (var j = i + 1; j < lines.Length; j++)
                {
                    var t = lines[j].Trim();
                    if (t.Length == 0 || CommentLine.IsMatch(t)) continue;
                    if (t.StartsWith(":", StringComparison.Ordinal)) break;          // 进入下一个标签，合法
                    AddAt(issues, offsets[j], Math.Max(1, t.Length), j + 1,
                        "上一行已经 exit /b，这里的语句永远不会执行（死代码）", "R112");
                    break;
                }
            }

            // R117：@echo off 之后再用 echo off
            for (var i = 0; i < lines.Length; i++)
            {
                var t = lines[i].Trim();
                if (!Regex.IsMatch(t, @"^@?echo\s+off$", RegexOptions.IgnoreCase)) continue;
                if (i == 0) continue;

                AddAt(issues, offsets[i], Math.Max(1, t.Length), i + 1,
                    "@echo off 只需在首行写一次，这里重复了", "R117");
                break;
            }

            // R118：if exist 之后用了 == 比较
            for (var i = 0; i < lines.Length; i++)
            {
                var t = lines[i].Trim();
                if (!Regex.IsMatch(t, @"^if\s+(?:not\s+)?exist\b", RegexOptions.IgnoreCase)) continue;
                if (!t.Contains("==")) continue;

                AddAt(issues, offsets[i], Math.Max(1, t.Length), i + 1,
                    "if exist 后面不能再接 == 比较；需要同时判断存在与相等请拆成两个 if", "R118");
                break;
            }

            // R89：cd 到变量路径却未加引号
            for (var i = 0; i < lines.Length; i++)
            {
                var t = lines[i].Trim();
                if (!Regex.IsMatch(t, @"^(?:@?cd|@?chdir)\b[^\r\n]*?%(?:[A-Za-z_]\w*)%", RegexOptions.IgnoreCase)) continue;
                if (t.Contains('"')) continue;

                AddAt(issues, offsets[i], Math.Max(1, t.Length), i + 1,
                    "cd 到变量路径却没加引号，路径含空格时会切到错误的目录", "R89");
                break;
            }

            // R90：重定向目标用变量但未加引号
            for (var i = 0; i < lines.Length; i++)
            {
                var t = lines[i].Trim();
                if (!Regex.IsMatch(t, @"[>]{1,2}\s*%(?:[A-Za-z_]\w*)%", RegexOptions.IgnoreCase)) continue;

                AddAt(issues, offsets[i], Math.Max(1, t.Length), i + 1,
                    "重定向目标写成了不带引号的变量，路径含空格时会被拆开；应写 >\"%~1\" 这类带引号形式", "R90");
                break;
            }

            // R97：文件末尾缺少换行
            if (!text.EndsWith("\n", StringComparison.Ordinal))
                AddAt(issues, Math.Max(0, text.Length - 1), 1, Math.Max(1, lines.Length),
                    "文件末尾缺少换行，某些编辑器/合并工具处理时会把最后一行与下一条命令拼在一起", "R97");

            // R108：注释风格混用（:: 与 rem）
            {
                var hasColonComment = lines.Any(l => l.TrimStart().StartsWith("::", StringComparison.Ordinal));
                var hasRemComment = lines.Any(l => l.TrimStart().StartsWith("rem ", StringComparison.OrdinalIgnoreCase));
                if (hasColonComment && hasRemComment)
                    AddAt(issues, 0, 1, 1, "文件里同时用了 :: 与 rem 两种注释风格；:: 在括号块内会报错，建议统一用 rem", "R108");
            }

            // R122：脚本用 %~dp0 但没加引号也不是路径开头（容易误用）
            for (var i = 0; i < lines.Length; i++)
            {
                var t = lines[i].Trim();
                if (!Regex.IsMatch(t, @"%~dp0[^""\\\s]*", RegexOptions.IgnoreCase)) continue;
                if (Regex.IsMatch(t, @"""[^""]*%~dp0", RegexOptions.IgnoreCase)) continue;

                AddAt(issues, offsets[i], Math.Max(1, t.Length), i + 1,
                    "%~dp0 指向脚本目录（以 \\ 结尾），拼文件名时要小心多余/缺失的反斜杠，建议整体加引号", "R122");
                break;
            }

            return issues.Take(MaxIssues).ToList();
        }

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
    }
}
