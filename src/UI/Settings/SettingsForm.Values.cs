// BureauCache — SettingsForm (partie 4)
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

partial class SettingsForm
{
    Icon settingsIcon;
    int iconAccent;
    Point? restoredScroll;
    // ---------- Valeurs ----------
    void LoadValues()
    {
        loading = true;
        tHide.Checked = Settings.HideIcons;
        tQuiet.Checked = Settings.QuietStart;
        tStartup.Checked = Startup.Enabled;
        sLang.Selected = Settings.Language;
        sSide.Selected = Settings.RightSide ? 1 : 0;
        sZone.Selected = Settings.EdgeZone;
        sScreen.Selected = Settings.ScreenIdx + 1;
        slDelay.Value = Settings.Delay;
        tAuto.Checked = Settings.AutoClose;
        tFull.Checked = Settings.NoFullscreen;
        tHot.Checked = Settings.HotkeyOn;
        hk.Mods = Settings.HotMods;
        hk.Key = Settings.HotKey;
        hk.Invalidate();
        sTheme.Selected = Settings.ThemeMode;
        swatch.Selected = Settings.Accent;
        swatch.Invalidate();
        tGlass.Checked = Settings.Glass;
        tLive.Checked = Settings.LiveBlur;
        tCapture.Checked = Settings.HidePanelInCapture;
        tLive.Enabled = Settings.Glass && Settings.HidePanelInCapture;
        tAnim.Checked = Settings.Animations;
        sView.Selected = Settings.GridView ? 1 : 0;
        slWidth.Value = Settings.PanelWidth;
        tWidgets.Checked = Settings.Widgets;
        tWeather.Checked = Settings.WeatherOn;
        tPreview.Checked = Settings.HoverPreview;
        tSApps.Checked = Settings.SearchApps;
        tSRecent.Checked = Settings.SearchRecent;
        tSSettings.Checked = Settings.SearchSettings;
        tSCmd.Checked = Settings.SearchCommands;
        sEngine.Selected = Settings.WebEngine;
        tGroups.Checked = Settings.GroupCats;
        tRecents.Checked = Settings.ShowRecents;
        tSystem.Checked = Settings.ShowSystem;
        tNew.Checked = Settings.NewBadge;
        sNewDays.Selected = Math.Max(0, Array.IndexOf(NewDaysOpts, Settings.NewDays));
        tArchive.Checked = Settings.ArchiveOn;
        sArchDays.Selected = Math.Max(0, Array.IndexOf(ArchiveOpts, Settings.ArchiveDays));
        if (!repo.Box.Focused) repo.Box.Text = Settings.UpdateRepo;
        tAutoUpd.Checked = Settings.AutoUpdate;
        RebuildCustoms();
        UpdateTexts();
        loading = false;
    }

    void UpdateTexts()
    {
        lDelay.Text = Settings.Delay + " ms";
        lWidth.Text = Settings.PanelWidth + " px";
        lWeatherPlace.Text = string.IsNullOrEmpty(Settings.WeatherPlace) ? T("Aucun lieu choisi.", "No place chosen.", "Ningún lugar seleccionado.") : Settings.WeatherPlace;
        bool ok = act.HotkeyOk == null || act.HotkeyOk();
        if (!Settings.HotkeyOn) { lHotStatus.Text = T("Raccourci désactivé.", "Shortcut turned off.", "Atajo desactivado."); lHotStatus.Tag = null; }
        else if (ok) { lHotStatus.Text = T("Clique dans le champ puis appuie sur tes touches.", "Click the box, then press your keys.", "Haz clic en el campo y pulsa las teclas."); lHotStatus.Tag = null; }
        else { lHotStatus.Text = T("Déjà utilisé par une autre application : choisis-en un autre.", "Already used by another app: pick another one.", "Otra aplicación ya lo usa. Elige otro."); lHotStatus.Tag = "err"; }
        lHotStatus.ForeColor = SystemInformation.HighContrast ? SystemColors.WindowText : lHotStatus.Tag != null ? Theme.Danger : Theme.FgDim;
        if (Installer.IsInstalled && Installer.RunningFromInstall)
        {
            lInstall.Text = T("Installé, présent dans le menu Démarrer et les applications.", "Installed, in the Start menu and installed apps.", "Instalado y disponible en el menú Inicio y en Aplicaciones instaladas.");
            bInstall.Text = T("Désinstaller", "Uninstall", "Desinstalar"); bInstall.Primary = false; bInstall.Danger = true;
        }
        else if (Installer.IsInstalled)
        {
            lInstall.Text = T("Une version installée existe : la mettre à jour avec celle-ci.", "An installed version exists: update it with this one.", "Ya hay una versión instalada. Puedes actualizarla con esta.");
            bInstall.Text = T("Mettre à jour", "Update", "Actualizar"); bInstall.Primary = true; bInstall.Danger = false;
        }
        else
        {
            lInstall.Text = T("Ajoute BureauCache au menu Démarrer et aux applications.", "Adds BureauCache to the Start menu and installed apps.", "Añade BureauCache al menú Inicio y a Aplicaciones instaladas.");
            bInstall.Text = T("Installer", "Install", "Instalar"); bInstall.Primary = true; bInstall.Danger = false;
        }
        bInstall.Invalidate();
        bool? promoted = ShellUtil.TrayIconPromoted(Application.ExecutablePath);
        lTray.Text = promoted == true ? T("✓ L'icône est affichée à côté de l'horloge.", "✓ The icon is shown next to the clock.", "✓ El icono aparece junto al reloj.")
                   : promoted == false ? T("Rangée dans la flèche ^ : ouvre le réglage, puis active BureauCache.", "Hidden in the ^ arrow: open the setting, then turn BureauCache on.", "El icono está en la flecha ^. Abre el ajuste y activa BureauCache.")
                   : T("Si l'icône est dans la flèche ^, ouvre le réglage pour l'afficher.", "If the icon is in the ^ arrow, open the setting to show it.", "Si el icono está en la flecha ^, abre el ajuste para mostrarlo.");
        bArchiveUndo.Enabled = Archiver.CanUndo;
        bArchiveUndo.Invalidate();
        var release = Updater.AvailableRelease;
        bCheck.Text = release == null ? T("Vérifier", "Check", "Comprobar") : T("Installer la ", "Install ", "Instalar ") + release.Version;
        if (release != null)
            lUpdate.Text = T("Version ", "Version ", "Versión ") + release.Version + T(" disponible.", " available.", " disponible.");
        else if (lUpdate.Text == "-" || lUpdate.Text.Length == 0)
            lUpdate.Text = Updater.RepoValid ? T("Clique pour vérifier.", "Click to check.", "Haz clic para comprobar.") : T("Dépôt invalide.", "Invalid repository.", "Repositorio no válido.");
    }

    void UpdateArchiveText()
    {
        int n = Archiver.Candidates().Count;
        lArchive.Text = n == 0 ? T("Rien à ranger pour le moment.", "Nothing to tidy up right now.", "Por ahora no hay nada que archivar.")
                               : n + T(" élément(s) inutilisé(s) depuis " + Settings.ArchiveDays + " jours.", " item(s) unused for " + Settings.ArchiveDays + " days.", " elemento(s) sin usar durante " + Settings.ArchiveDays + " días.");
    }

    void OnSettingsChanged()
    {
        if (IsDisposed) return;
        LoadValues();
        if (nav.Selected == 4) UpdateArchiveText();
        ArrangePages();
        Restyle();
    }

    void ShowPage(int i)
    {
        if (i < 0 || i >= pages.Count) return;
        for (int k = 0; k < pages.Count; k++) pages[k].Visible = k == i;
        nav.Selected = i;
        pageTitle.Text = pageLayouts[i].Title;
        pageSummary.Text = pageLayouts[i].Summary;
        if (i == 4) { RebuildCustoms(); UpdateArchiveText(); }
        ArrangePages();
    }

    public void SelectPage(int i) { ShowPage(i); }

    public void RestoreWindowState(SettingsForm previous)
    {
        FormWindowState state = previous.WindowState;
        Rectangle bounds = state == FormWindowState.Normal ? previous.Bounds : previous.RestoreBounds;
        StartPosition = FormStartPosition.Manual;
        Bounds = bounds;
        WindowState = state;
        SelectPage(previous.CurrentPage);
        Point scroll = previous.restoredScroll ?? previous.pages[previous.CurrentPage].AutoScrollPosition;
        restoredScroll = previous.restoredScroll ?? new Point(-scroll.X, -scroll.Y);
        RestorePageScroll();
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        RestorePageScroll();
    }

    void RestorePageScroll()
    {
        if (!Visible || !restoredScroll.HasValue) return;
        ArrangePages();
        pages[CurrentPage].AutoScrollPosition = restoredScroll.Value;
        restoredScroll = null;
    }

    void Restyle()
    {
        BackColor = SystemInformation.HighContrast ? SystemColors.Window : Theme.Bg;
        nav.RefreshPalette();
        Style(this);
        if (settingsIcon == null || iconAccent != Settings.Accent.ToArgb())
        {
            Icon old = settingsIcon;
            settingsIcon = Ui.MakeIcon(Settings.Accent);
            Icon = settingsIcon;
            iconAccent = Settings.Accent.ToArgb();
            if (old != null) old.Dispose();
        }
        if (IsHandleCreated) Native.SetDarkTitleBar(Handle, Settings.Dark && !SystemInformation.HighContrast);
        foreach (Panel p in pages) if (p.IsHandleCreated) Native.SetDarkScrollbars(p.Handle, Settings.Dark && !SystemInformation.HighContrast);
        Invalidate(true);
    }

    void Style(Control c)
    {
        bool contrast = SystemInformation.HighContrast;
        foreach (Control ch in c.Controls)
        {
            Color pb = contrast ? (ch.Parent is SettingsNavigation ? SystemColors.Control : SystemColors.Window)
                : ch.Parent is Card ? Theme.Card : ch.Parent is SettingsNavigation ? Theme.Side : Theme.Bg;
            if (ch is Label)
            {
                ch.BackColor = pb;
                ch.ForeColor = contrast ? SystemColors.WindowText : (ch.Tag as string) == "err" ? Theme.Danger : dim.Contains((Label)ch) ? Theme.FgDim : Theme.Fg;
            }
            else if (ch is FlatControl) ch.BackColor = pb;
            else if (ch is InputBox) { ch.BackColor = pb; ((InputBox)ch).ApplyTheme(contrast); continue; }
            else if (ch is Panel && !(ch is Card) && !(ch is SettingsNavigation) && !(c is SettingsNavigation)) ch.BackColor = BackColor;
            Style(ch);
            ch.Invalidate();
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Native.SetDarkTitleBar(Handle, Settings.Dark && !SystemInformation.HighContrast);
    }

    protected override void OnSystemColorsChanged(EventArgs e)
    {
        base.OnSystemColorsChanged(e);
        if (layoutReady && !Disposing) Restyle();
    }

    // On revient souvent ici après avoir changé le réglage de Windows : on met l'état de l'icône à jour
    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        if (!loading)
        {
            if (nav.Selected == 4) RebuildCustoms();
            UpdateTexts();
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        foreach (Panel p in pages) Native.SetDarkScrollbars(p.Handle, Settings.Dark && !SystemInformation.HighContrast);
        UpdateTexts();
        ArrangePages();
    }

    protected override bool ProcessDialogKey(Keys keyData)
    {
        if (keyData != Keys.Escape || hk.Focused) return base.ProcessDialogKey(keyData);
        HideToTray();
        return true;
    }

    // La croix et le bouton « Réduire » rangent la fenêtre dans les icônes près de l'horloge
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; HideToTray(); }
        base.OnFormClosing(e);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (WindowState == FormWindowState.Minimized && Visible)
        {
            HideToTray();
            WindowState = FormWindowState.Normal; // prête à réapparaître normalement au prochain clic sur l'icône
        }
    }

    void HideToTray()
    {
        Hide();
        if (act.HiddenToTray != null) act.HiddenToTray();
    }
}
