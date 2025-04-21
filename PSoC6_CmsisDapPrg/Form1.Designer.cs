namespace PSoC6_CmsisDapPrg
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            btProgram = new Button();
            tbStatus = new TextBox();
            btAnalyzeFile = new Button();
            ofd = new OpenFileDialog();
            btScanUSB = new Button();
            cbProgs = new ComboBox();
            groupBox1 = new GroupBox();
            groupBox2 = new GroupBox();
            tbFirmware = new TextBox();
            btSelect = new Button();
            gbProgram = new GroupBox();
            btInfo = new Button();
            btVerify = new Button();
            btErase = new Button();
            pbMain = new ProgressBar();
            groupBox1.SuspendLayout();
            groupBox2.SuspendLayout();
            gbProgram.SuspendLayout();
            SuspendLayout();
            // 
            // btProgram
            // 
            btProgram.Location = new Point(6, 20);
            btProgram.Name = "btProgram";
            btProgram.Size = new Size(171, 24);
            btProgram.TabIndex = 0;
            btProgram.Text = "Program";
            btProgram.UseVisualStyleBackColor = true;
            btProgram.Click += btProgram_Click;
            // 
            // tbStatus
            // 
            tbStatus.Location = new Point(12, 176);
            tbStatus.Multiline = true;
            tbStatus.Name = "tbStatus";
            tbStatus.ScrollBars = ScrollBars.Vertical;
            tbStatus.Size = new Size(1105, 495);
            tbStatus.TabIndex = 1;
            // 
            // btAnalyzeFile
            // 
            btAnalyzeFile.Location = new Point(928, 22);
            btAnalyzeFile.Name = "btAnalyzeFile";
            btAnalyzeFile.Size = new Size(171, 24);
            btAnalyzeFile.TabIndex = 2;
            btAnalyzeFile.Text = "Analyze USB dump";
            btAnalyzeFile.UseVisualStyleBackColor = true;
            btAnalyzeFile.Visible = false;
            btAnalyzeFile.Click += btAnalyzeFile_Click;
            // 
            // btScanUSB
            // 
            btScanUSB.Location = new Point(6, 22);
            btScanUSB.Name = "btScanUSB";
            btScanUSB.Size = new Size(171, 24);
            btScanUSB.TabIndex = 3;
            btScanUSB.Text = "Scan USB";
            btScanUSB.UseVisualStyleBackColor = true;
            btScanUSB.Click += btScanUSB_Click;
            // 
            // cbProgs
            // 
            cbProgs.DropDownWidth = 400;
            cbProgs.FormattingEnabled = true;
            cbProgs.Location = new Point(183, 22);
            cbProgs.Name = "cbProgs";
            cbProgs.Size = new Size(916, 23);
            cbProgs.TabIndex = 4;
            // 
            // groupBox1
            // 
            groupBox1.Controls.Add(btScanUSB);
            groupBox1.Controls.Add(cbProgs);
            groupBox1.Location = new Point(12, 7);
            groupBox1.Name = "groupBox1";
            groupBox1.Size = new Size(1105, 55);
            groupBox1.TabIndex = 5;
            groupBox1.TabStop = false;
            groupBox1.Text = "Select programmer";
            // 
            // groupBox2
            // 
            groupBox2.Controls.Add(tbFirmware);
            groupBox2.Controls.Add(btSelect);
            groupBox2.Location = new Point(12, 63);
            groupBox2.Name = "groupBox2";
            groupBox2.Size = new Size(1105, 55);
            groupBox2.TabIndex = 6;
            groupBox2.TabStop = false;
            groupBox2.Text = "Firmware";
            // 
            // tbFirmware
            // 
            tbFirmware.Location = new Point(183, 22);
            tbFirmware.Name = "tbFirmware";
            tbFirmware.Size = new Size(916, 23);
            tbFirmware.TabIndex = 4;
            // 
            // btSelect
            // 
            btSelect.Location = new Point(6, 22);
            btSelect.Name = "btSelect";
            btSelect.Size = new Size(171, 24);
            btSelect.TabIndex = 3;
            btSelect.Text = "Select";
            btSelect.UseVisualStyleBackColor = true;
            btSelect.Click += btSelect_Click;
            // 
            // gbProgram
            // 
            gbProgram.Controls.Add(btInfo);
            gbProgram.Controls.Add(btVerify);
            gbProgram.Controls.Add(btErase);
            gbProgram.Controls.Add(btProgram);
            gbProgram.Controls.Add(btAnalyzeFile);
            gbProgram.Location = new Point(12, 119);
            gbProgram.Name = "gbProgram";
            gbProgram.Size = new Size(1105, 55);
            gbProgram.TabIndex = 7;
            gbProgram.TabStop = false;
            gbProgram.Text = "Programmer";
            // 
            // btInfo
            // 
            btInfo.Location = new Point(537, 20);
            btInfo.Name = "btInfo";
            btInfo.Size = new Size(171, 24);
            btInfo.TabIndex = 5;
            btInfo.Text = "PSoC6 Info";
            btInfo.UseVisualStyleBackColor = true;
            btInfo.Click += btInfo_Click;
            // 
            // btVerify
            // 
            btVerify.Location = new Point(360, 20);
            btVerify.Name = "btVerify";
            btVerify.Size = new Size(171, 24);
            btVerify.TabIndex = 4;
            btVerify.Text = "Verify";
            btVerify.UseVisualStyleBackColor = true;
            btVerify.Click += btVerify_Click;
            // 
            // btErase
            // 
            btErase.Location = new Point(183, 20);
            btErase.Name = "btErase";
            btErase.Size = new Size(171, 24);
            btErase.TabIndex = 3;
            btErase.Text = "Erase";
            btErase.UseVisualStyleBackColor = true;
            btErase.Click += btErase_Click;
            // 
            // pbMain
            // 
            pbMain.Location = new Point(12, 677);
            pbMain.Name = "pbMain";
            pbMain.Size = new Size(1105, 17);
            pbMain.TabIndex = 5;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1128, 702);
            Controls.Add(pbMain);
            Controls.Add(gbProgram);
            Controls.Add(groupBox2);
            Controls.Add(groupBox1);
            Controls.Add(tbStatus);
            Font = new Font("Consolas", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Name = "Form1";
            Text = "PSoC6 CMSIS-DAP Programmer V 1.0 (c) Rolf Nooteboom";
            groupBox1.ResumeLayout(false);
            groupBox2.ResumeLayout(false);
            groupBox2.PerformLayout();
            gbProgram.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button btProgram;
        public TextBox tbStatus;
        private Button btAnalyzeFile;
        private OpenFileDialog ofd;
        private Button btScanUSB;
        private ComboBox cbProgs;
        private GroupBox groupBox1;
        private GroupBox groupBox2;
        private TextBox tbFirmware;
        private Button btSelect;
        private GroupBox gbProgram;
        private Button btErase;
        private Button btVerify;
        private ProgressBar pbMain;
        private Button btInfo;
    }
}
