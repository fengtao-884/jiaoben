namespace 脚本
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
            button1 = new Button();
            numLevel = new NumericUpDown();
            label1 = new Label();
            btnStop = new Button();
            button2 = new Button();
            label4 = new Label();
            numRun = new NumericUpDown();
            button3 = new Button();
            button4 = new Button();
            ((System.ComponentModel.ISupportInitialize)numLevel).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numRun).BeginInit();
            SuspendLayout();
            // 
            // button1
            // 
            button1.Location = new Point(58, 41);
            button1.Name = "button1";
            button1.Size = new Size(72, 42);
            button1.TabIndex = 0;
            button1.Text = "刷资源";
            button1.UseVisualStyleBackColor = true;
            button1.Click += button1_Click;
            // 
            // numLevel
            // 
            numLevel.Location = new Point(66, 12);
            numLevel.Name = "numLevel";
            numLevel.Size = new Size(64, 23);
            numLevel.TabIndex = 1;
            numLevel.Value = new decimal(new int[] { 40, 0, 0, 0 });
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(16, 12);
            label1.Name = "label1";
            label1.Size = new Size(44, 17);
            label1.TabIndex = 2;
            label1.Text = "等级：";
            // 
            // btnStop
            // 
            btnStop.Location = new Point(150, 41);
            btnStop.Name = "btnStop";
            btnStop.Size = new Size(72, 42);
            btnStop.TabIndex = 5;
            btnStop.Text = "Stop";
            btnStop.UseVisualStyleBackColor = true;
            btnStop.Click += btnStop_Click;
            // 
            // button2
            // 
            button2.Location = new Point(58, 89);
            button2.Name = "button2";
            button2.Size = new Size(72, 42);
            button2.TabIndex = 6;
            button2.Text = "军备收集";
            button2.UseVisualStyleBackColor = true;
            button2.Click += button2_Click;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(136, 14);
            label4.Name = "label4";
            label4.Size = new Size(68, 17);
            label4.TabIndex = 7;
            label4.Text = "运行次数：";
            // 
            // numRun
            // 
            numRun.Location = new Point(200, 12);
            numRun.Maximum = new decimal(new int[] { 100000, 0, 0, 0 });
            numRun.Name = "numRun";
            numRun.Size = new Size(64, 23);
            numRun.TabIndex = 8;
            numRun.Value = new decimal(new int[] { 10, 0, 0, 0 });
            // 
            // button3
            // 
            button3.Location = new Point(58, 142);
            button3.Name = "button3";
            button3.Size = new Size(72, 42);
            button3.TabIndex = 9;
            button3.Text = "波兰守卫";
            button3.UseVisualStyleBackColor = true;
            button3.Click += button3_Click;
            // 
            // button4
            // 
            button4.Location = new Point(150, 142);
            button4.Name = "button4";
            button4.Size = new Size(72, 42);
            button4.TabIndex = 10;
            button4.Text = "复仇X";
            button4.UseVisualStyleBackColor = true;
            button4.Click += button4_Click;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 17F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(366, 196);
            Controls.Add(button4);
            Controls.Add(button3);
            Controls.Add(numRun);
            Controls.Add(label4);
            Controls.Add(button2);
            Controls.Add(btnStop);
            Controls.Add(label1);
            Controls.Add(numLevel);
            Controls.Add(button1);
            Name = "Form1";
            Text = "Form1";
            ((System.ComponentModel.ISupportInitialize)numLevel).EndInit();
            ((System.ComponentModel.ISupportInitialize)numRun).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button button1;
        private NumericUpDown numLevel;
        private Label label1;
        private Button btnStop;
        private Button button2;
        private Label label4;
        private NumericUpDown numRun;
        private Button button3;
        private Button button4;
    }
}
