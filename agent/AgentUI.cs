// TabDeck Agent arayuzu: sistem tepsisi simgesi ve ayar penceresi (WinForms, C# 5).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace TabDeck
{
    // ---------- Etkinlik gunlugu ----------

    /// Arayuzun gosterdigi son olaylar. Is parcaciklarindan yazilir, arayuz zamanlayiciyla okur.
    static class Log
    {
        static readonly List<string> lines = new List<string>();
        static int version;
        static string lastIp;
        static DateTime lastSeen;

        public static int Version { get { return version; } }

        public static void Write(string message)
        {
            lock (lines)
            {
                lines.Add(DateTime.Now.ToString("HH:mm:ss") + "   " + message);
                if (lines.Count > 300) lines.RemoveAt(0);
                version++;
            }
        }

        public static void Action(string ip, string what, string error)
        {
            Write((error == null ? "✓ " : "✗ ") + what.Trim() + (error == null ? "" : "  →  " + error));
        }

        public static void Seen(string ip)
        {
            lock (lines)
            {
                if (ip != lastIp) { lastIp = ip; version++; }
                lastSeen = DateTime.Now;
            }
        }

        public static string[] Snapshot()
        {
            lock (lines)
            {
                var copy = lines.ToArray();
                Array.Reverse(copy); // en yeni ustte
                return copy;
            }
        }

        public static string LastClient(out DateTime when)
        {
            lock (lines) { when = lastSeen; return lastIp; }
        }

        public static void Clear()
        {
            lock (lines) { lines.Clear(); version++; }
        }
    }

    // ---------- Uygulama ikonu ----------

    static class AppIcon
    {
        static readonly Color[] Tiles =
        {
            Color.FromArgb(0x7C, 0x4D, 0xFF), Color.FromArgb(0x26, 0xC6, 0xDA),
            Color.FromArgb(0xFF, 0x70, 0x43), Color.FromArgb(0x66, 0xBB, 0x6A),
        };

        static GraphicsPath Rounded(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float d = radius * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        /// Koyu zemin uzerinde 2x2 renkli tus (tabletteki uygulama ikonuyla ayni motif).
        public static Bitmap Draw(int size)
        {
            var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (var bg = Rounded(new RectangleF(0, 0, size - 1, size - 1), size * 0.22f))
                using (var b = new SolidBrush(Color.FromArgb(0x16, 0x18, 0x1D)))
                    g.FillPath(b, bg);
                float pad = size * 0.17f, gap = size * 0.07f;
                float tile = (size - 2 * pad - gap) / 2;
                for (int i = 0; i < 4; i++)
                {
                    float x = pad + (i % 2) * (tile + gap), y = pad + (i / 2) * (tile + gap);
                    using (var path = Rounded(new RectangleF(x, y, tile, tile), Math.Max(1f, tile * 0.18f)))
                    using (var b = new SolidBrush(Tiles[i]))
                        g.FillPath(b, path);
                }
            }
            return bmp;
        }

        /// Cok boyutlu .ico yazar (PNG icerikli girisler; Windows Vista ve sonrasi destekler).
        public static void WriteIco(string path)
        {
            int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 128, 256 };
            var images = new List<byte[]>();
            foreach (int s in sizes)
                using (var bmp = Draw(s))
                using (var ms = new MemoryStream())
                {
                    bmp.Save(ms, ImageFormat.Png);
                    images.Add(ms.ToArray());
                }
            using (var w = new BinaryWriter(File.Create(path)))
            {
                w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
                int offset = 6 + 16 * sizes.Length;
                for (int i = 0; i < sizes.Length; i++)
                {
                    w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                    w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                    w.Write((byte)0); w.Write((byte)0);
                    w.Write((short)1); w.Write((short)32);
                    w.Write(images[i].Length); w.Write(offset);
                    offset += images[i].Length;
                }
                foreach (var img in images) w.Write(img);
            }
        }

        /// Exe'ye gomulu ikon; derlemede eklenmediyse cizilerek uretilir.
        public static Icon Load(int size)
        {
            var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("TabDeck.tabdeck.ico");
            if (s != null) using (s) return new Icon(s, size, size);
            using (var bmp = Draw(size)) return Icon.FromHandle(bmp.GetHicon());
        }
    }

    // ---------- Windows acilisinda baslatma ----------

    static class Autostart
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string Name = "TabDeck Agent";

        public static bool Enabled
        {
            get
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
                    return k != null && k.GetValue(Name) != null;
            }
            set
            {
                using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (value) k.SetValue(Name, "\"" + Application.ExecutablePath + "\" --tray");
                    else if (k.GetValue(Name) != null) k.DeleteValue(Name);
                }
            }
        }
    }

    // ---------- Uygulama yasam dongusu ----------

    static class AgentApp
    {
        static Mutex instance;
        static EventWaitHandle showSignal;
        static MainForm form;
        static NotifyIcon tray;
        static bool exiting;

        public static void Run(bool startHidden)
        {
            if (!AcquireInstance()) return;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Program.StartServer();

            form = new MainForm();
            form.FormClosing += (s, e) =>
            {
                if (exiting || e.CloseReason != CloseReason.UserClosing) return;
                e.Cancel = true;
                form.Hide();
                ShowTrayHintOnce();
            };

            tray = new NotifyIcon { Icon = AppIcon.Load(16), Text = "TabDeck Agent", Visible = true };
            var menu = new ContextMenuStrip();
            menu.Items.Add("Aç", null, (s, e) => ShowWindow());
            menu.Items.Add("Ekran görüntüleri klasörü", null, (s, e) => MainForm.OpenScreenshots());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Çıkış", null, (s, e) => Exit());
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += (s, e) => ShowWindow();

            // Ikinci kez acilan kopya pencereyi one getirmek icin sinyal gonderir
            var watcher = new Thread(() =>
            {
                while (true)
                {
                    showSignal.WaitOne();
                    try { form.BeginInvoke(new Action(ShowWindow)); } catch { }
                }
            }) { IsBackground = true };
            var handle = form.Handle; // BeginInvoke icin pencere tutamacini simdi olustur
            watcher.Start();

            if (!startHidden) form.Show();
            Application.Run();
            tray.Visible = false;
        }

        static bool AcquireInstance()
        {
            try
            {
                bool created;
                instance = new Mutex(true, @"Local\TabDeckAgent.Instance", out created);
                showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\TabDeckAgent.Show");
                if (created) return true;
                showSignal.Set();
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                // Calisan kopya yonetici olarak acilmis; sinyal gonderilemez
                MessageBox.Show("TabDeck Agent zaten yönetici olarak çalışıyor.\nSistem tepsisindeki simgeye çift tıklayın.",
                    "TabDeck Agent", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }
        }

        static void ReleaseInstance()
        {
            if (instance == null) return;
            try { instance.ReleaseMutex(); } catch { }
            instance.Dispose();
            instance = null;
        }

        public static void ShowWindow()
        {
            form.Show();
            if (form.WindowState == FormWindowState.Minimized) form.WindowState = FormWindowState.Normal;
            form.Activate();
        }

        static bool hintShown;

        static void ShowTrayHintOnce()
        {
            if (hintShown) return;
            hintShown = true;
            tray.ShowBalloonTip(3000, "TabDeck Agent çalışmaya devam ediyor",
                "Tablet bağlantısı açık. Pencereyi tepsideki simgeden açabilir, sağ tıklayıp kapatabilirsiniz.", ToolTipIcon.Info);
        }

        public static void Exit()
        {
            exiting = true;
            Program.StopServer();
            tray.Visible = false;
            ReleaseInstance();
            Application.Exit();
        }

        /// Yonetici olarak yeniden baslatir; UAC reddedilirse calismaya devam eder.
        public static void RestartAsAdmin()
        {
            Program.StopServer();
            ReleaseInstance();
            if (Elevation.Relaunch(form.Visible ? "--admin" : "--admin --tray"))
            {
                exiting = true;
                tray.Visible = false;
                Application.Exit();
                return;
            }
            AcquireInstance();
            Program.StartServer();
            MessageBox.Show(form, "Yönetici izni verilmedi; normal yetkiyle devam ediliyor.", "TabDeck Agent",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    // ---------- Tema ----------

    static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(0x12, 0x14, 0x18);
        public static readonly Color Card = Color.FromArgb(0x1C, 0x1F, 0x26);
        public static readonly Color Border = Color.FromArgb(0x2C, 0x30, 0x3A);
        public static readonly Color Input = Color.FromArgb(0x25, 0x29, 0x32);
        public static readonly Color Text = Color.FromArgb(0xE8, 0xEA, 0xED);
        public static readonly Color Muted = Color.FromArgb(0x9A, 0xA0, 0xA6);
        public static readonly Color Accent = Color.FromArgb(0x7C, 0x4D, 0xFF);
        public static readonly Color Green = Color.FromArgb(0x4C, 0xAF, 0x50);
        public static readonly Color Red = Color.FromArgb(0xEF, 0x53, 0x50);
        public static readonly Color Amber = Color.FromArgb(0xFF, 0xB3, 0x00);

        public static Button Button(string text, bool primary)
        {
            return new RoundButton(primary) { Text = text };
        }

        public static TextBox TextBox()
        {
            return new TextBox { BackColor = Input, ForeColor = Text, BorderStyle = BorderStyle.FixedSingle, Dock = DockStyle.Fill };
        }

        public static Label Label(string text, Color color, float size, FontStyle style)
        {
            return new Label
            {
                Text = text, ForeColor = color, AutoSize = true,
                Font = new Font("Segoe UI", size, style), BackColor = Color.Transparent,
            };
        }
    }

    /// Kendi cizilen duz dugme: sabit yukseklik, yuvarlak kenar, metne gore genislik.
    /// (FlatStyle.Flat dugmeler AutoSize ile alt kenarini kirpiyordu.)
    class RoundButton : Button
    {
        const int H = 32, PadX = 14;
        readonly bool primary;
        bool hover, down;

        public RoundButton(bool primary)
        {
            this.primary = primary;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.SupportsTransparentBackColor, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            BackColor = Color.Transparent;
            ForeColor = Color.White;
            Cursor = Cursors.Hand;
            Font = new Font("Segoe UI", 9.5f);
            Margin = new Padding(0, 0, 8, 0);
            Height = H;
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            Size = new Size(TextRenderer.MeasureText(Text, Font).Width + 2 * PadX, H);
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var parentBg = new SolidBrush(Parent != null ? Parent.BackColor : Theme.Card))
                g.FillRectangle(parentBg, ClientRectangle);

            Color fill = primary ? Theme.Accent : Theme.Input;
            if (hover) fill = primary ? Color.FromArgb(0x95, 0x75, 0xFF) : Theme.Border;
            if (down) fill = primary ? Color.FromArgb(0x65, 0x3A, 0xE0) : Color.FromArgb(0x35, 0x3A, 0x45);
            if (!Enabled) fill = Theme.Input;

            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (var path = new GraphicsPath())
            {
                float d = 12;
                path.AddArc(r.X, r.Y, d, d, 180, 90);
                path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                path.CloseFigure();
                using (var b = new SolidBrush(fill)) g.FillPath(b, path);
                if (!primary) using (var p = new Pen(Theme.Border)) g.DrawPath(p, path);
            }
            TextRenderer.DrawText(g, Text, Font, ClientRectangle, Enabled ? ForeColor : Theme.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            if (Focused && ShowFocusCues)
                ControlPaint.DrawFocusRectangle(g, Rectangle.Inflate(ClientRectangle, -3, -3));
        }
    }

    /// Yuvarlak kenarli, baslikli kart.
    class Card : Panel
    {
        readonly string title;

        public Card(string title)
        {
            this.title = title;
            DoubleBuffered = true;
            BackColor = Theme.Bg;
            Padding = new Padding(18, 46, 18, 16);
            Dock = DockStyle.Fill;
            Margin = new Padding(0);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = new GraphicsPath())
            {
                int d = 20;
                path.AddArc(r.X, r.Y, d, d, 180, 90);
                path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                path.CloseFigure();
                using (var b = new SolidBrush(Theme.Card)) g.FillPath(b, path);
                using (var p = new Pen(Theme.Border)) g.DrawPath(p, path);
            }
            using (var f = new Font("Segoe UI Semibold", 11f))
                TextRenderer.DrawText(g, title, f, new Point(18, 14), Theme.Text);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Invalidate();
        }
    }

    // ---------- Ana pencere ----------

    class MainForm : Form
    {
        readonly Label status, mode, ipValue, ipOther, portValue, tokenValue, lastClient, obsStatus;
        readonly Button adminButton, showToken;
        readonly NumericUpDown port;
        readonly TextBox obsUrl, obsPassword, screenshotDir;
        readonly CheckBox autostart, runAsAdmin;
        readonly ListBox log;
        readonly ToolTip tips = new ToolTip();
        bool tokenVisible;
        int shownLogVersion = -1;

        public MainForm()
        {
            Text = "TabDeck Agent";
            Icon = AppIcon.Load(32);
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = new Font("Segoe UI", 9.75f);
            ClientSize = new Size(900, 680);
            MinimumSize = new Size(820, 640);
            StartPosition = FormStartPosition.CenterScreen;
            Padding = new Padding(16);

            // --- Ust bilgi ---
            var header = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = Theme.Bg };
            var logo = new PictureBox { Image = AppIcon.Draw(44), Size = new Size(44, 44), Location = new Point(2, 8) };
            var title = Theme.Label("TabDeck Agent", Theme.Text, 17f, FontStyle.Bold);
            title.Location = new Point(56, 4);
            var ver = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false);
            var version = Theme.Label("sürüm " + (ver.Length > 0 ? ((AssemblyInformationalVersionAttribute)ver[0]).InformationalVersion : "?"),
                Theme.Muted, 9f, FontStyle.Regular);
            version.Location = new Point(59, 38);

            var right = new FlowLayoutPanel
            {
                Dock = DockStyle.Right, AutoSize = true, FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false, BackColor = Theme.Bg, Padding = new Padding(0, 14, 0, 0),
            };
            adminButton = Theme.Button("Yönetici olarak yeniden başlat", false);
            adminButton.Click += (s, e) => AgentApp.RestartAsAdmin();
            tips.SetToolTip(adminButton, "Yönetici olarak çalışan oyunlar ön plandayken kısayol ve Discord tuşlarının çalışması için gerekir.");
            mode = Theme.Label("", Theme.Amber, 10f, FontStyle.Bold);
            mode.Margin = new Padding(12, 6, 12, 0);
            status = Theme.Label("", Theme.Green, 10f, FontStyle.Bold);
            status.Margin = new Padding(12, 6, 0, 0);
            right.Controls.Add(adminButton);
            right.Controls.Add(mode);
            right.Controls.Add(status);
            header.Controls.Add(right);
            header.Controls.Add(logo);
            header.Controls.Add(title);
            header.Controls.Add(version);

            // --- Govde: baglanti karti ustte, ayarlar ve etkinlik altta yan yana ---
            var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, BackColor = Theme.Bg };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 228));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // Baglanti karti
            var conn = new Card("Tablette girilecek bilgiler") { Margin = new Padding(0, 0, 0, 12) };
            var tiles = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2, BackColor = Theme.Card };
            tiles.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
            tiles.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22));
            tiles.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
            tiles.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            tiles.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            ipValue = BigValue();
            ipOther = Theme.Label("", Theme.Muted, 8.5f, FontStyle.Regular);
            portValue = BigValue();
            tokenValue = BigValue();
            showToken = Theme.Button("Göster", false);
            showToken.Click += (s, e) => { tokenVisible = !tokenVisible; RefreshConnection(); };
            var newToken = Theme.Button("Yeni token", false);
            newToken.Click += (s, e) => RegenerateToken();

            tiles.Controls.Add(Tile("PC IP ADRESİ", ipValue, ipOther, () => FirstIp(), null), 0, 0);
            tiles.Controls.Add(Tile("PORT", portValue, null, () => Program.cfg.port.ToString(), null), 1, 0);
            tiles.Controls.Add(Tile("TOKEN", tokenValue, null, () => Program.cfg.token, new Control[] { showToken, newToken }), 2, 0);
            var hint = Theme.Label("Tablette TabDeck → ⚙️ Ayarlar'a bu üç bilgiyi girip \"Bağlantıyı test et\"e basın. Tablet ve PC aynı Wi-Fi ağında olmalı.",
                Theme.Muted, 9f, FontStyle.Regular);
            hint.Margin = new Padding(0, 6, 0, 0);
            tiles.Controls.Add(hint, 0, 1);
            tiles.SetColumnSpan(hint, 3);
            conn.Controls.Add(tiles);
            body.Controls.Add(conn, 0, 0);
            body.SetColumnSpan(conn, 2);

            // Ayarlar karti
            var settings = new Card("Ayarlar") { Margin = new Padding(0, 0, 6, 0) };
            var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = Theme.Card, AutoScroll = true };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            port = new NumericUpDown
            {
                Minimum = 1024, Maximum = 65535, Width = 100,
                BackColor = Theme.Input, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle,
            };
            obsUrl = Theme.TextBox();
            obsPassword = Theme.TextBox();
            obsPassword.UseSystemPasswordChar = true;
            screenshotDir = Theme.TextBox();
            var browse = Theme.Button("…", false);
            browse.Click += (s, e) => BrowseScreenshots();
            var dirRow = Row(screenshotDir, browse);

            var obsTest = Theme.Button("OBS bağlantısını test et", false);
            obsTest.Click += (s, e) => TestObs();
            obsStatus = Theme.Label("", Theme.Muted, 9f, FontStyle.Regular);
            obsStatus.Margin = new Padding(4, 8, 0, 0);
            var obsRow = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Theme.Card, WrapContents = false, Margin = new Padding(0) };
            obsRow.Controls.Add(obsTest);
            obsRow.Controls.Add(obsStatus);

            autostart = new CheckBox { Text = "Windows açılışında başlat (tepside)", ForeColor = Theme.Text, AutoSize = true };
            runAsAdmin = new CheckBox { Text = "Yönetici olarak çalıştır (oyunlar için)", ForeColor = Theme.Text, AutoSize = true };
            tips.SetToolTip(runAsAdmin, "Oyun yönetici olarak çalışıyorsa Windows, normal programların gönderdiği tuşları engeller.\nMedya ve ses kontrolleri bundan etkilenmez.");

            var save = Theme.Button("Kaydet", true);
            save.Click += (s, e) => SaveSettings();
            var openDir = Theme.Button("Ekran görüntüleri", false);
            openDir.Click += (s, e) => OpenScreenshots();
            var buttons = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Theme.Card, Margin = new Padding(0, 10, 0, 0) };
            buttons.Controls.Add(save);
            buttons.Controls.Add(openDir);

            AddRow(grid, "Port", port);
            AddRow(grid, "OBS adresi", obsUrl);
            AddRow(grid, "OBS şifresi", obsPassword);
            AddRow(grid, "", obsRow);
            AddRow(grid, "Ekran görüntüleri", dirRow);
            AddRow(grid, "", autostart);
            AddRow(grid, "", runAsAdmin);
            AddRow(grid, "", buttons);
            settings.Controls.Add(grid);
            body.Controls.Add(settings, 0, 1);

            // Etkinlik karti
            var activity = new Card("Etkinlik") { Margin = new Padding(6, 0, 0, 0) };
            lastClient = Theme.Label("", Theme.Muted, 9f, FontStyle.Regular);
            lastClient.Dock = DockStyle.Top;
            lastClient.Padding = new Padding(0, 0, 0, 8);
            log = new ListBox
            {
                Dock = DockStyle.Fill, BackColor = Theme.Card, ForeColor = Theme.Text, BorderStyle = BorderStyle.None,
                Font = new Font("Consolas", 9f), IntegralHeight = false, HorizontalScrollbar = true,
            };
            var clear = Theme.Button("Temizle", false);
            clear.Dock = DockStyle.Bottom;
            clear.Click += (s, e) => Log.Clear();
            activity.Controls.Add(log);
            activity.Controls.Add(lastClient);
            activity.Controls.Add(clear);
            body.Controls.Add(activity, 1, 1);

            Controls.Add(body);
            Controls.Add(header);

            LoadSettings();
            RefreshConnection();

            var timer = new System.Windows.Forms.Timer { Interval = 1000 };
            timer.Tick += (s, e) => RefreshLive();
            timer.Start();
            RefreshLive();
        }

        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        /// Windows 10 20H1+ / 11'de baslik cubugunu koyu yapar; eski surumlerde etkisiz.
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            int on = 1;
            try { DwmSetWindowAttribute(Handle, 20 /* DWMWA_USE_IMMERSIVE_DARK_MODE */, ref on, 4); } catch { }
        }

        // ----- Yerlesim yardimcilari -----

        static Label BigValue()
        {
            var l = Theme.Label("", Theme.Text, 20f, FontStyle.Bold);
            l.Font = new Font("Segoe UI Semibold", 20f);
            return l;
        }

        Control Tile(string caption, Label value, Label extra, Func<string> copyText, Control[] more)
        {
            var p = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Theme.Card };
            p.Controls.Add(Theme.Label(caption, Theme.Muted, 8.5f, FontStyle.Bold));
            p.Controls.Add(value);
            if (extra != null) p.Controls.Add(extra);
            var row = new FlowLayoutPanel
            {
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Theme.Card,
                WrapContents = false, Margin = new Padding(0, 6, 0, 2),
            };
            var copy = Theme.Button("Kopyala", false);
            copy.Click += (s, e) =>
            {
                try { Clipboard.SetText(copyText()); copy.Text = "Kopyalandı ✓"; }
                catch { copy.Text = "Kopyalanamadı"; }
                var t = new System.Windows.Forms.Timer { Interval = 1500 };
                t.Tick += (s2, e2) => { copy.Text = "Kopyala"; t.Dispose(); };
                t.Start();
            };
            row.Controls.Add(copy);
            if (more != null) foreach (var c in more) row.Controls.Add(c);
            p.Controls.Add(row);
            return p;
        }

        static Control Row(Control fill, Control fixedRight)
        {
            var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Height = 36, BackColor = Theme.Card, Margin = new Padding(0) };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            fill.Margin = new Padding(0, 6, 6, 0);
            fixedRight.Margin = new Padding(0, 2, 0, 2);
            t.Controls.Add(fill, 0, 0);
            t.Controls.Add(fixedRight, 1, 0);
            return t;
        }

        static void AddRow(TableLayoutPanel grid, string label, Control control)
        {
            int r = grid.RowCount++;
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var l = Theme.Label(label, Theme.Muted, 9.5f, FontStyle.Regular);
            l.Margin = new Padding(0, 9, 8, 0);
            grid.Controls.Add(l, 0, r);
            control.Margin = new Padding(0, 4, 0, 4);
            grid.Controls.Add(control, 1, r);
        }

        // ----- Veri -----

        static string FirstIp()
        {
            var ips = Program.LocalIps();
            return ips.Count > 0 ? ips[0] : "";
        }

        void RefreshConnection()
        {
            var ips = Program.LocalIps();
            ipValue.Text = ips.Count > 0 ? ips[0] : "bulunamadı";
            ipOther.Text = ips.Count > 1 ? "diğer: " + string.Join(", ", ips.GetRange(1, Math.Min(3, ips.Count - 1))) : "";
            portValue.Text = Program.cfg.port.ToString();
            tokenValue.Text = tokenVisible ? Program.cfg.token : new string('•', Program.cfg.token.Length);
            showToken.Text = tokenVisible ? "Gizle" : "Göster";
        }

        void RefreshLive()
        {
            bool admin = Elevation.IsSelfElevated();
            if (Program.Running)
            {
                status.Text = "● Çalışıyor";
                status.ForeColor = Theme.Green;
            }
            else
            {
                status.Text = "● " + (Program.ServerError ?? "Durdu");
                status.ForeColor = Theme.Red;
            }
            mode.Text = admin ? "Yönetici modu" : "";
            adminButton.Visible = !admin;

            DateTime when;
            string ip = Log.LastClient(out when);
            lastClient.Text = ip == null ? "Henüz tablet bağlanmadı."
                : "Son bağlanan tablet: " + ip + "  ·  " + when.ToString("HH:mm:ss");

            if (Log.Version != shownLogVersion)
            {
                shownLogVersion = Log.Version;
                log.BeginUpdate();
                log.Items.Clear();
                log.Items.AddRange(Log.Snapshot());
                log.EndUpdate();
            }
        }

        void LoadSettings()
        {
            var c = Program.cfg;
            port.Value = Math.Max(port.Minimum, Math.Min(port.Maximum, c.port));
            obsUrl.Text = c.obsUrl;
            obsPassword.Text = c.obsPassword;
            screenshotDir.Text = c.screenshotDir;
            autostart.Checked = Autostart.Enabled;
            runAsAdmin.Checked = c.runAsAdmin;
        }

        void SaveSettings()
        {
            var c = Program.cfg;
            int oldPort = c.port;
            c.port = (int)port.Value;
            c.obsUrl = obsUrl.Text.Trim();
            c.obsPassword = obsPassword.Text;
            c.screenshotDir = screenshotDir.Text.Trim();
            c.runAsAdmin = runAsAdmin.Checked;
            try
            {
                Program.SaveConfig();
                Autostart.Enabled = autostart.Checked;
            }
            catch (Exception e)
            {
                MessageBox.Show(this, "Kaydedilemedi: " + e.Message, "TabDeck Agent", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            Log.Write("Ayarlar kaydedildi");
            if (c.port != oldPort) Program.StartServer();
            RefreshConnection();

            if (c.runAsAdmin && !Elevation.IsSelfElevated() &&
                MessageBox.Show(this, "Yönetici modu bir sonraki açılışta etkin olacak. Şimdi yönetici olarak yeniden başlatılsın mı?",
                    "TabDeck Agent", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                AgentApp.RestartAsAdmin();
        }

        void RegenerateToken()
        {
            if (MessageBox.Show(this, "Yeni bir token oluşturulsun mu?\nTabletteki ayarlarda da yeni token'ı girmeniz gerekecek.",
                    "TabDeck Agent", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            Program.cfg.token = Program.NewToken();
            Program.SaveConfig();
            tokenVisible = true;
            RefreshConnection();
            Log.Write("Yeni token oluşturuldu");
        }

        void BrowseScreenshots()
        {
            using (var d = new FolderBrowserDialog { Description = "OBS ekran görüntülerinin kaydedileceği klasör" })
            {
                if (Directory.Exists(screenshotDir.Text)) d.SelectedPath = screenshotDir.Text;
                if (d.ShowDialog(this) == DialogResult.OK) screenshotDir.Text = d.SelectedPath;
            }
        }

        public static void OpenScreenshots()
        {
            string dir = Program.cfg.screenshotDir != "" ? Program.cfg.screenshotDir
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "screenshots");
            Directory.CreateDirectory(dir);
            Process.Start("explorer.exe", "\"" + dir + "\"");
        }

        void TestObs()
        {
            // Kaydedilmemis degerlerle dener; kaydetmeden once dogrulamak icin
            var probe = new ObsClient { Url = obsUrl.Text.Trim(), Password = obsPassword.Text };
            obsStatus.Text = "Deneniyor…";
            obsStatus.ForeColor = Theme.Muted;
            ThreadPool.QueueUserWorkItem(delegate
            {
                string text;
                Color color;
                try
                {
                    var v = probe.Request("GetVersion", null);
                    text = "✓ Bağlandı: OBS " + v["obsVersion"] + " (WebSocket " + v["obsWebSocketVersion"] + ")";
                    color = Theme.Green;
                }
                catch (Exception e)
                {
                    text = "✗ " + e.Message;
                    color = Theme.Red;
                }
                finally { probe.Reset(); }
                try { BeginInvoke(new Action(() => { obsStatus.Text = text; obsStatus.ForeColor = color; })); } catch { }
            });
        }
    }
}
