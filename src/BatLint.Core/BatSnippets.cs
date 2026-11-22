using System;
using System.Collections.Generic;
using System.Linq;

namespace BatLint.Core
{
    /// <summary> 一条常用输入习惯 / One frequently used input pattern. </summary>
    internal sealed class BatSnippet
    {
        /// <summary> 分类名 / Category name. </summary>
        public string Group { get; set; }

        /// <summary> 插入文本（单行，不含换行）/ Text inserted on accept. </summary>
        public string Code { get; set; }

        /// <summary> 一句话说明 / One-line description. </summary>
        public string Memo { get; set; }
    }

    /// <summary> 常用 bat 输入习惯表，供 AutoC 片段补全使用 / Common batch patterns for snippet completion. </summary>
    internal static class BatSnippets
    {
        /// <summary> 构造条目的简写 / Shorthand factory. </summary>
        private static BatSnippet S(string group, string code, string memo) =>
            new BatSnippet { Group = group, Code = code, Memo = memo };

        /// <summary> 全部输入习惯（按类别聚合，单行片段）/ All patterns, grouped, single-lined. </summary>
        public static readonly BatSnippet[] Table =
        {
            // ── 骨架 / Skeleton ──
            S("骨架", "@echo off", "关闭命令回显，脚本第一行"),
            S("骨架", "setlocal enabledelayedexpansion", "开启延迟展开，块内用 !VAR!"),
            S("骨架", "endlocal", "结束局部环境，与 setlocal 配对"),
            S("骨架", "exit /b %ERRORLEVEL%", "透传退出码给上层调用者"),
            S("骨架", "exit /b 0", "返回成功"),
            S("骨架", "exit /b 1", "返回失败"),
            S("骨架", "chcp 65001 >nul", "切 UTF-8 代码页，不打印提示"),
            S("骨架", "chcp 936 >nul", "切 GBK 代码页，旧脚本常用"),
            S("骨架", "cd /d \"%~dp0\"", "切到脚本所在目录（最常用的一行）"),
            S("骨架", "pushd \"%~dp0\"", "切到脚本目录并压栈，可 popd 返回"),
            S("骨架", "title \"脚本名\"", "设置窗口标题"),

            // ── 变量 / Variables ──
            S("变量", "set \"A=5\"", "赋值，引号包住整个表达式"),
            S("变量", "set \"A=%B%\"", "用另一个变量的值赋值"),
            S("变量", "set /a A=1+2", "算术运算赋值"),
            S("变量", "set /a A+=1", "自增"),
            S("变量", "set /p A=请输入:", "等待用户输入并存入 A"),
            S("变量", "set \"A=\"", "清空变量"),
            S("变量", "set \"A=%A:old=new%\"", "字符串替换：把 old 换成 new"),
            S("变量", "set \"A=%A:~0,3%\"", "取前 3 个字符"),
            S("变量", "set \"A=%~1\"", "取第一个参数并去掉外层引号"),
            S("变量", "set \"A=%~dp0\"", "脚本所在目录（含尾部反斜杠）"),
            S("变量", "set \"A=%~nx0\"", "脚本文件名加扩展名"),
            S("变量", "set \"A=%DATE:~0,10%\"", "截取日期前 10 位 yyyy-MM-dd"),
            S("变量", "set \"A=%RANDOM%\"", "0~32767 的随机数"),
            S("变量", "if not defined A set \"A=默认值\"", "未定义时给默认值"),
            S("变量", "echo %A%", "展开变量（解析时展开）"),
            S("变量", "echo !A!", "延迟展开，块内取新值"),
            S("变量", "echo %~nx0", "打印脚本文件名"),
            S("变量", "echo %PATH:;=&echo.%", "逐行打印 PATH 的每一项"),

            // ── 条件 / Condition ──
            S("条件", "if \"%1\"==\"\" (", "无参数时进入块"),
            S("条件", "if not \"%1\"==\"\" (", "有参数时进入块"),
            S("条件", "if exist \"path\" (", "路径存在则进入块"),
            S("条件", "if not exist \"path\" (", "路径不存在则进入块"),
            S("条件", "if errorlevel 1 (", "退出码 >= 1 时进入块"),
            S("条件", "if /i \"%A%\"==\"y\" (", "忽略大小写比较"),
            S("条件", "if defined A (", "变量已定义则进入块"),
            S("条件", "if \"%ERRORLEVEL%\"==\"0\" (", "精确判断退出码等于 0"),
            S("条件", "if \"%A%\"==\"1\" (echo one) else (echo other)", "单行 if/else，括号块可写一行"),
            S("条件", "if exist \"f.txt\" del \"f.txt\"", "存在则删除，单行写法"),
            S("条件", "if /i not \"%A%\"==\"y\" goto :eof", "条件不满足直接结束"),
            S("条件", ") else (", "条件块的 else 分支"),

            // ── 循环 / Loop ──
            S("循环", "for %%i in (*.txt) do", "遍历匹配的文件"),
            S("循环", "for /l %%i in (1,1,10) do", "计数循环：起点,步长,终点"),
            S("循环", "for /f \"tokens=1,2 delims=,\" %%a in (f.txt) do", "按分隔符读文本列"),
            S("循环", "for /f \"delims=\" %%i in ('dir /b') do", "整行读取命令输出"),
            S("循环", "for /f \"usebackq delims=\" %%i in (`cmd`) do", "反引号形式执行命令"),
            S("循环", "for /f \"skip=1 tokens=*\" %%i in (f.txt) do", "跳过表头，从第二行开始"),
            S("循环", "for /d %%i in (*) do", "只遍历目录"),
            S("循环", "for /r %%i in (*.bat) do", "递归遍历子目录"),
            S("循环", "for %%i in (a b c) do", "遍历字面量集合"),
            S("循环", "for %%i in (\"a b\" \"c d\") do", "遍历含空格的项，必须加引号"),

            // ── 流程 / Flow ──
            S("流程", "goto :eof", "跳到脚本末尾，结束当前子过程"),
            S("流程", "goto :label", "跳转到标签"),
            S("流程", ":label", "定义标签"),
            S("流程", "call :label", "调用子过程并返回"),
            S("流程", "call :label \"arg1\"", "带参数调用子过程"),
            S("流程", "call \"other.bat\"", "调用另一个批处理"),
            S("流程", "shift", "参数左移一位，取 %2 之后的参数"),
            S("流程", "if /i \"%~1\"==\"\" goto :usage", "参数校验失败跳到用法说明"),

            // ── 目录 / Directory ──
            S("目录", "cd /d \"D:\\\"", "切换盘符与目录，跨盘必须 /d"),
            S("目录", "pushd \"D:\\path\"", "切换并压栈，可 popd 返回"),
            S("目录", "popd", "回到 pushd 之前的目录"),
            S("目录", "md \"dir\"", "创建目录（存在也不报错）"),
            S("目录", "rd /s /q \"dir\"", "递归删除目录，不可恢复"),
            S("目录", "dir /b /s", "递归列出文件，仅名字"),
            S("目录", "dir /s /b \"*.log\"", "递归找所有 log 的完整路径"),
            S("目录", "subst X: \"D:\\long\\path\"", "把长路径映射成盘符"),

            // ── 文件 / Files ──
            S("文件", "copy /y \"a.txt\" \"b.txt\"", "复制并覆盖不询问"),
            S("文件", "copy /b \"a\"+\"b\" \"c\"", "二进制合并两个文件"),
            S("文件", "move /y \"a.txt\" \"b.txt\"", "移动并覆盖不询问"),
            S("文件", "xcopy /e /i /y \"src\" \"dst\"", "递归复制，含空目录"),
            S("文件", "robocopy \"src\" \"dst\" /e /r:1 /w:1", "大文件/长路径更可靠"),
            S("文件", "robocopy \"src\" \"dst\" /mir", "镜像同步，会删除目标多余文件"),
            S("文件", "del /f /q \"*.tmp\"", "强制静默删除"),
            S("文件", "ren \"a.txt\" \"b.txt\"", "重命名"),
            S("文件", "type \"f.txt\"", "输出文件内容"),
            S("文件", "type \"f.txt\" | findstr \"kw\"", "边读边过滤，大文件也不卡"),
            S("文件", "findstr /i /c:\"kw\" \"f.txt\"", "按字面量过滤内容"),
            S("文件", "findstr /s /i /n /c:\"kw\" *.txt", "递归搜索并显示行号"),
            S("文件", "forfiles /p \"dir\" /m *.log /c \"cmd /c del @path\"", "批量删除旧文件"),
            S("文件", "mklink /d \"link\" \"target\"", "创建目录符号链接（需管理员）"),

            // ── 输出 / Output ──
            S("输出", "echo hello", "打印一行文本"),
            S("输出", "echo.", "打印空行"),
            S("输出", "echo %DATE% %TIME%", "打印当前日期与时间"),
            S("输出", "echo hello > \"f.txt\"", "覆盖写入文件"),
            S("输出", "echo hello >> \"f.txt\"", "追加写入文件"),
            S("输出", ">>\"%LOG%\" 2>&1", "追加日志并把错误一起写入"),
            S("输出", ">nul 2>&1", "丢弃标准输出与错误输出"),
            S("输出", "2>nul", "只丢弃错误输出"),
            S("输出", "2>&1", "把错误输出并入标准输出"),

            // ── 进程与服务 / Process ──
            S("进程", "start \"\" \"app.exe\"", "异步启动，空标题不能省"),
            S("进程", "start \"\" /wait \"app.exe\"", "启动并等待结束才继续"),
            S("进程", "taskkill /f /im \"name.exe\"", "按进程名强杀"),
            S("进程", "taskkill /f /pid 1234", "按 PID 强杀"),
            S("进程", "tasklist | findstr \"name\"", "查找进程是否在运行"),
            S("进程", "timeout /t 5 /nobreak >nul", "等待 5 秒且不可被打断"),
            S("进程", "net start \"service\"", "启动服务"),
            S("进程", "net stop \"service\"", "停止服务"),
            S("进程", "sc query \"service\"", "查询服务状态"),
            S("进程", "sc start \"service\"", "启动服务（比 net 更细）"),
            S("进程", "net use \"\\\\server\\share\" /user:name pass", "映射网络共享"),
            S("进程", "schtasks /create /tn \"t\" /tr \"cmd /c x.bat\" /sc daily /st 08:00 /f", "创建每日定时任务"),
            S("进程", "schtasks /run /tn \"t\"", "立即运行指定任务"),
            S("进程", "reg add \"HKCU\\Software\\App\" /v Name /t REG_SZ /d Value /f", "写注册表，/f 免确认"),
            S("进程", "reg query \"HKLM\\Software\\App\" /v Name", "读注册表值"),
            S("进程", "icacls \"dir\" /grant user:(oi)(ci)f /t", "递归授予目录权限"),
            S("进程", "takeown /f \"file\" /a", "取得文件所有权"),
            S("进程", "ping -n 2 127.0.0.1 >nul", "延时约 1 秒，比 timeout 更兼容"),
            S("进程", "shutdown /r /t 0", "立即重启"),
            S("进程", "curl -L -o \"f.zip\" \"url\"", "下载文件（Win10 起自带）"),

            // ── 其他 / Misc ──
            S("其他", "pause", "等待按键，调试用"),
            S("其他", "cls", "清屏"),
            S("其他", "color 0A", "设置前景与背景色"),
            S("其他", "whoami", "显示当前用户"),
            S("其他", "where \"exe\"", "查可执行文件位置"),
            S("其他", "ver", "显示系统版本"),
            S("其他", "cmd /c \"...\"", "在新 cmd 里执行并返回"),
            S("其他", "powershell -NoProfile -Command \"...\"", "调用 PowerShell（会引入依赖）"),
            S("其他", "systeminfo", "系统详细信息"),
            S("其他", "ipconfig /flushdns", "清 DNS 缓存"),
            S("其他", "mode con cols=120 lines=40", "设置窗口大小")
        };

        /// <summary> 按前缀匹配片段，最多返回 limit 条 / Snippets whose code starts with the prefix. </summary>
        public static IEnumerable<BatSnippet> Matches(string prefix, int limit = 20) =>
            string.IsNullOrEmpty(prefix)
                ? Enumerable.Empty<BatSnippet>()
                : Table.Where(s => s.Code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).Take(limit);

        /// <summary> 按分类聚合，供帮助页展示 / Group patterns by category for the help window. </summary>
        public static IEnumerable<IGrouping<string, BatSnippet>> ByGroup() =>
            Table.GroupBy(s => s.Group);
    }
}
