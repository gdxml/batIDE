using System;

namespace batIDE.UI
{
    /// <summary>
    /// 光标行的「这行做什么」说明。实际渲染由 <see cref="CwdNote"/> 统一负责：
    /// 两者共用 Scintilla 的 EOL 注解通道，一行只有一个注解，
    /// 所以这里只登记文本，拼装与重绘都交给 CwdNote，避免互相清空。
    /// </summary>
    internal sealed class LineNote
    {
        private readonly CwdNote _owner;
        private int _line = -1;
        private string _text;

        public LineNote(CwdNote owner) => _owner = owner ?? throw new ArgumentNullException(nameof(owner));

        /// <summary> 注解是否已显示 / Whether a note is shown. </summary>
        public bool HasNote => _line >= 0;

        /// <summary> 注解所在行（0 基）/ Line the note sits on. </summary>
        public int Line => _line;

        /// <summary> 现在不再有插入文本的退回模式 / The inserted-text fallback is gone. </summary>
        public bool InsertMode => false;

        /// <summary> 样式由 CwdNote 统一配置 / Styles are owned by CwdNote. </summary>
        public void Setup()
        {
        }

        /// <summary> 在指定行末显示说明，内容未变则跳过 / Show the hint on a line, skipping identical text. </summary>
        public void Show(int line, string text)
        {
            if (string.IsNullOrEmpty(text)) { Clear(); return; }
            if (line == _line && text == _text) return;
            if (line < 0) return;

            _line = line;
            _text = text;
            _owner.SetNote(line, text);
        }

        /// <summary> 清除说明 / Remove the hint. </summary>
        public void Clear()
        {
            if (_line < 0 && _text == null) return;

            _line = -1;
            _text = null;
            _owner.SetNote(-1, null);
        }

        /// <summary> 原生注解模式下不需要处理光标越界 / No-op in native annotation mode. </summary>
        public void Guard()
        {
        }

        /// <summary> 原生注解模式下不需要重染 / No-op in native annotation mode. </summary>
        public void Repaint()
        {
        }
    }
}
