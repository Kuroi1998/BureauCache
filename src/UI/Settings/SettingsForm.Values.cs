// BureauCache — SettingsForm (partie 4)
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

partial class SettingsForm
{
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
        lWeatherPlace.Text = string.IsNullOrEmpty(Settings.WeatherPlace) ? T("Aucun lieu choisi.", "No place chosen.") : Settings.WeatherPlace;
        bool ok = act.HotkeyOk == null || act.HotkeyOk();
        if (!Settings.HotkeyOn) { lHotStatus.Text = T("Raccourci désactivé.", "Shortcut turned off."); lHotStatus.Tag = null; }
        else if (ok) { lHotStatus.Text = T("Clique dans le champ puis appuie sur tes touches.", "Click the box, then press your keys."); lHotStatus.Tag = null; }
        else { lHotStatus.Text = T("Déjà utilisé par une autre application : choisis-en un autre.", "Already used by another app: pick another one."); lHotStatus.Tag = "err"; }
        lHotStatus.ForeColor = lHotStatus.Tag != null ? Theme.Danger : Theme.FgDim;
        if (Installer.IsInstalled && Installer.RunningFromInstall)
        {
            lInstall.Text = T("Installé, présent dans le menu Démarrer et les applications.", "Installed, in the Start menu and installed apps.");
            bInstall.Text = T("Désinstaller", "Uninstall"); bInstall.Primary = false; bInstall.Danger = true;
        }
        else if (Installer.IsInstalled)
        {
            lInstall.Text = T("Une version installée existe : la mettre à jour avec celle-ci.", "An installed version exists: update it with this one.");
            bInstall.Text = T("Mettre à jour", "Update"); bInstall.Primary = true; bInstall.Danger = false;
        }
        else
        {
            lInstall.Text = T("Ajoute BureauCache au menu Démarrer et aux applications.", "Adds BureauCache to the Start menu and installed apps.");
            bInstall.Text = T("Installer", "Install"); bInstall.Primary = true; bInstall.Danger = false;
        }
        bInstall.Invalidate();
        bool? promoted = ShellUtil.TrayIconPromoted(Application.ExecutablePath);
        lTray.Text = promoted == true ? T("✓ L'icône est affichée à côté de l'horloge.", "✓ The icon is shown next to the clock.")
                   : promoted == false ? T("Rangée dans la flèche ^ : ouvre le réglage, puis active BureauCache.", "Hidden in the ^ arrow: open the setting, then turn BureauCache on.")
                   : T("Si l'icône est dans la flèche ^, ouvre le réglage pour l'afficher.", "If the icon is in the ^ arrow, open the setting to show it.");
        bArchiveUndo.Enabled = Archiver.CanUndo;
        bArchiveUndo.Invalidate();
        var release = Updater.AvailableRelease;
        bCheck.Text = release == null ? T("Vérifier", "Check") : T("Installer la ", "Install ") + release.Version;
        if (release != null)
            lUpdate.Text = T("Version ", "Version ") + release.Version + T(" disponible.", " available.");
        else if (lUpdate.Text == "-" || lUpdate.Text.Length == 0)
            lUpdate.Text = Updater.RepoValid ? T("Clique pour vérifier.", "Click to check.") : T("Dépôt invalide.", "Invalid repository.");
    }

    void UpdateArchiveText()
    {
        int n = Archiver.Candidates().Count;
        lArchive.Text = n == 0 ? T("Rien à ranger pour le moment.", "Nothing to tidy up right now.")
                               : n + T(" élément(s) inutilisé(s) depuis " + Settings.ArchiveDays + " jours.", " item(s) unused for " + Settings.ArchiveDays + " days.");
    }

    void OnSettingsChanged()
    {
        if (IsDisposed) return;
        LoadValues();
        if (nav.Selected == 4) UpdateArchiveText();
        Restyle();
    }

    void ShowPage(int i)
    {
        for (int k = 0; k < pages.Count; k++) pages[k].Visible = k == i;
        nav.Selected = i;
        if (i == 4) { RebuildCustoms(); UpdateArchiveText(); }
    }

    public void SelectPage(int i) { ShowPage(i); }

    void Restyle()
    {
        BackColor = Theme.Bg;
        Style(this);
        Icon old = Icon;
        Icon = Ui.MakeIcon(Settings.Accent);
        if (old != null) old.Dispose();
        if (IsHandleCreated) Native.SetDarkTitleBar(Handle, Settings.Dark);
        foreach (Panel p in pages) if (p.IsHandleCreated) Native.SetDarkScrollbars(p.Handle, Settings.Dark);
        Invalidate(true);
    }

    void Style(Control c)
    {
        foreach (Control ch in c.Controls)
        {
            Color pb = ch.Parent is Card ? Theme.Card : ch.Parent is SideNav ? Theme.Side : Theme.Bg;
            if (ch is Label)
            {
                ch.BackColor = pb;
                ch.ForeColor = (ch.Tag as string) == "err" ? Theme.Danger : dim.Contains((Label)ch) ? Theme.FgDim : Theme.Fg;
            }
            else if (ch is FlatControl) ch.BackColor = ch is SideNav ? Theme.Side : pb;
            else if (ch is InputBox) { ch.BackColor = pb; ((InputBox)ch).ApplyTheme(); continue; }
            else if (ch is Panel && !(ch is Card)) ch.BackColor = Theme.Bg;
            Style(ch);
            ch.Invalidate();
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Native.SetDarkTitleBar(Handle, Settings.Dark);
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
        foreach (Panel p in pages) Native.SetDarkScrollbars(p.Handle, Settings.Dark);
        UpdateTexts();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape && !HotkeyBox.Capturing) Hide();
        base.OnKeyDown(e);
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
