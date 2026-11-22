using System;
using System.Collections.Generic;

namespace BatLint.Core
{
    /// <summary>
    /// 行级特征扫描：用 Aho–Corasick 自动机在**一遍**扫描里识别该行出现了哪些关键字/记号，
    /// 之后每条规则先查位掩码再决定要不要跑正则 —— 这是把“每行 30 多个正则”降成
    /// “每行 1 次自动机 + 少量命中正则”的关键。
    /// </summary>
    internal static class FastScan
    {
        /// <summary> 特征位（最多 64 个，够用）/ Feature bits. </summary>
        [Flags]
        internal enum F : ulong
        {
            None = 0,
            Set = 1UL << 0,
            SetA = 1UL << 1,
            SetP = 1UL << 2,
            SetLocal = 1UL << 3,
            EndLocal = 1UL << 4,
            For = 1UL << 5,
            ForF = 1UL << 6,
            ForL = 1UL << 7,
            ForR = 1UL << 8,
            If = 1UL << 9,
            IfExist = 1UL << 10,
            Goto = 1UL << 11,
            Call = 1UL << 12,
            Echo = 1UL << 13,
            EchoOff = 1UL << 14,
            Del = 1UL << 15,
            Rd = 1UL << 16,
            Copy = 1UL << 17,
            Move = 1UL << 18,
            Xcopy = 1UL << 19,
            Robocopy = 1UL << 20,
            Reg = 1UL << 21,
            Sc = 1UL << 22,
            Pushd = 1UL << 23,
            Popd = 1UL << 24,
            Pause = 1UL << 25,
            Start = 1UL << 26,
            Exit = 1UL << 27,
            Mkdir = 1UL << 28,
            ErrorLevel = 1UL << 29,
            Percent = 1UL << 30,
            Bang = 1UL << 31,
            DoublePercent = 1UL << 32,
            Redirect = 1UL << 33,
            Pipe = 1UL << 34,
            Amp = 1UL << 35,
            ParenOpen = 1UL << 36,
            Label = 1UL << 37,
            EnabledDelay = 1UL << 38,
            PathVar = 1UL << 39,
            DelayedVar = 1UL << 40,
            Tilde = 1UL << 41,
            Timeout = 1UL << 42,
            Mklink = 1UL << 43,
            Attrib = 1UL << 44,
            Type = 1UL << 45,
            Ren = 1UL << 46,
            Shift = 1UL << 47,
            Quote = 1UL << 48
        }

        private sealed class Node
        {
            public readonly Dictionary<char, int> Next = new Dictionary<char, int>(2);
            public int Fail;
            public ulong Output;
        }

        private static readonly List<Node> Trie = new List<Node>();
        private static int[] _failBuilt = Array.Empty<int>();
        private static bool _ready;
        private static readonly object Gate = new object();

        /// <summary> 关键字 → 特征位（全小写匹配）/ Keyword to feature. </summary>
        private static readonly (string Text, F Feature)[] Patterns =
        {
            ("setlocal enabledelayedexpansion", F.EnabledDelay),
            ("setlocal", F.SetLocal),
            ("endlocal", F.EndLocal),
            ("set /a", F.SetA),
            ("set /p", F.SetP),
            ("set", F.Set),
            ("for /f", F.ForF),
            ("for /l", F.ForL),
            ("for /r", F.ForR),
            ("for", F.For),
            ("if not exist", F.IfExist),
            ("if exist", F.IfExist),
            ("if", F.If),
            ("goto", F.Goto),
            ("call", F.Call),
            ("echo off", F.EchoOff),
            ("echo", F.Echo),
            ("del", F.Del),
            ("erase", F.Del),
            ("rd", F.Rd),
            ("rmdir", F.Rd),
            ("copy", F.Copy),
            ("move", F.Move),
            ("xcopy", F.Xcopy),
            ("robocopy", F.Robocopy),
            ("reg add", F.Reg),
            ("reg query", F.Reg),
            ("reg delete", F.Reg),
            ("reg", F.Reg),
            ("sc create", F.Sc),
            ("sc config", F.Sc),
            ("sc ", F.Sc),
            ("pushd", F.Pushd),
            ("popd", F.Popd),
            ("pause", F.Pause),
            ("start", F.Start),
            ("exit", F.Exit),
            ("mkdir", F.Mkdir),
            ("md", F.Mkdir),
            ("%errorlevel%", F.ErrorLevel),
            ("%%", F.DoublePercent),
            ("%", F.Percent),
            ("!", F.Bang),
            (">", F.Redirect),
            ("|", F.Pipe),
            ("&", F.Amp),
            ("(", F.ParenOpen),
            (":", F.Label),
            ("path=", F.PathVar),
            ("~", F.Tilde),
            ("timeout", F.Timeout),
            ("mklink", F.Mklink),
            ("attrib", F.Attrib),
            ("type", F.Type),
            ("ren", F.Ren),
            ("rename", F.Ren),
            ("shift", F.Shift),
            ("\"", F.Quote)
        };

        private static void Build()
        {
            if (_ready) return;
            lock (Gate)
            {
                if (_ready) return;

                Trie.Clear();
                Trie.Add(new Node());                      // 根节点 0

                foreach (var (text, feature) in Patterns)
                {
                    var node = 0;
                    foreach (var ch in text)
                    {
                        if (!Trie[node].Next.TryGetValue(ch, out var next))
                        {
                            next = Trie.Count;
                            Trie.Add(new Node());
                            Trie[node].Next[ch] = next;
                        }
                        node = next;
                    }
                    Trie[node].Output |= (ulong)feature;
                }

                // 构建失败指针（BFS）
                var fail = new int[Trie.Count];
                var queue = new Queue<int>();

                foreach (var kv in Trie[0].Next)
                {
                    fail[kv.Value] = 0;
                    queue.Enqueue(kv.Value);
                }

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    var failNode = Trie[current].Fail;

                    foreach (var kv in Trie[current].Next)
                    {
                        var ch = kv.Key;
                        var target = kv.Value;

                        var candidate = failNode;
                        while (candidate != 0 && !Trie[candidate].Next.ContainsKey(ch))
                            candidate = Trie[candidate].Fail;

                        var failTarget = Trie[candidate].Next.TryGetValue(ch, out var ft) && ft != target ? ft : 0;
                        Trie[target].Fail = failTarget;
                        Trie[target].Output |= Trie[failTarget].Output;
                        queue.Enqueue(target);
                    }
                }

                _failBuilt = fail;
                _ready = true;
            }
        }

        /// <summary> 单遍扫描，返回该行命中的特征 / One-pass feature scan of a line. </summary>
        private static readonly bool Disabled =
            Environment.GetEnvironmentVariable("BATLINT_NOFAST") == "1";

        internal static F Features(string line)
        {
            if (Disabled) return (F)ulong.MaxValue;      // 诊断用：关闭快速筛选，退回全量正则
            if (string.IsNullOrEmpty(line)) return F.None;
            Build();

            ulong mask = 0;
            var node = 0;

            for (var i = 0; i < line.Length; i++)
            {
                var c = char.ToLowerInvariant(line[i]);

                while (node != 0 && !Trie[node].Next.ContainsKey(c))
                    node = Trie[node].Fail;

                node = Trie[node].Next.TryGetValue(c, out var next) ? next : 0;
                mask |= Trie[node].Output;
            }

            return (F)mask;
        }
    }
}
