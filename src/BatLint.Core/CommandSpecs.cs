using System;
using System.Collections.Generic;
using System.Linq;

namespace BatLint.Core
{
    /// <summary>
    /// 参数级语法（方案 B）：为 copy / move / xcopy / robocopy / reg / sc 建“开关 + 操作数”语法，
    /// 用于「开关拼错」「参数不够」「开关冲突」「子命令拼错」四类提示。
    /// 数据来源：Windows 自带 help 输出 + 官方文档，逐条手工核对。
    /// </summary>
    internal static class CommandSpecs
    {
        /// <summary> 一个开关 / One switch. </summary>
        internal sealed class SwitchSpec
        {
            public SwitchSpec(string name, bool takesValue = false, params string[] aliases)
            {
                Name = name;
                TakesValue = takesValue;
                Names = new[] { name }.Concat(aliases).Select(x => x.ToLowerInvariant()).ToArray();
            }

            public string Name { get; }

            /// <summary> 是否后面要跟值（如 /D:日期、/EXCLUDE:文件）。 </summary>
            public bool TakesValue { get; }

            public string[] Names { get; }
        }

        /// <summary> 一条命令的语法 / Syntax of one command. </summary>
        internal sealed class CommandSpec
        {
            public CommandSpec(string name, int minOperands, int maxOperands, SwitchSpec[] switches,
                               Dictionary<string, CommandSpec> subCommands = null)
            {
                Name = name;
                MinOperands = minOperands;
                MaxOperands = maxOperands;
                Switches = switches;
                SubCommands = subCommands;
                SwitchNames = switches.SelectMany(s => s.Names).Distinct().ToArray();
            }

            public string Name { get; }
            public int MinOperands { get; }
            public int MaxOperands { get; }
            public SwitchSpec[] Switches { get; }

            /// <summary> reg / sc 这类有二级子命令的命令 / Two-level commands. </summary>
            public Dictionary<string, CommandSpec> SubCommands { get; }

            public string[] SwitchNames { get; }

            public SwitchSpec Find(string normalized)
                => Switches.FirstOrDefault(s => s.Names.Contains(normalized));
        }

        /// <summary> 冲突开关对（同时出现即互斥）/ Mutually exclusive switch pairs. </summary>
        internal static readonly (string Command, string A, string B, string Message)[] Conflicts =
        {
            ("copy", "a", "b", "/A（文本）和 /B（二进制）不能同时使用，后者生效"),
            ("copy", "y", "-y", "/Y 和 /-Y 互相矛盾，只保留一个"),
            ("move", "y", "-y", "/Y 和 /-Y 互相矛盾，只保留一个"),
            ("xcopy", "y", "-y", "/Y 和 /-Y 互相矛盾，只保留一个"),
            ("xcopy", "a", "m", "/A 与 /M 都处理存档属性，但重置方式不同，只保留一个")
        };

        private static Dictionary<string, CommandSpec> _all;

        /// <summary> 全部命令语法 / All command specs. </summary>
        internal static Dictionary<string, CommandSpec> All
        {
            get
            {
                if (_all != null) return _all;

                var copy = new CommandSpec("copy", 1, 2, new[]
                {
                    new SwitchSpec("a"), new SwitchSpec("b"), new SwitchSpec("d"), new SwitchSpec("v"),
                    new SwitchSpec("n"), new SwitchSpec("y"), new SwitchSpec("-y"), new SwitchSpec("z"),
                    new SwitchSpec("l")
                });

                var move = new CommandSpec("move", 2, 2, new[]
                {
                    new SwitchSpec("y"), new SwitchSpec("-y")
                });

                var xcopy = new CommandSpec("xcopy", 1, 2, new[]
                {
                    new SwitchSpec("a"), new SwitchSpec("m"), new SwitchSpec("d", takesValue: true),
                    new SwitchSpec("exclude", takesValue: true), new SwitchSpec("p"), new SwitchSpec("s"),
                    new SwitchSpec("e"), new SwitchSpec("v"), new SwitchSpec("w"), new SwitchSpec("c"),
                    new SwitchSpec("i"), new SwitchSpec("-i"), new SwitchSpec("q"), new SwitchSpec("f"),
                    new SwitchSpec("l"), new SwitchSpec("g"), new SwitchSpec("h"), new SwitchSpec("r"),
                    new SwitchSpec("t"), new SwitchSpec("u"), new SwitchSpec("k"), new SwitchSpec("n"),
                    new SwitchSpec("o"), new SwitchSpec("x"), new SwitchSpec("y"), new SwitchSpec("-y"),
                    new SwitchSpec("z"), new SwitchSpec("b"), new SwitchSpec("j"), new SwitchSpec("compress"),
                    new SwitchSpec("sparse")
                });

                var robocopy = new CommandSpec("robocopy", 2, 3, new[]
                {
                    new SwitchSpec("a", takesValue: true),
                    new SwitchSpec("b"),
                    new SwitchSpec("bytes"),
                    new SwitchSpec("compress"),
                    new SwitchSpec("copy", takesValue: true),
                    new SwitchSpec("copyall"),
                    new SwitchSpec("create"),
                    new SwitchSpec("dcopy", takesValue: true),
                    new SwitchSpec("dir"),
                    new SwitchSpec("dst"),
                    new SwitchSpec("e"),
                    new SwitchSpec("efsraw"),
                    new SwitchSpec("eta"),
                    new SwitchSpec("exclude", takesValue: true),
                    new SwitchSpec("f"),
                    new SwitchSpec("fat"),
                    new SwitchSpec("fft"),
                    new SwitchSpec("fp"),
                    new SwitchSpec("ia"),
                    new SwitchSpec("if"),
                    new SwitchSpec("im"),
                    new SwitchSpec("iomaxsize", takesValue: true),
                    new SwitchSpec("iorate", takesValue: true),
                    new SwitchSpec("ipg", takesValue: true),
                    new SwitchSpec("is"),
                    new SwitchSpec("it"),
                    new SwitchSpec("j"),
                    new SwitchSpec("job", takesValue: true),
                    new SwitchSpec("l"),
                    new SwitchSpec("lev"),
                    new SwitchSpec("lfsm", takesValue: true),
                    new SwitchSpec("log", takesValue: true),
                    new SwitchSpec("m"),
                    new SwitchSpec("max", takesValue: true),
                    new SwitchSpec("maxage", takesValue: true),
                    new SwitchSpec("maxlad", takesValue: true),
                    new SwitchSpec("mig"),
                    new SwitchSpec("mir"),
                    new SwitchSpec("min", takesValue: true),
                    new SwitchSpec("minage", takesValue: true),
                    new SwitchSpec("minlad", takesValue: true),
                    new SwitchSpec("mon", takesValue: true),
                    new SwitchSpec("mot", takesValue: true),
                    new SwitchSpec("mov"),
                    new SwitchSpec("move"),
                    new SwitchSpec("mt", takesValue: true),
                    new SwitchSpec("nc"),
                    new SwitchSpec("ndl"),
                    new SwitchSpec("nfl"),
                    new SwitchSpec("njh"),
                    new SwitchSpec("njs"),
                    new SwitchSpec("nocopy"),
                    new SwitchSpec("nodcopy"),
                    new SwitchSpec("nodd"),
                    new SwitchSpec("nooffload"),
                    new SwitchSpec("nosd"),
                    new SwitchSpec("np"),
                    new SwitchSpec("ns"),
                    new SwitchSpec("pf"),
                    new SwitchSpec("purge"),
                    new SwitchSpec("quit"),
                    new SwitchSpec("r", takesValue: true),
                    new SwitchSpec("reg", takesValue: true),
                    new SwitchSpec("rh", takesValue: true),
                    new SwitchSpec("s"),
                    new SwitchSpec("save", takesValue: true),
                    new SwitchSpec("sec"),
                    new SwitchSpec("secfix"),
                    new SwitchSpec("sj"),
                    new SwitchSpec("sl", takesValue: true),
                    new SwitchSpec("sparse"),
                    new SwitchSpec("tbd"),
                    new SwitchSpec("tee"),
                    new SwitchSpec("threshold", takesValue: true),
                    new SwitchSpec("timfix"),
                    new SwitchSpec("ts"),
                    new SwitchSpec("unicode"),
                    new SwitchSpec("unilog"),
                    new SwitchSpec("v"),
                    new SwitchSpec("w", takesValue: true),
                    new SwitchSpec("x"),
                    new SwitchSpec("xa", takesValue: true),
                    new SwitchSpec("xc", takesValue: true),
                    new SwitchSpec("xd", takesValue: true),
                    new SwitchSpec("xf", takesValue: true),
                    new SwitchSpec("xj"),
                    new SwitchSpec("xjd"),
                    new SwitchSpec("xjf"),
                    new SwitchSpec("xl"),
                    new SwitchSpec("xn", takesValue: true),
                    new SwitchSpec("xo", takesValue: true),
                    new SwitchSpec("xx"),
                    new SwitchSpec("z"),
                    new SwitchSpec("zb"),
                });

                var regSub = BuildRegSubCommands();
                var reg = new CommandSpec("reg", 1, 99, Array.Empty<SwitchSpec>(), regSub);

                var scSub = BuildScSubCommands();
                var sc = new CommandSpec("sc", 1, 99, Array.Empty<SwitchSpec>(), scSub);

                _all = new Dictionary<string, CommandSpec>(StringComparer.OrdinalIgnoreCase)
                {
                    ["copy"] = copy,
                    ["move"] = move,
                    ["xcopy"] = xcopy,
                    ["robocopy"] = robocopy,
                    ["reg"] = reg,
                    ["sc"] = sc
                };

                return _all;
            }
        }

        /// <summary> reg 的子命令与其开关 / reg subcommands and their switches. </summary>
        private static Dictionary<string, CommandSpec> BuildRegSubCommands()
        {
            var add = new CommandSpec("add", 1, 1, new[]
            {
                new SwitchSpec("v", takesValue: true), new SwitchSpec("ve"), new SwitchSpec("t", takesValue: true),
                new SwitchSpec("s", takesValue: true), new SwitchSpec("d", takesValue: true),
                new SwitchSpec("f"), new SwitchSpec("reg", takesValue: true)
            });

            var delete = new CommandSpec("delete", 1, 1, new[]
            {
                new SwitchSpec("v", takesValue: true), new SwitchSpec("ve"), new SwitchSpec("va"),
                new SwitchSpec("f"), new SwitchSpec("reg", takesValue: true)
            });

            var query = new CommandSpec("query", 1, 1, new[]
            {
                new SwitchSpec("v", takesValue: true), new SwitchSpec("ve"), new SwitchSpec("s"),
                new SwitchSpec("se"), new SwitchSpec("t", takesValue: true), new SwitchSpec("reg", takesValue: true)
            });

            var export = new CommandSpec("export", 2, 2, new[]
            {
                new SwitchSpec("y")
            });

            var import = new CommandSpec("import", 1, 1, Array.Empty<SwitchSpec>());

            var copy = new CommandSpec("copy", 2, 2, new[]
            {
                new SwitchSpec("s"), new SwitchSpec("f")
            });

            var compare = new CommandSpec("compare", 2, 2, new[]
            {
                new SwitchSpec("v", takesValue: true), new SwitchSpec("ve"), new SwitchSpec("s"),
                new SwitchSpec("se"), new SwitchSpec("t", takesValue: true), new SwitchSpec("reg", takesValue: true),
                new SwitchSpec("d")
            });

            var flags = new CommandSpec("flags", 1, 1, Array.Empty<SwitchSpec>());
            var save = new CommandSpec("save", 2, 2, new[] { new SwitchSpec("y") });
            var restore = new CommandSpec("restore", 2, 2, Array.Empty<SwitchSpec>());
            var load = new CommandSpec("load", 2, 2, Array.Empty<SwitchSpec>());
            var unload = new CommandSpec("unload", 1, 1, Array.Empty<SwitchSpec>());

            return new Dictionary<string, CommandSpec>(StringComparer.OrdinalIgnoreCase)
            {
                ["add"] = add, ["delete"] = delete, ["query"] = query, ["export"] = export,
                ["import"] = import, ["copy"] = copy, ["compare"] = compare, ["flags"] = flags,
                ["save"] = save, ["restore"] = restore, ["load"] = load, ["unload"] = unload
            };
        }

        /// <summary> sc 的子命令（参数用 key= value 形式）/ sc subcommands using key=value options. </summary>
        private static Dictionary<string, CommandSpec> BuildScSubCommands()
        {
            var create = new CommandSpec("create", 1, 1, new[]
            {
                new SwitchSpec("type", takesValue: true), new SwitchSpec("start", takesValue: true),
                new SwitchSpec("error", takesValue: true), new SwitchSpec("binpath", takesValue: true),
                new SwitchSpec("group", takesValue: true), new SwitchSpec("tag", takesValue: true),
                new SwitchSpec("depend", takesValue: true), new SwitchSpec("obj", takesValue: true),
                new SwitchSpec("displayname", takesValue: true), new SwitchSpec("password", takesValue: true)
            });

            var config = new CommandSpec("config", 1, 1, new[]
            {
                new SwitchSpec("type", takesValue: true), new SwitchSpec("start", takesValue: true),
                new SwitchSpec("error", takesValue: true), new SwitchSpec("binpath", takesValue: true),
                new SwitchSpec("group", takesValue: true), new SwitchSpec("tag", takesValue: true),
                new SwitchSpec("depend", takesValue: true), new SwitchSpec("obj", takesValue: true),
                new SwitchSpec("displayname", takesValue: true), new SwitchSpec("password", takesValue: true)
            });

            var query = new CommandSpec("query", 0, 1, new[]
            {
                new SwitchSpec("type", takesValue: true), new SwitchSpec("state", takesValue: true),
                new SwitchSpec("ri", takesValue: true), new SwitchSpec("group", takesValue: true),
                new SwitchSpec("resumable", takesValue: true)
            });

            var queryex = new CommandSpec("queryex", 0, 1, new[]
            {
                new SwitchSpec("type", takesValue: true), new SwitchSpec("state", takesValue: true),
                new SwitchSpec("ri", takesValue: true), new SwitchSpec("group", takesValue: true),
                new SwitchSpec("resumable", takesValue: true)
            });

            var failure = new CommandSpec("failure", 1, 1, new[]
            {
                new SwitchSpec("reset", takesValue: true), new SwitchSpec("reboot", takesValue: true),
                new SwitchSpec("command", takesValue: true), new SwitchSpec("actions", takesValue: true)
            });

            var control = new CommandSpec("control", 2, 2, Array.Empty<SwitchSpec>());
            var sdset = new CommandSpec("sdset", 2, 2, Array.Empty<SwitchSpec>());
            var sdshow = new CommandSpec("sdshow", 1, 1, Array.Empty<SwitchSpec>());
            var noArg = new[] { "start", "stop", "pause", "continue", "delete", "interrogate", "pause", "showsid", "triggerinfo" };

            var map = new Dictionary<string, CommandSpec>(StringComparer.OrdinalIgnoreCase)
            {
                ["create"] = create, ["config"] = config, ["query"] = query, ["queryex"] = queryex,
                ["failure"] = failure, ["control"] = control, ["sdset"] = sdset, ["sdshow"] = sdshow,
                ["enumdepend"] = new CommandSpec("enumdepend", 1, 2, Array.Empty<SwitchSpec>()),
                ["getdisplayname"] = new CommandSpec("getdisplayname", 1, 1, Array.Empty<SwitchSpec>()),
                ["getkeyname"] = new CommandSpec("getkeyname", 1, 1, Array.Empty<SwitchSpec>()),
                ["qc"] = new CommandSpec("qc", 1, 1, Array.Empty<SwitchSpec>()),
                ["qdescription"] = new CommandSpec("qdescription", 1, 1, Array.Empty<SwitchSpec>()),
                ["qfailure"] = new CommandSpec("qfailure", 1, 1, Array.Empty<SwitchSpec>()),
                ["qtriggerinfo"] = new CommandSpec("qtriggerinfo", 1, 1, Array.Empty<SwitchSpec>())
            };

            foreach (var name in noArg)
                map[name] = new CommandSpec(name, 1, 1, Array.Empty<SwitchSpec>());

            return map;
        }

        /// <summary> 子命令候选（用于拼写建议）/ All subcommand names. </summary>
        internal static IEnumerable<string> SubCommandNames(string command)
            => All.TryGetValue(command, out var spec) && spec.SubCommands != null
                ? spec.SubCommands.Keys
                : Enumerable.Empty<string>();
    }
}
