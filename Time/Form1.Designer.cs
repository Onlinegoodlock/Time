using System.Drawing;
using System.Windows.Forms;

namespace Time
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        private void InitializeComponent()
        {
            this.SuspendLayout();

            this.AutoScaleMode   = AutoScaleMode.Font;
            this.ClientSize      = new Size(220, 66);
            this.FormBorderStyle = FormBorderStyle.None;
            this.TopMost         = true;
            this.ShowInTaskbar   = false;

            this.Text = "悬浮时钟";
            this.ResumeLayout(false);
        }
    }
}
