namespace SaveEditor
{
    partial class frmSystemEditor
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.lstSaveSlot = new System.Windows.Forms.ListBox();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.mnuFile = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuLoadSave = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuWriteSave = new System.Windows.Forms.ToolStripMenuItem();
            this.mnuExit = new System.Windows.Forms.ToolStripMenuItem();
            this.chklstFlags = new System.Windows.Forms.CheckedListBox();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.menuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.lstSaveSlot);
            this.groupBox1.Location = new System.Drawing.Point(0, 24);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(296, 432);
            this.groupBox1.TabIndex = 0;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Save Slot Editor";
            // 
            // lstSaveSlot
            // 
            this.lstSaveSlot.Dock = System.Windows.Forms.DockStyle.Left;
            this.lstSaveSlot.FormattingEnabled = true;
            this.lstSaveSlot.Location = new System.Drawing.Point(3, 16);
            this.lstSaveSlot.Name = "lstSaveSlot";
            this.lstSaveSlot.Size = new System.Drawing.Size(157, 413);
            this.lstSaveSlot.TabIndex = 0;
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.chklstFlags);
            this.groupBox2.Location = new System.Drawing.Point(304, 24);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(472, 432);
            this.groupBox2.TabIndex = 1;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Flag Editor";
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.mnuFile});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(800, 24);
            this.menuStrip1.TabIndex = 2;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // mnuFile
            // 
            this.mnuFile.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.mnuLoadSave,
            this.mnuWriteSave,
            this.mnuExit});
            this.mnuFile.Name = "mnuFile";
            this.mnuFile.Size = new System.Drawing.Size(37, 20);
            this.mnuFile.Text = "&File";
            // 
            // mnuLoadSave
            // 
            this.mnuLoadSave.Name = "mnuLoadSave";
            this.mnuLoadSave.Size = new System.Drawing.Size(180, 22);
            this.mnuLoadSave.Text = "&Load Save";
            this.mnuLoadSave.Click += new System.EventHandler(this.mnuLoadSave_Click);
            // 
            // mnuWriteSave
            // 
            this.mnuWriteSave.Name = "mnuWriteSave";
            this.mnuWriteSave.Size = new System.Drawing.Size(180, 22);
            this.mnuWriteSave.Text = "&Write Save";
            this.mnuWriteSave.Click += new System.EventHandler(this.mnuWriteSave_Click);
            // 
            // mnuExit
            // 
            this.mnuExit.Name = "mnuExit";
            this.mnuExit.Size = new System.Drawing.Size(180, 22);
            this.mnuExit.Text = "E&xit";
            this.mnuExit.Click += new System.EventHandler(this.mnuExit_Click);
            // 
            // chklstFlags
            // 
            this.chklstFlags.Dock = System.Windows.Forms.DockStyle.Fill;
            this.chklstFlags.FormattingEnabled = true;
            this.chklstFlags.Location = new System.Drawing.Point(3, 16);
            this.chklstFlags.Name = "chklstFlags";
            this.chklstFlags.Size = new System.Drawing.Size(466, 413);
            this.chklstFlags.TabIndex = 0;
            // 
            // frmSystemEditor
            // 
            this.AllowDrop = true;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 462);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.menuStrip1);
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "frmSystemEditor";
            this.Text = "System Editor";
            this.Load += new System.EventHandler(this.frmSystemEditor_Load);
            this.DragDrop += new System.Windows.Forms.DragEventHandler(this.frmSystemEditor_DragDrop);
            this.DragEnter += new System.Windows.Forms.DragEventHandler(this.frmSystemEditor_DragEnter);
            this.groupBox1.ResumeLayout(false);
            this.groupBox2.ResumeLayout(false);
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.ListBox lstSaveSlot;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem mnuFile;
        private System.Windows.Forms.ToolStripMenuItem mnuLoadSave;
        private System.Windows.Forms.ToolStripMenuItem mnuWriteSave;
        private System.Windows.Forms.ToolStripMenuItem mnuExit;
        private System.Windows.Forms.CheckedListBox chklstFlags;
    }
}