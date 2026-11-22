using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BatLint.Core
{
    /// <summary> 补全数据源：盘符、路径、环境变量 / Completion sources: drives, paths, variables. </summary>
    internal static class CompletionSource
    {
        /// <summary> cmd 内置动态变量（不在环境变量表中）/ Built-in dynamic variables not present in the environment block. </summary>
        public static readonly string[] BuiltIn =
        {
            "CD", "DATE", "TIME", "RANDOM", "ERRORLEVEL", "CMDCMDLINE",
            "CMDEXTVERSION", "HIGHESTNUMANODENUMBER", "PROMPT"
        };

        /// <summary> 参数与路径宏（%~dp0 一族）/ Argument and path macros. </summary>
        public static readonly string[] Macros =
        {
            "%~dp0", "%~nx0", "%~n0", "%~x0", "%~f0", "%~p0",
            "%~dp1", "%~nx1", "%~n1", "%~1", "%~2", "%~*"
        };

        /// <summary> 可用盘符根目录 / Ready drive roots. </summary>
        public static IEnumerable<string> Drives() =>
            DriveInfo.GetDrives()
                .Where(d => d.IsReady)
                .Select(d => d.RootDirectory.FullName)
                .OrderBy(x => x);

        /// <summary> 目录枚举缓存，避免每次按键重复枚举（网络盘尤其明显）/ Cache directory listings between keystrokes. </summary>
        private static readonly Dictionary<string, CacheEntry> PathCache =
            new Dictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);

        /// <summary> 缓存有效期 / Cache lifetime. </summary>
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(3);

        /// <summary> 一条目录缓存 / One cached listing. </summary>
        private sealed class CacheEntry
        {
            public DateTime At;
            public List<string> Items;
        }

        /// <summary> 列出与 prefix 匹配的子目录与文件，目录带尾部反斜杠 / Sub-directories and files matching the prefix. </summary>
        public static IEnumerable<string> Paths(string prefix)
        {
            var endsWithSep = prefix.EndsWith("\\");
            var dir = endsWithSep ? prefix : Path.GetDirectoryName(prefix);
            var leaf = endsWithSep ? string.Empty : Path.GetFileName(prefix);

            if (string.IsNullOrEmpty(dir)) return Enumerable.Empty<string>();

            return Entries(dir).Where(x => LeafName(x).StartsWith(leaf, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary> 取目录名用于前缀比较 / File-system name used for prefix matching. </summary>
        private static string LeafName(string path) => Path.GetFileName(path.TrimEnd('\\'));

        /// <summary> 按目录缓存条目，同一目录下不同前缀共用一次枚举 / Cache entries per directory so prefixes share one enumeration. </summary>
        private static List<string> Entries(string dir)
        {
            if (PathCache.TryGetValue(dir, out var hit) && DateTime.UtcNow - hit.At < CacheTtl)
                return hit.Items;

            var items = EnumerateEntries(dir).ToList();
            if (PathCache.Count > 200) PathCache.Clear();
            PathCache[dir] = new CacheEntry { At = DateTime.UtcNow, Items = items };
            return items;
        }

        /// <summary> 枚举目录下的子目录与文件 / Enumerate immediate sub-directories and files. </summary>
        private static IEnumerable<string> EnumerateEntries(string dir)
        {
            try
            {
                if (!Directory.Exists(dir)) return Enumerable.Empty<string>();

                var dirs = Directory.EnumerateDirectories(dir).Select(d => d + "\\");
                var files = Directory.EnumerateFiles(dir);

                return dirs.Concat(files).OrderBy(x => x, StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return Enumerable.Empty<string>();
            }
        }

        /// <summary> 环境变量名 + cmd 内置变量 / Environment variable names plus built-ins. </summary>
        public static IEnumerable<string> Variables() =>
            Environment.GetEnvironmentVariables()
                .Keys.Cast<string>()
                .Concat(BuiltIn)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(k => k, StringComparer.OrdinalIgnoreCase);
    }
}
