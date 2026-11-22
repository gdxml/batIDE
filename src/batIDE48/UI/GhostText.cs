using System;
using System.Drawing;

namespace batIDE.UI
{
    /// <summary> 行内灰字：插入真实文本并用专用样式染色，任何输入前撤销 / Inline ghost text rendered with a dedicated style. </summary>
    internal sealed class GhostText
    {
        /// <summary> 专用样式号，避开 LexBatch 的 0~8 / Dedicated style id, clear of LexBatch 0..8. </summary>
        private const int GhostStyle = 40;

        private readonly ScintillaNET.Scintilla _ed;
        private int _start = -1;
        private int _length;
        private bool _repainting;

        /// <summary> 绑定编辑器 / Bind to the editor. </summary>
        public GhostText(ScintillaNET.Scintilla editor) => _ed = editor;

        /// <summary> 设置灰色样式，必须在编辑器样式初始化之后调用 / Configure the grey style after editor styling is ready. </summary>
        public void Setup()
        {
            _ed.Styles[GhostStyle].ForeColor = Color.FromArgb(0x9A, 0x9A, 0x9A);
            _ed.Styles[GhostStyle].Font = "Consolas";
            _ed.Styles[GhostStyle].Size = 11;
            _ed.Styles[GhostStyle].Italic = true;
        }

        /// <summary> 是否存在灰字 / Whether ghost text is present. </summary>
        public bool HasGhost => _length > 0;

        /// <summary> 灰字起点 / Start position of the ghost text. </summary>
        public int Start => _start;

        /// <summary> 把灰字的插入与删除合并为一步撤销 / Fold ghost edits into a single undo step. </summary>
        private void NoUndo(Action action)
        {
            _ed.BeginUndoAction();
            try { action(); }
            finally { _ed.EndUndoAction(); }
        }

        /// <summary> 显示灰字：光标留在灰字之前 / Show ghost text keeping the caret before it. </summary>
        public void Show(string text)
        {
            Clear();
            if (string.IsNullOrEmpty(text)) return;

            var pos = _ed.CurrentPosition;
            NoUndo(() => _ed.InsertText(pos, text));
            _start = pos;
            _length = text.Length;
            Repaint();
            _ed.SetSel(pos, pos);
        }

        /// <summary> 删除灰字，文本变化会触发词法器重算 / Remove the ghost text; the lexer re-runs on the change. </summary>
        public void Clear()
        {
            if (_length <= 0) { _start = -1; return; }

            var start = _start;
            var length = _length;
            _start = -1;
            _length = 0;

            if (start < 0 || start + length > _ed.TextLength) return;
            NoUndo(() => _ed.DeleteRange(start, length));
        }

        /// <summary> 接受灰字：保留文本、恢复默认样式、光标移到末尾 / Accept the ghost text as real input. </summary>
        public void Accept()
        {
            if (_length <= 0) return;

            var start = _start;
            var end = start + _length;
            _start = -1;
            _length = 0;

            _ed.StartStyling(start);
            _ed.SetStyling(end - start, 0);
            _ed.SetSel(end, end);
        }

        /// <summary> 重新染色，抵消词法器的覆盖 / Re-apply the grey style after the lexer runs. </summary>
        public void Repaint()
        {
            if (_repainting) return;
            if (_length <= 0 || _start < 0) return;
            if (_start + _length > _ed.TextLength) return;

            _repainting = true;
            try
            {
                _ed.StartStyling(_start);
                _ed.SetStyling(_length, GhostStyle);
            }
            finally
            {
                _repainting = false;
            }
        }
    }
}

