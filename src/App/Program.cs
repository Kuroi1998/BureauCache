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
        if (cmd == "--restore") { Native.RestoreDesktopIcons(); WindowsBar.Restore(); return; }
        Settings.Load();
        L.Refresh();
        if (cmd == "--uninstall") { UninstallFromCommandLine(); return; }

        bool created;
        using (Mutex m = new Mutex(true, MutexName, out created))
        {
            if (!created)
            {
                // L'instance principale crée le signal juste après le mutex : on l'attend jusqu'à 5 s, sinon la
                // demande d'affichage serait perdue lors de deux lancements presque simultanés (R-15)
                for (int i = 0; i < 50; i++)
                {
                    try { using (EventWaitHandle show = EventWaitHandle.OpenExisting(ShowEvent)) show.Set(); return; }
                    catch (WaitHandleCannotBeOpenedException) { Thread.Sleep(100); }
                    catch { return; }
                }
                return;
            }
            using (EventWaitHandle show = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEvent))
            using (EventWaitHandle quit = new EventWaitHandle(false, EventResetMode.AutoReset, QuitEvent))
            {
                Updater.StartState start = Updater.BeginStart(Application.ExecutablePath);
                Updater.TrialStarted = start == Updater.StartState.Trial;
                if (start == Updater.StartState.RolledBack)
                {
                    // L'ancienne version est revenue à la place de l'exe : on la relance, une fois le mutex libéré
                    Dialogs.Warn(null, L.T("La nouvelle version de BureauCache n'a pas réussi à démarrer.\nLa version précédente a été restaurée.",
                                           "The new version of BureauCache failed to start.\nThe previous version has been restored.", "La nueva versión de BureauCache no pudo iniciarse.\nSe restauró la versión anterior."));
                    App.RelaunchPath = Application.ExecutablePath;
                }
                else
                {
                    if (Installer.RunningFromInstall) try { Installer.RegisterVersion(); } catch { }
                    StartWatchdog();
                    try { Application.Run(new App(show, quit)); }
                    finally { Native.RestoreDesktopIcons(); WindowsBar.Restore(); }
                }
            }
        }
        if (App.RelaunchPath != null)
            try { Process.Start(App.RelaunchPath); } catch { }
        Installer.StartFolderRemoval();
    }

    // Vrai si le processus de surveillance tourne : la barre BureauCache en a besoin (D-013, BAR-07)
    internal static bool WatchdogStarted;

    static void StartWatchdog()
    {
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo(Application.ExecutablePath, "--watchdog " + Process.GetCurrentProcess().Id);
            psi.UseShellExecute = false;
            using (Process p = Process.Start(psi)) WatchdogStarted = p != null && !p.HasExited;
        }
        catch { }
    }

    static void Watchdog(string pidText)
    {
        int pid;
        if (!int.TryParse(pidText, out pid)) return;
        try { Process.GetProcessById(pid).WaitForExit(); } catch { }
        Thread.Sleep(1500);
        if (InstanceExists()) return;
        Native.RestoreDesktopIcons();   // plantage : icônes et barre Windows reviennent
        // Explorer absent ou restauration non constatée : nouvel essai pendant 2 minutes, sauf si une instance a repris
        for (int i = 0; i < 60 && !WindowsBar.Restore(); i++)
        {
            Thread.Sleep(2000);
            if (InstanceExists()) return;
        }
    }

    static bool InstanceExists()
    {
        try { using (Mutex.OpenExisting(MutexName)) return true; }
        catch (WaitHandleCannotBeOpenedException) { return false; }
        catch (UnauthorizedAccessException) { return true; }
    }

    static void UninstallFromCommandLine()
    {
        if (!Dialogs.Ask(null, Installer.UninstallQuestion)) return;
        try { using (EventWaitHandle quit = EventWaitHandle.OpenExisting(QuitEvent)) quit.Set(); } catch { }
        for (int i = 0; i < 50 && InstanceExists(); i++) Thread.Sleep(100);
        if (InstanceExists())
        {
            Dialogs.Warn(null, L.T("BureauCache est encore ouvert. Ferme-le avant de relancer la désinstallation.",
                                   "BureauCache is still running. Close it before trying to uninstall again.", "BureauCache sigue en ejecución. Ciérralo antes de volver a intentar desinstalarlo."));
            return;
        }
        Installer.ReportUninstall(Installer.Uninstall());
        Installer.StartFolderRemoval();
    }
}
