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
        public bool runAsAdmin = false;   // true = her acilista yonetici olarak yeniden basla
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
            if (args.Length == 1 && args[0] == "--test-audio")
            {
                TestAudio();
                return;
            }
            if (args.Length == 4 && args[0] == "--appvolume")
            {
                // Komut satirindan: TabDeckAgent.exe --appvolume Spotify set 40
                var r = CoreAudio.AppVolume(args[1], args[2], int.Parse(args[3]));
                Console.WriteLine(args[1] + ": %" + r["level"] + " sessiz=" + r["muted"]);
                return;
            }
            if (args.Length == 1 && args[0] == "--test-appaudio")
            {
                // Gelistirme testi: ses karistiricisini listeler; ilk uygulamada +5 / -5 / sessiz x2 dener
                var apps = CoreAudio.ListApps();
                foreach (var app in apps)
                    Console.WriteLine(string.Format("{0,-22} {1,-30} %{2,-4} sessiz={3}", app["name"], app["title"], app["level"], app["muted"]));
                if (apps.Count > 0)
                {
                    string n = (string)apps[0]["name"];
                    Console.WriteLine(n + " +5   -> %" + CoreAudio.AppVolume(n, "up", 5)["level"]);
                    Console.WriteLine(n + " -5   -> %" + CoreAudio.AppVolume(n, "down", 5)["level"]);
                    Console.WriteLine(n + " mute -> " + CoreAudio.AppVolume(n, "mute", 0)["muted"]);
                    Console.WriteLine(n + " mute -> " + CoreAudio.AppVolume(n, "mute", 0)["muted"]);
                }
                return;
            }
            if (args.Length == 1 && args[0] == "--test-foreground")
            {
                Console.WriteLine("engelleyen on plan: " + (Elevation.BlockingForeground() ?? "(yok)"));
                return;
            }
            cfg = LoadConfig();
            Obs.Url = cfg.obsUrl;
            Obs.Password = cfg.obsPassword;

            bool wantAdmin = cfg.runAsAdmin || Array.IndexOf(args, "--admin") >= 0;
            bool admin = Elevation.IsSelfElevated();
            if (wantAdmin && !admin)
            {
                if (Elevation.Relaunch()) return;
                Console.WriteLine("Yonetici izni verilmedi; normal yetkiyle devam ediliyor.");
            }
            Console.Title = admin ? "TabDeck Agent (yonetici)" : "TabDeck Agent";

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
            Console.WriteLine(" Yetki: " + (admin ? "yonetici (yonetici olarak calisan oyunlarda da tus gonderir)"
                : "normal (yonetici olarak calisan oyunlarda kisayollar engellenir)"));
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
                c.token = new Random().Next(10000000, 99999999).ToString();
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

        /// Gelistirme testi: ses seviyesini Core Audio ve WM_APPCOMMAND ile bir adim artirip geri alir.
        static void TestAudio()
        {
            Console.WriteLine("baslangic : " + CoreAudio.Level().ToString("0.00") + " sessiz=" + CoreAudio.Muted());
            CoreAudio.StepUp(); Thread.Sleep(200);
            Console.WriteLine("CoreAudio+: " + CoreAudio.Level().ToString("0.00"));
            CoreAudio.StepDown(); Thread.Sleep(200);
            Console.WriteLine("CoreAudio-: " + CoreAudio.Level().ToString("0.00"));
            AppCommand.Send(AppCommand.VOLUME_UP); Thread.Sleep(500);
            Console.WriteLine("APPCMD +  : " + CoreAudio.Level().ToString("0.00"));
            AppCommand.Send(AppCommand.VOLUME_DOWN); Thread.Sleep(500);
            Console.WriteLine("APPCMD -  : " + CoreAudio.Level().ToString("0.00"));
            CoreAudio.ToggleMute(); Thread.Sleep(200);
            bool m1 = CoreAudio.Muted();
            CoreAudio.ToggleMute(); Thread.Sleep(200);
            Console.WriteLine("sessiz    : " + m1 + " -> " + CoreAudio.Muted());
            Console.WriteLine("yonetici  : " + Elevation.IsSelfElevated());
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
                    string ip = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();
                    if (TokenGuard.IsBlocked(ip))
                    {
                        status = 429;
                        result = Err("Cok fazla hatali token denemesi; 60 sn sonra tekrar deneyin");
                    }
                    else if (tok != cfg.token)
                    {
                        TokenGuard.Fail(ip);
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
                    else if (method == "GET" && path == "/audio/apps")
                        result = new Dictionary<string, object> { { "ok", true }, { "apps", CoreAudio.ListApps() } };
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

        /// Tus gonderildikten sonra: on plandaki pencere yonetici olarak calisiyorsa Windows (UIPI)
        /// tuslari sessizce engeller. Bunu tablete acik bir hata olarak bildir.
        static object InputResult()
        {
            string blocker = Elevation.BlockingForeground();
            if (blocker == null) return Ok();
            return Err(blocker + " yonetici olarak calisiyor; Windows bu yuzden tuslari engelliyor. "
                + "TabDeck Agent'i yonetici olarak baslatin (Baslat > TabDeck Agent (yonetici)).");
        }

        static object RunAction(Dictionary<string, object> a)
        {
            string type = Str(a, "type");
            Console.WriteLine(DateTime.Now.ToString("HH:mm:ss") + "  " + type + "  " + Str(a, "value"));
            switch (type)
            {
                case "hotkey":
                    Input.SendHotkey(Str(a, "value"));
                    return InputResult();
                case "text":
                    Input.TypeText(Str(a, "value"));
                    if (Str(a, "arg") == "enter") Input.SendHotkey("enter");
                    return InputResult();
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
                    {
                        string cmd = Str(a, "value");
                        Input.SendMedia(cmd);
                        return cmd == "volup" || cmd == "voldown" || cmd == "mute" ? (object)CoreAudio.MasterStatus() : Ok();
                    }
                case "appvolume":
                    {
                        int amount;
                        string op = Str(a, "arg");
                        if (!int.TryParse(Str(a, "arg2"), out amount)) amount = op == "set" ? 50 : 5;
                        return CoreAudio.AppVolume(Str(a, "value"), op == "" ? "up" : op, amount);
                    }
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

        /// Medya ve ses tus gondermeden yapilir; boylece yonetici olarak calisan bir oyun
        /// on plandayken de calisir (UIPI yalnizca enjekte edilen tuslari engeller).
        public static void SendMedia(string cmd)
        {
            switch (cmd)
            {
                case "playpause": AppCommand.Send(AppCommand.PLAY_PAUSE); break;
                case "next": AppCommand.Send(AppCommand.NEXT); break;
                case "prev": AppCommand.Send(AppCommand.PREV); break;
                case "stop": AppCommand.Send(AppCommand.STOP); break;
                case "volup": CoreAudio.StepUp(); break;
                case "voldown": CoreAudio.StepDown(); break;
                case "mute": CoreAudio.ToggleMute(); break;
                default: throw new Exception("Bilinmeyen medya komutu: " + cmd);
            }
        }
    }

    // ---------- Token deneme siniri ----------

    /// Ayni IP'den 60 sn icinde 5 hatali token denemesinden sonra 60 sn engeller.
    static class TokenGuard
    {
        class Entry { public int Count; public DateTime WindowStart; public DateTime BlockedUntil; }
        static readonly Dictionary<string, Entry> Entries = new Dictionary<string, Entry>();

        public static bool IsBlocked(string ip)
        {
            lock (Entries)
            {
                Entry e;
                return Entries.TryGetValue(ip, out e) && e.BlockedUntil > DateTime.UtcNow;
            }
        }

        public static void Fail(string ip)
        {
            lock (Entries)
            {
                var now = DateTime.UtcNow;
                Entry e;
                if (!Entries.TryGetValue(ip, out e)) Entries[ip] = e = new Entry { WindowStart = now };
                if (now - e.WindowStart > TimeSpan.FromSeconds(60)) { e.WindowStart = now; e.Count = 0; }
                if (++e.Count >= 5)
                {
                    e.BlockedUntil = now.AddSeconds(60);
                    e.Count = 0;
                    Console.WriteLine(DateTime.Now.ToString("HH:mm:ss") + "  " + ip + " hatali token nedeniyle 60 sn engellendi");
                }
            }
        }
    }

    // ---------- Medya komutlari (WM_APPCOMMAND) ----------

    static class AppCommand
    {
        public const int VOLUME_MUTE = 8, VOLUME_DOWN = 9, VOLUME_UP = 10;
        public const int NEXT = 11, PREV = 12, STOP = 13, PLAY_PAUSE = 14;
        const int WM_APPCOMMAND = 0x0319;
        const int WS_POPUP = unchecked((int)0x80000000);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr CreateWindowEx(int exStyle, string cls, string name, int style,
            int x, int y, int w, int h, IntPtr parent, IntPtr menu, IntPtr inst, IntPtr param);

        [DllImport("user32.dll")]
        static extern bool DestroyWindow(IntPtr h);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);

        /// Kendi gizli penceremize WM_APPCOMMAND gonderir; DefWindowProc bunu shell'e iletir ve
        /// Windows komutu etkin medya oturumuna (Spotify, tarayici...) uygular.
        public static void Send(int command)
        {
            IntPtr h = CreateWindowEx(0, "STATIC", "TabDeck", WS_POPUP, 0, 0, 0, 0,
                IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            if (h == IntPtr.Zero) throw new System.ComponentModel.Win32Exception();
            try { SendMessage(h, WM_APPCOMMAND, h, (IntPtr)(command << 16)); }
            finally { DestroyWindow(h); }
        }
    }

    // ---------- Ana ses seviyesi (Core Audio) ----------

    static class CoreAudio
    {
        [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        class MMDeviceEnumerator { }

        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDeviceEnumerator
        {
            [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IMMDeviceCollection devices);
            [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
        }

        [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDeviceCollection
        {
            [PreserveSig] int GetCount(out uint count);
            [PreserveSig] int Item(uint index, out IMMDevice device);
        }

        [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IAudioSessionManager2
        {
            [PreserveSig] int GetAudioSessionControl(IntPtr guid, int flags, out IntPtr control);
            [PreserveSig] int GetSimpleAudioVolume(IntPtr guid, int flags, out IntPtr volume);
            [PreserveSig] int GetSessionEnumerator(out IAudioSessionEnumerator sessions);
        }

        [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IAudioSessionEnumerator
        {
            [PreserveSig] int GetCount(out int count);
            [PreserveSig] int GetSession(int index, out IAudioSessionControl2 session);
        }

        [ComImport, Guid("bfb7ff88-7239-4fc9-8fa2-07c950be9c6d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IAudioSessionControl2
        {
            // IAudioSessionControl
            [PreserveSig] int GetState(out int state);
            [PreserveSig] int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string name);
            [PreserveSig] int SetDisplayName(string name, ref Guid ctx);
            [PreserveSig] int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string path);
            [PreserveSig] int SetIconPath(string path, ref Guid ctx);
            [PreserveSig] int GetGroupingParam(out Guid param);
            [PreserveSig] int SetGroupingParam(ref Guid param, ref Guid ctx);
            [PreserveSig] int RegisterAudioSessionNotification(IntPtr notify);
            [PreserveSig] int UnregisterAudioSessionNotification(IntPtr notify);
            // IAudioSessionControl2
            [PreserveSig] int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
            [PreserveSig] int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
            [PreserveSig] int GetProcessId(out uint pid);
            [PreserveSig] int IsSystemSoundsSession();
            [PreserveSig] int SetDuckingPreference(bool optOut);
        }

        [ComImport, Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface ISimpleAudioVolume
        {
            [PreserveSig] int SetMasterVolume(float level, ref Guid ctx);
            [PreserveSig] int GetMasterVolume(out float level);
            [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid ctx);
            [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
        }

        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDevice
        {
            [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams,
                [MarshalAs(UnmanagedType.IUnknown)] out object iface);
        }

        [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IAudioEndpointVolume
        {
            [PreserveSig] int RegisterControlChangeNotify(IntPtr notify);
            [PreserveSig] int UnregisterControlChangeNotify(IntPtr notify);
            [PreserveSig] int GetChannelCount(out uint count);
            [PreserveSig] int SetMasterVolumeLevel(float db, ref Guid ctx);
            [PreserveSig] int SetMasterVolumeLevelScalar(float level, ref Guid ctx);
            [PreserveSig] int GetMasterVolumeLevel(out float db);
            [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
            [PreserveSig] int SetChannelVolumeLevel(uint channel, float db, ref Guid ctx);
            [PreserveSig] int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid ctx);
            [PreserveSig] int GetChannelVolumeLevel(uint channel, out float db);
            [PreserveSig] int GetChannelVolumeLevelScalar(uint channel, out float level);
            [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid ctx);
            [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
            [PreserveSig] int GetVolumeStepInfo(out uint step, out uint stepCount);
            [PreserveSig] int VolumeStepUp(ref Guid ctx);
            [PreserveSig] int VolumeStepDown(ref Guid ctx);
        }

        static IAudioEndpointVolume Endpoint()
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            IMMDevice device;
            Check(enumerator.GetDefaultAudioEndpoint(0 /* eRender */, 1 /* eMultimedia */, out device));
            Guid iid = typeof(IAudioEndpointVolume).GUID;
            object o;
            Check(device.Activate(ref iid, 23 /* CLSCTX_ALL */, IntPtr.Zero, out o));
            return (IAudioEndpointVolume)o;
        }

        static void Check(int hr) { if (hr != 0) Marshal.ThrowExceptionForHR(hr); }

        public static void StepUp() { Guid g = Guid.Empty; Check(Endpoint().VolumeStepUp(ref g)); }

        public static void StepDown() { Guid g = Guid.Empty; Check(Endpoint().VolumeStepDown(ref g)); }

        public static void ToggleMute()
        {
            var ep = Endpoint();
            bool muted;
            Check(ep.GetMute(out muted));
            Guid g = Guid.Empty;
            Check(ep.SetMute(!muted, ref g));
        }

        public static float Level()
        {
            float v;
            Check(Endpoint().GetMasterVolumeLevelScalar(out v));
            return v;
        }

        // ----- Uygulama bazli ses (Windows ses karistiricisindaki oturumlar) -----

        class Session
        {
            public string Process;   // ornek: "Spotify"
            public string Title;     // ornek: "Spotify Premium" ya da dosya aciklamasi
            public ISimpleAudioVolume Volume;
        }

        /// Tum etkin cikis aygitlarindaki ses oturumlari (suresi dolmuslar ve sistem sesleri haric).
        static List<Session> Sessions()
        {
            var list = new List<Session>();
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            IMMDeviceCollection devices;
            Check(enumerator.EnumAudioEndpoints(0 /* eRender */, 1 /* DEVICE_STATE_ACTIVE */, out devices));
            uint deviceCount;
            Check(devices.GetCount(out deviceCount));
            Guid iid = typeof(IAudioSessionManager2).GUID;
            for (uint d = 0; d < deviceCount; d++)
            {
                IMMDevice device;
                if (devices.Item(d, out device) != 0) continue;
                object o;
                if (device.Activate(ref iid, 23, IntPtr.Zero, out o) != 0) continue;
                IAudioSessionEnumerator sessions;
                if (((IAudioSessionManager2)o).GetSessionEnumerator(out sessions) != 0) continue;
                int count;
                sessions.GetCount(out count);
                for (int i = 0; i < count; i++)
                {
                    IAudioSessionControl2 ctl;
                    if (sessions.GetSession(i, out ctl) != 0) continue;
                    int state;
                    ctl.GetState(out state);
                    if (state == 2 /* Expired */ || ctl.IsSystemSoundsSession() == 0) continue;
                    uint pid;
                    ctl.GetProcessId(out pid);
                    if (pid == 0) continue;
                    string name, title = null;
                    try
                    {
                        var p = System.Diagnostics.Process.GetProcessById((int)pid);
                        name = p.ProcessName;
                        try { title = p.MainModule.FileVersionInfo.FileDescription; } catch { }
                    }
                    catch { continue; }
                    string display;
                    ctl.GetDisplayName(out display);
                    if (!string.IsNullOrEmpty(display) && !display.StartsWith("@")) title = display;
                    list.Add(new Session { Process = name, Title = string.IsNullOrEmpty(title) ? name : title, Volume = (ISimpleAudioVolume)ctl });
                }
            }
            return list;
        }

        /// Ses karistiricisindaki uygulamalar: [{name, title, level, muted}] (ayni surec bir kez).
        public static List<Dictionary<string, object>> ListApps()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<Dictionary<string, object>>();
            foreach (var s in Sessions())
            {
                if (!seen.Add(s.Process)) continue;
                float level;
                bool muted;
                s.Volume.GetMasterVolume(out level);
                s.Volume.GetMute(out muted);
                result.Add(new Dictionary<string, object> {
                    { "name", s.Process }, { "title", s.Title },
                    { "level", (int)Math.Round(level * 100) }, { "muted", muted } });
            }
            result.Sort((a, b) => string.Compare((string)a["title"], (string)b["title"], StringComparison.CurrentCultureIgnoreCase));
            return result;
        }

        /// Bir uygulamanin (surec adi) tum ses oturumlarina uygular. op: up | down | mute | set.
        /// amount: up/down icin yuzde adim, set icin hedef yuzde. Yeni seviye ve sessiz durumu doner.
        public static Dictionary<string, object> AppVolume(string process, string op, int amount)
        {
            process = process.Trim();
            if (process.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) process = process.Substring(0, process.Length - 4);
            var matches = Sessions().FindAll(s => string.Equals(s.Process, process, StringComparison.OrdinalIgnoreCase));
            if (matches.Count == 0)
                throw new Exception(process + " su an ses karistiricisinda yok (uygulama kapali ya da hic ses calmadi)");

            float level;
            bool muted;
            matches[0].Volume.GetMasterVolume(out level);
            matches[0].Volume.GetMute(out muted);
            switch (op)
            {
                case "up": level = Math.Min(1f, level + amount / 100f); muted = false; break;
                case "down": level = Math.Max(0f, level - amount / 100f); break;
                case "set": level = Math.Max(0f, Math.Min(1f, amount / 100f)); muted = false; break;
                case "mute": muted = !muted; break;
                default: throw new Exception("Bilinmeyen islem: " + op);
            }
            Guid g = Guid.Empty;
            foreach (var s in matches)
            {
                if (op != "mute") Check(s.Volume.SetMasterVolume(level, ref g));
                Check(s.Volume.SetMute(muted, ref g));
            }
            return new Dictionary<string, object> { { "ok", true }, { "level", (int)Math.Round(level * 100) }, { "muted", muted } };
        }

        /// Ana ses seviyesi bilgisi (tablet butonda gosterir).
        public static Dictionary<string, object> MasterStatus()
        {
            return new Dictionary<string, object> { { "ok", true }, { "level", (int)Math.Round(Level() * 100) }, { "muted", Muted() } };
        }

        public static bool Muted()
        {
            bool m;
            Check(Endpoint().GetMute(out m));
            return m;
        }
    }

    // ---------- Yonetici yetkisi ----------

    static class Elevation
    {
        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr h, out int pid);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr OpenProcess(uint access, bool inherit, int pid);

        [DllImport("kernel32.dll")]
        static extern bool CloseHandle(IntPtr h);

        [DllImport("advapi32.dll", SetLastError = true)]
        static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);

        [DllImport("advapi32.dll", SetLastError = true)]
        static extern bool GetTokenInformation(IntPtr token, int cls, out int value, int len, out int ret);

        const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000, TOKEN_QUERY = 0x8;
        const int TokenElevation = 20;

        public static bool IsSelfElevated()
        {
            using (var id = System.Security.Principal.WindowsIdentity.GetCurrent())
                return new System.Security.Principal.WindowsPrincipal(id)
                    .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }

        /// Agent normal yetkideyken on plandaki pencere yonetici olarak calisiyorsa o programin adini dondurur.
        public static string BlockingForeground()
        {
            if (IsSelfElevated()) return null;
            int pid;
            GetWindowThreadProcessId(GetForegroundWindow(), out pid);
            if (pid == 0) return null;
            bool elevated;
            IntPtr h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (h == IntPtr.Zero) elevated = true; // korunan surec (anti-hile vb.)
            else
            {
                IntPtr token;
                if (!OpenProcessToken(h, TOKEN_QUERY, out token)) elevated = true;
                else
                {
                    int value, ret;
                    elevated = GetTokenInformation(token, TokenElevation, out value, 4, out ret) && value != 0;
                    CloseHandle(token);
                }
                CloseHandle(h);
            }
            if (!elevated) return null;
            try { return Process.GetProcessById(pid).ProcessName + ".exe"; }
            catch { return "On plandaki program"; }
        }

        /// Kendini yonetici olarak yeniden baslatir. UAC reddedilirse false.
        public static bool Relaunch()
        {
            var psi = new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName, "--admin")
            {
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
            };
            try { Process.Start(psi); return true; }
            catch (System.ComponentModel.Win32Exception) { return false; }
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
