using ScintillaNET;
using SD = System.Drawing;

namespace batIDE.UI
{
    /// <summary> bat 语法配色表：与 LexBatch 的 9 个样式一一对应 / Palette matching LexBatch styles. </summary>
    internal static class SyntaxColors
    {
        /// <summary> 0 普通文本 / Plain text. </summary>
        public static readonly SD.Color Default = SD.Color.FromArgb(0x1F, 0x1F, 0x1F);

        /// <summary> 1 行注释 REM / Line comment. </summary>
        public static readonly SD.Color Comment = SD.Color.FromArgb(0x00, 0x80, 0x00);

        /// <summary> 2 关键字 / Keyword. </summary>
        public static readonly SD.Color Keyword = SD.Color.FromArgb(0x00, 0x00, 0xFF);

        /// <summary> 3 标签 :label / Label. </summary>
        public static readonly SD.Color Label = SD.Color.FromArgb(0xAF, 0x00, 0xDB);

        /// <summary> 4 @echo off 的 @ / Hide mark. </summary>
        public static readonly SD.Color HideMark = SD.Color.FromArgb(0x80, 0x80, 0x80);

        /// <summary> 5 命令 set/echo/call / Command. </summary>
        public static readonly SD.Color Command = SD.Color.FromArgb(0x79, 0x5E, 0x26);

        /// <summary> 6 标识符 %NAME% / Identifier. </summary>
        public static readonly SD.Color Identifier = SD.Color.FromArgb(0x00, 0x10, 0x80);

        /// <summary> 7 运算符 & | &gt; / Operator. </summary>
        public static readonly SD.Color Operator = SD.Color.FromArgb(0xD0, 0x60, 0x00);

        /// <summary> 8 标签后的注释 / Comment after label. </summary>
        public static readonly SD.Color AfterLabel = SD.Color.FromArgb(0x00, 0x80, 0x00);

        /// <summary> 编辑器底色 / Editor background. </summary>
        public static readonly SD.Color EditorBack = SD.Color.White;

        /// <summary> 当前行底色 / Current line background. </summary>
        public static readonly SD.Color CaretLineBack = SD.Color.FromArgb(0xE8, 0xF2, 0xFE);

        /// <summary> 光标颜色 / Caret color. </summary>
        public static readonly SD.Color Caret = SD.Color.Black;

        /// <summary> 行号前景 / Line number foreground. </summary>
        public static readonly SD.Color LineNumberFore = SD.Color.Gray;

        /// <summary> 行号背景 / Line number background. </summary>
        public static readonly SD.Color LineNumberBack = SD.Color.FromArgb(0xF5, 0xF5, 0xF5);

        /// <summary> 把整套配色套到编辑器 / Apply the whole palette to the editor. </summary>
        public static void Apply(Scintilla ed)
        {
            ed.Styles[0].ForeColor = Default;
            ed.Styles[1].ForeColor = Comment;
            ed.Styles[2].ForeColor = Keyword;
            ed.Styles[2].Bold = true;
            ed.Styles[3].ForeColor = Label;
            ed.Styles[4].ForeColor = HideMark;
            ed.Styles[5].ForeColor = Command;
            ed.Styles[6].ForeColor = Identifier;
            ed.Styles[7].ForeColor = Operator;
            ed.Styles[8].ForeColor = AfterLabel;

            ed.CaretLineVisible = true;
            ed.CaretLineBackColor = CaretLineBack;
            ed.CaretForeColor = Caret;

            ed.Styles[Style.LineNumber].ForeColor = LineNumberFore;
            ed.Styles[Style.LineNumber].BackColor = LineNumberBack;
        }
    }
}
