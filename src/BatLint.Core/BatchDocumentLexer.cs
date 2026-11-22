using System;
using System.Collections.Generic;

namespace BatLint.Core
{
    /// <summary>
    /// 文档级词法：保留换行 token，供解析器判断“块在哪结束”。
    /// </summary>
    internal static class BatchDocumentLexer
    {
        internal enum Kind { Word, Operator, ParenOpen, ParenClose, NewLine, End }

        internal sealed class Token
        {
            public Token(Kind kind, string text, int start, int length, int line, bool quoted)
            {
                TKind = kind;
                Text = text;
                Start = start;
                Length = length;
                Line = line;
                Quoted = quoted;
            }

            public Kind TKind { get; }
            public string Text { get; }

            /// <summary> 文档内偏移 / Offset in the document. </summary>
            public int Start { get; }

            public int Length { get; }
            public int Line { get; }

            /// <summary> 1 基行号 / 1-based line number. </summary>
            public int LineNumber => Line + 1;

            public bool Quoted { get; }

            public bool IsWord(string text) =>
                TKind == Kind.Word && Text.Equals(text, StringComparison.OrdinalIgnoreCase);

            public override string ToString() => $"{TKind}:{Text}@L{LineNumber}+{Start}";
        }

        /// <summary> 把整篇脚本切成 token（保留换行）。 </summary>
        public static List<Token> Tokenize(string text)
        {
            var tokens = new List<Token>();
            if (string.IsNullOrEmpty(text)) return tokens;

            var line = 0;
            var offset = 0;
            var i = 0;

            while (i < text.Length)
            {
                var c = text[i];

                if (c == '\r') { i++; offset++; continue; }   // \r 也占一个字节，偏移必须累加
                if (c == '\n')
                {
                    tokens.Add(new Token(Kind.NewLine, "\n", offset, 1, line, false));
                    i++;
                    offset++;
                    line++;
                    continue;
                }

                if (c == ' ' || c == '\t') { i++; offset++; continue; }

                if (c == '(') { tokens.Add(new Token(Kind.ParenOpen, "(", offset, 1, line, false)); i++; offset++; continue; }
                if (c == ')') { tokens.Add(new Token(Kind.ParenClose, ")", offset, 1, line, false)); i++; offset++; continue; }

                if (c == '<' || c == '>' || c == '|' || c == '&')
                {
                    var start = offset;
                    var sb = new System.Text.StringBuilder();
                    while (i < text.Length && (text[i] == '<' || text[i] == '>' || text[i] == '|' || text[i] == '&'))
                    {
                        sb.Append(text[i]);
                        i++;
                        offset++;
                    }
                    tokens.Add(new Token(Kind.Operator, sb.ToString(), start, sb.Length, line, false));
                    continue;
                }

                var wordStart = offset;
                var word = new System.Text.StringBuilder();
                var quoted = false;

                while (i < text.Length)
                {
                    var ch = text[i];
                    if (ch == '\r' || ch == '\n' || ch == ' ' || ch == '\t' || ch == '(' || ch == ')' ||
                        ch == '<' || ch == '>' || ch == '|' || ch == '&')
                        break;

                    if (ch == '"')
                    {
                        quoted = true;
                        i++;
                        offset++;
                        while (i < text.Length && text[i] != '"')
                        {
                            if (text[i] == '^' && i + 1 < text.Length)
                            {
                                word.Append(text[i + 1]);
                                i += 2;
                                offset += 2;
                                continue;
                            }
                            word.Append(text[i]);
                            i++;
                            offset++;
                        }
                        if (i < text.Length) { i++; offset++; }
                        continue;
                    }

                    if (ch == '^' && i + 1 < text.Length)
                    {
                        word.Append(text[i + 1]);
                        i += 2;
                        offset += 2;
                        continue;
                    }

                    word.Append(ch);
                    i++;
                    offset++;
                }

                if (word.Length > 0)
                    tokens.Add(new Token(Kind.Word, word.ToString(), wordStart, word.Length, line, quoted));
            }

            tokens.Add(new Token(Kind.End, string.Empty, offset, 0, line, false));
            return tokens;
        }
    }
}
