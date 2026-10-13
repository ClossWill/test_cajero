
namespace CRM9250N_WinFormDemo
{
    partial class Form1
    {
        /// <summary>
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows 窗体设计器生成的代码

        /// <summary>
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.iPowerOnInitEx = new System.Windows.Forms.Button();
            this.iSetCommPara = new System.Windows.Forms.Button();
            this.hOpenLogicDevice = new System.Windows.Forms.Button();
            this.iOpenShutter = new System.Windows.Forms.Button();
            this.groupBox4 = new System.Windows.Forms.GroupBox();
            this.iDispenseEx = new System.Windows.Forms.Button();
            this.iPrepareDispense = new System.Windows.Forms.Button();
            this.groupBox5 = new System.Windows.Forms.GroupBox();
            this.iStoreMoneyEx = new System.Windows.Forms.Button();
            this.iCloseShutter2 = new System.Windows.Forms.Button();
            this.iCashCount = new System.Windows.Forms.Button();
            this.iCancelDeposit = new System.Windows.Forms.Button();
            this.iPrepareDeposit = new System.Windows.Forms.Button();
            this.iPrepareNextTransaction2 = new System.Windows.Forms.Button();
            this.clear = new System.Windows.Forms.Button();
            this.text1 = new System.Windows.Forms.TextBox();
            this.RetractNE = new System.Windows.Forms.Button();
            this.RetractNF = new System.Windows.Forms.Button();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.iGetDevVersionEx = new System.Windows.Forms.Button();
            this.iFirmwareDownload = new System.Windows.Forms.Button();
            this.iGetNvNotesInfoEx = new System.Windows.Forms.Button();
            this.groupBox1.SuspendLayout();
            this.groupBox4.SuspendLayout();
            this.groupBox5.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.iPowerOnInitEx);
            this.groupBox1.Controls.Add(this.iSetCommPara);
            this.groupBox1.Controls.Add(this.hOpenLogicDevice);
            this.groupBox1.Location = new System.Drawing.Point(11, 12);
            this.groupBox1.Margin = new System.Windows.Forms.Padding(2);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Padding = new System.Windows.Forms.Padding(2);
            this.groupBox1.Size = new System.Drawing.Size(141, 120);
            this.groupBox1.TabIndex = 0;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Power On Init";
            // 
            // iPowerOnInitEx
            // 
            this.iPowerOnInitEx.Location = new System.Drawing.Point(11, 88);
            this.iPowerOnInitEx.Margin = new System.Windows.Forms.Padding(2);
            this.iPowerOnInitEx.Name = "iPowerOnInitEx";
            this.iPowerOnInitEx.Size = new System.Drawing.Size(105, 25);
            this.iPowerOnInitEx.TabIndex = 9;
            this.iPowerOnInitEx.Text = "iPowerOnInitEx";
            this.iPowerOnInitEx.UseVisualStyleBackColor = true;
            this.iPowerOnInitEx.Click += new System.EventHandler(this.iPowerOnInitEx_Click);
            // 
            // iSetCommPara
            // 
            this.iSetCommPara.Location = new System.Drawing.Point(11, 51);
            this.iSetCommPara.Margin = new System.Windows.Forms.Padding(2);
            this.iSetCommPara.Name = "iSetCommPara";
            this.iSetCommPara.Size = new System.Drawing.Size(105, 25);
            this.iSetCommPara.TabIndex = 0;
            this.iSetCommPara.Text = "iSetCommPara";
            this.iSetCommPara.UseVisualStyleBackColor = true;
            this.iSetCommPara.Click += new System.EventHandler(this.iSetCommPara_Click);
            // 
            // hOpenLogicDevice
            // 
            this.hOpenLogicDevice.ForeColor = System.Drawing.SystemColors.HotTrack;
            this.hOpenLogicDevice.Location = new System.Drawing.Point(11, 17);
            this.hOpenLogicDevice.Margin = new System.Windows.Forms.Padding(2);
            this.hOpenLogicDevice.Name = "hOpenLogicDevice";
            this.hOpenLogicDevice.Size = new System.Drawing.Size(105, 25);
            this.hOpenLogicDevice.TabIndex = 0;
            this.hOpenLogicDevice.Text = "hOpenLogicDevice";
            this.hOpenLogicDevice.UseVisualStyleBackColor = true;
            this.hOpenLogicDevice.Click += new System.EventHandler(this.hOpenLogicDevice_Click);
            // 
            // iOpenShutter
            // 
            this.iOpenShutter.Location = new System.Drawing.Point(11, 51);
            this.iOpenShutter.Margin = new System.Windows.Forms.Padding(2);
            this.iOpenShutter.Name = "iOpenShutter";
            this.iOpenShutter.Size = new System.Drawing.Size(105, 25);
            this.iOpenShutter.TabIndex = 0;
            this.iOpenShutter.Text = "iOpenShutter";
            this.iOpenShutter.UseVisualStyleBackColor = true;
            this.iOpenShutter.Click += new System.EventHandler(this.iOpenShutter_Click);
            // 
            // groupBox4
            // 
            this.groupBox4.Controls.Add(this.iDispenseEx);
            this.groupBox4.Controls.Add(this.iPrepareDispense);
            this.groupBox4.Location = new System.Drawing.Point(318, 12);
            this.groupBox4.Margin = new System.Windows.Forms.Padding(2);
            this.groupBox4.Name = "groupBox4";
            this.groupBox4.Padding = new System.Windows.Forms.Padding(2);
            this.groupBox4.Size = new System.Drawing.Size(132, 84);
            this.groupBox4.TabIndex = 2;
            this.groupBox4.TabStop = false;
            this.groupBox4.Text = "Dispense";
            // 
            // iDispenseEx
            // 
            this.iDispenseEx.Location = new System.Drawing.Point(14, 51);
            this.iDispenseEx.Margin = new System.Windows.Forms.Padding(2);
            this.iDispenseEx.Name = "iDispenseEx";
            this.iDispenseEx.Size = new System.Drawing.Size(105, 25);
            this.iDispenseEx.TabIndex = 9;
            this.iDispenseEx.Text = "iDispenseEx";
            this.iDispenseEx.UseVisualStyleBackColor = true;
            this.iDispenseEx.Click += new System.EventHandler(this.iDispenseEx_Click);
            // 
            // iPrepareDispense
            // 
            this.iPrepareDispense.Location = new System.Drawing.Point(14, 14);
            this.iPrepareDispense.Margin = new System.Windows.Forms.Padding(2);
            this.iPrepareDispense.Name = "iPrepareDispense";
            this.iPrepareDispense.Size = new System.Drawing.Size(114, 25);
            this.iPrepareDispense.TabIndex = 0;
            this.iPrepareDispense.Text = "iPrepareDispense";
            this.iPrepareDispense.UseVisualStyleBackColor = true;
            this.iPrepareDispense.Click += new System.EventHandler(this.iPrepareDispense_Click);
            // 
            // groupBox5
            // 
            this.groupBox5.Controls.Add(this.iGetNvNotesInfoEx);
            this.groupBox5.Controls.Add(this.iStoreMoneyEx);
            this.groupBox5.Controls.Add(this.iCloseShutter2);
            this.groupBox5.Controls.Add(this.iCashCount);
            this.groupBox5.Controls.Add(this.iCancelDeposit);
            this.groupBox5.Controls.Add(this.iPrepareDeposit);
            this.groupBox5.Controls.Add(this.iOpenShutter);
            this.groupBox5.Location = new System.Drawing.Point(172, 4);
            this.groupBox5.Margin = new System.Windows.Forms.Padding(2);
            this.groupBox5.Name = "groupBox5";
            this.groupBox5.Padding = new System.Windows.Forms.Padding(2);
            this.groupBox5.Size = new System.Drawing.Size(130, 257);
            this.groupBox5.TabIndex = 3;
            this.groupBox5.TabStop = false;
            this.groupBox5.Text = "Deposit";
            // 
            // iStoreMoneyEx
            // 
            this.iStoreMoneyEx.Location = new System.Drawing.Point(11, 224);
            this.iStoreMoneyEx.Margin = new System.Windows.Forms.Padding(2);
            this.iStoreMoneyEx.Name = "iStoreMoneyEx";
            this.iStoreMoneyEx.Size = new System.Drawing.Size(105, 25);
            this.iStoreMoneyEx.TabIndex = 8;
            this.iStoreMoneyEx.Text = "iStoreMoneyEx";
            this.iStoreMoneyEx.UseVisualStyleBackColor = true;
            this.iStoreMoneyEx.Click += new System.EventHandler(this.StoreMoneyEx_Click);
            // 
            // iCloseShutter2
            // 
            this.iCloseShutter2.Location = new System.Drawing.Point(11, 86);
            this.iCloseShutter2.Margin = new System.Windows.Forms.Padding(2);
            this.iCloseShutter2.Name = "iCloseShutter2";
            this.iCloseShutter2.Size = new System.Drawing.Size(105, 25);
            this.iCloseShutter2.TabIndex = 0;
            this.iCloseShutter2.Text = "iCloseShutter";
            this.iCloseShutter2.UseVisualStyleBackColor = true;
            this.iCloseShutter2.Click += new System.EventHandler(this.iCloseShutter_Click);
            // 
            // iCashCount
            // 
            this.iCashCount.Location = new System.Drawing.Point(11, 121);
            this.iCashCount.Margin = new System.Windows.Forms.Padding(2);
            this.iCashCount.Name = "iCashCount";
            this.iCashCount.Size = new System.Drawing.Size(105, 25);
            this.iCashCount.TabIndex = 0;
            this.iCashCount.Text = "iCashCount";
            this.iCashCount.UseVisualStyleBackColor = true;
            this.iCashCount.Click += new System.EventHandler(this.iCashCount_Click);
            // 
            // iCancelDeposit
            // 
            this.iCancelDeposit.Location = new System.Drawing.Point(11, 189);
            this.iCancelDeposit.Margin = new System.Windows.Forms.Padding(2);
            this.iCancelDeposit.Name = "iCancelDeposit";
            this.iCancelDeposit.Size = new System.Drawing.Size(115, 25);
            this.iCancelDeposit.TabIndex = 0;
            this.iCancelDeposit.Text = "iCancelDepositEx";
            this.iCancelDeposit.UseVisualStyleBackColor = true;
            this.iCancelDeposit.Click += new System.EventHandler(this.iCancelDeposit_Click);
            // 
            // iPrepareDeposit
            // 
            this.iPrepareDeposit.Location = new System.Drawing.Point(11, 16);
            this.iPrepareDeposit.Margin = new System.Windows.Forms.Padding(2);
            this.iPrepareDeposit.Name = "iPrepareDeposit";
            this.iPrepareDeposit.Size = new System.Drawing.Size(105, 25);
            this.iPrepareDeposit.TabIndex = 0;
            this.iPrepareDeposit.Text = "iPrepareDeposit";
            this.iPrepareDeposit.UseVisualStyleBackColor = true;
            this.iPrepareDeposit.Click += new System.EventHandler(this.iPrepareDeposit_Click);
            // 
            // iPrepareNextTransaction2
            // 
            this.iPrepareNextTransaction2.Location = new System.Drawing.Point(22, 220);
            this.iPrepareNextTransaction2.Margin = new System.Windows.Forms.Padding(2);
            this.iPrepareNextTransaction2.Name = "iPrepareNextTransaction2";
            this.iPrepareNextTransaction2.Size = new System.Drawing.Size(115, 25);
            this.iPrepareNextTransaction2.TabIndex = 0;
            this.iPrepareNextTransaction2.Text = "iPrepareNextTran";
            this.iPrepareNextTransaction2.UseVisualStyleBackColor = true;
            this.iPrepareNextTransaction2.Click += new System.EventHandler(this.iPrepareNextTransaction_Click);
            // 
            // clear
            // 
            this.clear.Location = new System.Drawing.Point(355, 236);
            this.clear.Margin = new System.Windows.Forms.Padding(2);
            this.clear.Name = "clear";
            this.clear.Size = new System.Drawing.Size(105, 25);
            this.clear.TabIndex = 0;
            this.clear.Text = "ClearText";
            this.clear.UseVisualStyleBackColor = true;
            this.clear.Click += new System.EventHandler(this.clear_Click);
            // 
            // text1
            // 
            this.text1.Location = new System.Drawing.Point(11, 265);
            this.text1.Margin = new System.Windows.Forms.Padding(2);
            this.text1.Multiline = true;
            this.text1.Name = "text1";
            this.text1.Size = new System.Drawing.Size(458, 181);
            this.text1.TabIndex = 5;
            // 
            // RetractNE
            // 
            this.RetractNE.Location = new System.Drawing.Point(6, 48);
            this.RetractNE.Name = "RetractNE";
            this.RetractNE.Size = new System.Drawing.Size(75, 23);
            this.RetractNE.TabIndex = 6;
            this.RetractNE.Text = "RetractNE";
            this.RetractNE.UseVisualStyleBackColor = true;
            this.RetractNE.Click += new System.EventHandler(this.RetractNE_Click);
            // 
            // RetractNF
            // 
            this.RetractNF.Location = new System.Drawing.Point(6, 20);
            this.RetractNF.Name = "RetractNF";
            this.RetractNF.Size = new System.Drawing.Size(75, 23);
            this.RetractNF.TabIndex = 6;
            this.RetractNF.Text = "RetractNF";
            this.RetractNF.UseVisualStyleBackColor = true;
            this.RetractNF.Click += new System.EventHandler(this.RetractNF_Click);
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.RetractNF);
            this.groupBox2.Controls.Add(this.RetractNE);
            this.groupBox2.Location = new System.Drawing.Point(332, 112);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(94, 79);
            this.groupBox2.TabIndex = 7;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Retract";
            // 
            // iGetDevVersionEx
            // 
            this.iGetDevVersionEx.Location = new System.Drawing.Point(22, 148);
            this.iGetDevVersionEx.Name = "iGetDevVersionEx";
            this.iGetDevVersionEx.Size = new System.Drawing.Size(116, 23);
            this.iGetDevVersionEx.TabIndex = 8;
            this.iGetDevVersionEx.Text = "iGetDevVersionEx";
            this.iGetDevVersionEx.UseVisualStyleBackColor = true;
            this.iGetDevVersionEx.Click += new System.EventHandler(this.iGetDevVersionEx_Click);
            // 
            // iFirmwareDownload
            // 
            this.iFirmwareDownload.Location = new System.Drawing.Point(22, 182);
            this.iFirmwareDownload.Name = "iFirmwareDownload";
            this.iFirmwareDownload.Size = new System.Drawing.Size(116, 23);
            this.iFirmwareDownload.TabIndex = 9;
            this.iFirmwareDownload.Text = "iFirmwareDownload";
            this.iFirmwareDownload.UseVisualStyleBackColor = true;
            this.iFirmwareDownload.Click += new System.EventHandler(this.iFirmwareDownload_Click);
            // 
            // iGetNvNotesInfoEx
            // 
            this.iGetNvNotesInfoEx.Location = new System.Drawing.Point(5, 156);
            this.iGetNvNotesInfoEx.Name = "iGetNvNotesInfoEx";
            this.iGetNvNotesInfoEx.Size = new System.Drawing.Size(120, 23);
            this.iGetNvNotesInfoEx.TabIndex = 10;
            this.iGetNvNotesInfoEx.Text = "iGetNvNotesInfoEx";
            this.iGetNvNotesInfoEx.UseVisualStyleBackColor = true;
            this.iGetNvNotesInfoEx.Click += new System.EventHandler(this.button1_Click);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(471, 448);
            this.Controls.Add(this.iFirmwareDownload);
            this.Controls.Add(this.iPrepareNextTransaction2);
            this.Controls.Add(this.iGetDevVersionEx);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.text1);
            this.Controls.Add(this.clear);
            this.Controls.Add(this.groupBox5);
            this.Controls.Add(this.groupBox4);
            this.Controls.Add(this.groupBox1);
            this.Margin = new System.Windows.Forms.Padding(2);
            this.Name = "Form1";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "CRM9250N";
            this.groupBox1.ResumeLayout(false);
            this.groupBox4.ResumeLayout(false);
            this.groupBox5.ResumeLayout(false);
            this.groupBox2.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Button hOpenLogicDevice;
        private System.Windows.Forms.Button iSetCommPara;
        private System.Windows.Forms.Button iOpenShutter;
        private System.Windows.Forms.GroupBox groupBox4;
        private System.Windows.Forms.Button iPrepareDispense;
        private System.Windows.Forms.GroupBox groupBox5;
        private System.Windows.Forms.Button iCloseShutter2;
        private System.Windows.Forms.Button iCashCount;
        private System.Windows.Forms.Button iCancelDeposit;
        private System.Windows.Forms.Button iPrepareDeposit;
        private System.Windows.Forms.Button iPrepareNextTransaction2;
        private System.Windows.Forms.Button clear;
        private System.Windows.Forms.TextBox text1;
        private System.Windows.Forms.Button iPowerOnInitEx;
        private System.Windows.Forms.Button iDispenseEx;
        private System.Windows.Forms.Button RetractNE;
        private System.Windows.Forms.Button RetractNF;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.Button iStoreMoneyEx;
        private System.Windows.Forms.Button iGetDevVersionEx;
        private System.Windows.Forms.Button iFirmwareDownload;
        private System.Windows.Forms.Button iGetNvNotesInfoEx;
    }
}

