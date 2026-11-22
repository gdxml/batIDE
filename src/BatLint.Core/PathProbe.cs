using System;
using System.Collections.Generic;
using System.IO;

namespace BatLint.Core
{
    /// <summary>
    /// 判断一个命令名是否真的存在（内置命令由调用方判断，这里只看 PATH 上的可执行文件）。
    /// 结果按名字缓存，避免逐行重复 IO。
    /// </summary>
    internal static class PathProbe
    {
        private static readonly Dictionary<string, bool> Cache =
            new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        private static readonly object Gate = new object();
        private static string _pathSnapshot;
        private static string[] _directories = Array.Empty<string>();

        /// <summary> PATH 上是否有这个可执行文件（.exe/.bat/.cmd/.com）。 </summary>
        public static bool Exists(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return false;

            lock (Gate)
            {
                if (Cache.TryGetValue(command, out var known)) return known;

                var dirs = Directories();
                var found = false;

                foreach (var dir in dirs)
                {
                    foreach (var ext in new[] { ".exe", ".bat", ".cmd", ".com" })
                    {
                        try
                        {
                            if (!File.Exists(Path.Combine(dir, command + ext))) continue;
                            found = true;
                            break;
                        }
                        catch
                        {
                            // 非法路径跳过
                        }
                    }

                    if (found) break;
                }

                Cache[command] = found;
                return found;
            }
        }

        private static string[] Directories()
        {
            var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            if (path == _pathSnapshot && _directories.Length > 0) return _directories;

            var list = new List<string>();
            foreach (var raw in path.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    var dir = Environment.ExpandEnvironmentVariables(raw.Trim().Trim('"'));
                    if (dir.Length > 0) list.Add(dir);
                }
                catch
                {
                    // 忽略无法展开的项
                }
            }

            _pathSnapshot = path;
            _directories = list.ToArray();
            return _directories;
        }
    }
}
