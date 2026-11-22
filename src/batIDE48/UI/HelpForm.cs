using System;
using System.Drawing;
using System.Windows.Forms;
using BatLint.Core;

namespace batIDE.UI
{
    /// <summary> 语法帮助窗口：左侧主题、右侧由易到难的示例 / Help window with topics and graded examples. </summary>
    public partial class HelpForm : Form
    {
        private static readonly Color NavSelected = Color.FromArgb(232, 242, 254);
        private static readonly Color NavHover = Color.FromArgb(240, 244, 250);
        private static readonly Color CodeColor = Color.FromArgb(12, 68, 124);
        private static readonly Color EffectColor = Color.FromArgb(95, 94, 90);

        private Button _selected;

        /// <summary> 构造并填充左侧主题列表 / Build the form and populate the topic list. </summary>
        public HelpForm()
        {
            InitializeComponent();
            BuildNavigation();
        }

        /// <summary> 生成左侧主题按钮 / Create the topic buttons. </summary>
        private void BuildNavigation()
        {
            foreach (var topic in HelpContent.Topics)
            {
                var button = new Button
                {
                    Text = "   " + topic.Name,
                    Tag = topic,
                    Width = 202,
                    Height = 36,
                    Margin = new Padding(0, 0, 0, 2),
                    TextAlign = ContentAlignment.MiddleLeft,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.Transparent,
                    ForeColor = Color.FromArgb(45, 45, 45),
                    Font = new Font("Microsoft YaHei UI", 9.5F),
                    Cursor = Cursors.Hand
                };

                button.FlatAppearance.BorderSize = 0;
                button.FlatAppearance.MouseOverBackColor = NavHover;
                button.Click += (s, e) => Select(topic, button);
                navPanel.Controls.Add(button);
            }

            if (navPanel.Controls.Count > 0) Select(HelpContent.Topics[0], (Button)navPanel.Controls[0]);
        }

        /// <summary> 选中主题并渲染右侧内容 / Select a topic and render its content. </summary>
        private void Select(HelpTopic topic, Button button)
        {
            if (_selected != null && _selected != button) _selected.BackColor = Color.Transparent;
            _selected = button;
            button.BackColor = NavSelected;

            titleLabel.Text = topic.Name;
            Render(topic);
        }

        /// <summary> 渲染主题说明与示例 / Render summary and examples. </summary>
        private void Render(HelpTopic topic)
        {
            helpText.Clear();

            Append(topic.Summary + Environment.NewLine + Environment.NewLine, helpText.Font, EffectColor);

            var normal = helpText.Font;
            var mono = new Font("Consolas", 10.5F);

            for (var i = 0; i < topic.Examples.Length; i++)
            {
                var item = topic.Examples[i];

                Append($"{i + 1}.  ", normal, EffectColor);
                Append(item.Code + Environment.NewLine, mono, CodeColor);
                Append("     →  " + item.Effect + Environment.NewLine + Environment.NewLine, normal, EffectColor);
            }

            helpText.SelectionStart = 0;
            helpText.ScrollToCaret();
        }

        /// <summary> 以指定字体与颜色追加文本 / Append text with the given font and color. </summary>
        private void Append(string text, Font font, Color color)
        {
            helpText.SelectionStart = helpText.TextLength;
            helpText.SelectionLength = 0;
            helpText.SelectionFont = font;
            helpText.SelectionColor = color;
            helpText.AppendText(text);
        }
    }
}
