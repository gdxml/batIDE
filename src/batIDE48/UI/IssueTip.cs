using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using BatLint.Core;
using ScintillaNET;

namespace batIDE.UI
{
    /// <summary>
    /// 把鼠标停在红色波浪线上时，弹出该处报错原因（规则号 + 说明 + 行号）。
    /// Shows the lint message when the mouse dwells on a squiggle.
    /// </summary>
    internal sealed class IssueTip
    {
        /// <summary> 悬停判定时长（毫秒）/ How long the mouse must rest before the tip appears. </summary>
        private const int DwellMs = 350;

        private readonly Scintilla _editor;
        private readonly Func<List<LintIssue>> _issuesProvider;
        private readonly IssueTooltip _tip = new IssueTooltip();

        private int _shownStart = -1;
        private int _shownLength = -1;
        private bool _attached;

        /// <summary> 构造并绑定编辑器 / Bind to the editor. </summary>
        public IssueTip(Scintilla editor, Func<List<LintIssue>> issuesProvider)
        {
            _editor = editor;
            _issuesProvider = issuesProvider;
        }

        /// <summary> 挂事件 / Hook the dwell events. </summary>
        public void Setup()
        {
            _editor.MouseDwellTime = DwellMs;
            // 句柄可能是在 Setup 之后才创建，属性设置要再补一次，否则 SCI_SETMOUSEDWELLTIME 不会生效
            _editor.HandleCreated += (s2, e2) => _editor.MouseDwellTime = DwellMs;
            if (_editor.IsHandleCreated) _editor.MouseDwellTime = DwellMs;
            _editor.DwellStart += (s, e) => ShowFor(e.Position, e.X, e.Y);
            _editor.DwellEnd += (s, e) => Hide();
            _editor.TextChanged += (s, e) => Hide();
            _editor.UpdateUI += (s, e) => HideIfStale();
            _editor.KeyDown += (s, e) => Hide();
            Attach();
        }

        /// <summary> 检查结束时调用：清掉可能已失效的提示 / Called after a lint pass. </summary>
        public void Invalidate() => Hide();

        /// <summary> 供自测调用的公开入口 / Public entry used by the self test. </summary>
        public void ShowFor(int position, int x, int y)
        {
            var issue = FindAt(position);
            if (issue == null)
            {
                Hide();
                return;
            }

            if (issue.Start == _shownStart && issue.Length == _shownLength && _tip.Visible) return;

            Attach();
            if (_tip.Parent == null) return;

            _shownStart = issue.Start;
            _shownLength = issue.Length;
            _editor.Cursor = Cursors.Help;

            var screen = _editor.PointToScreen(new Point(x, y));
            var local = _tip.Parent.PointToClient(screen);
            _tip.ShowIssue(issue, new Point(local.X + 14, local.Y + 20), _tip.Parent.ClientSize);
        }

        private void Hide()
        {
            if (_shownStart < 0 && !_tip.Visible) return;
            _shownStart = -1;
            _shownLength = -1;
            _tip.HideIssue();
            if (!_editor.IsDisposed) _editor.Cursor = Cursors.IBeam;
        }

        /// <summary> 文字被改动后旧提示可能对不上，先收起 / Drop the tip when the document may have shifted. </summary>
        private void HideIfStale()
        {
            if (_shownStart < 0) return;
            var issue = FindAt(_shownStart);
            if (issue == null || issue.Start != _shownStart) Hide();
        }

        /// <summary> 浮层第一次显示时才挂到编辑器父容器上 / Lazily attach the panel to the editor's parent. </summary>
        private void Attach()
        {
            if (_attached) return;
            var parent = _editor.Parent;
            if (parent == null) return;

            _tip.Visible = false;
            _tip.Cursor = Cursors.Default;
            _tip.MouseDown += (s2, e2) =>
            {
                Hide();                     // 点浮层就收起，并把焦点还给编辑器
                if (!_editor.IsDisposed) _editor.Focus();
            };
            parent.Controls.Add(_tip);
            _attached = true;
        }

        /// <summary> 找覆盖该偏移的问题；同一位置多处时取最靠前的一条 / Find the issue covering a position. </summary>
        private LintIssue FindAt(int position)
        {
            if (position < 0) return null;

            var issues = _issuesProvider();
            if (issues == null || issues.Count == 0) return null;

            LintIssue best = null;
            foreach (var issue in issues)
            {
                int length = Math.Max(1, issue.Length);
                if (position < issue.Start || position >= issue.Start + length) continue;

                // 同一位置可能整行级（如 R34）与精确级（如 R33）重叠：取跨度最小的那条，信息更具体
                if (best == null) { best = issue; continue; }

                int bestLen = Math.Max(1, best.Length);
                if (length < bestLen) best = issue;
                else if (length == bestLen && issue.Start < best.Start) best = issue;
            }
            return best;
        }
    }
}
