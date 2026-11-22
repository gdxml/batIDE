using System.Collections.Generic;
using System.Linq;
using ScintillaNET;
using SD = System.Drawing;

namespace batIDE.UI
{
    /// <summary> 编辑器断点管理：红圈标记、点击切换、读取全部断点行 / Visual breakpoint management. </summary>
    internal sealed class BreakpointManager
    {
        private const int MarkerIndex = 1;
        private const int MarginIndex = 1;
        private readonly Scintilla _editor;
        private readonly HashSet<int> _lines = new HashSet<int>();

        /// <summary> 创建断点管理器 / Create the breakpoint manager. </summary>
        public BreakpointManager(Scintilla editor) => _editor = editor;

        /// <summary> 配置断点边栏与点击事件 / Configure the breakpoint margin and events. </summary>
        public void Setup()
        {
            _editor.Markers[MarkerIndex].Symbol = MarkerSymbol.Circle;
            _editor.Markers[MarkerIndex].SetBackColor(SD.Color.FromArgb(0xE2, 0x4B, 0x4A));
            _editor.Markers[MarkerIndex].SetForeColor(SD.Color.FromArgb(0xE2, 0x4B, 0x4A));

            _editor.Margins[MarginIndex].Type = MarginType.Symbol;
            _editor.Margins[MarginIndex].Width = 20;
            _editor.Margins[MarginIndex].Sensitive = true;
            _editor.Margins[MarginIndex].Mask = 1 << MarkerIndex;
            _editor.Margins[MarginIndex].BackColor = SD.Color.FromArgb(0xF5, 0xF5, 0xF5);

            _editor.MarginClick += (s, e) =>
            {
                if (e.Margin == MarginIndex) Toggle(_editor.LineFromPosition(e.Position) + 1);
            };
        }

        /// <summary> 切换指定行断点 / Toggle a breakpoint on the specified one-based line. </summary>
        public void Toggle(int line)
        {
            if (line < 1 || line > _editor.Lines.Count) return;

            var scintillaLine = _editor.Lines[line - 1];
            if (_lines.Contains(line))
            {
                _lines.Remove(line);
                scintillaLine.MarkerDelete(MarkerIndex);
            }
            else
            {
                _lines.Add(line);
                scintillaLine.MarkerAdd(MarkerIndex);
            }
        }

        /// <summary> 返回排序后的全部断点行 / Return all one-based breakpoint lines. </summary>
        public IReadOnlyList<int> GetLines() => _lines.OrderBy(x => x).ToList();

        /// <summary> 清空全部断点 / Clear all breakpoints. </summary>
        public void Clear()
        {
            foreach (var line in _lines.ToList()) _editor.Lines[line - 1].MarkerDelete(MarkerIndex);
            _lines.Clear();
        }
    }
}
