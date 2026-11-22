using System;
using System.Collections.Generic;
using System.Linq;

namespace BatLint.Core
{
    /// <summary> bat 命令与关键字表，供自动补全使用 / Command and keyword table for autocompletion. </summary>
    internal static class BatCommands
    {
        /// <summary> 常用命令（按字母序）/ Common commands, alphabetical. </summary>
        public static readonly string[] Commands =
        {
            "assoc", "attrib", "bcdedit", "break", "cacls", "call", "cd", "certutil", "chcp",
            "chdir", "chkdsk", "choice", "cipher", "cls", "cmd", "color", "comp", "compact",
            "convert", "copy", "cscript", "date", "defrag", "del", "dir", "diskpart", "doskey",
            "driverquery", "echo", "endlocal", "erase", "exit", "expand", "fc", "find",
            "findstr", "for", "forfiles", "ftype", "goto", "gpresult", "gpupdate", "help",
            "icacls", "if", "ipconfig", "label", "logoff", "makecab", "md", "mkdir", "mklink",
            "mode", "more", "move", "msg", "net", "nslookup", "openfiles", "path", "pathping",
            "pause", "ping", "popd", "powercfg", "prompt", "powershell", "pushd", "query",
            "rd", "reg", "regsvr32", "rem", "ren", "rename", "rmdir", "robocopy", "runas",
            "rundll32", "sc", "schtasks", "set", "setlocal", "setx", "shift", "shutdown", "sort",
            "start", "subst", "systeminfo", "takeown", "taskkill", "tasklist", "time", "timeout",
            "title", "tracert", "tree", "type", "ver", "verify", "vol", "where", "whoami",
            "wmic", "wscript", "xcopy"
        };

        /// <summary> 关键字与比较运算符 / Keywords and comparison operators. </summary>
        public static readonly string[] Keywords =
        {
            "defined", "do", "else", "equ", "errorlevel", "exist", "geq", "gtr",
            "in", "leq", "lss", "neq", "not"
        };

        /// <summary> 全部候选项（命令 + 关键字，已排序）/ All candidates, sorted. </summary>
        public static IEnumerable<string> All() =>
            Commands.Concat(Keywords).OrderBy(x => x, StringComparer.OrdinalIgnoreCase);

        /// <summary> 按指定分隔符生成 AutoC 列表串 / Build the AutoC list with the given separator. </summary>
        public static string AutoCList(char separator) =>
            string.Join(separator.ToString(), All());

        /// <summary> 生成变量补全串 %VAR% 形式 / Build the %VAR% list. </summary>
        public static string VariableList(string[] names) =>
            string.Join(" ", names.Select(n => $"%{n}%"));
    }
}
