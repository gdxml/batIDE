using System;
using System.Collections.Generic;
using System.Linq;
using static BatLint.Core.BatchDocumentLexer;

namespace BatLint.Core
{
    /// <summary>
    /// 递归下降解析器（S2-b）：只为控制结构建语法（if / for / ( ) 块 / else / do），
    /// 其它命令整行当不透明语句。
    ///
    /// 报错策略照 Roslyn：
    ///   · 期望的 token 缺失时插入一个零宽「missing token」，继续往下解析（不打断后续行）
    ///   · 只在结构上真正说不通时才报错，一元错只报一条
    ///   · 消息固定为「期望 X，实际找到 Y」
    /// </summary>
    internal static class BatchParser
    {
        internal sealed class Diagnostic
        {
            public Diagnostic(int start, int length, int line, string message, string rule)
            {
                Start = start;
                Length = length;
                Line = line;
                Message = message;
                Rule = rule;
            }

            public int Start { get; }
            public int Length { get; }
            public int Line { get; }
            public string Message { get; }
            public string Rule { get; }
        }

        /// <summary> 解析整篇脚本，返回结构级诊断。 </summary>
        public static List<Diagnostic> Parse(string text)
        {
            var diagnostics = new List<Diagnostic>();
            if (string.IsNullOrEmpty(text)) return diagnostics;

            var tokens = BatchDocumentLexer.Tokenize(text);
            var parser = new Walker(tokens, diagnostics);
            parser.ParseScript();
            return diagnostics;
        }

        /// <summary> 单文件内的解析状态 / Parse state for one script. </summary>
        private sealed class Walker
        {
            private readonly List<Token> _tokens;
            private readonly List<Diagnostic> _diagnostics;
            private int _index;

            /// <summary> 括号栈：开括号位置 + 行号 / Open paren stack. </summary>
            private readonly Stack<Token> _openParens = new Stack<Token>();

            /// <summary> 上一条语句是否是 if（用于 else 配对）/ Was the last statement an if? </summary>
            private bool _lastWasIf;

            /// <summary> 这个 if 的语句体是不是 ( ) 块 / Did that if use a parenthesized body? </summary>
            private bool _ifHadBlock;

            /// <summary> if 所在行号（单行形式要求 else 同行）/ Line of the if statement. </summary>
            private int _ifStartLine;

            public Walker(List<Token> tokens, List<Diagnostic> diagnostics)
            {
                _tokens = tokens;
                _diagnostics = diagnostics;
            }

            private Token Current => _tokens[Math.Min(_index, _tokens.Count - 1)];

            private Token Peek(int offset = 1)
                => _tokens[Math.Min(_index + offset, _tokens.Count - 1)];

            private void Advance() => _index++;

            /// <summary> 顶层：一条语句一条语句地走。 </summary>
            public void ParseScript()
            {
                while (Current.TKind != Kind.End)
                {
                    if (Current.TKind == Kind.NewLine) { Advance(); continue; }
                    ParseStatement();
                }

                // 走到文件末尾还有没闭合的括号：指向那个开括号，而不是文件尾
                foreach (var open in _openParens)
                {
                    _diagnostics.Add(new Diagnostic(open.Start, 1, open.LineNumber,
                        $"括号没有闭合：这里的 ( 一直到文件末尾都没有对应的 )", "R10"));
                }
            }

            /// <summary> 一条语句：可能带 if/for 前缀，也可能是普通命令或块。 </summary>
            private void ParseStatement()
            {
                var token = Current;

                if (token.TKind == Kind.ParenOpen)
                {
                    ParseBlock();
                    return;
                }

                if (token.TKind == Kind.ParenClose)
                {
                    // cmd 对多余的 ) 是静默忽略的（实测 exit 0），这里不报错，避免误报
                    Advance();
                    _lastWasIf = false;
                    return;
                }

                if (token.TKind != Kind.Word)
                {
                    Advance();
                    return;
                }

                if (token.IsWord("if"))
                {
                    ParseIf();
                    _lastWasIf = true;
                    return;
                }

                if (token.IsWord("else"))
                {
                    // cmd 实测：if (...) { } 之后的 else 可以换行；但 if X 命令 之后换行写 else 会报错
                    var valid = _lastWasIf && (_ifHadBlock || token.LineNumber == _ifStartLine);
                    if (!valid)
                        _diagnostics.Add(new Diagnostic(token.Start, token.Length, token.LineNumber,
                            _lastWasIf
                                ? "else 必须与 if 的 ( ) 块对齐，或与 if 的同一行写法保持同一行（cmd 会报「此时不应有 else」）"
                                : "else 没有配对的 if：else 必须紧跟 if 的语句或 if (...) { } 块", "R57"));

                    Advance();
                    SkipToLineEndOrBlockEnd();
                    _lastWasIf = false;
                    return;
                }

                if (token.IsWord("for"))
                {
                    ParseFor();
                    _lastWasIf = false;
                    return;
                }

                _lastWasIf = false;
                SkipToLineEndOrBlockEnd();
            }

            /// <summary> 解析 ( ... ) 块：内部语句递归解析。<summary>
            private void ParseBlock()
            {
                var open = Current;
                Advance();                       // 吃掉 (
                _openParens.Push(open);

                while (true)
                {
                    if (Current.TKind == Kind.End)
                    {
                        // 已经在 ParseScript 末尾统一报未闭合，这里直接返回
                        return;
                    }

                    if (Current.TKind == Kind.ParenClose)
                    {
                        _openParens.Pop();
                        Advance();               // 吃掉 )
                        return;
                    }

                    if (Current.TKind == Kind.NewLine) { Advance(); continue; }

                    ParseStatement();
                }
            }

            /// <summary> if：解析条件，然后看语句或块；支持 else。 </summary>
            private void ParseIf()
            {
                var ifToken = Current;
                _ifStartLine = ifToken.LineNumber;
                _ifHadBlock = false;
                Advance();                      // if

                // 条件到行尾 / 到块结束为止
                var conditionTokens = new List<Token>();
                while (Current.TKind == Kind.Word || Current.TKind == Kind.Operator)
                {
                    conditionTokens.Add(Current);
                    Advance();
                }

                if (conditionTokens.Count == 0)
                {
                    _diagnostics.Add(new Diagnostic(ifToken.Start, ifToken.Length, ifToken.LineNumber,
                        $"if 后面缺少条件（期望 if [not] A==B / exist 路径 / defined 变量 / errorlevel N）", "R48"));
                }

                // 条件后面直接跟块
                if (Current.TKind == Kind.ParenOpen)
                {
                    _ifHadBlock = true;
                    ParseBlock();
                    return;
                }

                // 条件后面跟单条命令
                SkipToLineEndOrBlockEnd();
            }

            /// <summary> for：必须形如 for [开关] %%v in (集合) do 命令。 </summary>
            private void ParseFor()
            {
                var forToken = Current;
                var line = forToken.LineNumber;
                Advance();                      // for

                var headerTokens = new List<Token>();
                Token setCloseParen = null;

                while (Current.TKind != Kind.End && Current.TKind != Kind.NewLine)
                {
                    // 遇到块内的 ) 说明这条 for 头没写完就到了块尾
                    if (Current.TKind == Kind.ParenClose && _openParens.Count > 0) break;

                    if (Current.TKind == Kind.ParenOpen)
                    {
                        // 集合括号内部不参与语句解析
                        var open = Current;
                        Advance();
                        var depth = 1;
                        while (depth > 0 && Current.TKind != Kind.End)
                        {
                            if (Current.TKind == Kind.ParenOpen) depth++;
                            else if (Current.TKind == Kind.ParenClose) depth--;
                            if (depth > 0) Advance();
                        }
                        if (Current.TKind == Kind.ParenClose) { setCloseParen = Current; Advance(); }
                        headerTokens.Add(open);

                        // 集合括号已吃掉：后面只可能是 do（或直接进入循环体）
                        if (Current.TKind == Kind.Word && Current.IsWord("do"))
                        {
                            headerTokens.Add(Current);
                            Advance();
                        }
                        break;
                    }

                    headerTokens.Add(Current);
                    Advance();
                }

                var hasDo = headerTokens.Any(t => t.IsWord("do"));
                if (!hasDo)
                {
                    // 缺 do：把诊断钉在 for 头末尾（零宽位置），提示插入
                    var at = setCloseParen
                             ?? (headerTokens.Count > 0 ? headerTokens[headerTokens.Count - 1] : forToken);
                    var end = at.Start + at.Length;
                    _diagnostics.Add(new Diagnostic(end, 1, line,
                        "for 缺少 do 关键字（期望 do，实际找到行尾/块末尾）；应写 for %%i in (集合) do 命令", "R49"));
                }

                // 循环体
                if (Current.TKind == Kind.ParenOpen) ParseBlock();
                else SkipToLineEndOrBlockEnd();
            }

            /// <summary> 跳到行尾或当前块的 ) 之前。 </summary>
            private void SkipToLineEndOrBlockEnd()
            {
                while (Current.TKind != Kind.End && Current.TKind != Kind.NewLine)
                {
                    if (Current.TKind == Kind.ParenClose && _openParens.Count > 0) return;
                    if (Current.TKind == Kind.ParenOpen)
                    {
                        ParseBlock();
                        continue;
                    }
                    Advance();
                }
            }
        }
    }
}
