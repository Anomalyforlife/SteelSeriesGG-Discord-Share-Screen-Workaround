// Windows-only application — platform warnings suppressed
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

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
    [PreserveSig] int GetCount(out uint count);
    [PreserveSig] int Item(uint n, [MarshalAs(UnmanagedType.Interface)] out IMMDevice ppDevice);
}

[ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMMDevice {
    [PreserveSig] int Activate(ref Guid iid, uint ctx, IntPtr p, [MarshalAs(UnmanagedType.IUnknown)] out object ppv);
    [PreserveSig] int OpenPropertyStore(uint access, [MarshalAs(UnmanagedType.Interface)] out IPropertyStore ppProp);
    [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
    [PreserveSig] int GetState(out uint state);
}

[ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IPropertyStore {
    [PreserveSig] int GetCount(out uint c);
    [PreserveSig] int GetAt(uint i, out PropertyKey key);
    [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant pv);
    [PreserveSig] int SetValue(ref PropertyKey key, ref PropVariant pv);
    [PreserveSig] int Commit();
}

[StructLayout(LayoutKind.Sequential)]
struct PropertyKey { public Guid fmtid; public uint pid; }

[StructLayout(LayoutKind.Sequential)]
struct PropVariant { public short vt; short r1, r2, r3; public IntPtr p; int p2; }

class Installer {

    static readonly PropertyKey PKEY_FriendlyName = new PropertyKey {
        fmtid = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), pid = 14
    };

    const string REG_RUN_KEY = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    const string APP_NAME    = "MuteDiscord";

    static List<string> GetAudioDevices() {
        var list = new List<string>();
        try {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorCoClass();
            IMMDeviceCollection col;
            if (enumerator.EnumAudioEndpoints(0, 1, out col) != 0) return list;
            uint count; col.GetCount(out count);
            for (uint i = 0; i < count; i++) {
                IMMDevice dev; col.Item(i, out dev);
                IPropertyStore store; dev.OpenPropertyStore(0, out store);
                var key = PKEY_FriendlyName; PropVariant pv;
                if (store.GetValue(ref key, out pv) == 0 && pv.vt == 31 && pv.p != IntPtr.Zero) {
                    string name = Marshal.PtrToStringUni(pv.p);
                    if (!string.IsNullOrEmpty(name)) list.Add(name);
                }
                Marshal.ReleaseComObject(store);
                Marshal.ReleaseComObject(dev);
            }
            Marshal.ReleaseComObject(col);
            Marshal.ReleaseComObject(enumerator);
        } catch { }
        return list;
    }

    static void WriteConfig(string installDir, List<string> devices) {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("  \"devices\": [");
        for (int i = 0; i < devices.Count; i++) {
            string comma = i < devices.Count - 1 ? "," : "";
            sb.AppendLine("    \"" + devices[i].Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"" + comma);
        }
        sb.AppendLine("  ]");
        sb.AppendLine("}");
        File.WriteAllText(Path.Combine(installDir, "config.json"), sb.ToString(), Encoding.UTF8);
    }

    static void SetStartup(string exePath, bool enable) {
        using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(REG_RUN_KEY, true)) {
            if (enable) key.SetValue(APP_NAME, "\"" + exePath + "\"");
            else        key.DeleteValue(APP_NAME, false);
        }
    }

    static bool GetStartupEnabled() {
        using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(REG_RUN_KEY, false)) {
            return key.GetValue(APP_NAME) != null;
        }
    }

    static void PrintHeader() {
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("╔══════════════════════════════════════════╗");
        Console.WriteLine("║         MuteDiscord  Installer           ║");
        Console.WriteLine("╚══════════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine();
    }

    static void Ok(string msg)   { Console.ForegroundColor = ConsoleColor.Green;  Console.WriteLine("  ✓ " + msg); Console.ResetColor(); }
    static void Warn(string msg) { Console.ForegroundColor = ConsoleColor.Yellow; Console.WriteLine("  ! " + msg); Console.ResetColor(); }
    static void Err(string msg)  { Console.ForegroundColor = ConsoleColor.Red;    Console.WriteLine("  ✗ " + msg); Console.ResetColor(); }

    static void Main() {
        PrintHeader();

        // --- Install directory ---
        string defaultInstallDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MuteDiscord");

        Console.WriteLine("Install directory:");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  [Press Enter to use: " + defaultInstallDir + "]");
        Console.ResetColor();
        Console.Write("> ");
        string input = Console.ReadLine().Trim();
        string installDir = string.IsNullOrEmpty(input) ? defaultInstallDir : input;
        Console.WriteLine();

        if (!Directory.Exists(installDir)) Directory.CreateDirectory(installDir);

        // Check if MuteDiscord is already running
        var running = System.Diagnostics.Process.GetProcessesByName("MuteDiscord");
        if (running.Length > 0) {
            Warn("MuteDiscord is currently running.");
            Console.WriteLine("  Close it now, then press any key to continue (or Escape to cancel).");
            while (true) {
                var k = Console.ReadKey(true);
                if (k.Key == ConsoleKey.Escape) {
                    Err("Installation cancelled.");
                    Console.ReadKey();
                    return;
                }
                if (System.Diagnostics.Process.GetProcessesByName("MuteDiscord").Length == 0) break;
                Warn("MuteDiscord is still running. Close it and press any key again.");
            }
            Ok("MuteDiscord closed.");
            Console.WriteLine();
        }

        // Extract embedded resources
        var asm = Assembly.GetExecutingAssembly();

        string destExe = Path.Combine(installDir, "MuteDiscord.exe");
        using (var src = asm.GetManifestResourceStream("MuteDiscord.exe"))
        using (var dst = File.Create(destExe))
            src.CopyTo(dst);
        Ok("MuteDiscord.exe installed to: " + installDir);

        string assetsDir = Path.Combine(installDir, "assets");
        if (!Directory.Exists(assetsDir)) Directory.CreateDirectory(assetsDir);
        using (var src = asm.GetManifestResourceStream("icon.png"))
        using (var dst = File.Create(Path.Combine(assetsDir, "icon.png")))
            src.CopyTo(dst);
        Ok("assets/icon.png extracted.");

        // --- Device selection ---
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("Active playback devices detected:");
        Console.ResetColor();

        var allDevices = GetAudioDevices();
        if (allDevices.Count == 0) {
            Warn("No devices detected. You will need to enter device names manually.");
        } else {
            for (int i = 0; i < allDevices.Count; i++) {
                string marker = allDevices[i].IndexOf("SteelSeries Sonar - Microphone", StringComparison.OrdinalIgnoreCase) >= 0 ? " ◄ default" : "";
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write("  [" + (i + 1) + "] ");
                Console.ResetColor();
                Console.WriteLine(allDevices[i] + marker);
            }
        }

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("Select TWO devices to monitor:");
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  1) SteelSeries Sonar - Microphone  (causes mic bleed into stream)");
        Console.WriteLine("  2) Your headset                    (causes double Discord audio)");
        Console.WriteLine();
        Console.WriteLine("  Enter two numbers separated by a comma (e.g. 1,3)");
        Console.WriteLine("  or press Enter to auto-select SteelSeries Sonar.");
        Console.ResetColor();
        Console.Write("> ");
        string selection = Console.ReadLine().Trim();
        Console.WriteLine();

        var selectedDevices = new List<string>();

        if (string.IsNullOrEmpty(selection)) {
            bool found = false;
            foreach (var d in allDevices)
                if (d.IndexOf("SteelSeries Sonar - Microphone", StringComparison.OrdinalIgnoreCase) >= 0) {
                    selectedDevices.Add(d); found = true;
                }
            if (!found) {
                Warn("SteelSeries Sonar not found. Enter the device name manually:");
                Console.Write("> ");
                string manual = Console.ReadLine().Trim();
                if (!string.IsNullOrEmpty(manual)) selectedDevices.Add(manual);
            }
            Warn("You still need to select your headset. Re-run and select both devices by number.");
        } else {
            foreach (var part in selection.Split(',')) {
                int idx;
                if (int.TryParse(part.Trim(), out idx) && idx >= 1 && idx <= allDevices.Count)
                    selectedDevices.Add(allDevices[idx - 1]);
            }
        }

        if (selectedDevices.Count < 2) {
            Err("You must select exactly 2 devices: SteelSeries Sonar Microphone and your headset.");
            Err("Re-run the installer and select both.");
            Console.WriteLine("\nPress any key to exit.");
            Console.ReadKey();
            return;
        }

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("Devices that will be monitored:");
        Console.ResetColor();
        foreach (var d in selectedDevices) Ok(d);

        WriteConfig(installDir, selectedDevices);
        Ok("config.json written.");

        // --- Windows startup ---
        Console.WriteLine();
        bool startupEnabled = GetStartupEnabled();
        Console.WriteLine("Launch at Windows startup: " + (startupEnabled ? "already enabled" : "not enabled"));
        Console.WriteLine((startupEnabled ? "Disable" : "Enable") + " it? (y/N)");
        Console.Write("> ");
        string toggleStartup = Console.ReadLine().Trim().ToLower();
        if (toggleStartup == "y" || toggleStartup == "yes") {
            SetStartup(destExe, !startupEnabled);
            Ok((startupEnabled ? "Disabled" : "Enabled") + " launch at startup.");
        }

        // --- Summary ---
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("══════════════════════════════════════════");
        Console.WriteLine("  Installation complete!");
        Console.WriteLine("══════════════════════════════════════════");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine("Press any key to launch MuteDiscord and exit.");
        Console.ReadKey();
        System.Diagnostics.Process.Start(destExe);
    }
}
