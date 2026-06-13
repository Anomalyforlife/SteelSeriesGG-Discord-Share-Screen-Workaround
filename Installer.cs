#pragma warning disable CA1416 // Windows-only APIs — this tool targets Windows exclusively
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

// ---- COM per enumerare dispositivi audio reali ----

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

// ---- Registry ----
[ComImport, Guid("00000000-0000-0000-0000-000000000000")]
class Unused { }

class Installer {

    static readonly PropertyKey PKEY_FriendlyName = new PropertyKey {
        fmtid = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), pid = 14
    };

    const string REG_RUN_KEY = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    const string APP_NAME       = "MuteDiscord";

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

        // --- Cartella di installazione ---
        string defaultInstallDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MuteDiscord");

        Console.WriteLine("Cartella di installazione:");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  [Invio per usare: " + defaultInstallDir + "]");
        Console.ResetColor();
        Console.Write("> ");
        string input = Console.ReadLine().Trim();
        string installDir = string.IsNullOrEmpty(input) ? defaultInstallDir : input;
        Console.WriteLine();

        // Crea la cartella se non esiste
        if (!Directory.Exists(installDir)) Directory.CreateDirectory(installDir);

        // Estrae le risorse embedded
        var asm = Assembly.GetExecutingAssembly();

        string destExe = Path.Combine(installDir, "MuteDiscord.exe");
        using (var src = asm.GetManifestResourceStream("MuteDiscord.exe"))
        using (var dst = File.Create(destExe))
            src.CopyTo(dst);
        Ok("MuteDiscord.exe installato in: " + installDir);

        string assetsDir = Path.Combine(installDir, "assets");
        if (!Directory.Exists(assetsDir)) Directory.CreateDirectory(assetsDir);
        using (var src = asm.GetManifestResourceStream("icon.png"))
        using (var dst = File.Create(Path.Combine(assetsDir, "icon.png")))
            src.CopyTo(dst);
        Ok("assets/icon.png estratto.");

        // --- Selezione dispositivi ---
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("Dispositivi audio di riproduzione rilevati:");
        Console.ResetColor();

        var allDevices = GetAudioDevices();
        if (allDevices.Count == 0) {
            Warn("Nessun dispositivo rilevato. Inserisci i nomi manualmente.");
        } else {
            for (int i = 0; i < allDevices.Count; i++) {
                string marker = allDevices[i].IndexOf("SteelSeries Sonar") >= 0 ? " ◄ default" : "";
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write("  [" + (i + 1) + "] ");
                Console.ResetColor();
                Console.WriteLine(allDevices[i] + marker);
            }
        }

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("Devi selezionare DUE dispositivi:");
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  1) SteelSeries Sonar - Microphone  (causa la cattura del microfono)");
        Console.WriteLine("  2) Le tue cuffie                   (causa il doppio audio Discord)");
        Console.WriteLine();
        Console.WriteLine("  Digita i numeri separati da virgola (es: 1,3)");
        Console.WriteLine("  oppure Invio per selezionare automaticamente SteelSeries Sonar.");
        Console.ResetColor();
        Console.Write("> ");
        string selection = Console.ReadLine().Trim();
        Console.WriteLine();

        var selectedDevices = new List<string>();

        if (string.IsNullOrEmpty(selection)) {
            // Default: tutti i dispositivi SteelSeries Sonar trovati
            bool found = false;
            foreach (var d in allDevices)
                if (d.IndexOf("SteelSeries Sonar", StringComparison.OrdinalIgnoreCase) >= 0) {
                    selectedDevices.Add(d); found = true;
                }
            if (!found) {
                Warn("SteelSeries Sonar non trovato. Inserisci il nome manualmente:");
                Console.Write("> ");
                string manual = Console.ReadLine().Trim();
                if (!string.IsNullOrEmpty(manual)) selectedDevices.Add(manual);
            }
            // Ricorda di aggiungere le cuffie
            Warn("Ricorda: devi aggiungere anche le tue cuffie (vedi step successivo).");
        } else {
            // Selezione per numero
            foreach (var part in selection.Split(',')) {
                int idx;
                if (int.TryParse(part.Trim(), out idx) && idx >= 1 && idx <= allDevices.Count)
                    selectedDevices.Add(allDevices[idx - 1]);
            }
        }

        // Aggiunta dispositivi extra manualmente
        Console.WriteLine("Vuoi aggiungere altri dispositivi? (es. le tue cuffie) (s/N)");
        Console.Write("> ");
        string addMore = Console.ReadLine().Trim().ToLower();
        while (addMore == "s" || addMore == "si" || addMore == "y") {
            Console.WriteLine("Nome dispositivo (esatto, come appare nel mixer audio di Windows):");
            Console.Write("> ");
            string extra = Console.ReadLine().Trim();
            if (!string.IsNullOrEmpty(extra)) { selectedDevices.Add(extra); Ok("Aggiunto: " + extra); }
            Console.WriteLine("Aggiungere un altro? (s/N)");
            Console.Write("> ");
            addMore = Console.ReadLine().Trim().ToLower();
        }

        if (selectedDevices.Count < 2) {
            Err("Devi selezionare esattamente 2 dispositivi: SteelSeries Sonar Microphone e le tue cuffie.");
            Err("Rilancia l'installer e seleziona entrambi.");
            Console.WriteLine("\nPremi un tasto per uscire.");
            Console.ReadKey();
            return;
        }

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("Dispositivi che verranno monitorati:");
        Console.ResetColor();
        foreach (var d in selectedDevices) Ok(d);

        // Scrivi config.json
        WriteConfig(installDir, selectedDevices);
        Ok("config.json scritto.");

        // --- Avvio automatico ---
        Console.WriteLine();
        bool startupEnabled = GetStartupEnabled();
        Console.WriteLine("Avvio automatico con Windows: " +
            (startupEnabled ? "già abilitato" : "non abilitato"));
        Console.WriteLine("Vuoi " + (startupEnabled ? "disabilitarlo" : "abilitarlo") + "? (s/N)");
        Console.Write("> ");
        string toggleStartup = Console.ReadLine().Trim().ToLower();
        if (toggleStartup == "s" || toggleStartup == "si" || toggleStartup == "y") {
            SetStartup(destExe, !startupEnabled);
            Ok((!startupEnabled ? "Abilitato" : "Disabilitato") + " avvio automatico.");
        }

        // --- Riepilogo ---
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("══════════════════════════════════════════");
        Console.WriteLine("  Installazione completata!");
        Console.WriteLine("══════════════════════════════════════════");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine("Avvia MuteDiscord con:");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  \"" + destExe + "\"");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine("Premi un tasto per uscire.");
        Console.ReadKey();
    }
}
