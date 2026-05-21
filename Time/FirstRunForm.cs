using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Time
{
    /// <summary>首次运行弹窗：询问是否创建开机自启动</summary>
    public class FirstRunForm : Form
    {
        // ── Win32：给按钮加 UAC 盾牌图标 ────────────────────────
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        private const int BCM_SETSHIELD = 0x0000160C;

        public FirstRunForm()
        {
            this.Text           = "首次运行";
            this.Size            = new Size(520, 280);
            this.StartPosition   = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox     = false;
            this.MinimizeBox     = false;

            // ── 说明文字 ─────────────────────────────────────────
            Label lblInfo = new Label();
            lblInfo.Text     = "欢迎使用悬浮时钟！\n\n" +
                               "是否创建开机自启动？\n" +
                               "（写入注册表需要管理员权限）";
            lblInfo.Location = new Point(20, 20);
            lblInfo.Size     = new Size(470, 90);
            lblInfo.Font      = new Font(this.Font.FontFamily, 10f);
            lblInfo.TextAlign = ContentAlignment.MiddleCenter;

            // ── "是"按钮（带 UAC 盾牌图标）───────────────────────
            Button btnYes = new Button();
            btnYes.Text       = " 是(&Y)";
            btnYes.Location   = new Point(140, 130);
            btnYes.Size       = new Size(110, 32);
            btnYes.Font       = new Font(this.Font.FontFamily, 10f);
            btnYes.FlatStyle  = FlatStyle.System;
            btnYes.UseVisualStyleBackColor = true;
            btnYes.Click     += (s, e) =>
            {
                // 以管理员身份重启，并传参 /setautostart
                RestartAsAdmin("/setautostart");
                this.DialogResult = DialogResult.OK;
                this.Close();
            };

            // ── "否"按钮 ─────────────────────────────────────────
            Button btnNo = new Button();
            btnNo.Text     = "否(&N)";
            btnNo.Location = new Point(280, 130);
            btnNo.Size     = new Size(90, 32);
            btnNo.Font     = new Font(this.Font.FontFamily, 10f);
            btnNo.Click   += (s, e) =>
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            };

            // ── 配置路径提示 ─────────────────────────────────────
            Label lblPath = new Label();
            lblPath.Text     = "配置文件位置：\n" + AppSettings.Instance.ConfigPath;
            lblPath.Location = new Point(20, 180);
            lblPath.Size     = new Size(470, 60);
            lblPath.ForeColor = Color.Gray;
            lblPath.Font      = new Font(this.Font.FontFamily, 9f);

            this.Controls.Add(lblInfo);
            this.Controls.Add(btnYes);
            this.Controls.Add(btnNo);
            this.Controls.Add(lblPath);

            // 窗口 Handle 创建后，给"是"按钮加 UAC 盾牌
            this.HandleCreated += (s, e) =>
            {
                SendMessage(btnYes.Handle, BCM_SETSHIELD, IntPtr.Zero, (IntPtr)1);
            };
        }

        /// <summary>以管理员权限重启自身，并携带指定参数</summary>
        private static void RestartAsAdmin(string arguments)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName  = Application.ExecutablePath;
                psi.Arguments = arguments;
                psi.Verb      = "runas";  // UAC 提权
                psi.UseShellExecute = true;
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show("无法提升权限：" + ex.Message, "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
