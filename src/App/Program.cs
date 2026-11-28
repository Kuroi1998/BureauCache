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
        Stopwatch startupClock = Stopwatch.StartNew();
        // Sécurité : les DLL système ne sont cherchées que dans System32, jamais à côté de l'exe (Téléchargements…)
        try { Native.SetDefaultDllDirectories(Native.LOAD_LIBRARY_SEARCH_SYSTEM32); } catch { }
        string cmd = args.Length > 0 ? args[0].ToLowerInvariant() : "";
        if (cmd == "--watchdog" && args.Length > 1) { Watchdog(args[1]); return; }
        if (cmd == "--restore") { Native.RestoreDesktopIcons(); WindowsBar.Restore(); TaskbandRoute.Restore(); return; }
        if (cmd == "--make-icon" && args.Length > 1) { InitWinForms(); Ui.WriteIco(args[1]); return; }
        if (cmd == "--uninstall") { InitSession(); UninstallFromCommandLine(); return; }

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
                InitSession();
                if (Settings.TaskbarOn && !Sandbox.Active) StartupTimeline.Start(startupClock);
                Updater.StartState start = Updater.BeginStart(Application.ExecutablePath);
                Updater.TrialStarted = start == Updater.StartState.Trial;
                if (start == Updater.StartState.RolledBack)
                {
                    // L'ancienne version est revenue à la place de l'exe : on la relance, une fois le mutex libéré.
                    // La barre Windows est rendue AVANT l'avertissement : la surveillance de la version plantée a vu notre
                    // mutex et s'est arrêtée, et l'ancienne version peut ne pas connaître la barre BureauCache (CDX-01).
                    PrepareRollback(WindowsBar.Restore, RetryRestore, () =>
                    {
                        string message = L.T("La nouvelle version de BureauCache n'a pas réussi à démarrer.\nLa version précédente a été restaurée.",
                                           "The new version of BureauCache failed to start.\nThe previous version has been restored.", "La nueva versión de BureauCache no pudo iniciarse.\nSe restauró la versión anterior.");
                        // Barre restée masquée après tous les essais : le dire, avec la sortie (T-052)
                        if (!WindowsBar.RestoreOk) message += "\n\n" + L.T("La barre Windows n'a pas pu être rétablie : elle reste masquée. Quitte BureauCache depuis son icône de la zone de notification, ou redémarre l'ordinateur.",
                                           "The Windows taskbar could not be restored and stays hidden. Quit BureauCache from its notification-area icon, or restart the computer.", "No se pudo restaurar la barra de Windows: sigue oculta. Sal de BureauCache desde su icono del área de notificación, o reinicia el equipo.");
                        Dialogs.Warn(null, message);
                    });
                    App.RelaunchPath = Application.ExecutablePath;
                }
                else
                {
                    if (Installer.RunningFromInstall) try { Installer.RegisterVersion(); } catch { }
                    StartWatchdog();
                    StartupTimeline.Mark("surveillance_lancee");
                    try { Application.Run(new App(show, quit)); }
                    finally
                    {
                        if (watchdogProcess != null) watchdogProcess.Dispose();
                        Native.RestoreDesktopIcons();
                        TaskbandRoute.Restore();
                        WindowsBar.Restore();
                    }
                }
            }
        }
        if (App.RelaunchPath != null)
        {
            if (App.RelaunchAfterUpdate)
                RelaunchUpdate(App.RelaunchPath, p => { using (Process.Start(p)) { } }, Updater.RollBack, msg => Dialogs.Warn(null, msg));
            else try { Process.Start(App.RelaunchPath); } catch { }
        }
        Installer.StartFolderRemoval();
    }

    static void InitWinForms()
    {
        try { Native.SetProcessDPIAware(); } catch { }
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) Ui.Scale = g.DpiX / 96f;
    }

    static void InitSession()
    {
        InitWinForms();
        Settings.Load();
        L.Refresh();
        // Taille de l'interface et échelle de l'écran fixées pour la session (T-045).
        Ui.FontZoom = Settings.InterfaceZoom / 100f;
        Ui.Scale *= Ui.FontZoom;
        Settings.RefreshWindowsAccent();
    }

    // Vrai si le processus de surveillance tourne : la barre BureauCache en a besoin (D-013, BAR-07)
    static Process watchdogProcess;
    internal static bool WatchdogRunning
    {
        get
        {
            try { return watchdogProcess != null && !watchdogProcess.HasExited; }
            catch (InvalidOperationException) { return false; }
            catch (System.ComponentModel.Win32Exception) { return false; }
        }
    }

    static void StartWatchdog()
    {
        try
        {
            using (Process current = Process.GetCurrentProcess())
            {
                ProcessStartInfo psi = new ProcessStartInfo(Application.ExecutablePath, "--watchdog " + current.Id);
                psi.UseShellExecute = false;
                watchdogProcess = Process.Start(psi);
            }
        }
        catch { }
    }

    static void Watchdog(string pidText)
    {
        int pid;
        if (!int.TryParse(pidText, out pid)) return;
        try { using (Process parent = Process.GetProcessById(pid)) parent.WaitForExit(); } catch { }
        Thread.Sleep(1500);
        if (InstanceExists()) return;
        Native.RestoreDesktopIcons();   // plantage : icônes et barre Windows reviennent
        // Barre et badges (D-016) retournent à Explorer. Explorer absent ou restauration non constatée : nouvel essai
        // de chacune pendant 2 minutes, sauf si une instance a repris (CDX-41)
        bool bar = false, badges = false;
        for (int i = 0; i < 60; i++)
        {
            if (!badges) badges = TaskbandRoute.Restore();
            if (!bar) bar = WindowsBar.Restore();
            if (bar && badges) return;
            Thread.Sleep(2000);
            if (InstanceExists()) return;
        }
    }

    // Windows refuse de lancer la version installée (binaire inutilisable sur ce PC) : sans ce filet, l'exe resterait
    // cassé, car la nouvelle version n'atteint jamais le code d'essai qui sait revenir en arrière (CDX-22). L'ancienne
    // version, gardée en « .old », est remise en place et lancée une seule fois.
    internal static void RelaunchUpdate(string exe, Action<string> start, Action<string> rollBack, Action<string> warn)
    {
        try { start(exe); return; }
        catch { }
        bool restored = false;
        try { rollBack(exe); restored = true; } catch (Exception ex) { Log.Warn("maj.retour", ex); }
        warn(restored
            ? L.T("Windows n'a pas pu lancer la nouvelle version de BureauCache. La version précédente a été remise en place.",
                  "Windows couldn't start the new version of BureauCache. The previous version has been put back.",
                  "Windows no pudo iniciar la nueva versión de BureauCache. Se restauró la versión anterior.")
            : L.T("Windows n'a pas pu lancer la nouvelle version de BureauCache, et la version précédente n'a pas pu être remise en place. Réinstalle BureauCache depuis GitHub.",
                  "Windows couldn't start the new version of BureauCache, and the previous version couldn't be put back. Reinstall BureauCache from GitHub.",
                  "Windows no pudo iniciar la nueva versión de BureauCache y no se pudo restaurar la anterior. Reinstala BureauCache desde GitHub."));
        if (restored) try { start(exe); } catch { }
    }

    internal static void PrepareRollback(Func<bool> restore, Action retry, Action warn)
    {
        bool restored = false;
        try { restored = restore(); } catch (Exception ex) { Log.Warn("barre.restauration", ex); }
        if (!restored) try { retry(); } catch { }
        warn();
    }

    // Explorer absent ou restauration non constatée : quelques essais avant l'avertissement (10 s au plus)
    static void RetryRestore()
    {
        for (int i = 0; i < 5 && !WindowsBar.Restore(); i++) Thread.Sleep(2000);
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
