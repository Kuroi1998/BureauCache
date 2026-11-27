// BureauCache — icône de notification (près de l'horloge) : clics, menu, infobulle et point d'information
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

partial class App
{
    // Le clic simple attend la durée d'un double-clic avant d'agir, pour ne pas ouvrir le panneau ET les paramètres
    readonly System.Windows.Forms.Timer clickTimer = new System.Windows.Forms.Timer();
    bool updateAvailable { get { return Updater.AvailableRelease != null; } }
    bool trayDoubleClick;
    bool panelWasOpenAtTrayClick;
    string trayState;

    void SetupTray()
    {
        trayMenu = Menus.Create();
        tray = new NotifyIcon();
        // Menu affiché par nous (et non automatiquement) : il passe au premier plan, se ferme quand on clique ailleurs
        // et s'ouvre au-dessus de la barre des tâches, même quand aucune fenêtre de BureauCache n'est visible.
        tray.MouseUp += (s, e) =>
        {
            if (e.Button == MouseButtons.Right) ShowTrayMenu();
            if (e.Button != MouseButtons.Left) return;
            if (trayDoubleClick) { trayDoubleClick = false; return; }
            panelWasOpenAtTrayClick = panel.IsOpen ||
                (DateTime.Now - panel.LastHidden).TotalMilliseconds < 200;
            clickTimer.Stop();
            clickTimer.Start();
        };
        tray.MouseDoubleClick += (s, e) =>
        {
            if (e.Button != MouseButtons.Left) return;
            trayDoubleClick = true;
            clickTimer.Stop();
            ShowSettings();
        };
        clickTimer.Interval = Math.Max(200, SystemInformation.DoubleClickTime);
        clickTimer.Tick += (s, e) => { clickTimer.Stop(); TogglePanelFromTray(); };
        tray.BalloonTipClicked += (s, e) => { if (balloonAction != null) balloonAction(); };
        RefreshTray();
        tray.Visible = true;
    }

    void TogglePanelFromTray()
    {
        // Cliquer sur l'icône fait perdre le focus au panneau, qui se ferme juste avant ce clic : on ne le rouvre pas
        if (panelWasOpenAtTrayClick && !Settings.Pinned) panel.HidePanel();
        else OpenManually(TargetScreen(Cursor.Position));
    }

    void ShowTrayMenu()
    {
        BuildTrayMenu();
        Native.SetForegroundWindow(hotkey.Handle);
        Point p = Cursor.Position;
        Rectangle wa = Screen.FromPoint(p).WorkingArea;
        bool bottom = p.Y > wa.Top + wa.Height / 2, right = p.X > wa.Left + wa.Width / 2;
        ToolStripDropDownDirection dir = bottom ? (right ? ToolStripDropDownDirection.AboveLeft : ToolStripDropDownDirection.AboveRight)
                                                : (right ? ToolStripDropDownDirection.BelowLeft : ToolStripDropDownDirection.BelowRight);
        trayMenu.Show(p, dir);
        // Le menu lui-même doit être au premier plan pour recevoir le clavier et se fermer quand on clique ailleurs
        Native.SetForegroundWindow(trayMenu.Handle);
        trayMenu.Focus();
        Native.PostMessage(hotkey.Handle, 0 /*WM_NULL*/, IntPtr.Zero, IntPtr.Zero);
    }

    void Balloon(string text, Action onClick)
    {
        balloonAction = onClick;
        tray.ShowBalloonTip(3000, "BureauCache", text, ToolTipIcon.Info);
    }

    void BuildTrayMenu()
    {
        Menus.Clear(trayMenu.Items);
        ToolStripMenuItem title = new ToolStripMenuItem("BureauCache " + AppInfo.Version +
            (updateAvailable ? L.T(" · mise à jour disponible", " · update available", " · actualización disponible") : "")) { Enabled = false };
        trayMenu.Items.Add(title);
        trayMenu.Items.Add(new ToolStripSeparator());

        ToolStripMenuItem open = Menus.Item(L.T("Ouvrir la liste", "Open the list", "Abrir la lista"), Glyphs.List, () => OpenManually(TargetScreen(Cursor.Position)));
        open.Font = Ui.Font(trayMenu.Font.Name, trayMenu.Font.Size, FontStyle.Bold);
        open.ShortcutKeyDisplayString = HotkeyBox.Format(Settings.HotMods, Settings.HotKey);
        trayMenu.Items.Add(open);
        trayMenu.Items.Add(Menus.Item(L.T("Paramètres…", "Settings…", "Configuración…"), Glyphs.Settings, ShowSettings));
        // Comme le menu de la barre de Windows : utile surtout quand la barre BureauCache la remplace (T-037)
        ToolStripMenuItem taskManager = Menus.Item(L.T("Gestionnaire des tâches", "Task Manager", "Administrador de tareas"), Glyphs.TaskManager,
            () => { try { ShellUtil.Open("taskmgr.exe", null); } catch { } });
        taskManager.ShortcutKeyDisplayString = L.T("Ctrl + Maj + Échap", "Ctrl + Shift + Esc", "Ctrl + Mayús + Esc");
        trayMenu.Items.Add(taskManager);
        trayMenu.Items.Add(new ToolStripSeparator());

        ToolStripMenuItem pin = Menus.Item(L.T("Épingler le panneau", "Pin the panel", "Fijar el panel"), Glyphs.Pin, () => panel.TogglePin());
        pin.Checked = Settings.Pinned;
        trayMenu.Items.Add(pin);
        ToolStripMenuItem hide = Menus.Item(L.T("Cacher les icônes du bureau", "Hide desktop icons", "Ocultar los iconos del escritorio"), Glyphs.Eye, () => { Settings.HideIcons = !Settings.HideIcons; Settings.Notify(); });
        hide.Checked = Settings.HideIcons;
        trayMenu.Items.Add(hide);
        ToolStripMenuItem start = Menus.Item(L.T("Lancer au démarrage de Windows", "Start with Windows", "Iniciar con Windows"), Glyphs.Power, () =>
        {
            try { Startup.Set(!Startup.Enabled, Installer.IsInstalled ? Installer.Exe : null); } catch { }
            Settings.Fire();
        });
        start.Checked = Startup.Enabled;
        trayMenu.Items.Add(start);
        trayMenu.Items.Add(new ToolStripSeparator());

        if (updateAvailable && Updater.Latest != null)
            trayMenu.Items.Add(Menus.Item(L.T("Installer la version ", "Install version ", "Instalar la versión ") + Updater.Latest, Glyphs.Download, InstallUpdate));
        else
            trayMenu.Items.Add(Menus.Item(L.T("Rechercher une mise à jour", "Check for updates", "Buscar actualizaciones"), Glyphs.Restart, CheckUpdateNow));
        trayMenu.Items.Add(Menus.Item(L.T("Visite guidée", "Guided tour", "Visita guiada"), Glyphs.School, ShowWelcome));
        trayMenu.Items.Add(new ToolStripSeparator());
        if (Settings.TaskbarOn)
            trayMenu.Items.Add(Menus.Item(L.T("Rétablir la barre des tâches de Windows", "Bring back the Windows taskbar", "Restablecer la barra de tareas de Windows"), Glyphs.Undo, RestoreWindowsBarByUser));
        trayMenu.Items.Add(Menus.Item(Settings.TaskbarOn
            ? L.T("Quitter (réaffiche les icônes et la barre Windows)", "Quit (shows icons and the Windows taskbar again)", "Salir (vuelve a mostrar los iconos y la barra de Windows)")
            : L.T("Quitter (réaffiche les icônes)", "Quit (shows icons again)", "Salir (vuelve a mostrar los iconos)"), Glyphs.Power, QuitByUser));
    }

    // Vérification demandée depuis le menu : on répond toujours par une bulle
    void CheckUpdateNow()
    {
        Background.Run(() =>
        {
            Updater.Release release = Updater.Check();
            try
            {
                panel.BeginInvoke(new Action(() =>
                {
                    if (!Updater.AcceptCheck(release)) return;
                    RefreshTray();
                    if (release.Available) Balloon(L.T("La version " + Updater.Latest + " est disponible. Clique ici pour l'installer.",
                                           "Version " + Updater.Latest + " is available. Click here to install it.", "La versión " + Updater.Latest + " está disponible. Haz clic para instalarla."), InstallUpdate);
                    else if (release.Incomplete) Balloon(Updater.IncompleteText(release), null);
                    else if (release.Error != null) Balloon(L.T("Vérification impossible : ", "Couldn't check: ", "No se pudo comprobar: ") + release.Error, null);
                    else Balloon(L.T("Tu as la dernière version (" + AppInfo.Version + ").", "You're up to date (" + AppInfo.Version + ").", "Tienes la última versión (" + AppInfo.Version + ")."), null);
                }));
            }
            catch { }
        });
    }

    // Icône et infobulle à jour : point orange = mise à jour, point vert = nouveaux fichiers sur le bureau
    void RefreshTray()
    {
        if (tray == null) return;
        int newFiles = Settings.NewBadge ? panel.All.Count(i => i.IsNew) : 0;
        Color? badge = updateAvailable ? Color.FromArgb(249, 115, 22) : newFiles > 0 ? Color.FromArgb(22, 163, 74) : (Color?)null;
        string tip = "BureauCache" + (Settings.HotkeyOn ? " · " + HotkeyBox.Format(Settings.HotMods, Settings.HotKey) : "");
        if (updateAvailable && Updater.Latest != null) tip += "\n" + L.T("Mise à jour ", "Update ", "Actualización ") + Updater.Latest + L.T(" disponible", " available", " disponible");
        else if (newFiles > 0) tip += "\n" + newFiles + L.T(newFiles > 1 ? " nouveaux fichiers" : " nouveau fichier", newFiles > 1 ? " new files" : " new file", newFiles > 1 ? " archivos nuevos" : " archivo nuevo");
        if (tip.Length > 63) tip = tip.Substring(0, 62) + "…"; // limite imposée par Windows

        string state = Settings.Accent.ToArgb() + "|" + (badge.HasValue ? badge.Value.ToArgb().ToString() : "-");
        if (state != trayState)
        {
            trayState = state;
            Icon old = tray.Icon;
            tray.Icon = Ui.MakeIcon(Settings.Accent, badge);
            if (old != null) old.Dispose();
        }
        if (tray.Text != tip) tray.Text = tip;
    }

    // Fenêtre de paramètres réduite ou fermée : la première fois, on explique où BureauCache continue de tourner
    void OnHiddenToTray()
    {
        if (Settings.TrayHintShown) return;
        Settings.TrayHintShown = true;
        Settings.SaveQuiet();
        Balloon(L.T("BureauCache continue ici, près de l'horloge (flèche ^ si l'icône est cachée). Clic : la liste · double-clic : les paramètres.",
                    "BureauCache keeps running here, by the clock (^ arrow if the icon is hidden). Click: the list · double-click: settings.", "BureauCache sigue activo junto al reloj (en la flecha ^ si el icono está oculto). Un clic abre la lista y un doble clic abre la configuración."), null);
    }
}
