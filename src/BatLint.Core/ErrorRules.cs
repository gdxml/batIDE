using System;
using System.Collections.Generic;
using System.Linq;

namespace BatLint.Core
{
    /// <summary>
    /// 规则目录：每条规则 = 编号 + 定性 + 一句话说明。
    /// 定性的依据是「错在哪一层」，不是「看起来像什么」：
    ///   syntaxError    cmd 解析阶段就会报错，或把后续内容吞掉
    ///   logicError     能跑，但结果与意图不符（恒真条件、旧值展开）
    ///   typeError      值的类型用错（把非数字丢给 set /a、把文件当目录）
    ///   nameError      引用了不存在的变量 / 标签 / 子过程
    ///   flowError      流程控制用错（裸调用、块内 goto、pushd 不配对）
    ///   pathError      路径写法会在含空格/跨盘时失效
    ///   ioError        读写、删除、重定向的副作用
    ///   styleWarning   不影响正确性的写法建议
    ///   dependencyWarning  破坏了 bat 的零依赖前提
    /// </summary>
    internal static class ErrorRules
    {
        public const string Syntax = "syntaxError";
        public const string Logic = "logicError";
        public const string Type = "typeError";
        public const string Name = "nameError";
        public const string Flow = "flowError";
        public const string Path = "pathError";
        public const string IO = "ioError";
        public const string Style = "styleWarning";
        public const string Dependency = "dependencyWarning";

        /// <summary> 一条规则的元数据 / Metadata of one rule. </summary>
        internal sealed class Rule
        {
            public Rule(string id, string kind, string title, string sample)
            {
                Id = id;
                Kind = kind;
                Title = title;
                Sample = sample;
            }

            public string Id { get; }
            public string Kind { get; }
            public string Title { get; }

            /// <summary> 触发样例，用于 --rules 与文档 / A triggering sample. </summary>
            public string Sample { get; }

            public string KindLabel => LabelOf(Kind);
        }

        /// <summary> 全部规则，按编号排序 / All rules, ordered by id. </summary>
        public static readonly IReadOnlyList<Rule> All = new List<Rule>
        {
            // ── styleWarning ──
            new Rule("R1",  Style, "set 等号两侧带空格", "set A = 1"),
            new Rule("R12", Style, "覆盖 PATH/TEMP 等系统变量", "set PATH=C:\\x"),
            new Rule("R19", Style, "pause 会让无人值守执行永久等待", "pause"),
            new Rule("R34", Style, "首行缺少 @echo off", "@rem 没有 @echo off"),
            new Rule("R70", Style, "@echo off 之后多余的 @", "@echo off / @echo x"),
            new Rule("R79", Style, "用 %CD% 拼脚本自身路径", "copy a.txt %CD%.txt"),
            new Rule("R67", Style, "用了 %1 却没先检查参数是否为空", "echo %1"),
            new Rule("R69", Style, "标签定义但从未被 goto/call 引用", ":unused"),

            // ── logicError ──
            new Rule("R2",  Logic, "括号块内用 %VAR% 取不到新值", "for ... do ( set A=1 & echo %A% )"),
            new Rule("R3",  Logic, "用了 !VAR! 却没开延迟展开", "echo !A!"),
            new Rule("R8",  Logic, "if 比较没加引号，变量为空时条件变形", "if %A%==1 echo x"),
            new Rule("R16", Logic, "for /f 默认按空格分词，读不到整行", "for /f %%i in (a.txt) do echo %%i"),
            new Rule("R21", Logic, "if errorlevel 0 恒为真（语义是 >=0）", "if errorlevel 0 echo ok"),
            new Rule("R25", Logic, "通配符写在引号内不会被展开", "for %%f in (\"*.txt\") do"),
            new Rule("R29", Logic, "for 同一行的 %VAR% 循环前就展开", "for ... do echo %A%"),
            new Rule("R33", Logic, "if 两侧都是字面量，条件恒定", "if not 1==2 echo hi"),
            new Rule("R41", Logic, "echo %VAR% 在变量为空时输出回显状态", "echo %A%"),
            new Rule("R43", Logic, "括号块内 %ERRORLEVEL% 是旧值", "if ... ( if %ERRORLEVEL% ... )"),
            new Rule("R51", Logic, "if 比较里出现多个 ==", "if 1==1==1 echo x"),
            new Rule("R55", Logic, "开关互相冲突（/Y 与 /-Y、/A 与 /B）", "copy /y /-y a.txt b.txt"),
            new Rule("R58", Logic, "同一行 set 后立刻用 %VAR%（取不到新值）", "set A=1 & echo %A%"),
            new Rule("R59", Logic, "for /l 步长为 0（死循环）", "for /l %%i in (1,0,5) do echo %%i"),
            new Rule("R63", Logic, "%ERRORLEVEL% 用 == 比数字（字符串比较）", "if %ERRORLEVEL%==0 echo ok"),
            new Rule("R65", Logic, "if exist 带通配符（判断不可靠）", "if exist *.txt echo ok"),
            new Rule("R71", Logic, "set 值里有未加引号的 &（赋值被截断）", "set A=x&y"),
            new Rule("R73", Logic, "for /f tokens 不够却用了超范围的 %%变量", "for /f \"tokens=1\" %%a in (x) do echo %%b"),
            new Rule("R74", Logic, "if 两侧都是空引号（恒真）", "if \"\"==\"\" echo x"),
            new Rule("R75", Logic, "%%x 用在 for 之外（不会被替换）", "echo %%i"),
            new Rule("R81", Logic, "循环体里把 %%i 写成了裸字母", "for %%i in (a) do echo i"),

            // ── typeError ──
            new Rule("R14", Type, "mkdir 只能建目录，被当成建文件", "mkdir C:\\a\\b.txt"),
            new Rule("R39", Type, "set /a 只能算整数，右侧不是数字", "set /a N=abc"),
            new Rule("R60", Type, "set /a 除以 0", "set /a N=1/0"),
            new Rule("R61", Type, "变量子串语法错误（冒号后缺起始下标）", "echo %A:,3%"),

            // ── nameError ──
            new Rule("R5",  Name, "goto 的目标标签不存在", "goto :nope"),
            new Rule("R11", Name, "标签重复定义，跳转会落到第一个", ":a ... :a"),
            new Rule("R13", Name, "变量未定义，运行时展开成空串", "echo %NAEM%"),
            new Rule("R52", Name, "命令名不存在（给出最接近的内置命令）", "setlcoal A=1"),
            new Rule("R26", Name, "call 的子过程不存在", "call :nope"),

            // ── flowError ──
            new Rule("R6",  Flow, "裸调用另一个 bat，当前脚本提前结束", "other.bat"),
            new Rule("R17", Flow, "if 后直接接 &，& 后面的命令无条件执行", "if 1==1 echo a & echo b"),
            new Rule("R18", Flow, "括号块内用 goto，括号计数错乱", "if ... ( goto :x )"),
            new Rule("R36", Flow, "pushd / popd 数量不一致，目录栈残留", "pushd D:\\a"),
            new Rule("R44", Flow, "exit 不带 /b 会关掉调用方窗口", "exit"),
            new Rule("R66", Flow, "setlocal 与 endlocal 数量不一致", "setlocal（无 endlocal）"),
            new Rule("R68", Flow, "子过程结尾没有 exit /b（会继续往下跑）", "call :sub 后无 exit /b"),

            // ── pathError ──
            new Rule("R4",  Path, "跨盘 cd 缺 /d，只显示目录不切换", "cd D:\\work"),
            new Rule("R27", Path, "PATH 拼接缺分号，两条路径粘成一条", "set PATH=C:\\a C:\\b"),
            new Rule("R31", Path, "%~dp0 等宏没加引号，路径含空格被截断", "copy %~dp0a.txt x"),
            new Rule("R35", Path, "cd \"%~dp0\" 缺 /d，从别的盘启动时不生效", "cd \"%~dp0\""),
            new Rule("R38", Path, "if exist 的目标没加引号", "if exist %F% echo x"),
            new Rule("R42", Path, "start 的路径没加引号", "start C:\\Program Files\\a.exe"),
            new Rule("R45", Path, "copy/move/xcopy 的路径没加引号", "copy %src% %dst%"),

            // ── ioError ──
            new Rule("R22", IO, "递归删除且含通配符或变量，删错不可恢复", "rd /s /q %D%\\%N%"),
            new Rule("R23", IO, "reg add 缺 /f，会弹确认框卡住", "reg add HKCU\\x /v a"),
            new Rule("R28", IO, "重定向符前的空格被写进文件", "echo hello >> a.txt"),
            new Rule("R30", IO, "del 通配符缺 /q，会等 Y/N 确认", "del *.tmp"),
            new Rule("R32", IO, "cmd 不认 \\n 转义，会原样输出", "echo a\\nb"),
            new Rule("R37", IO, "循环块内用 > 会每次清空文件", "for ... do echo x > log.txt"),
            new Rule("R64", IO, "同一行两个 > 重定向（后一个覆盖前一个）", "echo a > x > y"),
            new Rule("R82", IO, "单行 for 里用 > 反复清空同一文件", "for /l %%i in (1,1,3) do > out.txt echo hi"),

            // ── syntaxError ──
            new Rule("R7",  Syntax, "for 的循环变量要写 %%i", "for %i in (*) do"),
            new Rule("R46", Syntax, "if 写法无法识别（期望某种合法形态）", "if 1 2 echo x"),
            new Rule("R47", Syntax, "if 比较用了单个 =", "if 1=2 echo x"),
            new Rule("R48", Syntax, "if 写法不完整（缺操作数/比较符）", "if 1 2 echo x"),
            new Rule("R49", Syntax, "for 语法不完整（缺 do 或括号）", "for %%i in (1 2) echo %%i"),
            new Rule("R50", Syntax, "set 缺少 = 号", "set A 1"),
            new Rule("R53", Syntax, "命令开关不存在（给出最接近的开关）", "copy /q a.txt b.txt"),
            new Rule("R54", Syntax, "参数个数不够 / 选项缺少值", "move a.txt"),
            new Rule("R56", Syntax, "子命令不存在（reg / sc）", @"reg qurey HKCU\X /v a"),
            new Rule("R57", Syntax, "悬空 else（没有配对的 if）", "else echo x"),
            new Rule("R72", Syntax, "重定向符后面缺少目标文件", "type nul >"),
            new Rule("R76", Syntax, "goto 目标带空格", "goto :my label"),
            new Rule("R77", Syntax, "del/erase 只有开关没有目标", "del /s /q"),
            new Rule("R78", Syntax, "空的 ( ) 块", "if exist nul ( )"),
            new Rule("R80", Syntax, "if 条件后面没有命令（悬空 if，cmd 报语法错）", "if exist x / <换行> mkdir x"),
            new Rule("R83", Logic, "set 的引号写在等号右边（引号进了值）", "set A=\"hello world\""),
            new Rule("R84", Syntax, "for 缺少 in 关键字", "for %%i (a) do echo %%i"),
            new Rule("R85", Logic, "set /a 里用 %VAR%（块内会取旧值）", "set /a M=%N%+1"),
            new Rule("R86", Logic, "变量名以数字开头（会被当参数）", "set 1A=x"),
            new Rule("R87", IO, "xcopy 复制目录缺 /i（会停下提问）", "xcopy \"src dir\" \"dst dir\" /s"),
            new Rule("R88", IO, "move 覆盖已存在文件缺 /y（会卡住）", "move a.txt b.txt"),
            new Rule("R89", Path, "cd 到变量路径未加引号", "cd %DIR%"),
            new Rule("R90", IO, "重定向目标用变量未加引号", "echo x > %OUT%"),
            new Rule("R91", Path, "mkdir 路径含变量未加引号", @"mkdir %DIR%\sub"),
            new Rule("R92", Style, "pause 出现在 @echo off 之前", "pause / @echo off"),
            new Rule("R93", Style, "连续 3 行以上空行", "(三行空行)"),
            new Rule("R94", Style, "行尾有多余空格", "echo hi__"),
            new Rule("R95", Style, "Tab 与空格缩进混用", "(混用缩进)"),
            new Rule("R96", Style, "单行超过 200 字符", "(超长行)"),
            new Rule("R97", Style, "文件末尾缺少换行", "(末行无 CRLF)"),
            new Rule("R98", Style, "标签名含非 ASCII 字符", ":处理"),
            new Rule("R99", Flow, "用了 goto 但文件里没有标签", "goto :nope"),
            new Rule("R100", Style, "title 被重复设置", "title a / title b"),
            new Rule("R101", Style, "开了延迟展开但没用到 !VAR!", "setlocal enabledelayedexpansion"),
            new Rule("R102", Name, "!VAR! 引用了从未 set 的变量", "echo !NOPE!"),
            new Rule("R103", Logic, "同一变量 %VAR% 与 !VAR! 混用", "echo %A% !A!"),
            new Rule("R104", Style, "set /p 没有提示文本", "set /p A="),
            new Rule("R105", Style, "echo. 后面带空格", "echo. "),
            new Rule("R106", Path, "%1 / %* 拼路径未加引号", "cd %1"),
            new Rule("R107", IO, "dir 的通配符写在引号里", "dir \"*.txt\""),
            new Rule("R108", Style, "注释风格混用（:: 与 rem）", ":: a / rem b"),
            new Rule("R109", IO, "同一行多次重定向到同一文件", "echo a > x >> x"),
            new Rule("R110", Logic, "注释行里的 %VAR% / ! 仍会被展开", "rem 值=%PATH%"),
            new Rule("R111", Style, "变量赋值后从未被引用", "set UNUSED=1"),
            new Rule("R112", Logic, "exit /b 之后还有可执行语句（死代码）", "exit /b / echo x"),
            new Rule("R113", Logic, "goto 到下一行紧跟的标签（空转）", "goto :a / :a"),
            new Rule("R114", Path, "路径里用 / 作分隔符", "copy \"D:/a/b.txt\" ."),
            new Rule("R115", Style, "cd 不带参数（只打印目录）", "cd"),
            new Rule("R116", Logic, "cd 之后没有检查 errorlevel", "cd notexist"),
            new Rule("R117", Style, "@echo off 重复设置", "echo off（再次）"),
            new Rule("R118", Logic, "if exist 后面又接 == 比较", "if exist \"a\"==\"b\""),
            new Rule("R119", Style, "set 未加引号（值含空格会出错）", "set A=hello world"),
            new Rule("R120", Style, "start 缺少空标题参数", @"start C:\a b\x.exe"),
            new Rule("R121", Style, "echo 多参数未加引号", "echo a  b   c   d"),
            new Rule("R122", Path, "%~dp0 拼接未加引号", "copy %~dp0a.txt ."),
            new Rule("R123", Syntax, "变量用了全角 ！ ％", "echo ！VAR！"),
            new Rule("R62", Syntax, "echo off 缺 @（该行会被回显）", "echo off"),
            new Rule("R9",  Syntax, "括号块内不能用 :: 注释", "if ... ( :: note )"),
            new Rule("R10", Syntax, "括号没有闭合", "if 1==1 ("),
            new Rule("R20", Syntax, "start 的第一个引号参数是窗口标题", "start \"app.exe\""),
            new Rule("R24", Syntax, "引号数量为奇数", "echo \"abc"),
            new Rule("R40", Syntax, "无引号的 %1 比较，参数为空时变成 if ==", "if %1==x echo hi"),

            // ── dependencyWarning ──
            new Rule("R15", Dependency, "调用外部程序，破坏零依赖前提", "curl -O url"),
        }.AsReadOnly();

        private static readonly Dictionary<string, Rule> ById =
            All.ToDictionary(r => r.Id, r => r, StringComparer.OrdinalIgnoreCase);

        /// <summary> 定性英文名 / Kind of a rule id. </summary>
        public static string KindOf(string ruleId)
            => ruleId != null && ById.TryGetValue(ruleId, out var rule) ? rule.Kind : Logic;

        /// <summary> 定性中文名 / Chinese label of a kind. </summary>
        public static string LabelOf(string kind)
        {
            switch (kind)
            {
                case Syntax: return "语法错误";
                case Logic: return "逻辑错误";
                case Type: return "类型错误";
                case Name: return "名称错误";
                case Flow: return "流程错误";
                case Path: return "路径错误";
                case IO: return "读写错误";
                case Style: return "写法警告";
                case Dependency: return "外部依赖";
                default: return "问题";
            }
        }

        /// <summary> 形如 "logicError: 逻辑错误" / The "<kind>: <label>" form. </summary>
        public static string Describe(string ruleId) => KindOf(ruleId) + ": " + LabelOf(KindOf(ruleId));

        /// <summary> 规则标题（未知规则返回空）/ Title of a rule. </summary>
        public static string TitleOf(string ruleId)
            => ruleId != null && ById.TryGetValue(ruleId, out var rule) ? rule.Title : string.Empty;

        /// <summary> 按定性分组，供 batlint --rules 输出 / Grouped catalog for the CLI. </summary>
        public static IEnumerable<string> Grouped()
        {
            var order = new[] { Syntax, Logic, Type, Name, Flow, Path, IO, Style, Dependency };
            foreach (var kind in order)
            {
                yield return kind + " " + LabelOf(kind);
                foreach (var rule in All.Where(r => r.Kind == kind))
                    yield return "  " + rule.Id.PadRight(4) + rule.Title.PadRight(26) + " 例: " + rule.Sample;
                yield return string.Empty;
            }
        }
    }
}
