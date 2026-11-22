// BureauCache — Program
using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

static class Program
{
    // Objets nommés limités à la session Windows de l'utilisateur (« Local\ »)
    const string MutexName = @"Local\BureauCache_SingleInstance";
    const string ShowEvent = @"Local\BureauCache_ShowSettings";
    const string QuitEvent = @"Local\BureauCache_Quit";

    [STAThread]
    static void Main(string[] args)
    {
        // Sécurité : les DLL système ne sont cherchées que dans System32, jamais à côté de l'exe (Téléchargements…)
        try { Native.SetDefaultDllDirectories(Native.LOAD_LIBRARY_SEARCH_SYSTEM32); } catch { }
        try { Native.SetProcessDPIAware(); } catch { }
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) Ui.Scale = g.DpiX / 96f;

        string cmd = args.Length > 0 ? args[0].ToLowerInvariant() : "";
        if (cmd == "--watchdog" && args.Length > 1) { Watchdog(args[1]); return; }
        if (cmd == "--make-icon" && args.Length > 1) { Ui.WriteIco(args[1]); return; }
        if (cmd == "--restore") { Native.RestoreDesktopIcons(); return; }
        Settings.Load();
        L.Refresh();
        if (cmd == "--uninstall") { UninstallFromCommandLine(); return; }

        bool created;
        using (Mutex m = new Mutex(true, MutexName, out created))
        {
            if (!created)
            {
                try { EventWaitHandle.OpenExisting(ShowEvent).Set(); } catch { }
                return;
            }
            using (EventWaitHandle show = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEvent))
            using (EventWaitHandle quit = new EventWaitHandle(false, EventResetMode.AutoReset, QuitEvent))
            {
                Updater.CleanupOld();
                if (Installer.RunningFromInstall) try { Installer.RegisterVersion(); } catch { }
                StartWatchdog();
                try { Application.Run(new App(show, quit)); }
                finally { Native.RestoreDesktopIcons(); }
            }
        }
        if (App.RelaunchPath != null)
            try { Process.Start(App.RelaunchPath); } catch { }
    }

    static void StartWatchdog()
    {
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo(Application.ExecutablePath, "--watchdog " + Process.GetCurrentProcess().Id);
            psi.UseShellExecute = false;
            Process.Start(psi);
        }
        catch { }
    }

    static void Watchdog(string pidText)
    {
        int pid;
        if (!int.TryParse(pidText, out pid)) return;
        try { Process.GetProcessById(pid).WaitForExit(); } catch { }
        Thread.Sleep(1500);
        bool running;
        try { using (Mutex.OpenExisting(MutexName)) running = true; }
        catch { running = false; }
        if (!running) Native.RestoreDesktopIcons();
    }

    static void UninstallFromCommandLine()
    {
        if (!Dialogs.Ask(null, Installer.UninstallQuestion)) return;
        try { EventWaitHandle.OpenExisting(QuitEvent).Set(); } catch { }
        for (int i = 0; i < 50; i++)
        {
            bool running;
            try { using (Mutex.OpenExisting(MutexName)) running = true; }
            catch { running = false; }
            if (!running) break;
            Thread.Sleep(100);
        }
        Installer.Uninstall();
        Dialogs.Info(null, L.T("BureauCache a été désinstallé.", "BureauCache has been uninstalled."));
    }
}
