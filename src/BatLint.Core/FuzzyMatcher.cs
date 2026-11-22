using System;
using System.Collections.Generic;
using System.Linq;

namespace BatLint.Core
{
    /// <summary> 候选打分：前缀优先，其次子序列，连续段与词边界加权 / Candidate scoring with prefix, subsequence and boundary bonuses. </summary>
    internal static class FuzzyMatcher
    {
        /// <summary> 打分，0 表示不匹配 / Score a candidate; 0 means no match. </summary>
        public static int Score(string text, string query)
        {
            if (string.IsNullOrEmpty(query)) return 1;
            if (string.IsNullOrEmpty(text)) return 0;

            if (text.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            {
                var prefix = 1000 - text.Length * 2;
                if (text.StartsWith(query, StringComparison.Ordinal)) prefix += 60;
                return Math.Max(prefix, 1);
            }

            var ti = 0;
            var streak = 0;
            var score = 500;

            foreach (var qc in query)
            {
                var found = -1;
                for (var i = ti; i < text.Length; i++)
                {
                    if (char.ToLowerInvariant(text[i]) != char.ToLowerInvariant(qc)) continue;
                    found = i;
                    break;
                }

                if (found < 0) return 0;

                if (found == ti && ti > 0)
                {
                    streak++;
                    score += streak * 8;
                }
                else
                {
                    streak = 0;
                    score -= (found - ti) * 3;

                    var prev = found == 0 ? ' ' : text[found - 1];
                    if (prev == ' ' || prev == '/' || prev == '-' || prev == '_' || prev == '"') score += 40;
                }

                ti = found + 1;
            }

            score -= Math.Max(0, text.Length - query.Length) * 2;
            return Math.Max(score, 1);
        }

        /// <summary> 打分排序并截断 / Rank candidates and truncate. </summary>
        public static IEnumerable<string> Rank(IEnumerable<string> items, string query, int limit)
        {
            if (string.IsNullOrEmpty(query)) return items.Take(limit);

            return items
                .Select(x => new { Text = x, Score = Score(x, query) })
                .Where(p => p.Score > 0)
                .OrderByDescending(p => p.Score)
                .ThenBy(p => p.Text.Length)
                .ThenBy(p => p.Text, StringComparer.OrdinalIgnoreCase)
                .Take(limit)
                .Select(p => p.Text);
        }
    }
}
