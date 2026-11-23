// BureauCache — HotkeyWindow, App
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

// =====================================================================
//  Raccourci clavier global
// =====================================================================
class HotkeyWindow : NativeWindow
{
    public event Action Pressed;
    public HotkeyWindow() { CreateHandle(new CreateParams()); }

    public bool Register(bool on, int mods, int key)
    {
        Native.UnregisterHotKey(Handle, 1);
        if (!on || key == 0) return true;
        return Native.RegisterHotKey(Handle, 1, (uint)(mods | 0x4000), (uint)key);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x0312 && Pressed != null) Pressed();
        base.WndProc(ref m);
    }
}

// =====================================================================
//  Application
// =====================================================================
partial class App : ApplicationContext
{
    NotifyIcon tray;
    readonly PanelForm panel;
    SettingsForm settingsForm;
    readonly System.Windows.Forms.Timer poll;
    readonly List<FileSystemWatcher> watchers = new List<FileSystemWatcher>();
    readonly HotkeyWindow hotkey = new HotkeyWindow();
    ContextMenuStrip trayMenu;
    IntPtr listView = IntPtr.Zero;
    int edgeTicks, outsideTicks, pollCount;
    bool requireLeave, wasOpen, hotkeyOk = true, autoCloseArmed, lastEn;
    Action balloonAction;
    const int PollMs = 20;
    public static string RelaunchPath;
    bool installingUpdate;
    Screen[] screens = Screen.AllScreens; // mis en cache : la détection du bord tourne 50 fois par seconde

    public App(EventWaitHandle showSignal, EventWaitHandle quitSignal)
    {
        panel = new PanelForm();
        Settings.AttachToUi();
        IntPtr unused = panel.Handle;
        panel.SettingsRequested += (s, e) => ShowSettings();
        lastEn = L.En;

        SetupTray();

        string[] dirs = Paths.Desktops;
        foreach (string d in dirs)
        {
            try
            {
                FileSystemWatcher w = new FileSystemWatcher(d);
                FileSystemEventHandler h = delegate { panel.Dirty = true; };
                w.Created += h; w.Deleted += h; w.Changed += h;
                w.Renamed += delegate { panel.Dirty = true; };
                w.EnableRaisingEvents = true;
                watchers.Add(w);
            }
            catch { }
        }

        hotkey.Pressed += () =>
        {
            if (HotkeyBox.Capturing) return;
            if (panel.IsOpen && Settings.Pinned) { panel.ShowPanel(TargetScreen(Cursor.Position)); Native.SetForegroundWindow(panel.Handle); }
            else if (panel.IsOpen) panel.HidePanel();
            else OpenManually(TargetScreen(Cursor.Position));
        };

        Settings.Changed += OnSettingsChanged;
        SystemEvents.UserPreferenceChanged += (s, e) =>
        {
            if (Settings.RefreshSystemTheme() && Settings.ThemeMode == 0)
                panel.BeginInvoke(new Action(Settings.Fire));
        };
        SystemEvents.SessionEnding += delegate { Settings.Flush(); Native.RestoreDesktopIcons(); };
        SystemEvents.DisplaySettingsChanged += delegate { screens = Screen.AllScreens; };

        ApplyIconVisibility();
        panel.RequestLoad();
        RegisterHotkey();

        poll = new System.Windows.Forms.Timer();
        poll.Interval = PollMs;
        poll.Tick += Poll;
        poll.Start();

        Background.Run(() =>
        {
            while (true)
            {
                int i = WaitHandle.WaitAny(new WaitHandle[] { showSignal, quitSignal });
                try { panel.BeginInvoke(new Action(i == 0 ? (Action)ShowSettings : ExitThread)); } catch { }
                if (i == 1) break;
            }
        });

        PanelForm.RecycleLeftovers();
        bool updated = !Settings.FirstRun && Settings.LastVersion != Installer.Version;
        Settings.LastVersion = Installer.Version;
        if (Settings.Pinned) panel.ShowPinned(TargetScreen(Cursor.Position));
        if (Settings.FirstRun) { Settings.Save(); ShowWelcome(); }
        else if (updated)
        {
            Settings.SaveQuiet();
            Balloon(L.T("BureauCache a été mis à jour en " + Installer.Version + ". Clique pour voir les nouveautés.",
                        "BureauCache was updated to " + Installer.Version + ". Click to see what's new."),
                    () => { try { ShellUtil.Open("https://github.com/" + Updater.RepoName + "/releases/tag/v" + Installer.Version, null); } catch { } });
        }
        else if (!Settings.Pinned && !Settings.QuietStart) Balloon(L.T("Amène la souris sur le bord " + (Settings.RightSide ? "droit" : "gauche") + " de l'écran", "Move the mouse to the " + (Settings.RightSide ? "right" : "left") + " edge of the screen") +
            (Settings.HotkeyOn ? L.T(" ou appuie sur ", " or press ") + HotkeyBox.Format(Settings.HotMods, Settings.HotKey) : "") + ".", null);
    }

    void RegisterHotkey() { hotkeyOk = hotkey.Register(Settings.HotkeyOn, Settings.HotMods, Settings.HotKey); }

    void OnSettingsChanged()
    {
        L.Refresh();
        ApplyIconVisibility();
        RegisterHotkey();
        panel.OnSettingsChanged();
        RefreshTray();
        if (L.En != lastEn)
        {
            lastEn = L.En;
            panel.ReloadNow();
            if (settingsForm != null && !settingsForm.IsDisposed)
            {
                SettingsForm old2 = settingsForm;
                int page = old2.CurrentPage;
                Point loc = old2.Location;
                bool vis = old2.Visible;
                panel.BeginInvoke(new Action(() =>
                {
                    old2.Dispose();
                    settingsForm = new SettingsForm(Actions());
                    settingsForm.SelectPage(page);
                    settingsForm.StartPosition = FormStartPosition.Manual;
                    settingsForm.Location = loc;
                    if (vis) settingsForm.Show();
                }));
            }
        }
    }

    SettingsActions Actions()
    {
        return new SettingsActions
        {
            Preview = Preview, Quit = ExitThread, Install = DoInstall, Uninstall = DoUninstall,
            InstallUpdate = InstallUpdate, HotkeyOk = () => hotkeyOk, Welcome = ShowWelcome, HiddenToTray = OnHiddenToTray
        };
    }

    WelcomeForm welcome;

    void ShowWelcome()
    {
        if (welcome != null && !welcome.IsDisposed) { welcome.Activate(); return; }
        welcome = new WelcomeForm(() => OpenManually(TargetScreen(Cursor.Position)));
        welcome.Show();
        welcome.Activate();
        Native.SetForegroundWindow(welcome.Handle);
    }

    void ShowSettings()
    {
        if (settingsForm == null || settingsForm.IsDisposed) settingsForm = new SettingsForm(Actions());
        if (!settingsForm.Visible) settingsForm.Show();
        if (settingsForm.WindowState == FormWindowState.Minimized) settingsForm.WindowState = FormWindowState.Normal;
        settingsForm.Activate();
        Native.SetForegroundWindow(settingsForm.Handle);
    }

    void Preview() { OpenManually(settingsForm != null ? Screen.FromControl(settingsForm) : Screen.PrimaryScreen); }

    void OpenManually(Screen scr)
    {
        autoCloseArmed = false;
        outsideTicks = 0;
        panel.ShowPanel(scr);
    }

    void DoInstall()
    {
        try
        {
            Installer.Install();
            Dialogs.Info(settingsForm, L.T("BureauCache est installé.\nTu le trouveras dans le menu Démarrer et dans Paramètres > Applications.\n\nIl va redémarrer depuis son dossier d'installation.",
                "BureauCache is installed.\nYou'll find it in the Start menu and in Settings > Apps.\n\nIt will now restart from its install folder."));
            if (!Installer.RunningFromInstall) { RelaunchPath = Installer.Exe; ExitThread(); }
            else Settings.Fire();
        }
        catch (Exception ex)
        {
            Dialogs.Warn(settingsForm, L.T("L'installation a échoué :\n", "Installation failed:\n") + ex.Message);
        }
    }

    void DoUninstall()
    {
        if (installingUpdate || !Dialogs.Ask(settingsForm, Installer.UninstallQuestion)) return;
        Installer.Uninstall();
        ExitThread();
    }

    void InstallUpdate()
    {
        if (installingUpdate) return;
        Updater.Release release = Updater.AvailableRelease;
        if (release == null) { CheckUpdateNow(); return; }
        string notes = Updater.ShortNotes(release);
        if (!Dialogs.Ask(null, L.T("Installer BureauCache " + release.Version + " ?\nLe programme va redémarrer.", "Install BureauCache " + release.Version + "?\nThe program will restart.") +
            (notes.Length > 0 ? "\n\n" + L.T("Nouveautés :", "What's new:") + "\n" + notes : ""))) return;
        if (installingUpdate || Updater.AvailableRelease != release) return;
        installingUpdate = true;
        string exe = Application.ExecutablePath;
        Background.Run(() =>
        {
            string error = null;
            try { Updater.Download(release, exe); }
            catch (Exception ex) { error = ex.Message; }
            panel.BeginInvoke(new Action(() =>
            {
                installingUpdate = false;
                if (error == null) { RelaunchPath = exe; ExitThread(); }
                else Dialogs.Warn(null, L.T("La mise à jour a échoué :\n", "Update failed:\n") + error);
            }));
        });
    }

    void CheckUpdateInBackground()
    {
        if (installingUpdate || !Settings.AutoUpdate || !Updater.RepoValid) return;
        if (DateTime.Now - new DateTime(Settings.LastUpdateCheck) < TimeSpan.FromHours(24)) return;
        Background.Run(() =>
        {
            Updater.Release release = Updater.Check();
            panel.BeginInvoke(new Action(() =>
            {
                if (!Updater.AcceptCheck(release)) return;
                RefreshTray();
                if (release.Available) Balloon(L.T("BureauCache " + release.Version + " est disponible. Clique pour l'installer.",
                    "BureauCache " + release.Version + " is available. Click to install."), InstallUpdate);
            }));
        });
    }

    bool archiveDue;

    void RunArchive()
    {
        archiveDue = false;
        if (!Settings.ArchiveOn) return;
        // Une opération sur les fichiers est en cours dans le panneau (renommage, copie, corbeille : ses dialogues
        // laissent tourner ce minuteur) ou peut encore être annulée : on réessaie chaque minute (R-03)
        // Idem pendant une fenêtre modale de l'application (confirmation, export des réglages…)
        if (panel.Busy || panel.UndoPending || ModalOpen()) { archiveDue = true; return; }
        int n = Archiver.RunNow();
        if (n > 0)
        {
            panel.ReloadNow();
            Balloon(n + L.T(" élément(s) inutilisé(s) rangé(s) dans « " + Settings.ArchiveName + " ».", " unused item(s) moved into \"" + Settings.ArchiveName + "\"."),
                () => { try { ShellUtil.Open(Archiver.Folder, null); } catch { } });
        }
    }

    // Une fenêtre modale (dialogue, message, enregistrement de fichier) désactive ses fenêtres propriétaires
    static bool ModalOpen()
    {
        foreach (Form f in Application.OpenForms)
            if (f.IsHandleCreated && f.Visible && !Native.IsWindowEnabled(f.Handle)) return true;
        return false;
    }

    Screen TargetScreen(Point p)
    {
        Screen[] all = screens;
        if (Settings.ScreenIdx >= 0 && Settings.ScreenIdx < all.Length) return all[Settings.ScreenIdx];
        return Screen.FromPoint(p);
    }

    void ApplyIconVisibility()
    {
        if (listView == IntPtr.Zero || !Native.IsWindow(listView)) listView = Native.FindDesktopListView();
        if (listView == IntPtr.Zero) return;
        bool vis = Native.IsWindowVisible(listView);
        if (Settings.HideIcons && vis) Native.ShowWindow(listView, Native.SW_HIDE);
        else if (!Settings.HideIcons && !vis) Native.ShowWindow(listView, Native.SW_SHOW);
    }

    bool ScreenExistsAt(Point p)
    {
        foreach (Screen s in screens) if (s.Bounds.Contains(p)) return true;
        return false;
    }

    bool AtEdge(Point p, out Screen scr)
    {
        scr = Screen.FromPoint(p);
        Screen[] all = screens;
        if (Settings.ScreenIdx >= 0 && Settings.ScreenIdx < all.Length && !all[Settings.ScreenIdx].Bounds.Contains(p)) return false;
        Rectangle b = scr.Bounds;
        bool edge = Settings.RightSide
            ? p.X >= b.Right - 1 && !ScreenExistsAt(new Point(p.X + 1, p.Y))
            : p.X <= b.Left && !ScreenExistsAt(new Point(p.X - 1, p.Y));
        if (!edge) return false;
        if (Settings.EdgeZone == 0) return true;
        int third = b.Height / 3, rel = p.Y - b.Top;
        if (Settings.EdgeZone == 1) return rel < third;
        if (Settings.EdgeZone == 2) return rel >= third && rel < 2 * third;
        return rel >= 2 * third;
    }

    void Poll(object sender, EventArgs e)
    {
        pollCount++;
        if (pollCount % 50 == 0) ApplyIconVisibility();
        if (pollCount % 250 == 0) RefreshTray(); // toutes les 5 s : point « nouveaux fichiers » sur l'icône
        if (pollCount % 25 == 0 && panel.Dirty && !panel.Busy) panel.RequestLoad();
        if (pollCount == 3000 || pollCount % 1080000 == 0 || (archiveDue && pollCount % 3000 == 0))
            RunArchive();                                                             // 1 min après le démarrage, puis toutes les 6 h (reporté si besoin)
        if (pollCount == 1500 || pollCount % 180000 == 0) CheckUpdateInBackground();  // 30 s après le démarrage, puis toutes les heures

        Point p = Cursor.Position;
        bool open = panel.IsOpen;
        Screen scr;
        bool atEdge = AtEdge(p, out scr);
        if (wasOpen && !open && atEdge) requireLeave = true;
        wasOpen = open;
        if (requireLeave && !atEdge) requireLeave = false;

        if (!open)
        {
            if (!atEdge || requireLeave) { edgeTicks = 0; return; }
            edgeTicks++;
            int needed = (Settings.Delay + (Control.MouseButtons != MouseButtons.None ? 350 : 0)) / PollMs + 1;
            if (edgeTicks >= needed)
            {
                edgeTicks = 0;
                if (Settings.NoFullscreen && Native.ForegroundIsFullscreen()) { requireLeave = true; return; }
                outsideTicks = 0;
                autoCloseArmed = true;
                panel.ShowPanel(scr);
            }
        }
        else if (Settings.AutoClose && !Settings.Pinned)
        {
            Rectangle r = panel.Bounds;
            r.Inflate(20, 0);
            if (r.Contains(p)) autoCloseArmed = true;
            if (autoCloseArmed && !r.Contains(p) && !panel.MenuOpen && !panel.Busy) outsideTicks++;
            else outsideTicks = 0;
            if (outsideTicks >= 25) { outsideTicks = 0; panel.HidePanel(); }
        }
    }

    protected override void ExitThreadCore()
    {
        if (installingUpdate) return;
        clickTimer.Stop();
        poll.Stop();
        try { panel.FlushToast(); } catch { }
        Settings.Flush();
        try { panel.Unpin(); } catch { }
        try { hotkey.Register(false, 0, 0); hotkey.DestroyHandle(); } catch { }
        Native.RestoreDesktopIcons();
        foreach (FileSystemWatcher w in watchers) w.Dispose();
        tray.Visible = false;
        tray.Dispose();
        base.ExitThreadCore();
    }
}
