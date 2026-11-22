using System;
using System.Collections.Generic;
using System.Linq;

namespace BatLint.Core
{
    /// <summary> 命令的子命令与开关表 / Sub-commands and switches keyed by command name. </summary>
    internal static class BatVocabulary
    {
        /// <summary> 命令 → 子命令/开关 / Command to argument list. </summary>
        public static readonly IReadOnlyDictionary<string, string[]> Args =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["attrib"] = new[] { "+r", "-r", "+h", "-h", "+s", "-s", "/s", "/d" },
                ["cd"] = new[] { "/d" },
                ["chdir"] = new[] { "/d" },
                ["choice"] = new[] { "/c", "/n", "/t", "/d", "/m", "/cs" },
                ["copy"] = new[] { "/y", "/b", "/v", "/a", "/d", "/-y" },
                ["date"] = new[] { "/t" },
                ["del"] = new[] { "/f", "/q", "/s", "/p", "/a" },
                ["erase"] = new[] { "/f", "/q", "/s", "/p", "/a" },
                ["dir"] = new[] { "/a", "/b", "/o", "/s", "/w", "/p", "/t", "/x", "/r", "/-c" },
                ["exit"] = new[] { "/b" },
                ["find"] = new[] { "/v", "/n", "/i", "/c", "/off" },
                ["findstr"] = new[] { "/i", "/s", "/r", "/c", "/v", "/n", "/m", "/b", "/e", "/l", "/g", "/f", "/x", "/p", "/off" },
                ["for"] = new[] { "/d", "/r", "/l", "/f" },
                ["if"] = new[] { "/i" },
                ["move"] = new[] { "/y", "/-y" },
                ["net"] = new[] { "use", "start", "stop", "pause", "continue", "user", "group", "localgroup", "share", "session", "view", "config", "accounts", "statistics", "time", "print", "send" },
                ["rd"] = new[] { "/s", "/q" },
                ["rmdir"] = new[] { "/s", "/q" },
                ["reg"] = new[] { "add", "delete", "query", "export", "import", "copy", "compare", "load", "unload", "restore", "save" },
                ["ren"] = new string[0],
                ["robocopy"] = new[] { "/e", "/s", "/r:1", "/w:1", "/mir", "/mov", "/move", "/purge", "/xd", "/xf", "/log", "/np", "/njh", "/njs", "/tee", "/mt", "/z", "/b", "/xo", "/xn", "/xc", "/xo", "/l" },
                ["sc"] = new[] { "query", "start", "stop", "pause", "continue", "config", "create", "delete", "description", "qc", "failure", "sdshow", "sdset", "enumdepend" },
                ["set"] = new[] { "/a", "/p" },
                ["setlocal"] = new[] { "enabledelayedexpansion", "enabledextensions", "disabledelayedexpansion", "disableextensions" },
                ["start"] = new[] { "/wait", "/b", "/min", "/max", "/d", "/i", "/low", "/normal", "/high", "/realtime", "/affinity" },
                ["taskkill"] = new[] { "/f", "/im", "/pid", "/t", "/fi" },
                ["tasklist"] = new[] { "/v", "/fi", "/fo", "/svc", "/m", "/nh" },
                ["time"] = new[] { "/t" },
                ["timeout"] = new[] { "/t", "/nobreak" },
                ["tree"] = new[] { "/f", "/a" },
                ["where"] = new[] { "/r", "/q", "/f", "/t" },
                ["whoami"] = new[] { "/all", "/user", "/groups", "/priv", "/fo", "/upn", "/fqdn" },
                ["xcopy"] = new[] { "/e", "/i", "/y", "/s", "/h", "/r", "/k", "/o", "/d", "/u", "/c", "/q", "/f", "/l", "/b", "/v", "/w", "/p", "/g", "/t", "/-y" },
                ["mklink"] = new[] { "/d", "/h", "/j" },
                ["icacls"] = new[] { "/grant", "/remove", "/deny", "/reset", "/t", "/c", "/q", "/inheritance", "/grant:r" },
                ["takeown"] = new[] { "/f", "/r", "/a", "/d" },
                ["schtasks"] = new[] { "/create", "/delete", "/query", "/run", "/end", "/change", "/tn", "/tr", "/sc", "/st", "/ru", "/rp", "/f", "/rl", "/it" },
                ["shutdown"] = new[] { "/s", "/r", "/t", "/a", "/f", "/m", "/c", "/h" },
                ["ping"] = new[] { "-t", "-n", "-l", "-w", "-4", "-6", "-a" },
                ["ipconfig"] = new[] { "/all", "/flushdns", "/release", "/renew", "/displaydns", "/registerdns" },
                ["nslookup"] = new string[0],
                ["curl"] = new[] { "-o", "-L", "-s", "-S", "-f", "-k", "-H", "-X", "-d" },
                ["certutil"] = new[] { "-hashfile", "-decode", "-encode", "-urlcache", "-addstore", "-delstore" },
                ["powershell"] = new[] { "-NoProfile", "-ExecutionPolicy", "-Command", "-File", "-WindowStyle", "-NonInteractive", "Bypass" },
                ["wmic"] = new[] { "process", "service", "os", "logicaldisk", "computersystem", "product", "startup" },
                ["msg"] = new[] { "*", "/time", "/v", "/w" },
                ["runas"] = new[] { "/user", "/savecred", "/env" },
                ["doskey"] = new[] { "/macrofile", "/history", "/listsize", "/reinstall", "/macros" },
                ["compact"] = new[] { "/c", "/u", "/s", "/f", "/q", "/a", "/i", "/exe" },
                ["cipher"] = new[] { "/e", "/d", "/w", "/s", "/r", "/h" },
                ["expand"] = new[] { "-f:", "-d", "-r" },
                ["makecab"] = new[] { "/d", "/f", "/l", "/v" },
                ["forfiles"] = new[] { "/p", "/m", "/s", "/c", "/d" },
                ["sort"] = new[] { "/r", "/+n", "/m" },
                ["fc"] = new[] { "/b", "/n", "/c", "/a", "/l", "/t", "/w", "/u" },
                ["chkdsk"] = new[] { "/f", "/r", "/x", "/v", "/c", "/i", "/b" },
                ["format"] = new[] { "/q", "/fs", "/v", "/a", "/y", "/p" },
                ["bcdedit"] = new[] { "/set", "/enum", "/copy", "/delete", "/create", "/export", "/import" },
                ["powercfg"] = new[] { "/list", "/setactive", "/change", "/hibernate", "/energy", "/devicequery" },
                ["gpupdate"] = new[] { "/force", "/target", "/boot", "/logoff" },
                ["gpresult"] = new[] { "/r", "/v", "/z", "/h" },
                ["systeminfo"] = new[] { "/fo", "/s", "/nh" },
                ["driverquery"] = new[] { "/v", "/si", "/fo", "/nh" },
                ["openfiles"] = new[] { "/query", "/local", "/disconnect" },
                ["query"] = new[] { "user", "session", "process" },
                ["cscript"] = new[] { "//nologo", "//e:", "//t:" },
                ["wscript"] = new[] { "//nologo", "//b", "//t:" },
                ["regsvr32"] = new[] { "/s", "/u", "/i", "/n" },
                ["assoc"] = new string[0],
                ["ftype"] = new string[0],
                ["net1"] = new[] { "use", "start", "stop", "user" },
                ["vol"] = new string[0],
                ["type"] = new string[0],
                ["echo"] = new string[0]
            };

        /// <summary> 取某命令在当前 token 下的候选 / Candidates for the given command and token. </summary>
        public static IEnumerable<string> ArgsFor(string command, string token)
        {
            if (string.IsNullOrEmpty(command) || !Args.TryGetValue(command, out var list) || list.Length == 0)
                return Enumerable.Empty<string>();

            return token.Length == 0
                ? list.Where(IsSub)
                : list.Where(x => x.StartsWith(token, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary> 是否子命令（非开关）/ Whether the entry is a sub-command rather than a switch. </summary>
        private static bool IsSub(string x) => !x.StartsWith("/") && !x.StartsWith("-");
    }
}
