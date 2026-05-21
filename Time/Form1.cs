using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Time
{
    public partial class Form1 : Form
    {
        // ── Win32 API ─────────────────────────────────────────────
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst,
            ref Point pptDst, ref Size psize, IntPtr hdcSrc, ref Point pptSrc,
            uint crKey, ref BLENDFUNCTION pblend, uint dwFlags);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        private struct BLENDFUNCTION
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        private const byte AC_SRC_OVER  = 0;
        private const byte AC_SRC_ALPHA = 1;
        private const uint ULW_ALPHA    = 2;

        // ── 控件 ───────────────────────────────────────────────────
        private Timer      _timer;
        private Timer      _fastTimer;
        private NotifyIcon _notifyIcon;

        // ── 点击计数 & 短暂显示 ────────────────────────────────────
        private int   _clickCount = 0;
        private Timer _revealTimer;
        private int   _savedTransparency = -1;  // 临时保存的透明度

        public Form1()
        {
            InitializeComponent();
        }

        // 窗口创建时加上 WS_EX_LAYERED（UpdateLayeredWindow 必须）
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x00080000; // WS_EX_LAYERED
                return cp;
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // 禁用 WinForms 自动绘制（完全由 UpdateLayeredWindow 接管）
            this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);

            // ── 托盘图标 ────────────────────────────────────────────
            _notifyIcon         = new NotifyIcon();
            _notifyIcon.Icon    = CreateTrayIcon();
            _notifyIcon.Text    = "悬浮时钟";
            _notifyIcon.Visible = true;

            ContextMenu menu = new ContextMenu();
            menu.MenuItems.Add("设置(&S)…", new EventHandler(Menu_Settings));
            menu.MenuItems.Add("-");
            menu.MenuItems.Add("退出(&X)",  new EventHandler(Menu_Exit));
            _notifyIcon.ContextMenu = menu;
            _notifyIcon.Click      += new EventHandler(NotifyIcon_Click);

            // ── 短暂显示计时器（1 秒后恢复）────────────────────────
            _revealTimer          = new Timer();
            _revealTimer.Interval = 1000;
            _revealTimer.Tick    += RevealTimer_Tick;

            // ── 秒表计时器 ──────────────────────────────────────────
            _timer          = new Timer();
            _timer.Interval = 1000;
            _timer.Tick    += (s, ev) => RefreshDisplay();
            _timer.Start();

            // ── 快速刷新（0.1s）─────────────────────────────────────
            _fastTimer          = new Timer();
            _fastTimer.Interval = 100;
            _fastTimer.Tick    += (s, ev) => RefreshDisplay();

            // ── 窗口移动时刷新位置 ──────────────────────────────────
            this.LocationChanged += (s, ev) => RefreshDisplay();

            UpdateWindowSize();
            UpdateFastTimer();
            RefreshDisplay();
        }

        // ── 根据透明度决定是否启用 0.1s 快速刷新 ──────────────────
        private void UpdateFastTimer()
        {
            _fastTimer.Enabled = AppSettings.Instance.Transparency > 0;
        }

        // ── 托盘图标（GDI+ 绘制小钟表）────────────────────────────
        private static Icon CreateTrayIcon()
        {
            Bitmap bmp = new Bitmap(16, 16);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                g.FillEllipse(Brushes.White, 1, 1, 13, 13);
                g.DrawEllipse(new Pen(Color.Gray, 1f), 1, 1, 13, 13);
                g.DrawLine(new Pen(Color.Black, 1.5f), 7, 7, 7, 3);
                g.DrawLine(new Pen(Color.Black, 1.5f), 7, 7, 11, 7);
            }
            return Icon.FromHandle(bmp.GetHicon());
        }

        // ── 调整窗口大小，固定左上角 ─────────────────────────────
        private void UpdateWindowSize()
        {
            float sz     = AppSettings.Instance.FontSize;
            float dateSz = Math.Max(8f, sz * 0.4f);

            int w = (int)(sz * 7.5f) + 30;
            int h = (int)(sz * 1.6f + 8f + dateSz * 1.8f) + 30;
            this.ClientSize = new Size(Math.Max(w, 200), Math.Max(h, 80));

            this.Location = new Point(16, 16);
        }

        // ── 预乘 alpha（UpdateLayeredWindow 的 AC_SRC_ALPHA 要求 RGB 预乘）
        private void PremultiplyAlpha(Bitmap bmp)
        {
            Rectangle rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            BitmapData data = bmp.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);

            int stride = Math.Abs(data.Stride);
            int bytes  = stride * bmp.Height;
            byte[] pixels = new byte[bytes];
            Marshal.Copy(data.Scan0, pixels, 0, bytes);

            for (int y = 0; y < bmp.Height; y++)
            {
                for (int x = 0; x < bmp.Width; x++)
                {
                    int offset = y * stride + x * 4;
                    byte a = pixels[offset + 3];
                    if (a == 0 || a == 255) continue;

                    float f = a / 255f;
                    pixels[offset + 0] = (byte)(pixels[offset + 0] * f); // B
                    pixels[offset + 1] = (byte)(pixels[offset + 1] * f); // G
                    pixels[offset + 2] = (byte)(pixels[offset + 2] * f); // R
                }
            }

            Marshal.Copy(pixels, 0, data.Scan0, bytes);
            bmp.UnlockBits(data);
        }

        // ── 刷新显示：生成 Bitmap → 预乘 alpha → UpdateLayeredWindow ─
        private void RefreshDisplay()
        {
            if (!IsHandleCreated) return;

            AppSettings s = AppSettings.Instance;

            string timeText = DateTime.Now.ToString("HH:mm:ss");
            string dateText = DateTime.Now.ToString("yyyy/MM/dd");
            float dateSz    = Math.Max(8f, s.FontSize * 0.4f);

            // 透明度：0=不透明，100=完全透明
            int transparency = (_savedTransparency >= 0) ? _savedTransparency : s.Transparency;
            int alpha = (int)Math.Round((100 - transparency) / 100.0 * 255);
            alpha = Math.Max(0, Math.Min(255, alpha));

            using (Bitmap bmp = new Bitmap(this.Width, this.Height, PixelFormat.Format32bppArgb))
            using (Font timFont  = new Font("Segoe UI", s.FontSize, FontStyle.Bold))
            using (Font dateFont = new Font("Segoe UI", dateSz,     FontStyle.Regular))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Transparent);
                    g.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

                    // ★ 测量时间文字高度，日期与时间相隔 2px
                    SizeF timeSize = g.MeasureString(timeText, timFont);
                    float timeY = 2f;
                    float dateY = timeY + timeSize.Height + 2f;

                    Color textColor = Color.FromArgb(alpha, s.TextColor);
                    using (SolidBrush brush = new SolidBrush(textColor))
                    {
                        g.DrawString(timeText, timFont,  brush, 2f, timeY);
                        g.DrawString(dateText, dateFont, brush, 2f, dateY);
                    }
                }

                // 预乘 alpha 通道
                PremultiplyAlpha(bmp);

                // UpdateLayeredWindow 将 Bitmap 贴到窗口
                IntPtr screenDC = GetDC(IntPtr.Zero);
                IntPtr memDC    = CreateCompatibleDC(screenDC);
                IntPtr hBitmap  = IntPtr.Zero;
                IntPtr oldBitmap = IntPtr.Zero;

                try
                {
                    hBitmap = bmp.GetHbitmap(Color.FromArgb(0));
                    oldBitmap = SelectObject(memDC, hBitmap);

                    Point ptSrc = new Point(0, 0);
                    Point ptDst = new Point(this.Left, this.Top);
                    Size  size  = this.Size;

                    BLENDFUNCTION blend = new BLENDFUNCTION();
                    blend.BlendOp            = AC_SRC_OVER;
                    blend.BlendFlags         = 0;
                    blend.SourceConstantAlpha = 255;
                    blend.AlphaFormat        = AC_SRC_ALPHA;

                    UpdateLayeredWindow(this.Handle, screenDC, ref ptDst, ref size,
                        memDC, ref ptSrc, 0, ref blend, ULW_ALPHA);
                }
                finally
                {
                    if (hBitmap != IntPtr.Zero)
                    {
                        SelectObject(memDC, oldBitmap);
                        DeleteObject(hBitmap);
                    }
                    DeleteDC(memDC);
                    ReleaseDC(IntPtr.Zero, screenDC);
                }
            }
        }

        // ── 禁用 WinForms 默认绘制（Layered Window 不接收 WM_PAINT）─
        protected override void OnPaint(PaintEventArgs e)
        {
            // 所有渲染由 UpdateLayeredWindow 处理
        }

        // ── WndProc：Layered Window 的文字区域接收点击消息 ───────
        protected override void WndProc(ref Message m)
        {
            const int WM_LBUTTONDOWN = 0x0201;

            if (m.Msg == WM_LBUTTONDOWN)
            {
                _clickCount++;
                if (_clickCount >= 5)
                {
                    _clickCount = 0;
                    ShowTemporarily();
                }
            }

            base.WndProc(ref m);
        }

        // ── 短暂显示 1 秒（文字临时变不透明）──────────────────────
        private void ShowTemporarily()
        {
            _savedTransparency = AppSettings.Instance.Transparency;
            AppSettings.Instance.Transparency = 0; // 临时不透明
            RefreshDisplay();

            _revealTimer.Stop();
            _revealTimer.Start();
        }

        // ── 1 秒后恢复透明度，弹出退出确认 ────────────────────────
        private void RevealTimer_Tick(object sender, EventArgs e)
        {
            _revealTimer.Stop();

            if (_savedTransparency >= 0)
                AppSettings.Instance.Transparency = _savedTransparency;
            _savedTransparency = -1;
            RefreshDisplay();

            using (ExitConfirmForm dlg = new ExitConfirmForm())
            {
                if (dlg.ShowDialog() == DialogResult.Yes)
                {
                    _notifyIcon.Visible = false;
                    Application.Exit();
                }
            }
        }

        // ── 托盘单击 ──────────────────────────────────────────────
        private void NotifyIcon_Click(object sender, EventArgs e)
        {
            MouseEventArgs me = e as MouseEventArgs;
            if (me != null && me.Button == MouseButtons.Left)
                OpenSettings();
        }

        // ── 菜单-设置 ─────────────────────────────────────────────
        private void Menu_Settings(object sender, EventArgs e)
        {
            OpenSettings();
        }

        // ── 菜单-退出 ─────────────────────────────────────────────
        private void Menu_Exit(object sender, EventArgs e)
        {
            _notifyIcon.Visible = false;
            Application.Exit();
        }

        // ── 打开设置窗口 ──────────────────────────────────────────
        private void OpenSettings()
        {
            using (SettingsForm frm = new SettingsForm())
            {
                if (frm.ShowDialog() == DialogResult.OK)
                {
                    UpdateWindowSize();
                    UpdateFastTimer();
                    RefreshDisplay();
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_timer != null)       { _timer.Dispose();       _timer = null; }
                if (_fastTimer != null)   { _fastTimer.Dispose();   _fastTimer = null; }
                if (_revealTimer != null) { _revealTimer.Dispose(); _revealTimer = null; }
                if (_notifyIcon != null)  { _notifyIcon.Dispose();  _notifyIcon = null; }
                if (components != null)   { components.Dispose(); }
            }
            base.Dispose(disposing);
        }
    }
}
