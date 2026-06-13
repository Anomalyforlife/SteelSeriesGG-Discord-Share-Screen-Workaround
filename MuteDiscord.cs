using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

// ---- COM interfaces ----

[ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
class MMDeviceEnumeratorCoClass { }

[ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMMDeviceEnumerator {
    [PreserveSig] int EnumAudioEndpoints(int dataFlow, uint stateMask,
        [MarshalAs(UnmanagedType.Interface)] out IMMDeviceCollection ppDevices);
    [PreserveSig] int GetDefaultAudioEndpoint(int flow, int role, IntPtr ppDevice);
    [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr ppDevice);
    [PreserveSig] int RegisterEndpointNotificationCallback(IntPtr p);
    [PreserveSig] int UnregisterEndpointNotificationCallback(IntPtr p);
}

[ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMMDeviceCollection {
    [PreserveSig] int GetCount(out uint pcDevices);
    [PreserveSig] int Item(uint nDevice, [MarshalAs(UnmanagedType.Interface)] out IMMDevice ppDevice);
}

[ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMMDevice {
    [PreserveSig] int Activate(ref Guid iid, uint clsCtx, IntPtr pParams,
        [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
    [PreserveSig] int OpenPropertyStore(uint access,
        [MarshalAs(UnmanagedType.Interface)] out IPropertyStore ppProp);
    [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string ppstrId);
    [PreserveSig] int GetState(out uint dwState);
}

[ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IPropertyStore {
    [PreserveSig] int GetCount(out uint cProps);
    [PreserveSig] int GetAt(uint i, out PropertyKey key);
    [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant pv);
    [PreserveSig] int SetValue(ref PropertyKey key, ref PropVariant pv);
    [PreserveSig] int Commit();
}

[StructLayout(LayoutKind.Sequential)]
struct PropertyKey { public Guid fmtid; public uint pid; }

[StructLayout(LayoutKind.Sequential)]
struct PropVariant { public short vt; short r1, r2, r3; public IntPtr p; int p2; }

[ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IAudioSessionManager2 {
    [PreserveSig] int GetAudioSessionControl(IntPtr guid, uint flags, IntPtr ppControl);
    [PreserveSig] int GetSimpleAudioVolume(IntPtr guid, uint flags, IntPtr ppVolume);
    [PreserveSig] int GetSessionEnumerator(
        [MarshalAs(UnmanagedType.Interface)] out IAudioSessionEnumerator ppEnum);
}

[ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IAudioSessionEnumerator {
    [PreserveSig] int GetCount(out int count);
    [PreserveSig] int GetSession(int index,
        [MarshalAs(UnmanagedType.Interface)] out IAudioSessionControl2 ppSession);
}

[ComImport, Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IAudioSessionControl2 {
    [PreserveSig] int GetState(out int state);
    [PreserveSig] int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string name);
    [PreserveSig] int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string name, ref Guid ctx);
    [PreserveSig] int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string path);
    [PreserveSig] int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string path, ref Guid ctx);
    [PreserveSig] int GetGroupingParam(out Guid param);
    [PreserveSig] int SetGroupingParam(ref Guid param, ref Guid ctx);
    [PreserveSig] int RegisterAudioSessionNotification(IntPtr notify);
    [PreserveSig] int UnregisterAudioSessionNotification(IntPtr notify);
    [PreserveSig] int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
    [PreserveSig] int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
    [PreserveSig] int GetProcessId(out uint pid);
    [PreserveSig] int IsSystemSoundsSession();
    [PreserveSig] int SetDuckingPreference([MarshalAs(UnmanagedType.Bool)] bool optOut);
}

[ComImport, Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface ISimpleAudioVolume {
    [PreserveSig] int SetMasterVolume(float level, ref Guid ctx);
    [PreserveSig] int GetMasterVolume(out float level);
    [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid ctx);
    [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
}

// ---- Device editor form ----

class DeviceEditorForm : Form {
    public List<string> Devices { get; private set; }

    public DeviceEditorForm(List<string> current) {
        this.Text = "MuteDiscord — Modifica dispositivi";
        this.Width = 560;
        this.Height = 320;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.StartPosition = FormStartPosition.CenterScreen;
        this.BackColor = Color.FromArgb(30, 30, 30);
        this.ForeColor = Color.White;
        this.Font = new Font("Segoe UI", 9f);

        try {
            string iconPath = Path.Combine(
                Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "assets", "icon.png");
            if (File.Exists(iconPath))
                using (var bmp = new Bitmap(iconPath))
                    this.Icon = Icon.FromHandle(bmp.GetHicon());
        } catch { }

        var label = new Label {
            Text = "Dispositivi monitorati (uno per riga):",
            Top = 12, Left = 12, Width = 520, ForeColor = Color.White
        };

        var textBox = new TextBox {
            Multiline = true, ScrollBars = ScrollBars.Vertical,
            Top = 34, Left = 12, Width = 520, Height = 180,
            BackColor = Color.FromArgb(45, 45, 45), ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Text = string.Join(Environment.NewLine, current)
        };

        var btnSave = new Button {
            Text = "Salva", Top = 228, Left = 408, Width = 124, Height = 32,
            BackColor = Color.FromArgb(88, 101, 242), ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btnSave.FlatAppearance.BorderSize = 0;

        var btnCancel = new Button {
            Text = "Annulla", Top = 228, Left = 276, Width = 124, Height = 32,
            BackColor = Color.FromArgb(60, 60, 60), ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btnCancel.FlatAppearance.BorderSize = 0;

        btnSave.Click += (s, e) => {
            var lines = textBox.Text
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            var trimmed = new List<string>();
            foreach (var l in lines) { var t = l.Trim(); if (t.Length > 0) trimmed.Add(t); }
            if (trimmed.Count < 2) {
                MessageBox.Show("Inserisci almeno 2 dispositivi.", "MuteDiscord",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Devices = trimmed;
            this.DialogResult = DialogResult.OK;
            this.Close();
        };

        btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };

        this.Controls.AddRange(new Control[] { label, textBox, btnSave, btnCancel });
    }
}

// ---- Main app ----

class MuteDiscordApp : ApplicationContext {
    static readonly Guid IID_IAudioSessionManager2 = new Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F");
    static readonly PropertyKey PKEY_FriendlyName = new PropertyKey {
        fmtid = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), pid = 14
    };

    NotifyIcon trayIcon;
    string configPath;
    List<string> devices = new List<string>();
    readonly HashSet<string> mutedSessions = new HashSet<string>();
    readonly object lck = new object();
    Thread pollThread;
    volatile bool running = true;

    public MuteDiscordApp() {
        string exeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        configPath = Path.Combine(exeDir, "config.json");
        LoadConfig();

        Icon trayIco = LoadIcon(exeDir);

        var menuDevices = new MenuItem("Modifica dispositivi", OnEditDevices);
        var menuSep     = new MenuItem("-");
        var menuExit    = new MenuItem("Esci", OnExit);

        trayIcon = new NotifyIcon {
            Icon    = trayIco,
            Text    = "MuteDiscord — in ascolto...",
            Visible = true,
            ContextMenu = new ContextMenu(new[] { menuDevices, menuSep, menuExit })
        };

        pollThread = new Thread(PollLoop) { IsBackground = true };
        pollThread.Start();
    }

    static Icon LoadIcon(string exeDir) {
        try {
            string pngPath = Path.Combine(exeDir, "assets", "icon.png");
            if (File.Exists(pngPath))
                using (var bmp = new Bitmap(pngPath))
                    return Icon.FromHandle(bmp.GetHicon());
        } catch { }
        return SystemIcons.Application;
    }

    void LoadConfig() {
        if (!File.Exists(configPath)) return;
        string json = File.ReadAllText(configPath, Encoding.UTF8);
        int s = json.IndexOf('['), e = json.LastIndexOf(']');
        if (s < 0 || e < 0) return;
        string arr = json.Substring(s + 1, e - s - 1);
        devices.Clear();
        int pos = 0;
        while (pos < arr.Length) {
            int q1 = arr.IndexOf('"', pos); if (q1 < 0) break;
            int q2 = arr.IndexOf('"', q1 + 1); if (q2 < 0) break;
            string d = arr.Substring(q1 + 1, q2 - q1 - 1);
            if (d.Length > 0) devices.Add(d);
            pos = q2 + 1;
        }
    }

    void SaveConfig() {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("  \"devices\": [");
        for (int i = 0; i < devices.Count; i++) {
            string comma = i < devices.Count - 1 ? "," : "";
            sb.AppendLine("    \"" + devices[i].Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"" + comma);
        }
        sb.AppendLine("  ]");
        sb.AppendLine("}");
        File.WriteAllText(configPath, sb.ToString(), Encoding.UTF8);
    }

    void OnEditDevices(object sender, EventArgs e) {
        var form = new DeviceEditorForm(new List<string>(devices));
        if (form.ShowDialog() == DialogResult.OK) {
            devices = form.Devices;
            SaveConfig();
            lock (lck) { mutedSessions.Clear(); }
        }
    }

    void OnExit(object sender, EventArgs e) {
        running = false;
        trayIcon.Visible = false;
        trayIcon.Dispose();
        Application.Exit();
    }

    // ---- Audio polling ----

    string GetFriendlyName(IMMDevice dev) {
        IPropertyStore store;
        if (dev.OpenPropertyStore(0, out store) != 0) return "";
        var key = PKEY_FriendlyName; PropVariant pv;
        if (store.GetValue(ref key, out pv) != 0) { Marshal.ReleaseComObject(store); return ""; }
        string name = (pv.vt == 31 && pv.p != IntPtr.Zero) ? Marshal.PtrToStringUni(pv.p) : "";
        Marshal.ReleaseComObject(store);
        return name ?? "";
    }

    IMMDevice FindDevice(string deviceName) {
        var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorCoClass();
        IMMDeviceCollection col;
        int hr = enumerator.EnumAudioEndpoints(0, 1, out col);
        Marshal.ReleaseComObject(enumerator);
        if (hr != 0) return null;
        uint count; col.GetCount(out count);
        IMMDevice matched = null;
        for (uint i = 0; i < count; i++) {
            IMMDevice dev; col.Item(i, out dev);
            if (GetFriendlyName(dev) == deviceName) { matched = dev; break; }
            Marshal.ReleaseComObject(dev);
        }
        Marshal.ReleaseComObject(col);
        return matched;
    }

    void CheckAndMuteDevice(string deviceName, int[] discordPids) {
        var dev = FindDevice(deviceName);
        if (dev == null) return;

        object mgr2obj;
        var iidMgr = IID_IAudioSessionManager2;
        dev.Activate(ref iidMgr, 1, IntPtr.Zero, out mgr2obj);
        var mgr = (IAudioSessionManager2)mgr2obj;

        IAudioSessionEnumerator sessEnum;
        mgr.GetSessionEnumerator(out sessEnum);
        int sessCount; sessEnum.GetCount(out sessCount);
        var ctx = Guid.Empty;

        for (int s = 0; s < sessCount; s++) {
            IAudioSessionControl2 sess;
            sessEnum.GetSession(s, out sess);
            try {
                uint pid; if (sess.GetProcessId(out pid) != 0) continue;
                bool isDiscord = false;
                foreach (int dp in discordPids) if ((uint)dp == pid) { isDiscord = true; break; }
                if (!isDiscord) continue;

                string instanceId;
                if (sess.GetSessionInstanceIdentifier(out instanceId) != 0) instanceId = pid.ToString();
                string sessionKey = deviceName + "|" + instanceId;
                lock (lck) { if (mutedSessions.Contains(sessionKey)) continue; }

                var vol = (ISimpleAudioVolume)sess;
                bool already; vol.GetMute(out already);
                if (!already) vol.SetMute(true, ref ctx);
                lock (lck) { mutedSessions.Add(sessionKey); }
            } finally {
                Marshal.ReleaseComObject(sess);
            }
        }

        Marshal.ReleaseComObject(sessEnum);
        Marshal.ReleaseComObject(mgr);
        Marshal.ReleaseComObject(dev);
    }

    void PollLoop() {
        while (running) {
            try {
                var procs = Process.GetProcessesByName("discord");
                int[] pids = Array.ConvertAll(procs, p => p.Id);
                if (pids.Length == 0) {
                    lock (lck) { mutedSessions.Clear(); }
                } else {
                    List<string> snapshot;
                    lock (lck) { snapshot = new List<string>(devices); }
                    foreach (var d in snapshot) {
                        try { CheckAndMuteDevice(d, pids); } catch { }
                    }
                }
            } catch { }
            Thread.Sleep(500);
        }
    }
}

class Program {
    [STAThread]
    static void Main() {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MuteDiscordApp());
    }
}
