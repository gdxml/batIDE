using System;
using System.Collections.Generic;
using System.Linq;

namespace BatLint.Core
{
    /// <summary>
    /// 拼写建议（Roslyn SpellChecker 的等价实现）：
    /// 只用于「建议」，不用于判定语法；闸门严格，宁可不提示也不乱提示。
    ///   · 词长 &lt; 3 不提示（两字符多半是没打完）
    ///   · 按长度分配编辑预算：3~4 → 1，5~8 → 2，&gt;8 → 3（且 ≤ len/3）
    ///   · Damerau–Levenshtein（允许相邻换位，Calcualte → Calculate 才算 1）
    ///   · 长度差闸门 + 唯一最优（并列则静默）
    /// </summary>
    internal static class SpellSuggest
    {
        private const int MinLength = 3;

        /// <summary> 返回唯一最优候选；没有或并列时返回 null。 </summary>
        public static string Best(string word, IEnumerable<string> candidates)
        {
            var list = Match(word, candidates, 1);
            return list.Count > 0 ? list[0] : null;
        }

        /// <summary> 返回按距离排序的候选（最多 max 个，并列都保留）。 </summary>
        public static List<string> Match(string word, IEnumerable<string> candidates, int max)
        {
            var result = new List<string>();
            if (word == null || candidates == null) return result;

            var w = word.Trim();
            if (w.Length < MinLength) return result;

            var budget = Budget(w.Length);
            var scored = new List<(string Word, int Distance)>();

            foreach (var c in candidates
                         .Where(c => !string.IsNullOrEmpty(c))
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderBy(c => c, StringComparer.OrdinalIgnoreCase))
            {
                if (Math.Abs(c.Length - w.Length) > budget) continue;      // 长度差闸门
                var d = Distance(w.ToLowerInvariant(), c.ToLowerInvariant(), budget);
                if (d <= budget) scored.Add((c, d));
            }

            if (scored.Count == 0) return result;

            var bestDistance = scored.Min(x => x.Distance);
            var best = scored.Where(x => x.Distance == bestDistance).ToList();

            // 并列（且拼写不同）→ 静默，避免“Stack 建议成 HStack/VStack”
            if (best.Count > 1 && best.Select(x => x.Word).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
                return result;

            result.AddRange(best.Select(x => x.Word).Take(max));
            return result;
        }

        /// <summary> 按词长分配编辑预算 / Length-scaled edit budget. </summary>
        private static int Budget(int length)
        {
            if (length <= 4) return 1;
            if (length <= 8) return 2;
            return Math.Min(3, Math.Max(1, length / 3));
        }

        /// <summary> Damerau–Levenshtein（OSA，带换位与行早退）。 </summary>
        private static int Distance(string a, string b, int budget)
        {
            var n = a.Length;
            var m = b.Length;
            if (n == 0) return m;
            if (m == 0) return n;

            var prev2 = new int[m + 1];
            var prev = new int[m + 1];
            var cur = new int[m + 1];

            for (var j = 0; j <= m; j++) prev[j] = j;

            for (var i = 1; i <= n; i++)
            {
                cur[0] = i;
                var rowMin = cur[0];

                for (var j = 1; j <= m; j++)
                {
                    var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    var value = Math.Min(Math.Min(cur[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);

                    if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                        value = Math.Min(value, prev2[j - 2] + 1);              // 相邻换位

                    cur[j] = value;
                    if (value < rowMin) rowMin = value;
                }

                if (rowMin > budget) return budget + 1;                          // 整行超预算，早退

                Array.Copy(prev, prev2, m + 1);
                Array.Copy(cur, prev, m + 1);
            }

            return prev[m];
        }
    }
}
