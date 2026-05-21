using System;
using System.Drawing;
using System.IO;

namespace Time
{
    /// <summary>悬浮时钟设置（JSON 格式），持久化到 AppData</summary>
    public class AppSettings
    {
        // ── 默认值 ────────────────────────────────────────────────
        public float FontSize      { get; set; } = 28f;
        public Color TextColor     { get; set; } = Color.White;
        /// <summary>透明度 0~100；值越大越透明，0=完全不透明，100=完全透明。默认 20。</summary>
        public int   Transparency  { get; set; } = 20;
        /// <summary>是否已创建开机自启动。默认 false。</summary>
        public bool  AutoStart     { get; set; } = false;
        /// <summary>是否首次运行。首次运行后设为 false。默认 true。</summary>
        public bool  FirstRun      { get; set; } = true;

        // ── 存储路径 ──────────────────────────────────────────────
        private static readonly string _dir  =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                         "TimeOverlay");
        private static readonly string _file = Path.Combine(_dir, "settings.json");

        public string ConfigPath { get { return _file; } }

        // ── 单例 ──────────────────────────────────────────────────
        private static AppSettings _instance;
        public static AppSettings Instance
        {
            get
            {
                if (_instance == null)
                    _instance = Load();
                return _instance;
            }
        }

        // ── 加载 ──────────────────────────────────────────────────
        private static AppSettings Load()
        {
            AppSettings s = new AppSettings();
            if (!File.Exists(_file))
                return s;

            try
            {
                foreach (string line in File.ReadAllLines(_file))
                {
                    int colon = line.IndexOf(':');
                    if (colon < 0) continue;
                    string key = line.Substring(0, colon).Trim().Trim('"', ' ', '\t');
                    string val = line.Substring(colon + 1).Trim().Trim(',', '"', ' ', '\t');

                    if (key == "FontSize")
                    {
                        float f;
                        if (float.TryParse(val, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out f))
                            s.FontSize = Math.Max(8f, Math.Min(80f, f));
                    }
                    else if (key == "TextColor")
                    {
                        int argb;
                        if (int.TryParse(val, out argb))
                            s.TextColor = Color.FromArgb(argb);
                    }
                    else if (key == "Transparency")
                    {
                        int t;
                        if (int.TryParse(val, out t))
                            s.Transparency = Math.Max(0, Math.Min(100, t));
                    }
                    else if (key == "AutoStart")
                    {
                        bool b;
                        if (bool.TryParse(val, out b))
                            s.AutoStart = b;
                    }
                    else if (key == "FirstRun")
                    {
                        bool b;
                        if (bool.TryParse(val, out b))
                            s.FirstRun = b;
                    }
                }
            }
            catch { /* 读取失败时使用默认值 */ }

            return s;
        }

        // ── 保存 ──────────────────────────────────────────────────
        public void Save()
        {
            try
            {
                if (!Directory.Exists(_dir))
                    Directory.CreateDirectory(_dir);

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("{");
                sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,
                    "  \"FontSize\": {0},", FontSize).AppendLine();
                sb.AppendFormat("  \"TextColor\": {0},", TextColor.ToArgb()).AppendLine();
                sb.AppendFormat("  \"Transparency\": {0},", Transparency).AppendLine();
                sb.AppendFormat("  \"AutoStart\": {0},", AutoStart.ToString().ToLower()).AppendLine();
                sb.AppendFormat("  \"FirstRun\": {0}", FirstRun.ToString().ToLower()).AppendLine();
                sb.AppendLine("}");
                File.WriteAllText(_file, sb.ToString());
            }
            catch { /* 保存失败时静默忽略 */ }
        }
    }
}
