using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BatLint.Core;

namespace BatLint.Cli
{
    /// <summary> batlint 命令行入口：检查 bat 脚本并输出问题与外部依赖 / Command-line entry point for the batch linter. </summary>
    internal static class Program
    {
        /// <summary> 执行检查并返回退出码：0 无问题、1 有问题、2 用法错误 / Run and return an exit code. </summary>
        private static int Main(string[] args)
        {
            // 统一 UTF-8：管道、CI、Windows Terminal 都用它，GBK 只适配旧 cmd
            Console.OutputEncoding = Encoding.UTF8;

            if (args.Length == 0 || args.Any(a => a == "-h" || a == "--help"))
            {
                PrintUsage();
                return args.Length == 0 ? 2 : 0;
            }

            // 增量通道基准：只重查一行 vs 全量
            if (args.Any(a => a.Equals("--bench-incremental", StringComparison.OrdinalIgnoreCase)))
            {
                var target = args.FirstOrDefault(a => !a.StartsWith("-"));
                if (target == null || !File.Exists(target))
                {
                    Console.Error.WriteLine("--bench-incremental 需要一个存在的文件");
                    return 2;
                }

                var src = File.ReadAllText(target, Encoding.GetEncoding(936));
                var lineCount = src.Split('\n').Length;
                var rounds = 50;

                BatLinter.Check(src);
                BatLinter.CheckLines(src, 0, lineCount - 1);

                var swFull = System.Diagnostics.Stopwatch.StartNew();
                for (var i = 0; i < rounds; i++) BatLinter.Check(src);
                swFull.Stop();

                var swInc = System.Diagnostics.Stopwatch.StartNew();
                for (var i = 0; i < rounds; i++) BatLinter.CheckLines(src, lineCount / 2, lineCount / 2 + 2);
                swInc.Stop();

                var full = swFull.Elapsed.TotalMilliseconds / rounds;
                var inc = swInc.Elapsed.TotalMilliseconds / rounds;

                Console.WriteLine($"文件: {target}  行数: {lineCount}");
                Console.WriteLine($"全量检查    : {full:0.00} ms");
                Console.WriteLine($"增量(3 行)  : {inc:0.00} ms   加速 {full / Math.Max(inc, 0.001):0.0}x");
                return 0;
            }

            // 打印逐行目录注解（验证 CwdTracker）
            if (args.Any(a => a.Equals("--cwd", StringComparison.OrdinalIgnoreCase)))
            {
                var target = args.FirstOrDefault(a => !a.StartsWith("-"));
                if (target == null || !File.Exists(target))
                {
                    Console.Error.WriteLine("--cwd 需要一个存在的文件");
                    return 2;
                }

                var src = File.ReadAllText(target, Encoding.GetEncoding(936));
                var map = CwdTracker.Compute(src);
                var lines = src.Replace("\r\n", "\n").Split('\n');
                for (var i = 0; i < lines.Length; i++)
                {
                    var no = i + 1;
                    var note = map.TryGetValue(no, out var dir) ? CwdTracker.Format(dir) : string.Empty;
                    Console.WriteLine($"{no,4} | {lines[i],-40} {note}");
                }
                return 0;
            }

            // 打印插桩后的调试副本（验证探针布局）
            if (args.Any(a => a.Equals("--debug-script", StringComparison.OrdinalIgnoreCase)))
            {
                var target = args.FirstOrDefault(a => !a.StartsWith("-"));
                if (target == null || !File.Exists(target))
                {
                    Console.Error.WriteLine("--debug-script 需要一个存在的文件");
                    return 2;
                }

                var src = File.ReadAllText(target, Encoding.GetEncoding(936));
                Console.Write(InteractiveDebugger.BuildScript(src, Array.Empty<int>()));
                return 0;
            }

            // 习惯学习校验：打印从该文件学到的习惯（不写用户目录）
            if (args.Any(a => a.Equals("--habits", StringComparison.OrdinalIgnoreCase)))
            {
                var target = args.FirstOrDefault(a => !a.StartsWith("-"));
                if (target == null || !File.Exists(target))
                {
                    Console.Error.WriteLine("--habits 需要一个存在的文件");
                    return 2;
                }

                var src = File.ReadAllText(target, Encoding.GetEncoding(936));
                var habitIssues = BatLinter.Check(src);
                var temp = Path.Combine(Path.GetTempPath(), "batide-habits-probe.txt");
                if (File.Exists(temp)) File.Delete(temp);

                var habits = new HabitProfile(temp);
                var learned = habits.Learn(src, habitIssues);
                Console.WriteLine($"学到 {learned} 条（某行有诊断则跳过该行），去重后 {habits.Count} 条");

                foreach (var e in habits.Top(HabitProfile.Kind.Path)) Console.WriteLine($"  path    x{e.Count,-3} {e.Text}");
                foreach (var e in habits.Top(HabitProfile.Kind.Command)) Console.WriteLine($"  command x{e.Count,-3} {e.Text}");
                foreach (var e in habits.Top(HabitProfile.Kind.Variable)) Console.WriteLine($"  var     x{e.Count,-3} {e.Text}");
                foreach (var e in habits.Top(HabitProfile.Kind.Label)) Console.WriteLine($"  label   x{e.Count,-3} {e.Text}");

                if (File.Exists(temp))
                    Console.WriteLine($"容量上限: {HabitProfile.MaxEntries} 条 / {HabitProfile.MaxBytes / 1024} KB，文件实际 " +
                                      new FileInfo(temp).Length + " 字节");
                return 0;
            }

            // 增量通道校验：只跑行内规则，用于与全量结果对比
            if (args.Any(a => a.Equals("--linelint", StringComparison.OrdinalIgnoreCase)))
            {
                var target = args.FirstOrDefault(a => !a.StartsWith("-"));
                if (target == null || !File.Exists(target))
                {
                    Console.Error.WriteLine("--linelint 需要一个存在的文件");
                    return 2;
                }

                var src = File.ReadAllText(target, Encoding.GetEncoding(936));
                foreach (var issue in BatLinter.CheckLines(src, 0, int.MaxValue).OrderBy(i => i.Start))
                    Console.WriteLine($"行 {issue.Line,4}  {issue.Rule,-4} {issue.Message}");
                return 0;
            }

            if (args.Any(a => a.Equals("--bench", StringComparison.OrdinalIgnoreCase)))
            {
                var target = args.FirstOrDefault(a => !a.StartsWith("-"));
                if (target == null || !File.Exists(target))
                {
                    Console.Error.WriteLine("--bench 需要一个存在的文件");
                    return 2;
                }

                var src = File.ReadAllText(target, Encoding.GetEncoding(936));
                var lineCount = src.Split('\n').Length;
                var rounds = 20;

                BatLinter.Check(src);                                   // 预热（JIT + 自动机构建）
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var found = 0;
                for (var i = 0; i < rounds; i++) found = BatLinter.Check(src).Count;
                sw.Stop();

                var perRun = sw.Elapsed.TotalMilliseconds / rounds;
                Console.WriteLine($"文件: {target}");
                Console.WriteLine($"行数: {lineCount}   问题: {found}   重复: {rounds} 次");
                Console.WriteLine($"平均耗时: {perRun:0.00} ms   吞吐: {lineCount / perRun:0.0} 行/ms   {src.Length / perRun:0.0} 字符/ms");
                return 0;
            }

            if (args.Any(a => a.Equals("--fixes", StringComparison.OrdinalIgnoreCase)))
            {
                var target = args.FirstOrDefault(a => !a.StartsWith("-"));
                if (target == null || !File.Exists(target))
                {
                    Console.Error.WriteLine("--fixes 需要一个存在的文件");
                    return 2;
                }

                var src = File.ReadAllText(target, Encoding.GetEncoding(936));
                foreach (var issue in BatLinter.Check(src).OrderBy(i => i.Start))
                {
                    var fixes = QuickFixes.For(src, issue);
                    Console.WriteLine($"行 {issue.Line,4}  {issue.Rule,-4} {issue.Message}");
                    if (fixes.Count == 0) Console.WriteLine("           （无自动修复）");
                    foreach (var fix in fixes)
                        Console.WriteLine($"           → {fix.Title}   {string.Join(" ", fix.Edits)}");
                }
                return 0;
            }

            if (args.Any(a => a.Equals("--rules", StringComparison.OrdinalIgnoreCase)))
            {
                foreach (var line in ErrorRules.Grouped()) Console.WriteLine(line);
                return 0;
            }

            var path = args.FirstOrDefault(a => !a.StartsWith("-"));
            if (path == null)
            {
                Console.Error.WriteLine("缺少文件路径");
                PrintUsage();
                return 2;
            }

            if (!File.Exists(path))
            {
                Console.Error.WriteLine($"找不到文件：{path}");
                return 2;
            }

            var text = File.ReadAllText(path, Encoding.GetEncoding(936));
            var issues = BatLinter.Check(text);
            var deps = DependencyScanner.Scan(text);

            if (args.Any(a => a.Equals("--json", StringComparison.OrdinalIgnoreCase)))
                Console.WriteLine(ToJson(path, issues, deps));
            else
                PrintReport(path, issues, deps);

            return issues.Count == 0 ? 0 : 1;
        }

        /// <summary> 打印用法 / Print usage. </summary>
        private static void PrintUsage()
        {
            Console.WriteLine("batlint - 批处理静态检查 / Batch script static checker");
            Console.WriteLine();
            Console.WriteLine("用法：batlint <文件.bat> [选项]");
            Console.WriteLine();
            Console.WriteLine("  --json    以 JSON 输出，便于接入 CI");
            Console.WriteLine("  --quiet   只输出问题清单，省略表头与统计");
            Console.WriteLine("  --rules   列出全部规则（按定性分组）与触发样例");
            Console.WriteLine("  --fixes   列出每处问题可用的自动修复（编辑器里对应 Ctrl+.）");
            Console.WriteLine("  --bench   跑 20 遍统计检查耗时（行/ms 吞吐）");
            Console.WriteLine("  --bench-incremental  对比「全量检查」与「只重查 3 行」的耗时");
            Console.WriteLine("  --habits  打印从文件学到的习惯语法（不写用户目录）");
            Console.WriteLine("  --debug-script  打印插桩后的调试副本（用于验证探针）");
            Console.WriteLine("  --cwd     打印逐行「当前目录」注解（验证 cd 跟踪）");
            Console.WriteLine("  -h        显示本帮助");
            Console.WriteLine();
            Console.WriteLine("退出码：0 未发现问题，1 发现问题，2 用法错误");
        }

        /// <summary> 人性化输出 / Human-readable report. </summary>
        private static void PrintReport(string path, List<LintIssue> issues, List<Dependency> deps)
        {
            Console.WriteLine($"{path}");

            if (issues.Count == 0)
            {
                Console.WriteLine("  未发现可判定的问题");
            }
            else
            {
                foreach (var issue in issues.OrderBy(i => i.Line).ThenBy(i => i.Rule))
                    Console.WriteLine($"  行 {issue.Line,4}  {issue.Rule,-4} {issue.KindLabel,-18} {issue.Message}");
            }

            if (deps.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("外部依赖（目标机器可能没有）");
                foreach (var d in deps)
                    Console.WriteLine($"  {d.Name,-12} 行 {d.Line}");
            }

            Console.WriteLine();
            Console.WriteLine(issues.Count == 0
                ? $"通过，共 0 处问题"
                : $"共 {issues.Count} 处问题：" + string.Join("、", issues
                    .GroupBy(i => i.Rule)
                    .OrderBy(g => int.Parse(g.Key.Substring(1)))
                    .Select(g => $"{g.Key} {g.Count()}")));
        }

        /// <summary> JSON 输出，手写拼装以保持零依赖 / Hand-rolled JSON to avoid extra dependencies. </summary>
        private static string ToJson(string path, List<LintIssue> issues, List<Dependency> deps)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"file\": {Quote(path)},");

            sb.AppendLine("  \"issues\": [");
            sb.AppendLine(string.Join(",\n", issues
                .OrderBy(i => i.Line)
                .Select(i => $"    {{ \"line\": {i.Line}, \"rule\": {Quote(i.Rule)}, \"kind\": {Quote(i.Kind)}, \"kindLabel\": {Quote(i.KindLabel)}, \"message\": {Quote(i.Message)} }}")));
            sb.AppendLine("  ],");

            sb.AppendLine("  \"dependencies\": [");
            sb.AppendLine(string.Join(",\n", deps
                .Select(d => $"    {{ \"name\": {Quote(d.Name)}, \"line\": {d.Line} }}")));
            sb.AppendLine("  ],");

            sb.AppendLine($"  \"total\": {issues.Count}");
            sb.Append("}");
            return sb.ToString();
        }

        /// <summary> JSON 字符串转义 / Escape a JSON string. </summary>
        private static string Quote(string value) =>
            "\"" + (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t") + "\"";
    }
}
