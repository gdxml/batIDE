using System;
using System.Collections.Generic;

namespace BatLint.Core
{
    /// <summary> 命令语法速查：输入命令加空格时弹出的一行提示 / One-line syntax hints shown after a command plus space. </summary>
    internal static class BatSyntaxTips
    {
        /// <summary> 命令或「命令 子命令」→ 语法提示 / Command or command+sub-command to a syntax hint. </summary>
        private static readonly Dictionary<string, string> Tips = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["for"] = "FOR %v IN (set) DO cmd   |   FOR /L %v IN (start,step,end) DO   |   FOR /F [\"options\"] %v IN (file) DO",
            ["if"] = "IF [NOT] EXIST file cmd   |   IF [/I] str1==str2 cmd   |   IF [NOT] ERRORLEVEL n cmd   |   IF DEFINED var cmd",
            ["set"] = "SET \"name=value\"   |   SET /A name=expr   |   SET /P name=prompt   （等号两侧不能有空格）",
            ["set /a"] = "SET /A result=expression   （表达式里直接写变量名，不加 %%：SET /A n=n+1）",
            ["set /p"] = "SET /P name=提示文本   （从用户输入取值，不回车不继续）",
            ["setlocal"] = "SETLOCAL ENABLEDELAYEDEXPANSION   （开启后才能用 !VAR!）",
            ["call"] = "CALL :label [args]   （调用子过程；CALL other.bat 调用另一个脚本并返回）",
            ["goto"] = "GOTO :label   |   GOTO :EOF 结束当前子过程",
            ["start"] = "START \"\" \"program.exe\" [args]   （第一个引号是窗口标题，必须留空串）",
            ["exit"] = "EXIT /B [code]   （/B 只退出当前脚本，不加会关掉调用方的 cmd 窗口）",
            ["shift"] = "SHIFT [/n]   （参数整体左移，%1 变成原来的 %2）",
            ["echo"] = "ECHO text   |   ECHO. 输出空行   |   ECHO OFF 关闭回显（行首加 @ 连本条也不回显）",
            ["del"] = "DEL [/F] [/S] [/Q] files   （/F 强制只读，/S 含子目录，/Q 不询问）",
            ["erase"] = "ERASE [/F] [/S] [/Q] files   （与 DEL 相同）",
            ["copy"] = "COPY [/Y] [/B] src dst   （/Y 不询问覆盖）",
            ["xcopy"] = "XCOPY src dst [/E /I /Y /S /H /R /K /D]   （/E 含空目录，/I 目标按目录处理）",
            ["robocopy"] = "ROBOCOPY src dst [/E] [/MIR] [/R:n] [/W:n] [/XD dir] [/LOG:file]   （返回码 0~7 都算成功）",
            ["move"] = "MOVE [/Y] src dst   （/Y 不询问覆盖）",
            ["ren"] = "REN oldname newname   （只改名字，不能换目录）",
            ["rename"] = "RENAME oldname newname   （与 REN 相同）",
            ["md"] = "MD dir   （建目录；建文件请用 echo 内容>文件）",
            ["mkdir"] = "MKDIR dir   （建目录；建文件请用 echo 内容>文件）",
            ["rd"] = "RD [/S] [/Q] dir   （/S 连子目录一起删，/Q 不确认）",
            ["rmdir"] = "RMDIR [/S] [/Q] dir   （与 RD 相同）",
            ["pushd"] = "PUSHD dir   （切目录并压栈，UNC 路径也能切；配 POPD 返回）",
            ["popd"] = "POPD   （回到最近一次 PUSHD 的目录）",
            ["cd"] = "CD [/D] [dir]   （跨盘切换必须加 /D）   |   CD .. 上一级   |   CD 显示当前目录",
            ["chdir"] = "CHDIR [/D] [dir]   （与 CD 相同）",
            ["dir"] = "DIR [/A] [/B] [/S] [/O] [/W] [path]   （/B 只列名，/S 含子目录）",
            ["type"] = "TYPE file   （按当前代码页显示文本内容）",
            ["find"] = "FIND [/V] [/N] [/I] \"string\" file   （只能找字面串，正则要用 FINDSTR）",
            ["findstr"] = "FINDSTR [/I] [/S] [/R] [/C:\"str\"] [/V] [/N] \"pattern\" files",
            ["attrib"] = "ATTRIB [+R|-R] [+H|-H] [+S|-S] [path]   （+/- 分别是加与去）",
            ["where"] = "WHERE [/R dir] name   （在 PATH 里查可执行文件位置）",
            ["reg"] = "REG ADD key [/v name] [/t type] [/d data] [/f]   （/f 免确认，缺了会卡住）",
            ["reg add"] = "REG ADD \"HKCU\\Software\\App\" /v Name /t REG_SZ /d value /f",
            ["reg query"] = "REG QUERY \"HKCU\\Software\\App\" [/v Name] [/s]",
            ["net"] = "NET USE \\\\server\\share   |   NET USER   |   NET START service   |   NET SHARE",
            ["net use"] = "NET USE \\\\server\\share [/user:name password]   |   NET USE X: \\\\server\\share 映射盘符",
            ["sc"] = "SC query|start|stop|config|create name   （服务名与显示名不同，注意区分）",
            ["schtasks"] = "SCHTASKS /CREATE /TN \"task\" /TR \"cmd /c x.bat\" /SC DAILY /ST 08:00 /F",
            ["taskkill"] = "TASKKILL [/F] [/IM name] [/PID n] [/T]   （/T 连子进程一起结束）",
            ["tasklist"] = "TASKLIST [/V] [/FI filter] [/FO table|csv]   （/V 显示详细信息）",
            ["timeout"] = "TIMEOUT /T seconds [/NOBREAK]   （不加 /NOBREAK 可被按键打断）",
            ["choice"] = "CHOICE [/C keys] [/N] [/T seconds /D key] [/M text]",
            ["chcp"] = "CHCP 65001   （切 UTF-8；中文脚本一般用 936）",
            ["title"] = "TITLE 窗口标题",
            ["color"] = "COLOR bg   （bg 十六进制，首位背景、次位前景，如 0A 黑底绿字）",
            ["pause"] = "PAUSE   （等待按键；无人值守脚本里会永久阻塞）",
            ["mklink"] = "MKLINK [/D] [/H] [/J] link target   （/D 目录符号链接，/J 目录联接）",
            ["icacls"] = "ICACLS path /grant user:(OI)(CI)F   |   /remove user   |   /reset",
            ["shutdown"] = "SHUTDOWN /S|/R [/T seconds] [/F]   （/S 关机，/R 重启）",
            ["assoc"] = "ASSOC .ext=fileType   （查看或设置扩展名关联）",
            ["ftype"] = "FTYPE fileType=\"program\" \"%1\"   （设置关联调用的命令行）",
            ["ver"] = "VER   （显示 Windows 版本）",
            ["whoami"] = "WHOAMI [/USER] [/GROUPS]   （显示当前账号与所属组）"
        };

        /// <summary> 按行内容取语法提示；先试「命令 子命令」，再退回命令本身 / Look up a hint, preferring the two-word form. </summary>
        public static string For(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return null;

            var t = line.TrimStart();
            if (t.StartsWith("@")) t = t.Substring(1).TrimStart();
            if (t.StartsWith("::")) return null;

            var parts = t.Split(new[] { ' ', '\t' }, 3, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return null;

            if (parts.Length >= 2 && Tips.TryGetValue(parts[0] + " " + parts[1], out var twoWord)) return twoWord;
            return Tips.TryGetValue(parts[0], out var oneWord) ? oneWord : null;
        }
    }
}
