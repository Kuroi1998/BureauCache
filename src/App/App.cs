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
    // WM_ENDSESSION : vrai si la session se termine vraiment, faux si la déconnexion ou l'arrêt a été annulé (CDX-05)
    public event Action<bool> SessionEnded;
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
        if (m.Msg == 0x0016 /*WM_ENDSESSION*/ && SessionEnded != null) SessionEnded(m.WParam != IntPtr.Zero);
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
    readonly ClipboardWatcher clipboard = new ClipboardWatcher();
    ContextMenuStrip trayMenu;
    IntPtr listView = IntPtr.Zero;
    int pollCount;
    readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
    long edgeSince = -1, outsideSince = -1, nextArchive = 60000, nextUpdateCheck = 30000, lastPoll;
    bool requireLeave, wasOpen, hotkeyOk = true, autoCloseArmed;
    int lastLanguage;
    Action balloonAction;
    const int PollMs = 20;
    public static string RelaunchPath;
    public static bool RelaunchAfterUpdate;   // la relance suit un remplacement de l'exe par une mise à jour (CDX-22)
    bool installingUpdate;
    Screen[] screens = Screen.AllScreens; // mis en cache : la détection du bord tourne 50 fois par seconde

    public App(EventWaitHandle showSignal, EventWaitHandle quitSignal)
    {
        panel = new PanelForm();
        Settings.AttachToUi();
        IntPtr unused = panel.Handle;
        panel.SettingsRequested += (s, e) => ShowSettings();
        lastLanguage = L.CurrentLanguage;

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
        ClipboardHistory.Changed += panel.OnClipboardChanged;
        ApplyClipboard();
        ApplyTaskbar();
        bool lastContrast = SystemInformation.HighContrast;
        SystemEvents.UserPreferenceChanged += (s, e) =>
        {
            // Bascule du Contraste élevé : les fenêtres ouvertes reprennent leurs couleurs et leur barre de titre (QA52)
            bool contrast = SystemInformation.HighContrast;
            bool contrastChanged = contrast != lastContrast;
            lastContrast = contrast;
            bool accentChanged = Settings.RefreshWindowsAccent() && Settings.AccentFromWindows;   // accent de Windows suivi (T-045)
            if ((Settings.RefreshSystemTheme() && Settings.ThemeMode == 0) || contrastChanged || accentChanged)
                panel.BeginInvoke(new Action(Settings.Fire));
        };
        SystemEvents.SessionEnding += delegate { OnSessionEnding(); };
        hotkey.SessionEnded += OnSessionEnded;
        screenSignature = ScreenSignature(screens);
        SystemEvents.DisplaySettingsChanged += delegate
        {
            screens = Screen.AllScreens;
            try { panel.BeginInvoke(new Action(OnDisplaysChanged)); panel.BeginInvoke(new Action(OnDisplaysChangedForBar)); } catch { }
        };

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
        bool updated = !Settings.FirstRun && !Settings.ProfileUnreadable && Settings.LastVersion != Installer.Version;
        Settings.LastVersion = Installer.Version;
        if (Settings.Pinned) panel.ShowPinned(TargetScreen(Cursor.Position));
        if (Settings.ProfileUnreadable)
            Balloon(L.T("Tes réglages n'ont pas pu être lus (fichier occupé par un autre programme). BureauCache utilise les réglages par défaut sans rien écraser : relance-le pour les retrouver.",
                        "Your settings couldn't be read (file in use by another program). BureauCache uses the defaults without overwriting anything: restart it to get them back.",
                        "No se pudo leer tu configuración (otro programa usa el archivo). BureauCache usa los valores predeterminados sin sobrescribir nada: reinícialo para recuperarla."), null);
        else if (Settings.FirstRun) { Settings.Save(); ShowWelcome(); }
        else if (updated)
        {
            Settings.SaveQuiet();
            Balloon(L.T("BureauCache a été mis à jour en " + Installer.Version + ". Clique pour voir les nouveautés.",
                        "BureauCache was updated to " + Installer.Version + ". Click to see what's new.", "BureauCache se actualizó a la versión " + Installer.Version + ". Haz clic para ver las novedades."),
                    () => { try { ShellUtil.Open("https://github.com/" + Updater.RepoName + "/releases/tag/v" + Installer.Version, null); } catch { } });
        }
        else if (!Settings.Pinned && !Settings.QuietStart) Balloon(L.T("Amène la souris sur le bord " + (Settings.RightSide ? "droit" : "gauche") + " de l'écran", "Move the mouse to the " + (Settings.RightSide ? "right" : "left") + " edge of the screen", "Lleva el ratón al borde " + (Settings.RightSide ? "derecho" : "izquierdo") + " de la pantalla") +
            (Settings.HotkeyOn ? L.T(" ou appuie sur ", " or press ", " o pulsa ") + HotkeyBox.Format(Settings.HotMods, Settings.HotKey) : "") + ".", null);
    }

    // Écoute du presse-papiers seulement quand l'option est active ; la désactiver vide l'historique (D-011)
    void ApplyClipboard()
    {
        try { clipboard.Enable(Settings.ClipboardOn); } catch { }
    }

    void RegisterHotkey() { hotkeyOk = hotkey.Register(Settings.HotkeyOn, Settings.HotMods, Settings.HotKey); }

    void OnSettingsChanged()
    {
        L.Refresh();
        ApplyIconVisibility();
        RegisterHotkey();
        ApplyClipboard();
        ApplyTaskbar();
        panel.OnSettingsChanged();
        RefreshTray();
        if (L.CurrentLanguage != lastLanguage)
        {
            lastLanguage = L.CurrentLanguage;
            panel.ReloadNow();
            RebuildSettingsForm();
        }
    }

    // Recrée la fenêtre des Paramètres ouverte en gardant sa rubrique, son défilement et sa position
    void RebuildSettingsForm()
    {
        if (settingsForm == null || settingsForm.IsDisposed) return;
        SettingsForm old2 = settingsForm;
        panel.BeginInvoke(new Action(() =>
        {
            if (old2 != settingsForm || old2.IsDisposed) return;
            bool vis = old2.Visible;
            SettingsForm replacement = new SettingsForm(Actions());
            replacement.RestoreWindowState(old2);
            settingsForm = replacement;
            old2.Dispose();
            if (vis) settingsForm.Show();
        }));
    }

    string screenSignature;

    static string ScreenSignature(Screen[] all)
    {
        return string.Join("|", all.Select(s => s.DeviceName).ToArray());
    }

    // Écran branché, débranché ou réordonné : la liste « Écran » des Paramètres est reconstruite, pour que
    // l'affichage et le choix reposent sur la même disposition (R-10)
    void OnDisplaysChanged()
    {
        string signature = ScreenSignature(screens);
        if (signature == screenSignature) return;
        screenSignature = signature;
        RebuildSettingsForm();
    }

    SettingsActions Actions()
    {
        return new SettingsActions
        {
            Preview = Preview, Quit = QuitByUser, Install = DoInstall, Uninstall = DoUninstall,
            InstallUpdate = InstallUpdate, HotkeyOk = () => hotkeyOk, Welcome = ShowWelcome, HiddenToTray = OnHiddenToTray,
            Restart = RestartByUser
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
        // SW_RESTORE ramène une fenêtre réduite à son état d'avant, agrandi compris (T-038)
        if (settingsForm.WindowState == FormWindowState.Minimized) Native.ShowWindow(settingsForm.Handle, Native.SW_RESTORE);
        settingsForm.Activate();
        Native.SetForegroundWindow(settingsForm.Handle);
    }

    void Preview() { OpenManually(settingsForm != null ? Screen.FromControl(settingsForm) : Screen.PrimaryScreen); }

    void OpenManually(Screen scr)
    {
        autoCloseArmed = false;
        outsideSince = -1;
        edgeSince = -1;
        panel.ShowPanel(scr);
    }

    void DoInstall()
    {
        try
        {
            Installer.Install();
            Dialogs.Info(settingsForm, L.T("BureauCache est installé.\nTu le trouveras dans le menu Démarrer et dans Paramètres > Applications.\n\nIl va redémarrer depuis son dossier d'installation.",
                "BureauCache is installed.\nYou'll find it in the Start menu and in Settings > Apps.\n\nIt will now restart from its install folder.", "BureauCache está instalado.\nLo encontrarás en el menú Inicio y en Configuración > Aplicaciones.\n\nAhora se reiniciará desde la carpeta de instalación."));
            if (!Installer.RunningFromInstall) { RelaunchPath = Installer.Exe; ExitThread(); }
            else Settings.Fire();
        }
        catch (Exception ex)
        {
            Dialogs.Warn(settingsForm, L.T("L'installation a échoué :\n", "Installation failed:\n", "Error de instalación:\n") + ex.Message);
        }
    }

    void DoUninstall()
    {
        if (installingUpdate || !Dialogs.Ask(settingsForm, Installer.UninstallQuestion)) return;
        List<string> left = Installer.Uninstall();
        if (left.Count > 0) Installer.ReportUninstall(left);
        ExitThread();
    }

    void InstallUpdate()
    {
        if (installingUpdate) return;
        Updater.Release release = Updater.AvailableRelease;
        if (release == null) { CheckUpdateNow(); return; }
        string notes = Updater.ShortNotes(release);
        string origin = Updater.IsOfficial(release.Repository) ? "" :
            "\n\n" + L.T("Attention : cette version vient du dépôt « " + release.Repository + " », pas du dépôt officiel. Elle n'est pas signée par l'éditeur de BureauCache.",
                         "Warning: this version comes from the \"" + release.Repository + "\" repository, not the official one. It is not signed by the BureauCache publisher.", "Atención: esta versión proviene del repositorio «" + release.Repository + "», distinto del oficial. El editor de BureauCache no la ha firmado.");
        if (!Dialogs.Ask(null, L.T("Installer BureauCache " + release.Version + " ?\nLe programme va redémarrer.", "Install BureauCache " + release.Version + "?\nThe program will restart.", "¿Instalar BureauCache " + release.Version + "?\nEl programa se reiniciará.") + origin +
            (notes.Length > 0 ? "\n\n" + L.T("Nouveautés :", "What's new:", "Novedades:") + "\n" + notes : ""),
            origin.Length > 0)) return;   // dépôt non officiel : « Non » par défaut (AUD-01)
        if (installingUpdate || Updater.AvailableRelease != release) return;
        installingUpdate = true;
        string exe = Application.ExecutablePath;
        Background.Run(() =>
        {
            string error = null;
            try { Updater.Download(release, exe); }
            catch (Exception ex) { error = Http.FriendlyError(ex); }
            panel.BeginInvoke(new Action(() =>
            {
                installingUpdate = false;
                if (error == null) { RelaunchPath = exe; RelaunchAfterUpdate = true; ExitThread(); }
                else Dialogs.Warn(null, L.T("La mise à jour a échoué :\n", "Update failed:\n", "Error de actualización:\n") + error);
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
                    "BureauCache " + release.Version + " is available. Click to install.", "BureauCache " + release.Version + " está disponible. Haz clic para instalarlo.") +
                    (Updater.IsOfficial(release.Repository) ? "" : " " + L.T("Dépôt non officiel, version non signée.", "Unofficial repository, unsigned version.", "Repositorio no oficial, versión sin firmar.")), InstallUpdate);
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
            Balloon(n + L.T(" élément(s) inutilisé(s) rangé(s) dans « " + Settings.ArchiveName + " ».", " unused item(s) moved into \"" + Settings.ArchiveName + "\".", " elemento(s) sin usar movido(s) a «" + Settings.ArchiveName + "»."),
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

    // Confirme l'essai de cette version, sauf pendant le remplacement de l'exe par une autre mise à jour :
    // la confirmation effacerait alors le filet de la version suivante (Q24-03)
    void ConfirmTrial()
    {
        if (!Updater.TrialStarted || installingUpdate) return;
        Updater.TrialStarted = false;
        try { Updater.ConfirmStart(Application.ExecutablePath); } catch { }
    }

    // Sortie demandée par l'utilisateur (menu, Paramètres) : la version a démarré et répond, l'essai est
    // réussi même avant 10 s (R-26). Une sortie après une erreur WinForms ou une relance après mise à jour
    // ne passe pas par ici et ne confirme rien (Q24-08).
    void QuitByUser()
    {
        ConfirmTrial();
        ExitThread();
    }

    // Taille de l'interface changée (T-045) : BureauCache se relance une fois fermé, comme après une installation
    void RestartByUser()
    {
        RelaunchPath = Application.ExecutablePath;
        QuitByUser();
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
        try { clipboard.Close(); } catch { }
        CloseBar();
        Native.RestoreDesktopIcons();
        foreach (FileSystemWatcher w in watchers) w.Dispose();
        tray.Visible = false;
        tray.Dispose();
        base.ExitThreadCore();
    }
}
