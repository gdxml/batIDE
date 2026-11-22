using System;
using System.Collections.Generic;
using System.Text;

namespace BatLint.Core
{
    /// <summary>
    /// 轻量词法器：把一行 bat 切成 token，保留引号与 ^ 转义的语义，
    /// 用于参数级语法检查（不做完整文法，交给 Grammar/后续解析器）。
    /// </summary>
    internal static class BatchLexer
    {
        internal enum Kind { Word, Operator, ParenOpen, ParenClose }

        internal sealed class Token
        {
            public Token(Kind kind, string text, int start, bool quoted)
            {
                Kind = kind;
                Text = text;
                Start = start;
                Quoted = quoted;
            }

            public Kind Kind { get; }
            public string Text { get; }

            /// <summary> 行内偏移 / Offset inside the line. </summary>
            public int Start { get; }

            /// <summary> 是否来自引号内 / Came from quotes. </summary>
            public bool Quoted { get; }

            public override string ToString() => $"{Kind}:{Text}@{Start}";
        }

        /// <summary> 切分一行（不含行尾 & 之后的命令，按 & | 断开）。 </summary>
        public static List<Token> Tokenize(string line)
        {
            var tokens = new List<Token>();
            if (string.IsNullOrEmpty(line)) return tokens;

            var i = 0;
            while (i < line.Length)
            {
                var c = line[i];

                if (c == ' ' || c == '\t') { i++; continue; }

                // 括号
                if (c == '(') { tokens.Add(new Token(Kind.ParenOpen, "(", i, false)); i++; continue; }
                if (c == ')') { tokens.Add(new Token(Kind.ParenClose, ")", i, false)); i++; continue; }

                // 重定向与管道（含 2>&1 这类）
                if (c == '<' || c == '>' || c == '|' || c == '&')
                {
                    var start = i;
                    var text = new StringBuilder();
                    while (i < line.Length && (line[i] == '<' || line[i] == '>' || line[i] == '|' || line[i] == '&'))
                        text.Append(line[i++]);
                    while (i < line.Length && (line[i] == '&' || line[i] == '1' || line[i] == '2'))
                        text.Append(line[i++]);
                    tokens.Add(new Token(Kind.Operator, text.ToString(), start, false));
                    continue;
                }

                // 普通词（可含引号片段）
                var wordStart = i;
                var word = new StringBuilder();
                var quoted = false;

                while (i < line.Length)
                {
                    var ch = line[i];
                    if (ch == ' ' || ch == '\t' || ch == '(' || ch == ')' || ch == '<' || ch == '>' || ch == '|' || ch == '&')
                        break;

                    if (ch == '"')
                    {
                        quoted = true;
                        i++;
                        while (i < line.Length && line[i] != '"')
                        {
                            if (line[i] == '^' && i + 1 < line.Length) { word.Append(line[i + 1]); i += 2; continue; }
                            word.Append(line[i++]);
                        }
                        if (i < line.Length) i++;
                        continue;
                    }

                    if (ch == '^' && i + 1 < line.Length) { word.Append(line[i + 1]); i += 2; continue; }

                    word.Append(ch);
                    i++;
                }

                if (word.Length > 0) tokens.Add(new Token(Kind.Word, word.ToString(), wordStart, quoted));
                else if (i == wordStart) i++;                  // 防御：避免死循环
            }

            return tokens;
        }

        /// <summary> 去掉 @ 前缀与首尾空白 / Strip the @ prefix. </summary>
        public static string NormalizeCommand(string word)
            => string.IsNullOrEmpty(word) ? word : word.TrimStart('@').Trim();
    }
}
