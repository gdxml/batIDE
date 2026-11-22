namespace batIDE.UI
{
    /// <summary> 语法帮助窗体设计器部分 / Designer part of the syntax help window. </summary>
    partial class HelpForm
    {
        private System.ComponentModel.IContainer components = null;

        private System.Windows.Forms.SplitContainer splitContainer;
        private System.Windows.Forms.FlowLayoutPanel navPanel;
        private System.Windows.Forms.Panel detailPanel;
        private System.Windows.Forms.Label titleLabel;
        private System.Windows.Forms.RichTextBox helpText;
        private System.Windows.Forms.Label hintLabel;

        /// <summary> 释放窗体资源 / Release form resources. </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null) components.Dispose();
            base.Dispose(disposing);
        }

        /// <summary> 设计器初始化：左侧导航 + 右侧内容 / Designer init: left navigation and right content. </summary>
        private void InitializeComponent()
        {
            this.splitContainer = new System.Windows.Forms.SplitContainer();
            this.navPanel = new System.Windows.Forms.FlowLayoutPanel();
            this.hintLabel = new System.Windows.Forms.Label();
            this.detailPanel = new System.Windows.Forms.Panel();
            this.helpText = new System.Windows.Forms.RichTextBox();
            this.titleLabel = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).BeginInit();
            this.splitContainer.Panel1.SuspendLayout();
            this.splitContainer.Panel2.SuspendLayout();
            this.splitContainer.SuspendLayout();
            this.detailPanel.SuspendLayout();
            this.SuspendLayout();

            // splitContainer
            this.splitContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.splitContainer.Location = new System.Drawing.Point(0, 0);
            this.splitContainer.Name = "splitContainer";
            this.splitContainer.Panel1.Controls.Add(this.navPanel);
            this.splitContainer.Panel1.Controls.Add(this.hintLabel);
            this.splitContainer.Panel1MinSize = 180;
            this.splitContainer.Panel2.Controls.Add(this.detailPanel);
            this.splitContainer.Panel2MinSize = 360;
            this.splitContainer.Size = new System.Drawing.Size(1040, 700);
            this.splitContainer.SplitterDistance = 226;
            this.splitContainer.SplitterWidth = 1;
            this.splitContainer.TabIndex = 0;

            // hintLabel
            this.hintLabel.Dock = System.Windows.Forms.DockStyle.Top;
            this.hintLabel.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F, System.Drawing.FontStyle.Bold);
            this.hintLabel.ForeColor = System.Drawing.Color.FromArgb(60, 60, 60);
            this.hintLabel.Location = new System.Drawing.Point(0, 0);
            this.hintLabel.Name = "hintLabel";
            this.hintLabel.Padding = new System.Windows.Forms.Padding(14, 14, 0, 10);
            this.hintLabel.Size = new System.Drawing.Size(226, 52);
            this.hintLabel.TabIndex = 0;
            this.hintLabel.Text = "常见语法";

            // navPanel
            this.navPanel.AutoScroll = true;
            this.navPanel.BackColor = System.Drawing.Color.FromArgb(247, 247, 245);
            this.navPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.navPanel.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.navPanel.Location = new System.Drawing.Point(0, 52);
            this.navPanel.Name = "navPanel";
            this.navPanel.Padding = new System.Windows.Forms.Padding(8, 0, 8, 8);
            this.navPanel.Size = new System.Drawing.Size(226, 648);
            this.navPanel.TabIndex = 1;
            this.navPanel.WrapContents = false;

            // detailPanel
            this.detailPanel.BackColor = System.Drawing.Color.White;
            this.detailPanel.Controls.Add(this.helpText);
            this.detailPanel.Controls.Add(this.titleLabel);
            this.detailPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.detailPanel.Location = new System.Drawing.Point(0, 0);
            this.detailPanel.Name = "detailPanel";
            this.detailPanel.Padding = new System.Windows.Forms.Padding(20, 12, 20, 16);
            this.detailPanel.Size = new System.Drawing.Size(813, 700);
            this.detailPanel.TabIndex = 0;

            // titleLabel
            this.titleLabel.Dock = System.Windows.Forms.DockStyle.Top;
            this.titleLabel.Font = new System.Drawing.Font("Microsoft YaHei UI", 14F, System.Drawing.FontStyle.Bold);
            this.titleLabel.ForeColor = System.Drawing.Color.FromArgb(31, 31, 31);
            this.titleLabel.Location = new System.Drawing.Point(20, 12);
            this.titleLabel.Name = "titleLabel";
            this.titleLabel.Size = new System.Drawing.Size(773, 42);
            this.titleLabel.TabIndex = 0;
            this.titleLabel.Text = "选择一个语法主题";
            this.titleLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            // helpText
            this.helpText.BackColor = System.Drawing.Color.White;
            this.helpText.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.helpText.DetectUrls = false;
            this.helpText.Dock = System.Windows.Forms.DockStyle.Fill;
            this.helpText.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.helpText.ForeColor = System.Drawing.Color.FromArgb(45, 45, 45);
            this.helpText.Location = new System.Drawing.Point(20, 54);
            this.helpText.Name = "helpText";
            this.helpText.ReadOnly = true;
            this.helpText.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
            this.helpText.Size = new System.Drawing.Size(773, 630);
            this.helpText.TabIndex = 1;
            this.helpText.Text = "";

            // HelpForm
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.BackColor = System.Drawing.Color.White;
            this.ClientSize = new System.Drawing.Size(1040, 700);
            this.Controls.Add(this.splitContainer);
            this.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.MinimumSize = new System.Drawing.Size(800, 520);
            this.Name = "HelpForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "语法帮助 - batIDE";
            this.splitContainer.Panel1.ResumeLayout(false);
            this.splitContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).EndInit();
            this.splitContainer.ResumeLayout(false);
            this.detailPanel.ResumeLayout(false);
            this.ResumeLayout(false);
        }
    }
}
