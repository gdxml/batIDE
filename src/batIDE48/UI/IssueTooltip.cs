using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using BatLint.Core;

namespace batIDE.UI
{
    /// <summary>
    /// 报错浮层，白底圆角，两行：
    ///   ⓧ 错误类型 ：问题标题
    ///   Prompt : 提示内容（怎么改）
    /// </summary>
    internal sealed class IssueTooltip : Panel
    {
        private const int PadX = 10;
        private const int PadY = 8;
        private const int Radius = 6;
        private const int GlyphSize = 14;
        private const int GlyphGap = 7;
        private const int MaxWidth = 460;

        private static readonly Color BgColor = Color.White;
        private static readonly Color BorderColor = Color.FromArgb(0xDC, 0xDC, 0xDC);
        private static readonly Color ErrorRed = Color.FromArgb(0xD1, 0x34, 0x38);
        private static readonly Color TitleColor = Color.FromArgb(0x22, 0x22, 0x22);
        private static readonly Color LabelColor = Color.FromArgb(0x6A, 0x6A, 0x6A);
        private static readonly Color BodyColor = Color.FromArgb(0x33, 0x33, 0x33);

        private static readonly Font HeadFont = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
        private static readonly Font LabelFont = new Font("Consolas", 8.5f, FontStyle.Bold);
        private static readonly Font BodyFont = new Font("Microsoft YaHei UI", 9f);

        private string _head = string.Empty;
        private string _headRight = string.Empty;
        private string _body = string.Empty;
        private int _labelWidth;
        private int _bodyHeight;

        /// <summary> 构造浮层 / Create the hint panel. </summary>
        public IssueTooltip()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            TabStop = false;
            Visible = false;
            BackColor = BgColor;
        }

        /// <summary> 在父容器坐标处显示一条问题 / Show one issue at a parent-relative point. </summary>
        public void ShowIssue(LintIssue issue, Point locationInParent, Size parentSize)
        {
            _head = string.IsNullOrEmpty(issue.Kind) ? "error" : issue.Kind;        // 例：logicError（按需求不显示中文）
            _headRight = string.IsNullOrEmpty(issue.RuleTitle) ? string.Empty : "：" + issue.RuleTitle;
            _body = string.IsNullOrEmpty(issue.Message) ? "此处有问题" : issue.Message;

            _labelWidth = TextRenderer.MeasureText("Prompt : ", LabelFont).Width;

            int headWidth = GlyphSize + GlyphGap +
                            TextRenderer.MeasureText(_head + _headRight, HeadFont).Width;
            int bodyIdeal = TextRenderer.MeasureText(_body, BodyFont).Width + _labelWidth;

            int width = Math.Min(MaxWidth, Math.Max(210, Math.Max(headWidth, bodyIdeal) + PadX * 2));
            int inner = width - PadX * 2;

            _bodyHeight = Math.Max(BodyFont.Height,
                TextRenderer.MeasureText(_body, BodyFont, new Size(inner - _labelWidth, 0), TextFormatFlags.WordBreak).Height);

            int headHeight = Math.Max(GlyphSize, HeadFont.Height);
            int height = PadY + headHeight + 4 + Math.Max(_bodyHeight, LabelFont.Height) + PadY;
            Size = new Size(width, height);
            Region = RoundedRegion(Size, Radius);

            int x = locationInParent.X;
            int y = locationInParent.Y;
            if (x + Width > parentSize.Width) x = Math.Max(0, parentSize.Width - Width);
            if (y + Height > parentSize.Height) y = Math.Max(0, locationInParent.Y - Height - 16);

            Location = new Point(Math.Max(0, x), Math.Max(0, y));
            Visible = true;
            BringToFront();
            Invalidate();
        }

        /// <summary> 收起浮层 / Hide the hint. </summary>
        public void HideIssue()
        {
            if (Visible) Visible = false;
        }

        private static Region RoundedRegion(Size size, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(0, 0, d, d, 180, 90);
            path.AddArc(size.Width - d - 1, 0, d, d, 270, 90);
            path.AddArc(size.Width - d - 1, size.Height - d - 1, d, d, 0, 90);
            path.AddArc(0, size.Height - d - 1, d, d, 90, 90);
            path.CloseAllFigures();
            return new Region(path);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BgColor);

            // 圆角描边
            using (var pen = new Pen(BorderColor))
            using (var path = new GraphicsPath())
            {
                int d = Radius * 2;
                path.AddArc(1, 1, d, d, 180, 90);
                path.AddArc(Width - d - 2, 1, d, d, 270, 90);
                path.AddArc(Width - d - 2, Height - d - 2, d, d, 0, 90);
                path.AddArc(1, Height - d - 2, d, d, 90, 90);
                path.CloseAllFigures();
                g.DrawPath(pen, path);
            }

            int y = PadY;

            // ── 第一行：ⓧ（红色） + 错误类型 ：问题 ──
            int headHeight = Math.Max(GlyphSize, HeadFont.Height);
            int glyphTop = y + (headHeight - GlyphSize) / 2;
            DrawErrorGlyph(g, new Rectangle(PadX, glyphTop, GlyphSize, GlyphSize));

            int textLeft = PadX + GlyphSize + GlyphGap;
            TextRenderer.DrawText(g, _head, HeadFont,
                new Rectangle(textLeft, y, Width - textLeft - PadX, headHeight), TitleColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);

            int headWidth = TextRenderer.MeasureText(_head, HeadFont).Width;
            TextRenderer.DrawText(g, _headRight, BodyFont,
                new Rectangle(textLeft + headWidth, y, Width - textLeft - headWidth - PadX, headHeight), TitleColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);

            // ── 第二行：Prompt : 提示内容 ──
            int bodyTop = y + headHeight + 4;
            TextRenderer.DrawText(g, "Prompt :", LabelFont,
                new Rectangle(PadX, bodyTop, _labelWidth, LabelFont.Height), LabelColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

            TextRenderer.DrawText(g, _body, BodyFont,
                new Rectangle(PadX + _labelWidth + 2, bodyTop, Width - PadX * 2 - _labelWidth - 2, _bodyHeight), BodyColor,
                TextFormatFlags.Left | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
        }

        /// <summary> 红色 ⓧ 图标 / Red circled-X glyph. </summary>
        private static void DrawErrorGlyph(Graphics g, Rectangle box)
        {
            using var pen = new Pen(ErrorRed, 1.7f);
            g.DrawEllipse(pen, box);
            int inset = Math.Max(3, box.Width / 4);
            g.DrawLine(pen, box.Left + inset, box.Top + inset, box.Right - inset, box.Bottom - inset);
            g.DrawLine(pen, box.Right - inset, box.Top + inset, box.Left + inset, box.Bottom - inset);
        }
    }
}
