using System;
using System.Windows.Forms;
using ScintillaNET;

namespace batIDE.UI
{
    /// <summary> 查找替换窗口：非模态，直接操作编辑器的 target 区间 / Non-modal find and replace driving the editor target range. </summary>
    public partial class FindForm : Form
    {
        private readonly Scintilla _ed;

        /// <summary> 绑定要操作的编辑器 / Bind to the editor under search. </summary>
        public FindForm(Scintilla editor)
        {
            _ed = editor;
            InitializeComponent();
            txtFind.TextChanged += (s, e) => lblResult.Text = string.Empty;
        }

        /// <summary> 打开时聚焦查找框，并带入当前选中的文本 / Focus the find box and seed it with the selection. </summary>
        public void FocusFind(bool replaceMode)
        {
            var selected = _ed.SelectedText;
            if (!string.IsNullOrEmpty(selected) && selected.IndexOf('\n') < 0) txtFind.Text = selected;

            if (replaceMode) txtReplace.Focus(); else { txtFind.Focus(); txtFind.SelectAll(); }
        }

        /// <summary> 根据选项配置并执行一次搜索，返回起点或 -1 / Configure the target and search once. </summary>
        private int Search(string needle, int start, int end)
        {
            _ed.TargetStart = start;
            _ed.TargetEnd = end;
            _ed.SearchFlags = (chkCase.Checked ? SearchFlags.MatchCase : SearchFlags.None)
                              | (chkWord.Checked ? SearchFlags.WholeWord : SearchFlags.None);
            return _ed.SearchInTarget(needle);
        }

        /// <summary> 查找下一个，到边界后回绕 / Find next, wrapping at the document edges. </summary>
        private void OnFindNext(object sender, EventArgs e)
        {
            var needle = txtFind.Text;
            if (needle.Length == 0) return;

            var forward = !chkUp.Checked;
            var caret = _ed.CurrentPosition;
            var pos = forward
                ? Search(needle, caret, _ed.TextLength)
                : Search(needle, Math.Max(0, caret - 1), 0);

            if (pos < 0) pos = forward ? Search(needle, 0, _ed.TextLength) : Search(needle, _ed.TextLength, 0);

            if (pos < 0)
            {
                lblResult.Text = "没有匹配项";
                return;
            }

            _ed.SetSel(pos, _ed.TargetEnd);
            _ed.ScrollCaret();
            lblResult.Text = $"已定位：第 {_ed.LineFromPosition(pos) + 1} 行";
        }

        /// <summary> 替换当前匹配；没有选中匹配时先查找 / Replace the current match, finding one first if needed. </summary>
        private void OnReplace(object sender, EventArgs e)
        {
            var needle = txtFind.Text;
            if (needle.Length == 0) return;

            var selected = _ed.SelectedText;
            var hit = string.Equals(selected, needle, chkCase.Checked ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
            if (!hit) { OnFindNext(sender, e); return; }

            _ed.TargetStart = _ed.SelectionStart;
            _ed.TargetEnd = _ed.SelectionEnd;
            _ed.ReplaceTarget(txtReplace.Text);
            lblResult.Text = "已替换 1 处";
            OnFindNext(sender, e);
        }

        /// <summary> 全部替换，合并为一步撤销 / Replace all, collapsed into a single undo step. </summary>
        private void OnReplaceAll(object sender, EventArgs e)
        {
            var needle = txtFind.Text;
            if (needle.Length == 0) return;

            var replacement = txtReplace.Text;
            var count = 0;

            _ed.BeginUndoAction();
            try
            {
                var pos = 0;
                while (pos <= _ed.TextLength)
                {
                    var found = Search(needle, pos, _ed.TextLength);
                    if (found < 0) break;

                    _ed.ReplaceTarget(replacement);
                    pos = _ed.TargetEnd;
                    if (++count >= 100000) break;
                }
            }
            finally
            {
                _ed.EndUndoAction();
            }

            lblResult.Text = count == 0 ? "没有匹配项" : $"已替换 {count} 处";
        }

        /// <summary> Esc 关闭窗口 / Close on Escape. </summary>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData != Keys.Escape) return base.ProcessCmdKey(ref msg, keyData);
            Close();
            return true;
        }
    }
}
