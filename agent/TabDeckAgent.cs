// TabDeck PC Agent - tabletteki TabDeck uygulamasindan gelen komutlari calistirir.
// Windows'ta hazir bulunan .NET Framework csc.exe ile derlenir (build.bat). C# 5 uyumlu.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Reflection;

// Surum release script'inde (release-tabdeck.sh) degistirilir
[assembly: AssemblyTitle("TabDeck Agent")]
[assembly: AssemblyProduct("TabDeck")]
[assembly: AssemblyCopyright("GPL-3.0")]
[assembly: AssemblyFileVersion("0.0.0.0")]
[assembly: AssemblyInformationalVersion("dev")]

namespace TabDeck
{
    class Config
    {
        public int port = 8765;
        public string token = "";
        public string obsUrl = "ws://127.0.0.1:4455";
        public string obsPassword = "";
        public string screenshotDir = ""; // bos = agent klasoru\screenshots
    }

    static class Program
    {
        static Config cfg;
        static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
        static readonly ObsClient Obs = new ObsClient();

        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            if (args.Length == 2 && args[0] == "--test-apps")
            {
                ShellApps.Test(args[1]);
                return;
            }
            cfg = LoadConfig();
            Obs.Url = cfg.obsUrl;
            Obs.Password = cfg.obsPassword;

            var listener = new TcpListener(IPAddress.Any, cfg.port);
            try { listener.Start(); }
            catch (Exception e)
            {
                Console.WriteLine("Port " + cfg.port + " acilamadi: " + e.Message);
                Console.ReadLine();
                return;
            }

            Console.WriteLine("==============================================");
            Console.WriteLine(" TabDeck Agent calisiyor");
            Console.WriteLine(" Port : " + cfg.port);
            Console.WriteLine(" Token: " + cfg.token);
            foreach (var ip in LocalIps()) Console.WriteLine(" IP   : " + ip);
            Console.WriteLine(" Tablette bu IP, port ve token'i girin.");
            Console.WriteLine("==============================================");

            while (true)
            {
                var client = listener.AcceptTcpClient();
                ThreadPool.QueueUserWorkItem(delegate { HandleClient(client); });
            }
        }

        static Config LoadConfig()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "agent.json");
            Config c = null;
            if (File.Exists(path))
            {
                try { c = Json.Deserialize<Config>(File.ReadAllText(path)); }
                catch (Exception e) { Console.WriteLine("agent.json okunamadi: " + e.Message); }
            }
            if (c == null) c = new Config();
            if (string.IsNullOrEmpty(c.token))
            {
                c.token = new Random().Next(100000, 999999).ToString();
                File.WriteAllText(path, Json.Serialize(c));
            }
            return c;
        }

        static IEnumerable<string> LocalIps()
        {
            foreach (var a in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
                if (a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a))
                    yield return a.ToString();
        }

        // ---------- Minimal HTTP ----------

        static void HandleClient(TcpClient client)
        {
            using (client)
            using (var stream = client.GetStream())
            {
                try
                {
                    stream.ReadTimeout = 5000;
                    string method, path;
                    Dictionary<string, string> headers;
                    string body;
                    if (!ReadRequest(stream, out method, out path, out headers, out body)) return;

                    string tok;
                    headers.TryGetValue("x-token", out tok);
                    var query = new Dictionary<string, string>();
                    int q = path.IndexOf('?');
                    if (q >= 0)
                    {
                        foreach (var part in path.Substring(q + 1).Split('&'))
                        {
                            int eq = part.IndexOf('=');
                            if (eq > 0) query[part.Substring(0, eq)] = Uri.UnescapeDataString(part.Substring(eq + 1));
                        }
                        path = path.Substring(0, q);
                    }
                    object result;
                    int status = 200;
                    if (tok != cfg.token)
                    {
                        status = 401;
                        result = Err("Gecersiz token");
                    }
                    else if (method == "GET" && path == "/appicon")
                    {
                        string id, sizeStr;
                        int size;
                        query.TryGetValue("id", out id);
                        query.TryGetValue("size", out sizeStr);
                        if (!int.TryParse(sizeStr, out size)) size = 128;
                        WriteResponse(stream, 200, "image/png", ShellApps.IconPng(id ?? "", Math.Max(16, Math.Min(256, size))));
                        return;
                    }
                    else if (method == "GET" && path == "/apps")
                        result = new Dictionary<string, object> { { "ok", true }, { "apps", ShellApps.List() } };
                    else if (method == "GET" && path == "/ping")
                        result = new Dictionary<string, object> { { "ok", true }, { "name", Environment.MachineName } };
                    else if (method == "GET" && path == "/obs/scenes")
                        result = ObsScenes();
                    else if (method == "GET" && path == "/obs/inputs")
                        result = ObsInputs();
                    else if (method == "POST" && path == "/action")
                        result = RunAction(Json.Deserialize<Dictionary<string, object>>(body));
                    else
                    {
                        status = 404;
                        result = Err("Bulunamadi");
                    }
                    WriteResponse(stream, status, Json.Serialize(result));
                }
                catch (Exception e)
                {
                    try { WriteResponse(stream, 500, Json.Serialize(Err(e.Message))); } catch { }
                }
            }
        }

        static bool ReadRequest(Stream s, out string method, out string path,
            out Dictionary<string, string> headers, out string body)
        {
            method = path = body = null;
            headers = new Dictionary<string, string>();
            var buf = new List<byte>();
            int b;
            // Basliklari \r\n\r\n gorene kadar oku
            while ((b = s.ReadByte()) != -1)
            {
                buf.Add((byte)b);
                int n = buf.Count;
                if (n >= 4 && buf[n - 4] == 13 && buf[n - 3] == 10 && buf[n - 2] == 13 && buf[n - 1] == 10) break;
                if (n > 16384) return false;
            }
            if (b == -1) return false;
            var lines = Encoding.ASCII.GetString(buf.ToArray()).Split(new[] { "\r\n" }, StringSplitOptions.None);
            var first = lines[0].Split(' ');
            if (first.Length < 2) return false;
            method = first[0].ToUpperInvariant();
            path = first[1];
            for (int i = 1; i < lines.Length; i++)
            {
                int c = lines[i].IndexOf(':');
                if (c > 0) headers[lines[i].Substring(0, c).Trim().ToLowerInvariant()] = lines[i].Substring(c + 1).Trim();
            }
            string lenStr;
            int len = 0;
            if (headers.TryGetValue("content-length", out lenStr)) int.TryParse(lenStr, out len);
            var data = new byte[len];
            int read = 0;
            while (read < len)
            {
                int r = s.Read(data, read, len - read);
                if (r <= 0) break;
                read += r;
            }
            body = Encoding.UTF8.GetString(data, 0, read);
            return true;
        }

        static void WriteResponse(Stream s, int status, string json)
        {
            WriteResponse(s, status, "application/json; charset=utf-8", Encoding.UTF8.GetBytes(json));
        }

        static void WriteResponse(Stream s, int status, string contentType, byte[] bytes)
        {
            var head = "HTTP/1.1 " + status + " OK\r\nContent-Type: " + contentType + "\r\nContent-Length: "
                + bytes.Length + "\r\nConnection: close\r\n\r\n";
            var h = Encoding.ASCII.GetBytes(head);
            s.Write(h, 0, h.Length);
            s.Write(bytes, 0, bytes.Length);
        }

        static Dictionary<string, object> Ok() { return new Dictionary<string, object> { { "ok", true } }; }
        static Dictionary<string, object> Err(string m) { return new Dictionary<string, object> { { "ok", false }, { "error", m } }; }

        static string Str(Dictionary<string, object> d, string k)
        {
            object v;
            return d != null && d.TryGetValue(k, out v) && v != null ? v.ToString() : "";
        }

        // ---------- Aksiyonlar ----------

        static object RunAction(Dictionary<string, object> a)
        {
            string type = Str(a, "type");
            Console.WriteLine(DateTime.Now.ToString("HH:mm:ss") + "  " + type + "  " + Str(a, "value"));
            switch (type)
            {
                case "hotkey":
                    Input.SendHotkey(Str(a, "value"));
                    return Ok();
                case "text":
                    Input.TypeText(Str(a, "value"));
                    if (Str(a, "arg") == "enter") Input.SendHotkey("enter");
                    return Ok();
                case "launch":
                    {
                        string target = Str(a, "value");
                        // Baslat menusu uygulamalari (masaustu + Store): shell:AppsFolder\<kimlik>
                        if (target.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
                        {
                            Process.Start("explorer.exe", "\"" + target + "\"");
                            return Ok();
                        }
                        var psi = new ProcessStartInfo(target) { UseShellExecute = true };
                        string args = Str(a, "arg");
                        if (args != "") psi.Arguments = args;
                        // OBS gibi programlar kendi klasorlerinden calismak ister
                        if (Path.IsPathRooted(target) && File.Exists(target))
                            psi.WorkingDirectory = Path.GetDirectoryName(target);
                        Process.Start(psi);
                        return Ok();
                    }
                case "media":
                    Input.SendMedia(Str(a, "value"));
                    return Ok();
                case "obs":
                    return ObsAction(Str(a, "value"), Str(a, "arg"), Str(a, "arg2"));
                default:
                    return Err("Bilinmeyen aksiyon: " + type);
            }
        }

        static Dictionary<string, object> D(params object[] kv)
        {
            var d = new Dictionary<string, object>();
            for (int i = 0; i + 1 < kv.Length; i += 2) d[(string)kv[i]] = kv[i + 1];
            return d;
        }

        static string CurrentScene()
        {
            return Str(Obs.Request("GetCurrentProgramScene", null), "currentProgramSceneName");
        }

        static object ObsAction(string cmd, string arg, string arg2)
        {
            switch (cmd)
            {
                case "scene": Obs.Request("SetCurrentProgramScene", D("sceneName", arg)); break;
                case "preview": Obs.Request("SetCurrentPreviewScene", D("sceneName", arg)); break;
                case "stream": Obs.Request("ToggleStream", null); break;
                case "streamStart": Obs.Request("StartStream", null); break;
                case "streamStop": Obs.Request("StopStream", null); break;
                case "record": Obs.Request("ToggleRecord", null); break;
                case "recordStart": Obs.Request("StartRecord", null); break;
                case "recordStop": Obs.Request("StopRecord", null); break;
                case "pauseRecord": Obs.Request("ToggleRecordPause", null); break;
                case "chapter":
                    Obs.Request("CreateRecordChapter", arg != "" ? D("chapterName", arg) : null);
                    break;
                case "split": Obs.Request("SplitRecordFile", null); break;
                case "replay": Obs.Request("SaveReplayBuffer", null); break;
                case "replayToggle": Obs.Request("ToggleReplayBuffer", null); break;
                case "virtualcam": Obs.Request("ToggleVirtualCam", null); break;
                case "studio":
                    {
                        bool on = (bool)Obs.Request("GetStudioModeEnabled", null)["studioModeEnabled"];
                        Obs.Request("SetStudioModeEnabled", D("studioModeEnabled", !on));
                        break;
                    }
                case "transition": Obs.Request("TriggerStudioModeTransition", null); break;
                case "mute": Obs.Request("ToggleInputMute", D("inputName", arg)); break;
                case "volume":
                    {
                        double delta;
                        if (!double.TryParse(arg2.Replace(',', '.'), System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out delta))
                            return Err("dB degeri gecersiz: " + arg2);
                        double db = Convert.ToDouble(Obs.Request("GetInputVolume", D("inputName", arg))["inputVolumeDb"]);
                        db = Math.Max(-100, Math.Min(26, db + delta));
                        Obs.Request("SetInputVolume", D("inputName", arg, "inputVolumeDb", db));
                        break;
                    }
                case "source":
                    {
                        string scene = arg != "" ? arg : CurrentScene();
                        object id = Obs.Request("GetSceneItemId", D("sceneName", scene, "sourceName", arg2))["sceneItemId"];
                        bool on = (bool)Obs.Request("GetSceneItemEnabled", D("sceneName", scene, "sceneItemId", id))["sceneItemEnabled"];
                        Obs.Request("SetSceneItemEnabled", D("sceneName", scene, "sceneItemId", id, "sceneItemEnabled", !on));
                        break;
                    }
                case "filter":
                    {
                        bool on = (bool)Obs.Request("GetSourceFilter", D("sourceName", arg, "filterName", arg2))["filterEnabled"];
                        Obs.Request("SetSourceFilterEnabled", D("sourceName", arg, "filterName", arg2, "filterEnabled", !on));
                        break;
                    }
                case "media":
                    {
                        string act = arg2 == "" ? "playpause" : arg2;
                        if (act == "playpause")
                        {
                            string state = Str(Obs.Request("GetMediaInputStatus", D("inputName", arg)), "mediaState");
                            act = state == "OBS_MEDIA_STATE_PLAYING" ? "pause" : "play";
                        }
                        Obs.Request("TriggerMediaInputAction",
                            D("inputName", arg, "mediaAction", "OBS_WEBSOCKET_MEDIA_INPUT_ACTION_" + act.ToUpperInvariant()));
                        break;
                    }
                case "screenshot":
                    {
                        string dir = cfg.screenshotDir != "" ? cfg.screenshotDir
                            : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "screenshots");
                        Directory.CreateDirectory(dir);
                        string file = Path.Combine(dir, "obs-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".png");
                        Obs.Request("SaveSourceScreenshot",
                            D("sourceName", CurrentScene(), "imageFormat", "png", "imageFilePath", file));
                        break;
                    }
                default: return Err("Bilinmeyen OBS komutu: " + cmd);
            }
            return Ok();
        }

        static object ObsInputs()
        {
            var res = Obs.Request("GetInputList", null);
            var names = new List<string>();
            object inputs;
            if (res != null && res.TryGetValue("inputs", out inputs))
                foreach (var s in (System.Collections.ArrayList)inputs)
                    names.Add(Str((Dictionary<string, object>)s, "inputName"));
            names.Sort(StringComparer.CurrentCultureIgnoreCase);
            return new Dictionary<string, object> { { "ok", true }, { "inputs", names } };
        }

        static object ObsScenes()
        {
            var res = Obs.Request("GetSceneList", null);
            var names = new List<string>();
            object scenes;
            if (res != null && res.TryGetValue("scenes", out scenes))
                foreach (var s in (System.Collections.ArrayList)scenes)
                    names.Add(Str((Dictionary<string, object>)s, "sceneName"));
            names.Reverse(); // OBS listeyi ters sirada verir
            return new Dictionary<string, object> { { "ok", true }, { "scenes", names } };
        }
    }

    // ---------- Klavye / medya girdisi ----------

    static class Input
    {
        [StructLayout(LayoutKind.Sequential)]
        struct INPUT { public uint type; public KEYBDINPUT ki; public long pad; }

        [StructLayout(LayoutKind.Sequential)]
        struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint SendInput(uint n, INPUT[] inputs, int size);

        const uint KEYUP = 0x2, UNICODE = 0x4, EXTENDED = 0x1;

        static readonly Dictionary<string, ushort> Keys = BuildKeys();

        static Dictionary<string, ushort> BuildKeys()
        {
            var k = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase)
            {
                {"ctrl",0x11},{"control",0x11},{"shift",0x10},{"alt",0x12},{"win",0x5B},{"cmd",0x5B},
                {"enter",0x0D},{"return",0x0D},{"esc",0x1B},{"escape",0x1B},{"tab",0x09},{"space",0x20},
                {"backspace",0x08},{"delete",0x2E},{"del",0x2E},{"insert",0x2D},{"home",0x24},{"end",0x23},
                {"pageup",0x21},{"pagedown",0x22},{"up",0x26},{"down",0x28},{"left",0x25},{"right",0x27},
                {"printscreen",0x2C},{"prtsc",0x2C},{"pause",0x13},{"capslock",0x14},
                {"plus",0xBB},{"minus",0xBD},{"comma",0xBC},{"period",0xBE},
                {"volumeup",0xAF},{"volumedown",0xAE},{"volumemute",0xAD},
                {"playpause",0xB3},{"nexttrack",0xB0},{"prevtrack",0xB1},{"stop",0xB2}
            };
            for (int i = 1; i <= 24; i++) k["f" + i] = (ushort)(0x6F + i);
            for (char c = 'a'; c <= 'z'; c++) k[c.ToString()] = (ushort)char.ToUpperInvariant(c);
            for (char c = '0'; c <= '9'; c++) k[c.ToString()] = (ushort)c;
            for (int i = 0; i <= 9; i++) k["num" + i] = (ushort)(0x60 + i);
            return k;
        }

        static bool IsExtended(ushort vk)
        {
            return (vk >= 0x21 && vk <= 0x2E) || vk == 0x5B || (vk >= 0xAD && vk <= 0xB3);
        }

        static INPUT Key(ushort vk, bool up)
        {
            var i = new INPUT { type = 1 };
            i.ki.wVk = vk;
            i.ki.dwFlags = (up ? KEYUP : 0) | (IsExtended(vk) ? EXTENDED : 0);
            return i;
        }

        static void Send(List<INPUT> list)
        {
            SendInput((uint)list.Count, list.ToArray(), Marshal.SizeOf(typeof(INPUT)));
        }

        // "ctrl+shift+m" gibi bir kombinasyonu basar ve birakir
        public static void SendHotkey(string combo)
        {
            var vks = new List<ushort>();
            foreach (var part in combo.Split('+'))
            {
                var p = part.Trim();
                if (p == "") continue;
                ushort vk;
                if (!Keys.TryGetValue(p, out vk)) throw new Exception("Bilinmeyen tus: " + p);
                vks.Add(vk);
            }
            var list = new List<INPUT>();
            foreach (var vk in vks) list.Add(Key(vk, false));
            for (int i = vks.Count - 1; i >= 0; i--) list.Add(Key(vks[i], true));
            Send(list);
        }

        public static void TypeText(string text)
        {
            var list = new List<INPUT>();
            foreach (char c in text)
            {
                foreach (bool up in new[] { false, true })
                {
                    var i = new INPUT { type = 1 };
                    i.ki.wScan = c;
                    i.ki.dwFlags = UNICODE | (up ? KEYUP : 0);
                    list.Add(i);
                }
            }
            Send(list);
        }

        public static void SendMedia(string cmd)
        {
            string key;
            switch (cmd)
            {
                case "playpause": key = "playpause"; break;
                case "next": key = "nexttrack"; break;
                case "prev": key = "prevtrack"; break;
                case "stop": key = "stop"; break;
                case "volup": key = "volumeup"; break;
                case "voldown": key = "volumedown"; break;
                case "mute": key = "volumemute"; break;
                default: throw new Exception("Bilinmeyen medya komutu: " + cmd);
            }
            SendHotkey(key);
        }
    }

    // ---------- PC uygulamalari ve gercek ikonlari ----------

    static class ShellApps
    {
        [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IShellItemImageFactory
        {
            [PreserveSig]
            int GetImage(SIZE size, int flags, out IntPtr phbm);
        }

        [StructLayout(LayoutKind.Sequential)]
        struct SIZE { public int cx, cy; }

        [StructLayout(LayoutKind.Sequential)]
        struct BITMAP
        {
            public int bmType, bmWidth, bmHeight, bmWidthBytes;
            public ushort bmPlanes, bmBitsPixel;
            public IntPtr bmBits;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct BITMAPINFOHEADER
        {
            public uint biSize;
            public int biWidth, biHeight;
            public ushort biPlanes, biBitCount;
            public uint biCompression, biSizeImage;
            public int biXPelsPerMeter, biYPelsPerMeter;
            public uint biClrUsed, biClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct DIBSECTION
        {
            public BITMAP dsBm;
            public BITMAPINFOHEADER dsBmih;
            public uint dsBitfields0, dsBitfields1, dsBitfields2;
            public IntPtr dshSection;
            public uint dsOffset;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        static extern void SHCreateItemFromParsingName(string path, IntPtr pbc,
            [MarshalAs(UnmanagedType.LPStruct)] Guid riid, [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory ppv);

        [DllImport("gdi32.dll")]
        static extern int GetObject(IntPtr h, int size, ref DIBSECTION ds);

        [DllImport("gdi32.dll")]
        static extern bool DeleteObject(IntPtr h);

        const int SIIGBF_BIGGERSIZEOK = 0x1, SIIGBF_ICONONLY = 0x4;

        static readonly Dictionary<string, byte[]> IconCache = new Dictionary<string, byte[]>();

        static readonly string[] Skip = { "uninstall", "kaldır", "kaldir", "readme", "beni oku", "help", "yardım", "release notes", "website", "web sitesi", "manual" };

        /// Shell COM nesneleri STA is parcacigi ister.
        static T RunSta<T>(Func<T> f)
        {
            T result = default(T);
            Exception err = null;
            var t = new Thread(() =>
            {
                try { result = f(); }
                catch (Exception e) { err = e; }
            });
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            t.Join();
            if (err != null) throw err;
            return result;
        }

        /// Windows'un "Tum uygulamalar" listesi: [{id, name}]. id, shell:AppsFolder\ altindaki kimlik.
        public static List<Dictionary<string, object>> List()
        {
            return RunSta(() =>
            {
                var list = new List<Dictionary<string, object>>();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application"));
                try
                {
                    dynamic folder = shell.NameSpace("shell:AppsFolder");
                    foreach (dynamic item in folder.Items())
                    {
                        string name = item.Name;
                        string id = item.Path;
                        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(id)) continue;
                        string lower = name.ToLowerInvariant();
                        bool skip = false;
                        foreach (var s in Skip) if (lower.Contains(s)) { skip = true; break; }
                        if (skip || !seen.Add(name)) continue;
                        list.Add(new Dictionary<string, object> { { "id", id }, { "name", name } });
                    }
                }
                finally { Marshal.FinalReleaseComObject(shell); }
                list.Sort((a, b) => string.Compare((string)a["name"], (string)b["name"], StringComparison.CurrentCultureIgnoreCase));
                return list;
            });
        }

        /// Uygulamanin gercek ikonunu seffaf PNG olarak dondurur.
        public static byte[] IconPng(string id, int size)
        {
            string key = size + "|" + id;
            lock (IconCache)
            {
                byte[] cached;
                if (IconCache.TryGetValue(key, out cached)) return cached;
            }
            byte[] png = RunSta(() =>
            {
                IShellItemImageFactory factory;
                SHCreateItemFromParsingName("shell:AppsFolder\\" + id, IntPtr.Zero,
                    typeof(IShellItemImageFactory).GUID, out factory);
                try
                {
                    IntPtr hbm;
                    int hr = factory.GetImage(new SIZE { cx = size, cy = size }, SIIGBF_ICONONLY | SIIGBF_BIGGERSIZEOK, out hbm);
                    if (hr != 0) Marshal.ThrowExceptionForHR(hr);
                    try { return ToPng(hbm, size); }
                    finally { DeleteObject(hbm); }
                }
                finally { Marshal.FinalReleaseComObject(factory); }
            });
            lock (IconCache) IconCache[key] = png;
            return png;
        }

        static byte[] ToPng(IntPtr hbm, int size)
        {
            var ds = new DIBSECTION();
            GetObject(hbm, Marshal.SizeOf(typeof(DIBSECTION)), ref ds);
            System.Drawing.Bitmap bmp;
            if (ds.dsBm.bmBits != IntPtr.Zero && ds.dsBm.bmBitsPixel == 32)
            {
                // Alfa kanalini korumak icin bitleri dogrudan oku (FromHbitmap alfayi kaybeder)
                using (var raw = new System.Drawing.Bitmap(ds.dsBm.bmWidth, ds.dsBm.bmHeight, ds.dsBm.bmWidthBytes,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb, ds.dsBm.bmBits))
                {
                    bmp = new System.Drawing.Bitmap(raw);
                }
                if (ds.dsBmih.biHeight > 0) bmp.RotateFlip(System.Drawing.RotateFlipType.RotateNoneFlipY);
            }
            else
            {
                bmp = System.Drawing.Image.FromHbitmap(hbm);
            }
            using (bmp)
            {
                System.Drawing.Bitmap outBmp = bmp;
                if (bmp.Width != size || bmp.Height != size)
                {
                    outBmp = new System.Drawing.Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    using (var g = System.Drawing.Graphics.FromImage(outBmp))
                    {
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.DrawImage(bmp, 0, 0, size, size);
                    }
                }
                using (var ms = new MemoryStream())
                {
                    outBmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    if (outBmp != bmp) outBmp.Dispose();
                    return ms.ToArray();
                }
            }
        }

        /// Gelistirme testi: TabDeckAgent.exe --test-apps <klasor>
        public static void Test(string dir)
        {
            var apps = List();
            Console.WriteLine("uygulama sayisi: " + apps.Count);
            Directory.CreateDirectory(dir);
            int ok = 0, fail = 0;
            foreach (var a in apps)
            {
                string name = (string)a["name"];
                try
                {
                    var png = IconPng((string)a["id"], 128);
                    string safe = string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
                    File.WriteAllBytes(Path.Combine(dir, safe + ".png"), png);
                    ok++;
                }
                catch (Exception e)
                {
                    fail++;
                    Console.WriteLine("HATA " + name + ": " + e.Message);
                }
            }
            Console.WriteLine("ikon: " + ok + " basarili, " + fail + " hatali");
        }
    }

    // ---------- OBS WebSocket v5 istemcisi ----------

    class ObsClient
    {
        public string Url, Password;
        ClientWebSocket ws;
        readonly object gate = new object();
        readonly JavaScriptSerializer json = new JavaScriptSerializer();

        public Dictionary<string, object> Request(string type, Dictionary<string, object> data)
        {
            lock (gate)
            {
                try { return RequestOnce(type, data); }
                catch
                {
                    // Baglanti kopmus olabilir: bir kez yeniden dene
                    Close();
                    return RequestOnce(type, data);
                }
            }
        }

        Dictionary<string, object> RequestOnce(string type, Dictionary<string, object> data)
        {
            EnsureConnected();
            string id = Guid.NewGuid().ToString();
            var d = new Dictionary<string, object> { { "requestType", type }, { "requestId", id } };
            if (data != null) d["requestData"] = data;
            SendMsg(6, d);
            while (true)
            {
                var msg = Receive();
                if ((int)msg["op"] != 7) continue; // olaylari atla
                var r = (Dictionary<string, object>)msg["d"];
                if ((string)r["requestId"] != id) continue;
                var st = (Dictionary<string, object>)r["requestStatus"];
                if (!(bool)st["result"])
                {
                    object c;
                    st.TryGetValue("comment", out c);
                    throw new InvalidOperationException("OBS: " + (c ?? st["code"]));
                }
                object rd;
                return r.TryGetValue("responseData", out rd) ? (Dictionary<string, object>)rd : null;
            }
        }

        void EnsureConnected()
        {
            if (ws != null && ws.State == WebSocketState.Open) return;
            Close();
            ws = new ClientWebSocket();
            try { ws.ConnectAsync(new Uri(Url), Timeout(3000)).Wait(); }
            catch { Close(); throw new Exception("OBS'ye baglanilamadi (" + Url + "). OBS acik mi, WebSocket sunucusu etkin mi?"); }

            var hello = (Dictionary<string, object>)Receive()["d"];
            var ident = new Dictionary<string, object> { { "rpcVersion", 1 }, { "eventSubscriptions", 0 } };
            object auth;
            if (hello.TryGetValue("authentication", out auth) && auth != null)
            {
                var a = (Dictionary<string, object>)auth;
                string secret = B64Sha(Password + a["salt"]);
                ident["authentication"] = B64Sha(secret + a["challenge"]);
            }
            SendMsg(1, ident);
            var resp = Receive();
            if ((int)resp["op"] != 2) throw new Exception("OBS kimlik dogrulama basarisiz (sifre?)");
        }

        static string B64Sha(string s)
        {
            using (var sha = SHA256.Create())
                return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(s)));
        }

        static CancellationToken Timeout(int ms) { return new CancellationTokenSource(ms).Token; }

        void SendMsg(int op, object d)
        {
            var bytes = Encoding.UTF8.GetBytes(json.Serialize(new Dictionary<string, object> { { "op", op }, { "d", d } }));
            ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, Timeout(3000)).Wait();
        }

        Dictionary<string, object> Receive()
        {
            var ms = new MemoryStream();
            var buf = new byte[8192];
            WebSocketReceiveResult r;
            do
            {
                var t = ws.ReceiveAsync(new ArraySegment<byte>(buf), Timeout(5000));
                t.Wait();
                r = t.Result;
                if (r.MessageType == WebSocketMessageType.Close) throw new Exception("OBS baglantiyi kapatti");
                ms.Write(buf, 0, r.Count);
            } while (!r.EndOfMessage);
            return json.Deserialize<Dictionary<string, object>>(Encoding.UTF8.GetString(ms.ToArray()));
        }

        void Close()
        {
            if (ws != null) { try { ws.Abort(); ws.Dispose(); } catch { } }
            ws = null;
        }
    }
}
