using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using ScintillaNET;

namespace batIDE.UI
{
    /// <summary>
    /// 行尾右侧常驻显示「当前所在目录」（--{D:\Test\}）。
    /// 自己是 EOL 注解通道的唯一拥有者：光标行的命令说明（LineNote）也走这里，
    /// 统一拼在一行里渲染，避免两个作者互相 CLEARALL 把对方的注解抹掉。
    /// </summary>
    internal sealed class CwdNote
    {
        private const int SciEolAnnotationSetText = 2740;
        private const int SciEolAnnotationClearAll = 2744;
        private const int SciEolAnnotationSetVisible = 2745;
        private const int SciEolAnnotationGetVisible = 2746;
        private const int SciEolAnnotationSetStyleOffset = 2747;
        private const int SciGetCodepage = 2137;   // SCI_GETCODEPAGE（4008 是别的消息，发过去会让原生层踩坏内存）

        private const int EolAnnotationStandard = 1;
        private const int NativeStyle = 200;

        private readonly Scintilla _editor;
        private bool? _native;
        private int _codePage = -1;

        /// <summary> 0 基行号 → 路径注解（已是 --{...} 形式）。 </summary>
        private readonly Dictionary<int, string> _cwd = new Dictionary<int, string>();

        /// <summary> 0 基行号 → 光标行命令说明。 </summary>
        private readonly Dictionary<int, string> _notes = new Dictionary<int, string>();

        public CwdNote(Scintilla editor) => _editor = editor;

        [DllImport("user32.dll", CharSet = CharSet.Ansi, EntryPoint = "SendMessageA")]
        private static extern IntPtr SendMessageA(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        /// <summary> 初始化样式并探测原生支持 / Prepare styles and probe native support. </summary>
        public void Setup()
        {
            ApplyStyle();
            EnsureNative();
        }

        /// <summary>
        /// 设置整份文件的路径注解（key 为 1 基行号）；只改动有变化的行，
        /// 这样注解不会因为其它刷新而整片消失 / Diff-update per-line cwd annotations.
        /// </summary>
        public void SetCwd(IReadOnlyDictionary<int, string> annotations)
        {
            if (_editor.Handle == IntPtr.Zero) return;
            ApplyStyle();
            if (!EnsureNative()) return;

            var next = new Dictionary<int, string>();
            if (annotations != null)
            {
                foreach (var kv in annotations)
                {
                    if (string.IsNullOrEmpty(kv.Value)) continue;
                    next[kv.Key - 1] = kv.Value;
                }
            }

            var dirty = new List<int>();

            foreach (var line in _cwd.Keys)
                if (!next.ContainsKey(line)) dirty.Add(line);

            foreach (var kv in next)
            {
                if (_cwd.TryGetValue(kv.Key, out var old) && old == kv.Value) continue;
                dirty.Add(kv.Key);
            }

            foreach (var line in dirty)
            {
                if (next.TryGetValue(line, out var value)) _cwd[line] = value;
                else _cwd.Remove(line);

                Render(line);
            }
        }

        /// <summary> 光标行命令说明；line 传 -1 表示清掉 / Command hint on the caret line. </summary>
        public void SetNote(int line, string text)
        {
            if (_editor.Handle == IntPtr.Zero) return;
            if (!EnsureNative()) return;

            foreach (var old in new List<int>(_notes.Keys))
            {
                if (old == line) continue;
                if (old >= _editor.Lines.Count) { _notes.Remove(old); continue; }
                _notes.Remove(old);
                Render(old);
            }

            if (line < 0 || string.IsNullOrEmpty(text))
            {
                if (_notes.Count == 0) return;
                foreach (var old in new List<int>(_notes.Keys)) { _notes.Remove(old); Render(old); }
                return;
            }

            if (_notes.TryGetValue(line, out var prev) && prev == text) return;

            _notes[line] = text;
            Render(line);
        }

        /// <summary> 清空全部注解（换文件时才用）/ Clear everything, only when switching documents. </summary>
        public void Clear()
        {
            if (_editor.Handle == IntPtr.Zero) return;

            _cwd.Clear();
            _notes.Clear();

            if (EnsureNative()) SendMessageA(_editor.Handle, SciEolAnnotationClearAll, IntPtr.Zero, IntPtr.Zero);
        }

        /// <summary> 把某一行的路径与说明拼起来重绘 / Re-render one line. </summary>
        private void Render(int line)
        {
            if (line < 0 || line >= _editor.Lines.Count) return;

            _cwd.TryGetValue(line, out var cwd);
            _notes.TryGetValue(line, out var note);

            string body;

            if (!string.IsNullOrEmpty(note)) note = Clip(note);

            if (!string.IsNullOrEmpty(cwd) && !string.IsNullOrEmpty(note)) body = cwd + "  〔" + note + "〕";
            else if (!string.IsNullOrEmpty(cwd)) body = cwd;
            else if (!string.IsNullOrEmpty(note)) body = "〔" + note + "〕";
            else body = string.Empty;

            SendAnnotation(line, body);
        }

        private static string Clip(string text)
            => string.IsNullOrEmpty(text) ? text : text.Length <= 60 ? text : text.Substring(0, 59) + "…";

        private void ApplyStyle()
        {
            if (_editor.Handle == IntPtr.Zero) return;

            SendMessageA(_editor.Handle, SciEolAnnotationSetStyleOffset, (IntPtr)NativeStyle, IntPtr.Zero);

            var style = _editor.Styles[NativeStyle];
            style.Font = "Consolas";
            style.Size = 9;
            style.ForeColor = System.Drawing.Color.FromArgb(0x8A, 0x8A, 0x8A);   // 灰
            style.BackColor = _editor.Styles[Style.Default].BackColor;
            style.Italic = true;

            SendMessageA(_editor.Handle, SciEolAnnotationSetVisible, (IntPtr)EolAnnotationStandard, IntPtr.Zero);
        }

        private bool EnsureNative()
        {
            if (_native.HasValue) return _native.Value;

            var handle = _editor.Handle;
            if (handle == IntPtr.Zero) return false;

            SendMessageA(handle, SciEolAnnotationSetVisible, (IntPtr)EolAnnotationStandard, IntPtr.Zero);
            _native = SendMessageA(handle, SciEolAnnotationGetVisible, IntPtr.Zero, IntPtr.Zero) != IntPtr.Zero;
            return _native.Value;
        }

        /// <summary> 写入（或清空）某一行的注解，body 为空串即清除 / Set or clear the annotation of one line. </summary>
        private void SendAnnotation(int line, string body)
        {
            var bytes = EncodeDocumentText(body ?? string.Empty);
            var pointer = Marshal.AllocHGlobal(bytes.Length);

            try
            {
                Marshal.Copy(bytes, 0, pointer, bytes.Length);
                SendMessageA(_editor.Handle, SciEolAnnotationSetText, (IntPtr)line, pointer);
            }
            catch
            {
                // 注解失败不影响编辑
            }
            finally
            {
                Marshal.FreeHGlobal(pointer);
            }
        }

        private byte[] EncodeDocumentText(string text)
        {
            if (_codePage < 0)
                _codePage = (int)SendMessageA(_editor.Handle, SciGetCodepage, IntPtr.Zero, IntPtr.Zero);

            Encoding encoding;

            try
            {
                encoding = _codePage == 65001 ? Encoding.UTF8
                    : _codePage > 0 ? Encoding.GetEncoding(_codePage)
                    : Encoding.Default;
            }
            catch
            {
                encoding = Encoding.Default;
            }

            return encoding.GetBytes(text + "\0");
        }
    }
}
