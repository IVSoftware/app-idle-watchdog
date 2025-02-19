namespace AppIdleWatchdog
{
    partial class MainForm
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
            buttonMsg = new Button();
            SuspendLayout();
            // 
            // buttonMsg
            // 
            buttonMsg.AutoSize = true;
            buttonMsg.BackColor = Color.Green;
            buttonMsg.ForeColor = Color.White;
            buttonMsg.Location = new Point(90, 105);
            buttonMsg.Name = "buttonMsg";
            buttonMsg.Padding = new Padding(5);
            buttonMsg.Size = new Size(279, 53);
            buttonMsg.TabIndex = 0;
            buttonMsg.Text = "Show Message Box";
            buttonMsg.UseVisualStyleBackColor = false;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(13F, 32F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(478, 244);
            Controls.Add(buttonMsg);
            Font = new Font("Segoe UI", 12F);
            Margin = new Padding(4, 4, 4, 4);
            Name = "MainForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Main Form";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button buttonMsg;
    }
}
