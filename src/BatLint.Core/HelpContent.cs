using System.Collections.Generic;
using System.Linq;

namespace BatLint.Core
{
    /// <summary> 语法帮助的一个示例 / One help example. </summary>
    internal sealed class HelpExample
    {
        /// <summary> 示例代码 / Sample code. </summary>
        public string Code { get; set; }

        /// <summary> 作用说明 / What it does. </summary>
        public string Effect { get; set; }
    }

    /// <summary> 语法帮助的一个命令主题 / One command topic. </summary>
    internal sealed class HelpTopic
    {
        /// <summary> 左侧列表显示的名称 / Name shown in the navigation list. </summary>
        public string Name { get; set; }

        /// <summary> 一句话概述 / One-line summary. </summary>
        public string Summary { get; set; }

        /// <summary> 由易到难的示例 / Examples from basic to advanced. </summary>
        public HelpExample[] Examples { get; set; }
    }

    /// <summary> 内置语法帮助内容 / Built-in syntax help content. </summary>
    internal static class HelpContent
    {
        /// <summary> 全部主题（由基础到进阶）/ All topics ordered from basic to advanced. </summary>
        public static readonly HelpTopic[] Topics =
        {
            new HelpTopic
            {
                Name = "echo 输出",
                Summary = "把文字打印到控制台，或控制是否回显命令本身。",
                Examples = new[]
                {
                    new HelpExample { Code = "echo hello", Effect = "打印 hello" },
                    new HelpExample { Code = "echo.", Effect = "输出一个空行（点后面不要再加空格）" },
                    new HelpExample { Code = "@echo off", Effect = "关闭命令回显：之后只显示输出，不显示命令本身" },
                    new HelpExample { Code = "echo %PATH%", Effect = "打印环境变量 PATH 的值" },
                    new HelpExample { Code = "echo %DATE% %TIME%", Effect = "打印当前日期与时间" },
                    new HelpExample { Code = "echo hello > out.txt", Effect = "把 hello 写入 out.txt（覆盖原内容）" },
                    new HelpExample { Code = "echo hello >> out.txt", Effect = "把 hello 追加到 out.txt 末尾" },
                    new HelpExample { Code = "echo 中文内容", Effect = "文件需存为 GBK，否则控制台会乱码" },
                    new HelpExample { Code = "echo %ERRORLEVEL%", Effect = "打印上一条命令的退出码，0 表示成功" }
                }
            },
            new HelpTopic
            {
                Name = "set 变量",
                Summary = "定义变量、做算术、读用户输入；变量用 %NAME% 引用。",
                Examples = new[]
                {
                    new HelpExample { Code = "set NAME=world", Effect = "设置变量 NAME 为 world（等号两边不能有空格）" },
                    new HelpExample { Code = "set \"NAME=hello world\"", Effect = "推荐写法：整体加引号，值里有空格也安全" },
                    new HelpExample { Code = "echo %NAME%", Effect = "使用变量，输出 hello world" },
                    new HelpExample { Code = "set /a N=1+2*3", Effect = "算术运算，N 得到 7" },
                    new HelpExample { Code = "set /p AGE=请输入年龄：", Effect = "暂停并等待输入，用户输入写入 AGE" },
                    new HelpExample { Code = "set PATH", Effect = "只显示以 PATH 开头的变量（不加名字则列出全部）" },
                    new HelpExample { Code = "setlocal / endlocal", Effect = "局部变量作用域，endlocal 或脚本结束时恢复" }
                }
            },
            new HelpTopic
            {
                Name = "if 条件",
                Summary = "按字符串、数值、文件存在性、退出码分支。",
                Examples = new[]
                {
                    new HelpExample { Code = "if \"%1\"==\"\" echo 没有参数", Effect = "字符串比较，两侧都加引号可避免参数为空时报错" },
                    new HelpExample { Code = "if exist \"D:\\data\" echo 目录存在", Effect = "判断文件或目录是否存在" },
                    new HelpExample { Code = "if not exist log mkdir log", Effect = "不存在才创建目录" },
                    new HelpExample { Code = "if errorlevel 1 echo 失败", Effect = "退出码大于等于 1 时成立（不是等于）" },
                    new HelpExample { Code = "if %ERRORLEVEL%==0 echo 成功", Effect = "精确判断退出码等于 0" },
                    new HelpExample { Code = "if /i \"%YN%\"==\"y\" echo 是", Effect = "/i 忽略大小写比较" },
                    new HelpExample { Code = "if \"%1\"==\"\" (echo 用法) else (echo 继续)", Effect = "else 必须与右括号写在同一行" }
                }
            },
            new HelpTopic
            {
                Name = "for 循环",
                Summary = "遍历文件、目录、文本行、数字区间与命令输出。",
                Examples = new[]
                {
                    new HelpExample { Code = "for %%f in (*.txt) do echo %%f", Effect = "遍历当前目录的 txt 文件；脚本里必须用 %%" },
                    new HelpExample { Code = "for /d %%d in (*) do echo %%d", Effect = "/d 只遍历目录" },
                    new HelpExample { Code = "for /r %%f in (*.log) do echo %%f", Effect = "/r 递归子目录" },
                    new HelpExample { Code = "for /l %%i in (1,1,5) do echo %%i", Effect = "数字循环：从 1 开始，步长 1，到 5 结束" },
                    new HelpExample { Code = "for /f \"tokens=1,2 delims=,\" %%a in (x.csv) do echo %%a-%%b", Effect = "按逗号取前两列" },
                    new HelpExample { Code = "for /f \"delims=\" %%l in (x.txt) do echo %%l", Effect = "整行读取，保留行首空格" },
                    new HelpExample { Code = "for /f %%i in ('dir /b') do echo %%i", Effect = "遍历命令输出（注意用单引号）" }
                }
            },
            new HelpTopic
            {
                Name = "cd / dir 目录",
                Summary = "切换目录与查看目录内容；跨盘必须加 /d。",
                Examples = new[]
                {
                    new HelpExample { Code = "cd", Effect = "显示当前目录" },
                    new HelpExample { Code = "cd \\tools", Effect = "切到当前盘的 \\tools" },
                    new HelpExample { Code = "cd /d \"D:\\data\"", Effect = "同时切换驱动器与目录，跨盘必须加 /d" },
                    new HelpExample { Code = "cd ..", Effect = "回到上一级目录" },
                    new HelpExample { Code = "pushd \"D:\\data\"", Effect = "切目录并记住原位置" },
                    new HelpExample { Code = "popd", Effect = "回到 pushd 之前的目录" },
                    new HelpExample { Code = "dir /b /a-d", Effect = "只列文件名，不列目录，不带表头" },
                    new HelpExample { Code = "dir /s /b *.log", Effect = "递归列出所有 log 的完整路径" }
                }
            },
            new HelpTopic
            {
                Name = "call / goto 流程",
                Summary = "模块化脚本的两块基石：标签跳转与子过程调用。",
                Examples = new[]
                {
                    new HelpExample { Code = "goto :eof", Effect = "立即结束当前脚本或子过程" },
                    new HelpExample { Code = "goto :build", Effect = "跳到 :build 标签处继续执行" },
                    new HelpExample { Code = ":build", Effect = "定义标签，标签行不会被执行" },
                    new HelpExample { Code = "call :build arg1", Effect = "调用子过程，执行完回到调用点" },
                    new HelpExample { Code = "call other.bat", Effect = "调用另一个脚本并返回；不加 call 当前脚本会直接结束" },
                    new HelpExample { Code = "exit /b 1", Effect = "从子过程或脚本返回，并设置退出码 1" },
                    new HelpExample { Code = "if \"%1\"==\"\" goto :usage", Effect = "参数校验失败时跳到用法说明" }
                }
            },
            new HelpTopic
            {
                Name = "文件操作",
                Summary = "复制、移动、删除、创建目录；通配符与 /s /q 要格外小心。",
                Examples = new[]
                {
                    new HelpExample { Code = "copy a.txt b.txt", Effect = "复制文件（不复制子目录）" },
                    new HelpExample { Code = "xcopy src dst /E /I /Y", Effect = "复制目录树，/E 含空目录，/I 目标按目录处理" },
                    new HelpExample { Code = "robocopy src dst /E /R:1 /W:1", Effect = "大目录首选，失败重试次数可控" },
                    new HelpExample { Code = "move a.txt D:\\data\\", Effect = "移动文件，同盘内也可用于重命名" },
                    new HelpExample { Code = "ren a.txt b.txt", Effect = "重命名（不能跨目录）" },
                    new HelpExample { Code = "del /q *.tmp", Effect = "静默删除匹配文件（不进回收站）" },
                    new HelpExample { Code = "md log", Effect = "创建目录（已存在时返回错误码 1）" },
                    new HelpExample { Code = "rd /s /q old", Effect = "递归强删目录，不可恢复，务必先确认路径" }
                }
            },
            new HelpTopic
            {
                Name = "延迟展开 !VAR!",
                Summary = "同一条语句里刚赋值的变量，%VAR% 读不到，要用 !VAR!。",
                Examples = new[]
                {
                    new HelpExample { Code = "setlocal enabledelayedexpansion", Effect = "开启延迟展开，!VAR! 才可用" },
                    new HelpExample { Code = "set N=1 & set N=2 & echo %N%", Effect = "输出 1：整行先展开，%N% 是旧值" },
                    new HelpExample { Code = "set N=1 & set N=2 & echo !N!", Effect = "输出 2：!N! 在执行到该处时才取值" },
                    new HelpExample { Code = "for /l %%i in (1,1,3) do (set S=%%i & echo !S!)", Effect = "循环内必须用 !S!" },
                    new HelpExample { Code = "set \"STR=hello\" & echo !STR:~0,3!", Effect = "取子串，输出 hel" },
                    new HelpExample { Code = "set \"STR=hello\" & echo !STR:l=L!", Effect = "字符串替换，输出 heLLo" }
                }
            },
            new HelpTopic
            {
                Name = "重定向与管道",
                Summary = "把输出接到文件或下一条命令；1 是标准输出，2 是错误输出。",
                Examples = new[]
                {
                    new HelpExample { Code = "dir > list.txt", Effect = "标准输出覆盖写入文件" },
                    new HelpExample { Code = "dir >> list.txt", Effect = "追加写入" },
                    new HelpExample { Code = "dir 2> err.txt", Effect = "只把错误输出写入文件" },
                    new HelpExample { Code = "cmd /c build.bat >nul 2>&1", Effect = "丢弃全部输出（成功与错误都静默）" },
                    new HelpExample { Code = "tasklist | findstr /i chrome", Effect = "管道：把前一条命令的输出交给 findstr 过滤" },
                    new HelpExample { Code = "dir | more", Effect = "分页显示输出" }
                }
            },
            new HelpTopic
            {
                Name = "退出码与错误处理",
                Summary = "%ERRORLEVEL% 是上一条命令的结果，0 通常表示成功。",
                Examples = new[]
                {
                    new HelpExample { Code = "echo %ERRORLEVEL%", Effect = "打印上一条命令的退出码" },
                    new HelpExample { Code = "if errorlevel 1 goto :fail", Effect = "大于等于 1 就跳到失败处理（不是等于）" },
                    new HelpExample { Code = "my.exe & if %ERRORLEVEL% neq 0 echo 失败", Effect = "用 & 串联后立即判断退出码" },
                    new HelpExample { Code = "exit /b %ERRORLEVEL%", Effect = "把退出码透传给上层调用者" },
                    new HelpExample { Code = "set RC=%ERRORLEVEL%", Effect = "先存起来，避免后续命令覆盖" },
                    new HelpExample { Code = "echo 完成 & exit /b 0", Effect = "主动返回成功" }
                }
            },
            new HelpTopic
            {
                Name = "进程与服务",
                Summary = "查看、结束进程，启停 Windows 服务。",
                Examples = new[]
                {
                    new HelpExample { Code = "tasklist | findstr /i java", Effect = "查找指定进程" },
                    new HelpExample { Code = "taskkill /f /im notepad.exe", Effect = "按镜像名强制结束进程" },
                    new HelpExample { Code = "taskkill /f /pid 1234", Effect = "按进程号结束" },
                    new HelpExample { Code = "sc query MyService", Effect = "查看服务状态" },
                    new HelpExample { Code = "net start MyService", Effect = "启动服务（需管理员权限）" },
                    new HelpExample { Code = "net stop MyService", Effect = "停止服务" },
                    new HelpExample { Code = "start \"\" \"C:\\app\\a.exe\"", Effect = "启动程序；第一对空引号是窗口标题，不能省" }
                }
            },
            new HelpTopic
            {
                Name = "编码与兼容",
                Summary = "中文乱码几乎都与代码页和文件编码有关。",
                Examples = new[]
                {
                    new HelpExample { Code = "chcp 936", Effect = "切到 GBK，适合简体中文旧脚本" },
                    new HelpExample { Code = "chcp 65001", Effect = "切到 UTF-8，文件要存成 UTF-8 无 BOM" },
                    new HelpExample { Code = "type utf8.txt", Effect = "用错代码页时中文会显示成乱码" },
                    new HelpExample { Code = "@echo off & chcp 65001 >nul", Effect = "切码页但不打印切换信息" },
                    new HelpExample { Code = "set \"PYTHONIOENCODING=utf-8\"", Effect = "给子进程传递编码设置" }
                }
            },
            SnippetTopic()
        };

        /// <summary> 由输入习惯表生成的主题，与补全候选一一对应 / Topic generated from the snippet table. </summary>
        private static HelpTopic SnippetTopic() => new HelpTopic
        {
            Name = "输入习惯速查",
            Summary = "编辑器里输入前缀后按 Tab 即可插入；本页按类别列出全部候选。",
            Examples = BatSnippets.Table
                .Select(s => new HelpExample { Code = s.Code, Effect = $"[{s.Group}] {s.Memo}" })
                .ToArray()
        };

        /// <summary> 主题数量 / Number of topics. </summary>
        public static int Count => Topics.Length;
    }
}
