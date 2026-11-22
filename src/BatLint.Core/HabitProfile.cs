using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace BatLint.Core
{
    /// <summary>
    /// 用户习惯学习：只从**检查通过（没有诊断）的行**里学，避免把错写法记成习惯。
    /// 学什么：
    ///   · 绝对路径（如 D:\某个工程\data\）—— 用户常访问的目录
    ///   · 命令 + 常用开关组合（如 robocopy /mir /z）
    ///   · 变量名前缀（如 set DATA_…）
    ///   · 常用标签名前缀
    /// 容量控制：最多 500 条 / 100 KB，按「使用次数 + 最近使用」淘汰。
    /// </summary>
    internal sealed class HabitProfile
    {
        /// <summary> 存储条目上限（行数）/ Max entries. </summary>
        public const int MaxEntries = 500;

        /// <summary> 存储文件大小上限（字节）/ Max file size. </summary>
        public const int MaxBytes = 100 * 1024;

        internal enum Kind { Path = 0, Command = 1, Variable = 2, Label = 3 }

        internal sealed class Entry
        {
            public Kind Kind;
            public string Text;
            public int Count;
            public DateTime LastUsed;

            /// <summary> 价值分：次数为主，最近使用为辅（用于淘汰）/ Ranking value. </summary>
            public double Score => Count + 1.0 / Math.Max(1, (DateTime.UtcNow - LastUsed).TotalDays + 1);
        }

        private readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        private readonly string _path;

        /// <summary> 最近一次保存是否发生淘汰 / Whether the last save had to evict. </summary>
        public int EvictedOnLastSave { get; private set; }

        /// <summary> 创建实例（可指定存储路径）/ Create with an optional storage path. </summary>
        public HabitProfile(string path = null)
        {
            _path = path ?? DefaultPath();
        }

        /// <summary> 默认存储位置 / Default storage path. </summary>
        public static string DefaultPath()
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "batIDE");
            return Path.Combine(dir, "habits.txt");
        }

        /// <summary> 条目数 / Entry count. </summary>
        public int Count => _entries.Count;

        /// <summary> 从文件加载 / Load from disk. </summary>
        public void Load()
        {
            try
            {
                if (!File.Exists(_path)) return;

                foreach (var line in File.ReadAllLines(_path, Encoding.UTF8))
                {
                    var parts = line.Split('\t');
                    if (parts.Length < 3) continue;
                    if (!int.TryParse(parts[0], out var kind)) continue;
                    if (!int.TryParse(parts[1], out var count)) continue;

                    var text = parts[2];
                    if (string.IsNullOrWhiteSpace(text)) continue;

                    _entries[Key((Kind)kind, text)] = new Entry
                    {
                        Kind = (Kind)kind,
                        Text = text,
                        Count = count,
                        LastUsed = parts.Length > 3 && DateTime.TryParse(parts[3], out var t) ? t : DateTime.UtcNow
                    };
                }
            }
            catch
            {
                // 读失败不影响使用
            }
        }

        private DateTime _lastSave = DateTime.MinValue;

        /// <summary> 节流保存：最多 5 秒写一次盘，避免每次检查都做 IO / Throttled save. </summary>
        public void SaveThrottled()
        {
            if ((DateTime.UtcNow - _lastSave).TotalSeconds < 5) return;
            _lastSave = DateTime.UtcNow;
            Save();
        }

        /// <summary> 保存到磁盘，超过上限时按分数淘汰 / Save with eviction. </summary>
        public void Save()
        {
            EvictedOnLastSave = 0;
            try
            {
                var dir = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

                var kept = SelectForStorage(out var evicted);
                EvictedOnLastSave = evicted;

                var sb = new StringBuilder();
                foreach (var e in kept)
                    sb.Append((int)e.Kind).Append('\t').Append(e.Count).Append('\t').Append(e.Text)
                      .Append('\t').Append(e.LastUsed.ToString("yyyy-MM-dd HH:mm")).Append('\n');

                File.WriteAllText(_path, sb.ToString(), Encoding.UTF8);
            }
            catch
            {
                // 写失败不影响编辑
            }
        }

        /// <summary> 从一段脚本里学习（只学没有问题的行）/ Learn from lint-clean lines. </summary>
        public int Learn(string text, IEnumerable<LintIssue> issues)
        {
            if (string.IsNullOrEmpty(text)) return 0;

            // 有诊断的行号集合（这些行不学）
            var badLines = new HashSet<int>(issues?.Select(i => i.Line) ?? Enumerable.Empty<int>());
            var lines = ExtraRules.SplitLines(text, out _);
            var learned = 0;
            var changed = false;

            for (var i = 0; i < lines.Length; i++)
            {
                if (badLines.Contains(i + 1)) continue;

                var t = lines[i].Trim();
                if (t.Length < 4) continue;
                if (t.StartsWith("::") || t.StartsWith("rem", StringComparison.OrdinalIgnoreCase)) continue;

                // 路径：D:\a\b\ 这类
                foreach (var path in ExtractPaths(t))
                {
                    changed |= Bump(Kind.Path, path);
                    learned++;
                }

                // 命令 + 开关组合
                var cmd = ExtractCommandCombo(t);
                if (cmd != null)
                {
                    changed |= Bump(Kind.Command, cmd);
                    learned++;
                }

                // 变量前缀（set 的数据名）
                var variable = ExtractVariablePrefix(t);
                if (variable != null)
                {
                    changed |= Bump(Kind.Variable, variable);
                    learned++;
                }

                // 标签名
                if (t.StartsWith(":") && !t.StartsWith("::"))
                {
                    var label = t.Substring(1).Split(new[] { ' ', '\t' }, 2)[0];
                    if (label.Length >= 3)
                    {
                        changed |= Bump(Kind.Label, label);
                        learned++;
                    }
                }
            }

            if (changed) SaveThrottled();
            return learned;
        }

        /// <summary> 按前缀给出习惯建议（供补全优先排序）/ Suggestions for a prefix. </summary>
        public IEnumerable<string> Suggest(string prefix, Kind kind, int max = 20)
        {
            var p = prefix ?? string.Empty;
            return _entries.Values
                .Where(e => e.Kind == kind)
                .Where(e => p.Length == 0 || e.Text.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderByDescending(e => e.Score)
                .Take(max)
                .Select(e => e.Text);
        }

        /// <summary> 取某类全部条目（按分数）/ All entries of a kind ordered by score. </summary>
        public IReadOnlyList<Entry> Top(Kind kind, int max = 50)
            => _entries.Values.Where(e => e.Kind == kind).OrderByDescending(e => e.Score).Take(max).ToList();

        // ── 内部 ──

        private static string Key(Kind kind, string text) => (int)kind + "|" + text;

        private bool Bump(Kind kind, string text)
        {
            var key = Key(kind, text);
            if (_entries.TryGetValue(key, out var entry))
            {
                entry.Count++;
                entry.LastUsed = DateTime.UtcNow;
                return true;
            }

            _entries[key] = new Entry { Kind = kind, Text = text, Count = 1, LastUsed = DateTime.UtcNow };
            return true;
        }

        private List<Entry> SelectForStorage(out int evicted)
        {
            var ordered = _entries.Values.OrderByDescending(e => e.Score).ToList();

            var kept = new List<Entry>();
            var bytes = 0;
            var limit = Math.Min(MaxEntries, ordered.Count);

            foreach (var e in ordered)
            {
                if (kept.Count >= limit) break;

                var size = Encoding.UTF8.GetByteCount(e.Text) + 24;
                if (bytes + size > MaxBytes) continue;

                kept.Add(e);
                bytes += size;
            }

            evicted = ordered.Count - kept.Count;
            return kept;
        }

        /// <summary> 提取绝对路径（盘符 + 反斜杠），截到目录层级 / Extract absolute paths. </summary>
        private static IEnumerable<string> ExtractPaths(string line)
        {
            var index = 0;
            while (index < line.Length - 2)
            {
                var c = line[index];
                if (!char.IsLetter(c)) { index++; continue; }
                if (index + 2 >= line.Length || line[index + 1] != ':' || line[index + 2] != '\\') { index++; continue; }

                var end = index + 3;
                while (end < line.Length)
                {
                    var ch = line[end];
                    if (ch == '"' || ch == ' ' || ch == '\t' || ch == '&' || ch == '|' || ch == '>' || ch == '<') break;
                    end++;
                }

                var path = line.Substring(index, end - index);
                var lastSlash = path.LastIndexOf('\\');
                if (lastSlash > 3) path = path.Substring(0, lastSlash + 1);      // 只记目录
                if (path.Length > 3) yield return path;

                index = end;
            }
        }

        /// <summary> 提取「命令 + 开关」组合，如 robocopy /mir /z / Extract command + switches. </summary>
        private static string ExtractCommandCombo(string line)
        {
            var t = line.TrimStart('@', ' ');
            var space = t.IndexOf(' ');
            if (space <= 0) return null;

            var command = t.Substring(0, space);
            if (!BatCommands.Commands.Any(c => c.Equals(command, StringComparison.OrdinalIgnoreCase))) return null;

            var switches = t.Substring(space)
                .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(x => x.Length > 1 && (x[0] == '/' || x[0] == '-'))
                .Select(x => x.Split(':')[0])
                .Take(3)
                .ToList();

            return switches.Count == 0 ? null : command.ToLowerInvariant() + " " + string.Join(" ", switches);
        }

        /// <summary> set 变量命名习惯（好 DATA_ / FILE_ 这类前缀）/ Variable naming habit. </summary>
        private static readonly System.Text.RegularExpressions.Regex VarPrefix =
            new System.Text.RegularExpressions.Regex(@"^\s*@?set\s+""?([A-Za-z_]\w*)_",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase |
                System.Text.RegularExpressions.RegexOptions.Compiled);

        private static string ExtractVariablePrefix(string line)
        {
            var m = VarPrefix.Match(line);
            return m.Success ? m.Groups[1].Value + "_" : null;
        }
    }
}
