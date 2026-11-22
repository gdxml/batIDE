namespace batIDE.UI
{
    /// <summary> 主窗体设计器部分 / Designer part of the main window. </summary>
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        private System.Windows.Forms.MenuStrip menuStrip;
        private System.Windows.Forms.ToolStripMenuItem menuFile;
        private System.Windows.Forms.ToolStripMenuItem miOpen;
        private System.Windows.Forms.ToolStripMenuItem miSave;
        private System.Windows.Forms.ToolStripMenuItem miSaveAs;
        private System.Windows.Forms.ToolStripSeparator miSeparator;
        private System.Windows.Forms.ToolStripMenuItem miExit;
        private System.Windows.Forms.ToolStripMenuItem menuRun;
        private System.Windows.Forms.ToolStripMenuItem miRrun;
        private System.Windows.Forms.ToolStripMenuItem miRDebug;
        private System.Windows.Forms.ToolStripMenuItem menuEdit;
        private System.Windows.Forms.ToolStripMenuItem miQuickFix;
        private System.Windows.Forms.ToolStripSeparator miEditSep;
        private System.Windows.Forms.ToolStripMenuItem miPrevIssue;
        private System.Windows.Forms.ToolStripMenuItem miNextIssue;
        private System.Windows.Forms.ToolStripMenuItem miIme;

        private System.Windows.Forms.ToolStrip toolStrip;
        private System.Windows.Forms.ToolStripButton btnOpen;
        private System.Windows.Forms.ToolStripButton btnSave;
        private System.Windows.Forms.ToolStripSeparator toolSeparator;
        private System.Windows.Forms.ToolStripButton btnRun;
        private System.Windows.Forms.ToolStripButton btnFind;
        private System.Windows.Forms.ToolStripButton btnBreakpoint;
        private System.Windows.Forms.ToolStripSeparator toolSeparatorRight;
        private System.Windows.Forms.ToolStripButton btnHelp;

        private System.Windows.Forms.SplitContainer splitContainer;
        private System.Windows.Forms.Panel editorHost;
        private System.Windows.Forms.Label lblPlaceholder;
        private System.Windows.Forms.TextBox outputBox;

        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel lblStatus;
        private System.Windows.Forms.ToolStripStatusLabel lblHint;

        /// <summary> 释放窗体资源 / Release form resources. </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null) components.Dispose();
            base.Dispose(disposing);
        }

        /// <summary> 设计器初始化：全部控件在此声明，保证可视化编辑 / Designer init: controls declared here for visual editing. </summary>
        private void InitializeComponent()
        {
            this.menuStrip = new System.Windows.Forms.MenuStrip();
            this.menuFile = new System.Windows.Forms.ToolStripMenuItem();
            this.miOpen = new System.Windows.Forms.ToolStripMenuItem();
            this.miSave = new System.Windows.Forms.ToolStripMenuItem();
            this.miSaveAs = new System.Windows.Forms.ToolStripMenuItem();
            this.miSeparator = new System.Windows.Forms.ToolStripSeparator();
            this.miExit = new System.Windows.Forms.ToolStripMenuItem();
            this.menuRun = new System.Windows.Forms.ToolStripMenuItem();
            this.miRrun = new System.Windows.Forms.ToolStripMenuItem();
            this.miRDebug = new System.Windows.Forms.ToolStripMenuItem();
            this.menuEdit = new System.Windows.Forms.ToolStripMenuItem();
            this.miQuickFix = new System.Windows.Forms.ToolStripMenuItem();
            this.miEditSep = new System.Windows.Forms.ToolStripSeparator();
            this.miPrevIssue = new System.Windows.Forms.ToolStripMenuItem();
            this.miNextIssue = new System.Windows.Forms.ToolStripMenuItem();
            this.miIme = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStrip = new System.Windows.Forms.ToolStrip();
            this.btnOpen = new System.Windows.Forms.ToolStripButton();
            this.btnSave = new System.Windows.Forms.ToolStripButton();
            this.toolSeparator = new System.Windows.Forms.ToolStripSeparator();
            this.btnRun = new System.Windows.Forms.ToolStripButton();
            this.btnFind = new System.Windows.Forms.ToolStripButton();
            this.btnBreakpoint = new System.Windows.Forms.ToolStripButton();
            this.toolSeparatorRight = new System.Windows.Forms.ToolStripSeparator();
            this.btnHelp = new System.Windows.Forms.ToolStripButton();
            this.splitContainer = new System.Windows.Forms.SplitContainer();
            this.editorHost = new System.Windows.Forms.Panel();
            this.lblPlaceholder = new System.Windows.Forms.Label();
            this.outputBox = new System.Windows.Forms.TextBox();
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.lblStatus = new System.Windows.Forms.ToolStripStatusLabel();
            this.lblHint = new System.Windows.Forms.ToolStripStatusLabel();
            this.menuStrip.SuspendLayout();
            this.toolStrip.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).BeginInit();
            this.splitContainer.Panel1.SuspendLayout();
            this.splitContainer.Panel2.SuspendLayout();
            this.splitContainer.SuspendLayout();
            this.statusStrip.SuspendLayout();
            this.SuspendLayout();

            // menuStrip
            this.menuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { this.menuFile, this.menuEdit, this.menuRun });
            this.menuStrip.Location = new System.Drawing.Point(0, 0);
            this.menuStrip.Name = "menuStrip";
            this.menuStrip.Size = new System.Drawing.Size(1100, 25);
            this.menuStrip.TabIndex = 0;

            // menuFile
            this.menuFile.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.miOpen, this.miSave, this.miSaveAs, this.miSeparator, this.miExit});
            this.menuFile.Name = "menuFile";
            this.menuFile.Text = "文件(&F)";

            // miOpen
            this.miOpen.Name = "miOpen";
            this.miOpen.ShortcutKeys = System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.O;
            this.miOpen.Text = "打开(&O)...";
            this.miOpen.Click += new System.EventHandler(this.OnOpen);

            // miSave
            this.miSave.Name = "miSave";
            this.miSave.ShortcutKeys = System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.S;
            this.miSave.Text = "保存(&S)";
            this.miSave.Click += new System.EventHandler(this.OnSave);

            // miSaveAs
            this.miSaveAs.Name = "miSaveAs";
            this.miSaveAs.Text = "另存为(&A)...";
            this.miSaveAs.Click += new System.EventHandler(this.OnSaveAs);

            // miSeparator
            this.miSeparator.Name = "miSeparator";

            // miExit
            this.miExit.Name = "miExit";
            this.miExit.Text = "退出(&X)";
            this.miExit.Click += new System.EventHandler(this.OnExit);

            // menuRun
            this.menuRun.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { this.miRrun, this.miRDebug });
            this.menuRun.Name = "menuRun";
            this.menuRun.Text = "运行(&R)";

            // miRrun
            this.miRrun.Name = "miRrun";
            this.miRrun.ShortcutKeys = System.Windows.Forms.Keys.F5;
            this.miRrun.Text = "运行脚本(&R)";
            this.miRrun.Click += new System.EventHandler(this.OnRun);

            // miRDebug
            this.miRDebug.Name = "miRDebug";
            this.miRDebug.ShortcutKeys = System.Windows.Forms.Keys.F9;
            this.miRDebug.Text = "调试运行（轨迹 + 变量）（&D）";
            this.miRDebug.Click += new System.EventHandler(this.OnDebugRun);

            // menuEdit
            this.menuEdit.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.miQuickFix, this.miEditSep, this.miPrevIssue, this.miNextIssue, this.miIme });
            this.menuEdit.Name = "menuEdit";
            this.menuEdit.Text = "编辑(&E)";

            // miQuickFix
            this.miQuickFix.Name = "miQuickFix";
            this.miQuickFix.ShortcutKeyDisplayString = "Ctrl+.";
            this.miQuickFix.Text = "快速修复此问题(&Q)";
            this.miQuickFix.Click += new System.EventHandler(this.OnQuickFix);

            // miEditSep
            this.miEditSep.Name = "miEditSep";

            // miPrevIssue
            this.miPrevIssue.Name = "miPrevIssue";
            this.miPrevIssue.ShortcutKeyDisplayString = "Shift+F8";
            this.miPrevIssue.Text = "上一处问题(&P)";
            this.miPrevIssue.Click += new System.EventHandler(this.OnPrevIssue);

            // miNextIssue
            this.miNextIssue.Name = "miNextIssue";
            this.miNextIssue.ShortcutKeyDisplayString = "F8";
            this.miNextIssue.Text = "下一处问题(&N)";
            this.miNextIssue.Click += new System.EventHandler(this.OnNextIssue);

            // miIme：脚本编辑建议用英文输入法，避免 ！％：； 等被全角化
            this.miIme.Name = "miIme";
            this.miIme.Text = "编辑时禁用输入法(&I)";
            this.miIme.Click += new System.EventHandler(this.OnToggleIme);

            // toolStrip
            this.toolStrip.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.btnOpen, this.btnSave, this.toolSeparator, this.btnRun, this.btnFind, this.btnBreakpoint,
                this.toolSeparatorRight, this.btnHelp});
            this.toolStrip.Location = new System.Drawing.Point(0, 25);
            this.toolStrip.Name = "toolStrip";
            this.toolStrip.Padding = new System.Windows.Forms.Padding(6, 2, 6, 2);
            this.toolStrip.Size = new System.Drawing.Size(1100, 27);
            this.toolStrip.TabIndex = 1;

            // btnOpen
            this.btnOpen.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnOpen.Name = "btnOpen";
            this.btnOpen.Text = "打开";
            this.btnOpen.Click += new System.EventHandler(this.OnOpen);

            // btnSave
            this.btnSave.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnSave.Name = "btnSave";
            this.btnSave.Text = "保存";
            this.btnSave.Click += new System.EventHandler(this.OnSave);

            // toolSeparator
            this.toolSeparator.Name = "toolSeparator";

            // btnRun
            this.btnRun.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnRun.Name = "btnRun";
            this.btnRun.Text = "运行 (F5)";
            this.btnRun.Click += new System.EventHandler(this.OnRun);

            // btnFind
            this.btnFind.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnFind.Name = "btnFind";
            this.btnFind.Text = "查找 (Ctrl+F)";
            this.btnFind.Click += new System.EventHandler(this.OnFind);

            // btnBreakpoint
            this.btnBreakpoint.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnBreakpoint.Name = "btnBreakpoint";
            this.btnBreakpoint.Text = "断点 (F2)";
            this.btnBreakpoint.Click += new System.EventHandler(this.OnToggleBreakpoint);

            // toolSeparatorRight
            this.toolSeparatorRight.Name = "toolSeparatorRight";

            // btnHelp
            this.btnHelp.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.btnHelp.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnHelp.Name = "btnHelp";
            this.btnHelp.Text = "语法帮助";
            this.btnHelp.Click += new System.EventHandler(this.OnHelp);

            // splitContainer
            this.splitContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer.Location = new System.Drawing.Point(0, 52);
            this.splitContainer.Name = "splitContainer";
            this.splitContainer.Orientation = System.Windows.Forms.Orientation.Horizontal;
            this.splitContainer.Panel1.Controls.Add(this.editorHost);
            this.splitContainer.Panel1MinSize = 200;
            this.splitContainer.Panel2.Controls.Add(this.outputBox);
            this.splitContainer.Panel2MinSize = 80;
            this.splitContainer.Size = new System.Drawing.Size(1100, 646);
            this.splitContainer.SplitterDistance = 470;
            this.splitContainer.SplitterWidth = 6;
            this.splitContainer.TabIndex = 2;

            // editorHost
            this.editorHost.BackColor = System.Drawing.Color.White;
            this.editorHost.Controls.Add(this.lblPlaceholder);
            this.editorHost.Dock = System.Windows.Forms.DockStyle.Fill;
            this.editorHost.Location = new System.Drawing.Point(0, 0);
            this.editorHost.Name = "editorHost";
            this.editorHost.Size = new System.Drawing.Size(1100, 470);
            this.editorHost.TabIndex = 0;

            // lblPlaceholder
            this.lblPlaceholder.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblPlaceholder.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.lblPlaceholder.ForeColor = System.Drawing.Color.Silver;
            this.lblPlaceholder.Location = new System.Drawing.Point(0, 0);
            this.lblPlaceholder.Name = "lblPlaceholder";
            this.lblPlaceholder.Size = new System.Drawing.Size(1100, 470);
            this.lblPlaceholder.TabIndex = 0;
            this.lblPlaceholder.Text = "编辑器区域（运行时由 Scintilla 接管，设计器中不可见）";
            this.lblPlaceholder.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;

            // outputBox
            this.outputBox.BackColor = System.Drawing.Color.FromArgb(250, 250, 250);
            this.outputBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.outputBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.outputBox.Font = new System.Drawing.Font("Consolas", 9F);
            this.outputBox.Location = new System.Drawing.Point(0, 0);
            this.outputBox.Multiline = true;
            this.outputBox.Name = "outputBox";
            this.outputBox.ReadOnly = true;
            this.outputBox.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.outputBox.Size = new System.Drawing.Size(1100, 170);
            this.outputBox.TabIndex = 0;
            this.outputBox.WordWrap = false;

            // statusStrip
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { this.lblStatus, this.lblHint });
            this.statusStrip.Location = new System.Drawing.Point(0, 698);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.Size = new System.Drawing.Size(1100, 22);
            this.statusStrip.TabIndex = 3;

            // lblStatus
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Text = "就绪";

            // lblHint
            this.lblHint.Name = "lblHint";
            this.lblHint.Spring = true;
            this.lblHint.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.lblHint.ForeColor = System.Drawing.Color.Gray;

            // MainForm
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(1100, 720);
            this.Controls.Add(this.splitContainer);
            this.Controls.Add(this.toolStrip);
            this.Controls.Add(this.menuStrip);
            this.Controls.Add(this.statusStrip);
            this.MainMenuStrip = this.menuStrip;
            this.MinimumSize = new System.Drawing.Size(720, 480);
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "batIDE";
            this.menuStrip.ResumeLayout(false);
            this.menuStrip.PerformLayout();
            this.toolStrip.ResumeLayout(false);
            this.toolStrip.PerformLayout();
            this.splitContainer.Panel1.ResumeLayout(false);
            this.splitContainer.Panel2.ResumeLayout(false);
            this.splitContainer.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).EndInit();
            this.splitContainer.ResumeLayout(false);
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}
