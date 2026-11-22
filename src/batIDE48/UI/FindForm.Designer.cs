namespace batIDE.UI
{
    /// <summary> 查找替换窗体设计器部分 / Designer part of the find and replace window. </summary>
    partial class FindForm
    {
        private System.ComponentModel.IContainer components = null;

        private System.Windows.Forms.Label lblFind;
        private System.Windows.Forms.TextBox txtFind;
        private System.Windows.Forms.Label lblReplace;
        private System.Windows.Forms.TextBox txtReplace;
        private System.Windows.Forms.CheckBox chkCase;
        private System.Windows.Forms.CheckBox chkWord;
        private System.Windows.Forms.CheckBox chkUp;
        private System.Windows.Forms.Button btnFind;
        private System.Windows.Forms.Button btnReplace;
        private System.Windows.Forms.Button btnReplaceAll;
        private System.Windows.Forms.Label lblResult;

        /// <summary> 释放设计器资源 / Dispose designer resources. </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null) components.Dispose();
            base.Dispose(disposing);
        }

        /// <summary> 装配控件 / Build the controls. </summary>
        private void InitializeComponent()
        {
            this.lblFind = new System.Windows.Forms.Label();
            this.txtFind = new System.Windows.Forms.TextBox();
            this.lblReplace = new System.Windows.Forms.Label();
            this.txtReplace = new System.Windows.Forms.TextBox();
            this.chkCase = new System.Windows.Forms.CheckBox();
            this.chkWord = new System.Windows.Forms.CheckBox();
            this.chkUp = new System.Windows.Forms.CheckBox();
            this.btnFind = new System.Windows.Forms.Button();
            this.btnReplace = new System.Windows.Forms.Button();
            this.btnReplaceAll = new System.Windows.Forms.Button();
            this.lblResult = new System.Windows.Forms.Label();
            this.SuspendLayout();

            // lblFind
            this.lblFind.AutoSize = true;
            this.lblFind.Location = new System.Drawing.Point(14, 18);
            this.lblFind.Text = "查找";

            // txtFind
            this.txtFind.Location = new System.Drawing.Point(70, 14);
            this.txtFind.Size = new System.Drawing.Size(286, 23);

            // chkCase
            this.chkCase.AutoSize = true;
            this.chkCase.Location = new System.Drawing.Point(370, 16);
            this.chkCase.Text = "区分大小写";

            // lblReplace
            this.lblReplace.AutoSize = true;
            this.lblReplace.Location = new System.Drawing.Point(14, 52);
            this.lblReplace.Text = "替换";

            // txtReplace
            this.txtReplace.Location = new System.Drawing.Point(70, 48);
            this.txtReplace.Size = new System.Drawing.Size(286, 23);

            // chkWord
            this.chkWord.AutoSize = true;
            this.chkWord.Location = new System.Drawing.Point(370, 50);
            this.chkWord.Text = "全字匹配";

            // btnFind
            this.btnFind.Location = new System.Drawing.Point(70, 84);
            this.btnFind.Size = new System.Drawing.Size(96, 28);
            this.btnFind.Text = "查找下一个";
            this.btnFind.Click += new System.EventHandler(this.OnFindNext);

            // btnReplace
            this.btnReplace.Location = new System.Drawing.Point(174, 84);
            this.btnReplace.Size = new System.Drawing.Size(76, 28);
            this.btnReplace.Text = "替换";
            this.btnReplace.Click += new System.EventHandler(this.OnReplace);

            // btnReplaceAll
            this.btnReplaceAll.Location = new System.Drawing.Point(258, 84);
            this.btnReplaceAll.Size = new System.Drawing.Size(84, 28);
            this.btnReplaceAll.Text = "全部替换";
            this.btnReplaceAll.Click += new System.EventHandler(this.OnReplaceAll);

            // chkUp
            this.chkUp.AutoSize = true;
            this.chkUp.Location = new System.Drawing.Point(368, 90);
            this.chkUp.Text = "向上";

            // lblResult
            this.lblResult.AutoSize = true;
            this.lblResult.ForeColor = System.Drawing.Color.FromArgb(0x60, 0x60, 0x60);
            this.lblResult.Location = new System.Drawing.Point(70, 122);
            this.lblResult.Text = string.Empty;

            // FindForm
            this.AcceptButton = this.btnFind;
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(456, 150);
            this.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.KeyPreview = true;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "查找与替换";
            this.Controls.AddRange(new System.Windows.Forms.Control[] {
                this.lblFind, this.txtFind, this.chkCase,
                this.lblReplace, this.txtReplace, this.chkWord,
                this.btnFind, this.btnReplace, this.btnReplaceAll, this.chkUp, this.lblResult});

            this.ResumeLayout(false);
        }
    }
}
