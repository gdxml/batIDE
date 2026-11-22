using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace BatLint.Core
{
    /// <summary>
    /// 逐行推算「当前所在目录」，供编辑器在行尾右侧显示 --{D:\Test\} 这类提示。
    /// 处理 cd /d、cd（不带 /d）、pushd / popd、.. 与 . 的相对路径，
    /// 并把脚本里 set 出来的变量（以及真实环境变量）代进路径，尽量求出真实目录。
    /// </summary>
    internal static class CwdTracker
    {
        private static readonly Regex CdLine = new Regex(
            @"^\s*@?(?:cd|chdir)\b\s*(?<flags>/d)?\s*(?<target>"".*?""|[^\s&|]+)\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex PushdLine = new Regex(
            @"^\s*@?pushd\b\s*(?<target>"".*?""|[^\s&|]+)\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex PopdLine = new Regex(@"^\s*@?popd\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex SetLine = new Regex(
            @"^\s*@?set\s+(?:/(?<flag>[a-zA-Z]))?\s*(?<q1>""?)(?<name>[A-Za-z_][\w]*)=(?<val>[^""]*)(?<q2>""?)\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex SetLocalLine = new Regex(@"^\s*@?setlocal\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex EndLocalLine = new Regex(@"^\s*@?endlocal\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex PercentVar = new Regex(@"%([A-Za-z_][\w]*)%", RegexOptions.Compiled);
        private static readonly Regex BangVar = new Regex(@"!([A-Za-z_][\w]*)!", RegexOptions.Compiled);

        /// <summary> 每行（1 基）→ 该行开始时的当前目录。 </summary>
        public static Dictionary<int, string> Compute(string text, string startDirectory = null)
        {
            var result = new Dictionary<int, string>();
            if (string.IsNullOrEmpty(text)) return result;

            var lines = ExtraRules.SplitLines(text, out _);

            // 当前目录 + 每盘各自己的当前目录（cmd 的 per-drive cwd 行为）
            var current = Normalize(startDirectory);
            var perDrive = new Dictionary<char, string>();
            var stack = new Stack<string>();

            // 脚本里 set 出来的变量；setlocal/endlocal 时整体入栈/出栈
            var vars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var varScopes = new Stack<Dictionary<string, string>>();

            if (current != null && current.Length >= 2 && current[1] == ':')
                perDrive[char.ToUpperInvariant(current[0])] = current;

            var lineNo = 0;
            foreach (var raw in lines)
            {
                lineNo++;
                var t = raw.Trim();

                // 本行开始时看到的目录
                result[lineNo] = current ?? "?";

                if (t.Length == 0) continue;
                if (t.StartsWith("::", StringComparison.Ordinal) || t.StartsWith("rem", StringComparison.OrdinalIgnoreCase)) continue;

                if (SetLocalLine.IsMatch(t))
                {
                    varScopes.Push(new Dictionary<string, string>(vars, StringComparer.OrdinalIgnoreCase));
                    continue;
                }

                if (EndLocalLine.IsMatch(t))
                {
                    if (varScopes.Count > 0) vars = varScopes.Pop();
                    continue;
                }

                var set = SetLine.Match(t);
                if (set.Success)
                {
                    var flag = set.Groups["flag"].Value.ToLowerInvariant();
                    var name = set.Groups["name"].Value;
                    var value = set.Groups["val"].Value.Trim();

                    // set /p 读的是运行时输入；set /a 是算术求值，都不能当字面量用
                    if (flag == "p" || flag == "a")
                    {
                        vars.Remove(name);
                        continue;
                    }

                    if (value.Length == 0) { vars.Remove(name); continue; }

                    var expanded = Expand(value, vars, null);
                    vars[name] = expanded;
                    continue;
                }

                var cd = CdLine.Match(t);
                if (cd.Success)
                {
                    var target = Unquote(cd.Groups["target"].Value);
                    var withSlashD = cd.Groups["flags"].Success;

                    var resolved = Resolve(current, target, vars);

                    // 求不出来（路径非法 / 变量未知）时 cmd 会报错并保持原目录，这里同样保持
                    if (resolved == null) continue;

                    // 不带 /d 且换了盘：cmd 只改那个盘的当前目录，当前盘不变
                    if (!withSlashD && current != null && resolved.Length >= 2 && current.Length >= 2 &&
                        char.ToUpperInvariant(current[0]) != char.ToUpperInvariant(resolved[0]))
                    {
                        perDrive[char.ToUpperInvariant(resolved[0])] = resolved;
                        continue;
                    }

                    current = resolved;
                    perDrive[char.ToUpperInvariant(resolved[0])] = resolved;
                    continue;
                }

                var pushd = PushdLine.Match(t);
                if (pushd.Success)
                {
                    var target = Unquote(pushd.Groups["target"].Value);
                    var resolved = Resolve(current, target, vars);
                    if (resolved != null)
                    {
                        if (current != null) stack.Push(current);
                        current = resolved;
                        perDrive[char.ToUpperInvariant(resolved[0])] = resolved;
                    }
                    continue;
                }

                if (PopdLine.IsMatch(t))
                {
                    if (stack.Count > 0) current = stack.Pop();
                    continue;
                }
            }

            return result;
        }

        /// <summary> 把一行渲染成行尾注解文本 / Render the annotation text. </summary>
        public static string Format(string directory)
        {
            // 求不出路径时返回空串：界面不显示 "--{?}" 这种噪音
            if (string.IsNullOrEmpty(directory) || directory == "?") return string.Empty;

            return "--{" + directory + "}";
        }

        /// <summary> 展开 %VAR% 与 !VAR!：脚本变量优先，其次真实环境变量；未知的原样保留（未定义变量在 cmd 里就是原样）。 </summary>
        private static string Expand(string value, Dictionary<string, string> vars, HashSet<string> seen)
        {
            if (string.IsNullOrEmpty(value)) return value;
            if (value.IndexOf('%') < 0 && value.IndexOf('!') < 0) return value;

            seen = seen ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (seen.Count > 8) return value;      // 防自引用环

            var result = PercentVar.Replace(value, m =>
            {
                var name = m.Groups[1].Value;
                if (seen.Contains(name)) return m.Value;
                if (vars != null && vars.TryGetValue(name, out var v))
                {
                    var inner = new HashSet<string>(seen, StringComparer.OrdinalIgnoreCase) { name };
                    return Expand(v, vars, inner);
                }

                var env = Environment.GetEnvironmentVariable(name);
                return env ?? m.Value;
            });

            result = BangVar.Replace(result, m =>
            {
                var name = m.Groups[1].Value;
                if (seen.Contains(name)) return m.Value;
                if (vars != null && vars.TryGetValue(name, out var v))
                {
                    var inner = new HashSet<string>(seen, StringComparer.OrdinalIgnoreCase) { name };
                    return Expand(v, vars, inner);
                }

                var env = Environment.GetEnvironmentVariable(name);
                return env ?? m.Value;
            });

            return result;
        }

        /// <summary> 解析相对/绝对路径；无法求值时返回 null（调用方按「目录不变」处理）。 </summary>
        private static string Resolve(string baseDir, string target, Dictionary<string, string> vars = null)
        {
            if (string.IsNullOrWhiteSpace(target)) return baseDir;

            target = (Expand(target, vars, null) ?? target).Trim();

            // 展开后仍是未定义变量（cmd 会把 %X% 原样当成路径字符）→ 路径非法，目录不变
            if (target.Length == 0) return baseDir;

            try
            {
                // 纯盘符（D:）等价于该盘的根
                if (target.Length == 2 && target[1] == ':') target += "\\";

                if (baseDir == null || baseDir == "?")
                {
                    if (target.Length >= 2 && target[1] == ':') return Normalize(target);
                    return null;
                }

                var combined = Path.IsPathRooted(target) ? target : Path.Combine(baseDir, target);
                return Normalize(Path.GetFullPath(combined));
            }
            catch
            {
                // 路径非法（如 .\D:\download\）→ cmd 报错并保持原目录
                return baseDir;
            }
        }

        /// <summary> 统一成 D:\a\b\ 形式（带尾反斜杠）。 </summary>
        private static string Normalize(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            if (path == "?") return "?";

            try
            {
                var full = Path.GetFullPath(path);

                // 去掉多余的尾反斜杠后再统一补一个，避免把 D:\ 这种根目录变成 D:\\
                if (full.Length > 3) full = full.TrimEnd('\\');

                return full.EndsWith("\\", StringComparison.Ordinal) ? full : full + "\\";
            }
            catch
            {
                return null;
            }
        }

        private static string Unquote(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            var v = value.Trim();
            if (v.Length >= 2 && v[0] == '"' && v[v.Length - 1] == '"') v = v.Substring(1, v.Length - 2);
            return v.Trim();
        }
    }
}
