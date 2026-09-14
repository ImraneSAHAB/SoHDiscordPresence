using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace SOHDiscordPresence
{
    public class Config
    {
        public string client_id = "1549071331797114930";
        public string process_name = "soh";
        public string details = "The Legend of Zelda: Ocarina of Time";
        public string state = "Playing Ship of Harkinian";
        public string large_image = "https://i.imgur.com/uSWG02r.png";
        public string large_text = "Ocarina of Time";
        public string small_image = "https://i.imgur.com/Bor9Gtd.png";
        public string small_text = "Made by Imashiro";
        public bool hide_console = true;
        public int check_interval_seconds = 3;

        public static Config Load(string path)
        {
            Config config = new Config();
            if (!File.Exists(path)) return config;
            try
            {
                string json = File.ReadAllText(path);
                string val;
                
                val = ExtractJsonValue(json, "client_id");
                if (val != null) config.client_id = val;

                val = ExtractJsonValue(json, "process_name");
                if (val != null) config.process_name = val;

                val = ExtractJsonValue(json, "details");
                if (val != null) config.details = val;

                val = ExtractJsonValue(json, "state");
                if (val != null) config.state = val;

                val = ExtractJsonValue(json, "large_image");
                if (val != null) config.large_image = val;

                val = ExtractJsonValue(json, "large_text");
                if (val != null) config.large_text = val;

                val = ExtractJsonValue(json, "small_image");
                if (val != null) config.small_image = val;

                val = ExtractJsonValue(json, "small_text");
                if (val != null) config.small_text = val;

                val = ExtractJsonValue(json, "hide_console");
                if (val != null) bool.TryParse(val, out config.hide_console);

                val = ExtractJsonValue(json, "check_interval_seconds");
                if (val != null) int.TryParse(val, out config.check_interval_seconds);
            }
            catch (Exception ex)
            {
                Log("Config error: " + ex.Message);
            }
            return config;
        }

        private static string ExtractJsonValue(string json, string key)
        {
            int kIdx = json.IndexOf("\"" + key + "\"");
            if (kIdx == -1) return null;
            int colonIdx = json.IndexOf(':', kIdx);
            if (colonIdx == -1) return null;
            int start = colonIdx + 1;
            while (start < json.Length && (json[start] == ' ' || json[start] == '\t' || json[start] == '\r' || json[start] == '\n')) start++;
            if (start >= json.Length) return null;
            if (json[start] == '"')
            {
                int end = json.IndexOf('"', start + 1);
                if (end == -1) return null;
                return json.Substring(start + 1, end - start - 1);
            }
            else
            {
                int end = start;
                while (end < json.Length && json[end] != ',' && json[end] != '}' && json[end] != '\r' && json[end] != '\n') end++;
                return json.Substring(start, end - start).Trim();
            }
        }

        public static void Log(string msg)
        {
            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug.log");
                File.AppendAllText(logPath, string.Format("[{0}] {1}\r\n", DateTime.Now.ToString("HH:mm:ss"), msg));
            }
            catch { }
        }
    }

    public class MemoryReader
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out IntPtr lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll")]
        public static extern int VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);

        [StructLayout(LayoutKind.Sequential)]
        public struct MEMORY_BASIC_INFORMATION
        {
            public IntPtr BaseAddress;
            public IntPtr AllocationBase;
            public uint AllocationProtect;
            public IntPtr RegionSize;
            public uint State;
            public uint Protect;
            public uint Type;
        }

        public const uint PROCESS_VM_READ = 0x0010;
        public const uint PROCESS_QUERY_INFORMATION = 0x0400;
        public const uint MEM_COMMIT = 0x1000;
        public const uint PAGE_READWRITE = 0x04;
        public const uint PAGE_EXECUTE_READWRITE = 0x40;

        private IntPtr hProcess = IntPtr.Zero;
        private int processId = 0;
        private IntPtr saveContextAddr = IntPtr.Zero;

        public bool IsConnected
        {
            get { return hProcess != IntPtr.Zero; }
        }

        public bool Attach(string processName)
        {
            Process[] processes = Process.GetProcessesByName(processName);
            if (processes.Length == 0)
            {
                Detach();
                return false;
            }

            Process proc = processes[0];
            if (hProcess == IntPtr.Zero || processId != proc.Id)
            {
                Detach();
                processId = proc.Id;
                hProcess = OpenProcess(PROCESS_VM_READ | PROCESS_QUERY_INFORMATION, false, processId);
                if (hProcess == IntPtr.Zero) return false;
            }

            IntPtr mainBase = IntPtr.Zero;
            long mainSize = 0;
            try
            {
                ProcessModule mainMod = proc.MainModule;
                if (mainMod != null)
                {
                    mainBase = mainMod.BaseAddress;
                    mainSize = mainMod.ModuleMemorySize;
                }
            }
            catch { }

            saveContextAddr = FindSaveContext(mainBase, mainSize);
            return hProcess != IntPtr.Zero;
        }

        public void Detach()
        {
            if (hProcess != IntPtr.Zero)
            {
                CloseHandle(hProcess);
                hProcess = IntPtr.Zero;
            }
            processId = 0;
            saveContextAddr = IntPtr.Zero;
        }

        private bool VerifySaveContext(IntPtr addr)
        {
            byte[] buf = new byte[0x40];
            IntPtr read;
            if (ReadProcessMemory(hProcess, addr, buf, buf.Length, out read) && read.ToInt64() >= 0x38)
            {
                int linkAge = BitConverter.ToInt32(buf, 0x04);
                short hpCap = BitConverter.ToInt16(buf, 0x2E);
                short hp = BitConverter.ToInt16(buf, 0x30);
                if ((linkAge == 0 || linkAge == 1) && (hpCap >= 48 && hpCap <= 480) && (hpCap % 16 == 0) && (hp >= 0 && hp <= hpCap))
                {
                    return true;
                }
            }
            return false;
        }

        private IntPtr FindSaveContext(IntPtr mainBase, long mainSize)
        {
            if (mainBase != IntPtr.Zero)
            {
                IntPtr fixedAddr = new IntPtr(mainBase.ToInt64() + 0x258AB40);
                if (VerifySaveContext(fixedAddr)) return fixedAddr;
            }

            IntPtr address = IntPtr.Zero;
            MEMORY_BASIC_INFORMATION memInfo;
            uint sizeOfMemInfo = (uint)Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION));

            IntPtr bestAddr = IntPtr.Zero;
            int bestScore = -1;

            long mainStart = mainBase != IntPtr.Zero ? mainBase.ToInt64() : 0;
            long mainEnd = mainStart > 0 ? mainStart + mainSize : 0;

            while (VirtualQueryEx(hProcess, address, out memInfo, sizeOfMemInfo) != 0)
            {
                if (memInfo.State == MEM_COMMIT && (memInfo.Protect == PAGE_READWRITE || memInfo.Protect == PAGE_EXECUTE_READWRITE))
                {
                    long regionSize = memInfo.RegionSize.ToInt64();
                    if (regionSize > 0 && regionSize <= 64 * 1024 * 1024)
                    {
                        byte[] buffer = new byte[(int)regionSize];
                        IntPtr bytesRead;
                        if (ReadProcessMemory(hProcess, memInfo.BaseAddress, buffer, buffer.Length, out bytesRead))
                        {
                            int readSize = (int)bytesRead.ToInt64();
                            for (int i = 0; i <= readSize - 0x40; i += 4)
                            {
                                long mAddr = memInfo.BaseAddress.ToInt64() + i;

                                int linkAge = BitConverter.ToInt32(buffer, i + 0x04);
                                short hpCap = BitConverter.ToInt16(buffer, i + 0x2E);
                                short hp = BitConverter.ToInt16(buffer, i + 0x30);

                                if ((linkAge == 0 || linkAge == 1) && (hpCap >= 48 && hpCap <= 480) && (hpCap % 16 == 0) && (hp >= 0 && hp <= hpCap))
                                {
                                    int score = 10;
                                    if (mainStart > 0 && mAddr >= mainStart && mAddr < mainEnd) score += 500;
                                    if (hpCap >= 48 && hpCap <= 320) score += 200;

                                    if (score > bestScore)
                                    {
                                        bestScore = score;
                                        bestAddr = new IntPtr(mAddr);
                                    }
                                }
                            }
                        }
                    }
                }
                long nextAddr = memInfo.BaseAddress.ToInt64() + memInfo.RegionSize.ToInt64();
                if (nextAddr <= address.ToInt64()) break;
                address = new IntPtr(nextAddr);
            }

            return bestAddr;
        }

        public bool ReadSaveData(out string ageText, out string heartsText, Config config)
        {
            ageText = null;
            heartsText = null;

            if (!IsConnected || saveContextAddr == IntPtr.Zero) return false;
            if (!VerifySaveContext(saveContextAddr))
            {
                saveContextAddr = FindSaveContext(IntPtr.Zero, 0);
                if (saveContextAddr == IntPtr.Zero) return false;
            }

            byte[] data = new byte[0x40];
            IntPtr read;
            if (!ReadProcessMemory(hProcess, saveContextAddr, data, data.Length, out read) || read.ToInt64() < data.Length)
            {
                return false;
            }

            int entrance = BitConverter.ToInt32(data, 0x00);
            int linkAge = BitConverter.ToInt32(data, 0x04);
            int cutscene = BitConverter.ToInt32(data, 0x08);
            short healthCap = BitConverter.ToInt16(data, 0x2E);
            short health = BitConverter.ToInt16(data, 0x30);

            // Detect Title Screen / Main Menu / File Select / Opening Demo
            if (entrance == 0x00CD || (cutscene & 0xFFFF) >= 0xFFF0 || entrance == 0x00A5)
            {
                ageText = "Main Menu";
                heartsText = "Title Screen";
                return true;
            }

            int maxHearts = healthCap / 16;
            double curHearts = Math.Ceiling((double)health / 4.0) * 0.25;
            if (maxHearts <= 0 || maxHearts > 30) maxHearts = 3;
            if (curHearts < 0 || curHearts > maxHearts) curHearts = 0;

            if (linkAge == 0)
            {
                ageText = "Playing as Adult Link";
            }
            else if (linkAge == 1)
            {
                ageText = "Playing as Child Link";
            }
            else
            {
                ageText = "Playing as Link";
            }

            if (health <= 0)
            {
                heartsText = string.Format("💀 Game Over (0/{0})", maxHearts);
            }
            else
            {
                heartsText = string.Format("❤️ {0}/{1}", curHearts.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture), maxHearts);
            }

            return true;
        }
    }

    public class DiscordClient : IDisposable
    {
        private NamedPipeClientStream pipe;
        private string clientId;
        private long startTime;

        public DiscordClient(string clientId)
        {
            this.clientId = clientId;
            this.startTime = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        }

        public bool Connect()
        {
            if (pipe != null && pipe.IsConnected) return true;

            for (int i = 0; i < 10; i++)
            {
                try
                {
                    pipe = new NamedPipeClientStream(".", "discord-ipc-" + i, PipeDirection.InOut);
                    pipe.Connect(500);
                    Handshake();
                    return true;
                }
                catch
                {
                    if (pipe != null)
                    {
                        pipe.Dispose();
                        pipe = null;
                    }
                }
            }
            return false;
        }

        private void Handshake()
        {
            string payload = "{\"v\":1,\"client_id\":\"" + clientId + "\"}";
            WriteFrame(0, payload);
            ReadFrame();
        }

        public void UpdatePresence(string details, string state, string largeImg, string largeTxt, string smallImg, string smallTxt)
        {
            if (!Connect()) return;

            string json = "{"
                + "\"cmd\":\"SET_ACTIVITY\","
                + "\"args\":{"
                + "\"pid\":" + Process.GetCurrentProcess().Id + ","
                + "\"activity\":{"
                + "\"details\":\"" + EscapeJson(details) + "\","
                + "\"state\":\"" + EscapeJson(state) + "\","
                + "\"timestamps\":{\"start\":" + startTime + "},"
                + "\"assets\":{"
                + "\"large_image\":\"" + EscapeJson(largeImg) + "\","
                + "\"large_text\":\"" + EscapeJson(largeTxt) + "\","
                + "\"small_image\":\"" + EscapeJson(smallImg) + "\","
                + "\"small_text\":\"" + EscapeJson(smallTxt) + "\""
                + "}"
                + "}"
                + "},"
                + "\"nonce\":\"" + Guid.NewGuid().ToString() + "\""
                + "}";

            try
            {
                WriteFrame(1, json);
                ReadFrame();
            }
            catch
            {
                if (pipe != null)
                {
                    pipe.Dispose();
                    pipe = null;
                }
            }
        }

        public void ClearPresence()
        {
            if (pipe == null || !pipe.IsConnected) return;

            string json = "{"
                + "\"cmd\":\"SET_ACTIVITY\","
                + "\"args\":{"
                + "\"pid\":" + Process.GetCurrentProcess().Id + ","
                + "\"activity\":null"
                + "},"
                + "\"nonce\":\"" + Guid.NewGuid().ToString() + "\""
                + "}";

            try
            {
                WriteFrame(1, json);
                ReadFrame();
            }
            catch
            {
                if (pipe != null)
                {
                    pipe.Dispose();
                    pipe = null;
                }
            }
        }

        private void WriteFrame(int opcode, string json)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            byte[] header = new byte[8];
            BitConverter.GetBytes(opcode).CopyTo(header, 0);
            BitConverter.GetBytes(bytes.Length).CopyTo(header, 4);

            pipe.Write(header, 0, 8);
            pipe.Write(bytes, 0, bytes.Length);
            pipe.Flush();
        }

        private void ReadFrame()
        {
            byte[] header = new byte[8];
            int read = pipe.Read(header, 0, 8);
            if (read < 8) return;
            int length = BitConverter.ToInt32(header, 4);
            byte[] buffer = new byte[length];
            int total = 0;
            while (total < length)
            {
                int r = pipe.Read(buffer, total, length - total);
                if (r <= 0) break;
                total += r;
            }
        }

        private string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }

        public void Dispose()
        {
            if (pipe != null)
            {
                pipe.Dispose();
                pipe = null;
            }
        }
    }

    static class Program
    {
        private static NotifyIcon trayIcon;
        private static MemoryReader memReader = new MemoryReader();
        private static DiscordClient discord;
        private static System.Threading.Timer updateTimer;
        private static Config config;
        private static string configPath;
        private static string currentClientId = null;

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        [STAThread]
        static void Main()
        {
            Application.ThreadException += (s, e) => Config.Log("Thread error: " + e.Exception.Message);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => Config.Log("Unhandled error: " + e.ExceptionObject.ToString());

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
            config = Config.Load(configPath);
            Config.Log("Starting SOHDiscordPresence...");

            if (config.hide_console)
            {
                IntPtr handle = GetConsoleWindow();
                if (handle != IntPtr.Zero)
                {
                    ShowWindow(handle, 0);
                }
            }

            currentClientId = config.client_id;
            discord = new DiscordClient(config.client_id);

            ContextMenu menu = new ContextMenu();
            MenuItem titleItem = new MenuItem("SoH Discord Presence (Ship of Harkinian)");
            titleItem.Enabled = false;
            menu.MenuItems.Add(titleItem);
            menu.MenuItems.Add("-");
            menu.MenuItems.Add("Quitter", (s, e) => {
                trayIcon.Visible = false;
                Application.Exit();
            });

            Icon appIcon = null;
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
            if (File.Exists(iconPath))
            {
                try { appIcon = new Icon(iconPath); } catch { }
            }
            if (appIcon == null) appIcon = SystemIcons.Application;

            trayIcon = new NotifyIcon
            {
                Icon = appIcon,
                Text = "SoH Discord Presence",
                ContextMenu = menu,
                Visible = true
            };

            updateTimer = new System.Threading.Timer(OnTimerTick, null, 0, config.check_interval_seconds * 1000);

            Application.Run();
        }

        private static void OnTimerTick(object state)
        {
            try
            {
                config = Config.Load(configPath);

                if (discord == null || currentClientId != config.client_id)
                {
                    if (discord != null) discord.Dispose();
                    currentClientId = config.client_id;
                    discord = new DiscordClient(config.client_id);
                }

                Process[] procs = Process.GetProcessesByName(config.process_name);
                if (procs.Length == 0)
                {
                    if (discord != null) discord.ClearPresence();
                    memReader.Detach();
                    return;
                }

                bool attached = memReader.Attach(config.process_name);

                string details = config.details;
                string presenceState = config.state;

                if (attached)
                {
                    string ageText, heartsText;
                    if (memReader.ReadSaveData(out ageText, out heartsText, config))
                    {
                        details = ageText;
                        presenceState = heartsText;
                    }
                }

                discord.UpdatePresence(
                    details,
                    presenceState,
                    config.large_image,
                    config.large_text,
                    config.small_image,
                    config.small_text
                );
            }
            catch (Exception ex)
            {
                Config.Log("Timer tick error: " + ex.Message);
            }
        }
    }
}
