using System.Drawing;
using ScintillaNET;

namespace batIDE.UI
{
    /// <summary> 光标落在括号旁时高亮配对括号 / Highlight the matching bracket next to the caret. </summary>
    internal sealed class BraceMatcher
    {
        /// <summary> 专用指示器，0 号留给 lint 波浪线 / Dedicated indicator; 0 is reserved for lint squiggles. </summary>
        private const int IndicatorIndex = 1;

        /// <summary> 单次向两侧最多扫描的字符数，防止大文件卡顿 / Scan window on each side to keep large files responsive. </summary>
        private const int ScanLimit = 20000;

        private readonly Scintilla _ed;
        private int _lastA = -1;
        private int _lastB = -1;
        private int _lastPos = -1;

        /// <summary> 绑定编辑器 / Bind to the editor. </summary>
        public BraceMatcher(Scintilla editor) => _ed = editor;

        /// <summary> 配置指示器样式，必须在编辑器样式初始化之后调用 / Configure the indicator after editor styling. </summary>
        public void Setup()
        {
            _ed.Indicators[IndicatorIndex].Style = IndicatorStyle.RoundBox;
            _ed.Indicators[IndicatorIndex].ForeColor = Color.FromArgb(0xB0, 0xB0, 0xB0);
        }

        /// <summary> 文本变化后位置失效，先清掉记录 / Forget the cached positions after an edit. </summary>
        public void Reset()
        {
            Clear();
            _lastA = _lastB = -1;
            _lastPos = -1;
        }

        /// <summary> 按当前光标刷新高亮；光标没动就直接返回 / Refresh for the current caret, skipping when it has not moved. </summary>
        public void Update()
        {
            var pos = _ed.CurrentPosition;
            if (pos == _lastPos) return;

            _lastPos = pos;
            Clear();

            // 先看光标左侧：刚闭合完括号时更符合直觉；再看光标处
            if (!TryPair(pos - 1, out var a, out var b) && !TryPair(pos, out a, out b)) return;

            _ed.IndicatorCurrent = IndicatorIndex;
            _ed.IndicatorFillRange(a, 1);
            _ed.IndicatorFillRange(b, 1);
            _lastA = a;
            _lastB = b;
        }

        /// <summary> 清掉上一次的两处高亮 / Clear the two positions highlighted last time. </summary>
        private void Clear()
        {
            _ed.IndicatorCurrent = IndicatorIndex;
            if (_lastA >= 0 && _lastA < _ed.TextLength) _ed.IndicatorClearRange(_lastA, 1);
            if (_lastB >= 0 && _lastB < _ed.TextLength) _ed.IndicatorClearRange(_lastB, 1);
        }

        /// <summary> 判断该位置是否括号并找到配对 / Report the pair when the given index holds a bracket. </summary>
        private bool TryPair(int index, out int a, out int b)
        {
            a = b = -1;
            if (index < 0 || index >= _ed.TextLength) return false;

            var c = (char)_ed.GetCharAt(index);
            if (c != '(' && c != ')') return false;
            if (InQuote(index)) return false;

            var match = FindMatch(index, c);
            if (match < 0) return false;

            a = index;
            b = match;
            return true;
        }

        /// <summary> 该位置是否位于行内引号之中 / Whether the index sits inside a quoted span on its line. </summary>
        private bool InQuote(int index)
        {
            var line = _ed.LineFromPosition(index);
            var start = _ed.Lines[line].Position;
            var quotes = 0;

            for (var i = start; i < index; i++)
                if (_ed.GetCharAt(i) == '"') quotes++;

            return quotes % 2 == 1;
        }

        /// <summary> 按方向数括号深度找配对；引号内的括号不参与 / Walk in one direction counting depth outside quotes. </summary>
        private int FindMatch(int index, char bracket)
        {
            var step = bracket == '(' ? 1 : -1;
            var depth = 0;
            var limit = ScanLimit;

            for (var i = index; i >= 0 && i < _ed.TextLength && limit-- > 0; i += step)
            {
                var c = (char)_ed.GetCharAt(i);

                if (c == bracket) depth++;
                else if (c == (bracket == '(' ? ')' : '('))
                {
                    if (--depth == 0) return i;
                }
            }

            return -1;
        }
    }
}
