using System;
using System.Drawing;
using System.Windows.Forms;

namespace Time
{
    /// <summary>
    /// 退出确认弹窗：显示在屏幕右下角，3 秒后默认"否"自动关闭。
    /// DialogResult.Yes = 确认退出，否则继续运行。
    /// </summary>
    public class ExitConfirmForm : Form
    {
        private Label  _label;
        private Button _btnYes;
        private Button _btnNo;
        private Timer  _countdown;
        private int    _seconds = 3;

        public ExitConfirmForm()
        {
            // ── 窗体外观 ──────────────────────────────────────────────
            this.FormBorderStyle = FormBorderStyle.FixedToolWindow;
            this.StartPosition   = FormStartPosition.Manual;
            this.TopMost         = true;
            this.ShowInTaskbar   = false;
            this.ClientSize      = new Size(240, 100);
            this.BackColor       = Color.FromArgb(30, 30, 30);
            this.ForeColor       = Color.White;
            this.Text            = "悬浮时钟";

            // ── 问题标签 ──────────────────────────────────────────────
            _label            = new Label();
            _label.AutoSize   = false;
            _label.TextAlign  = ContentAlignment.MiddleCenter;
            _label.ForeColor  = Color.White;
            _label.Font       = new Font("微软雅黑", 10f);
            _label.Bounds     = new Rectangle(8, 8, 224, 40);
            _label.Text       = "是否关闭此程序？";
            this.Controls.Add(_label);

            // ── 倒计时标签 ────────────────────────────────────────────
            Label _hint        = new Label();
            _hint.AutoSize     = false;
            _hint.TextAlign    = ContentAlignment.MiddleCenter;
            _hint.ForeColor    = Color.Gray;
            _hint.Font         = new Font("微软雅黑", 8f);
            _hint.Bounds       = new Rectangle(8, 48, 224, 16);
            _hint.Text         = "（3 秒后默认关闭）";
            this.Controls.Add(_hint);

            // ── 按钮：是 ──────────────────────────────────────────────
            _btnYes             = new Button();
            _btnYes.Text        = "是";
            _btnYes.FlatStyle   = FlatStyle.Flat;
            _btnYes.BackColor   = Color.FromArgb(180, 40, 40);
            _btnYes.ForeColor   = Color.White;
            _btnYes.Font        = new Font("微软雅黑", 9f);
            _btnYes.Bounds      = new Rectangle(20, 66, 80, 26);
            _btnYes.Click      += (s, e) =>
            {
                _countdown.Stop();
                this.DialogResult = DialogResult.Yes;
                this.Close();
            };
            this.Controls.Add(_btnYes);

            // ── 按钮：否 ──────────────────────────────────────────────
            _btnNo              = new Button();
            _btnNo.Text         = "否";
            _btnNo.FlatStyle    = FlatStyle.Flat;
            _btnNo.BackColor    = Color.FromArgb(60, 60, 60);
            _btnNo.ForeColor    = Color.White;
            _btnNo.Font         = new Font("微软雅黑", 9f);
            _btnNo.Bounds       = new Rectangle(140, 66, 80, 26);
            _btnNo.Click       += (s, e) =>
            {
                _countdown.Stop();
                this.DialogResult = DialogResult.No;
                this.Close();
            };
            this.Controls.Add(_btnNo);

            // 保存 hint 引用以便倒计时更新
            _hintLabel = _hint;

            // ── 倒计时计时器 ──────────────────────────────────────────
            _countdown          = new Timer();
            _countdown.Interval = 1000;
            _countdown.Tick    += Countdown_Tick;
        }

        private Label _hintLabel;

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // 定位到屏幕右下角
            Screen scr     = Screen.PrimaryScreen;
            this.Location  = new Point(
                scr.WorkingArea.Right  - this.Width  - 16,
                scr.WorkingArea.Bottom - this.Height - 16);

            _countdown.Start();
            UpdateHint();
        }

        private void UpdateHint()
        {
            _hintLabel.Text = string.Format("（{0} 秒后自动关闭，默认\"否\"）", _seconds);
        }

        private void Countdown_Tick(object sender, EventArgs e)
        {
            _seconds--;
            UpdateHint();
            if (_seconds <= 0)
            {
                _countdown.Stop();
                this.DialogResult = DialogResult.No;
                this.Close();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _countdown != null)
                _countdown.Dispose();
            base.Dispose(disposing);
        }
    }
}
