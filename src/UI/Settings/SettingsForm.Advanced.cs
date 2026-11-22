// BureauCache — SettingsForm (partie 3)
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

partial class SettingsForm
{
    void RebuildCustoms()
    {
        string sig = string.Join("|", Settings.CustomCats.Select(c => c.Name + c.Glyph + c.Color.ToArgb()));
        if (sig == customSig) return;
        customSig = sig;
        Card saved = cur;
        int savedRow = rowY;
        cur = customCard;
        foreach (Control c in customCard.Controls.Cast<Control>().ToList()) c.Dispose();
        customCard.Controls.Clear();
        customCard.Separators.Clear();
        rowY = 0;
        foreach (CustomCat cc in Settings.CustomCats.ToList())
        {
            CustomCat c = cc;
            int count = Settings.CatOverride.Values.Count(v => string.Equals(v, c.Name, StringComparison.OrdinalIgnoreCase));
            FlatButton del = Button(T("Supprimer", "Delete"), false, S(100), () =>
            {
                if (!Ask(T("Supprimer la catégorie « " + c.Name + " » ?\nSes éléments retournent dans leur catégorie automatique.",
                           "Delete the \"" + c.Name + "\" category?\nIts items go back to their automatic category."))) return;
                Settings.DeleteCategory(c);
                Settings.Notify();
            });
            Row(c.Name, count + T(" élément(s)", " item(s)"), del, null, new GlyphTileControl(c.Glyph, c.Color));
            FlatButton edit = Button(T("Modifier", "Edit"), false, S(100), () => { if (CategoryDialog.Run(c) != null) Settings.Notify(); });
            edit.Location = new Point(del.Left - S(8) - edit.Width, del.Top);
            customCard.Controls.Add(edit);
        }
        FlatButton add = Button(T("Nouvelle catégorie", "New category"), true, S(170), () => { if (CategoryDialog.Run(null) != null) Settings.Notify(); });
        Row(Settings.CustomCats.Count == 0 ? T("Aucune catégorie personnalisée", "No custom category") : T("Ajouter une catégorie", "Add a category"),
            T("Ex. : Travail, Cours, Mods… avec ton icône et ta couleur.", "E.g. Work, School, Mods… with your own icon and color."), add, null);
        Style(customCard);
        cur = saved;
        rowY = savedRow;
    }

    void BuildAdvanced()
    {
        NewPage(T("Avancé", "Advanced"));
        Section(T("SAUVEGARDE", "BACKUP"));
        Row(T("Exporter mes réglages", "Export my settings"), T("Réglages, favoris, catégories et ordre.", "Settings, favorites, categories and order."),
            Button(T("Exporter…", "Export…"), false, S(130), Export), null);
        Row(T("Importer des réglages", "Import settings"), T("Remplace les réglages actuels.", "Replaces the current settings."),
            Button(T("Importer…", "Import…"), false, S(130), Import), null);
        EndSection();

        Section(T("MISES À JOUR", "UPDATES"));
        repo = new InputBox(S(230));
        repo.Box.Leave += (s, e) => { if (repo.Box.Text.Trim() != Settings.UpdateRepo) { Settings.UpdateRepo = repo.Box.Text.Trim(); Settings.Notify(); } };
        repo.Box.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; ActiveControl = null; } };
        repo.Box.HandleCreated += (s, e) => Native.SendMessage(repo.Box.Handle, 0x1501, (IntPtr)1, Updater.DefaultRepo);
        Row(T("Dépôt GitHub", "GitHub repository"), T("Laisse vide pour le dépôt officiel.", "Leave empty for the official repository."), repo, null);
        tAutoUpd = NewToggle(v => Settings.AutoUpdate = v);
        Row(T("Vérifier chaque jour", "Check daily"), null, tAutoUpd, null);
        bCheck = Button(T("Vérifier", "Check"), true, S(170), CheckUpdate);
        lUpdate = Row(T("Version ", "Version ") + Installer.Version, "-", bCheck, null);
        EndSection();

        Section(T("SÉCURITÉ", "SAFETY"));
        Note(T("Si BureauCache se ferme de façon inattendue, un petit processus de surveillance réaffiche automatiquement tes icônes du bureau.",
               "If BureauCache closes unexpectedly, a small watchdog process automatically brings your desktop icons back."));
        EndSection();
    }

    void Export()
    {
        Settings.Save();
        using (SaveFileDialog d = new SaveFileDialog())
        {
            d.FileName = T("BureauCache-reglages", "BureauCache-settings") + ".ini";
            d.Filter = T("Réglages BureauCache", "BureauCache settings") + " (*.ini)|*.ini";
            if (d.ShowDialog(this) != DialogResult.OK) return;
            try { File.Copy(Settings.FilePath, d.FileName, true); Dialogs.Info(this, T("Réglages exportés.", "Settings exported.")); }
            catch (Exception ex) { Dialogs.Info(this, ex.Message); }
        }
    }

    void Import()
    {
        using (OpenFileDialog d = new OpenFileDialog())
        {
            d.Filter = T("Réglages BureauCache", "BureauCache settings") + " (*.ini)|*.ini";
            if (d.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                string txt = File.ReadAllText(d.FileName);
                if (!txt.Contains("HideIcons=")) throw new Exception(T("Ce fichier n'est pas un fichier de réglages BureauCache.", "This is not a BureauCache settings file."));
                if (!Ask(T("Remplacer tes réglages actuels par ceux de ce fichier ?", "Replace your current settings with this file?"))) return;
                Settings.Load(d.FileName);
                L.Refresh();
                Settings.Notify();
                Dialogs.Info(this, T("Réglages importés.", "Settings imported."));
            }
            catch (Exception ex) { Dialogs.Info(this, ex.Message); }
        }
    }

    bool updateReady;

    void CheckUpdate()
    {
        if (updateReady) { if (act.InstallUpdate != null) act.InstallUpdate(); return; }
        Settings.UpdateRepo = repo.Box.Text.Trim();
        Settings.SaveQuiet();
        if (!Updater.RepoValid)
        {
            lUpdate.Text = T("Dépôt invalide (format : utilisateur/projet).", "Invalid repository (format: owner/name).");
            return;
        }
        bCheck.Enabled = false;
        lUpdate.Text = T("Vérification…", "Checking…");
        Background.Run(() =>
        {
            bool newer = Updater.Check();
            try
            {
                BeginInvoke(new Action(() =>
                {
                    bCheck.Enabled = true;
                    if (newer)
                    {
                        updateReady = true;
                        lUpdate.Text = T("Version ", "Version ") + Updater.Latest + T(" disponible.", " is available.");
                        bCheck.Text = T("Installer la ", "Install ") + Updater.Latest;
                    }
                    else if (Updater.Error != null) lUpdate.Text = Updater.Error;
                    else lUpdate.Text = T("Tu as la dernière version.", "You're up to date.");
                }));
            }
            catch { }
        });
    }

    FlatButton ResetButton(string text, Action a)
    {
        return Button(text, false, S(130), () =>
        {
            if (!Ask(T("Confirmer : ", "Confirm: ") + text.ToLower(L.Culture) + " ?")) return;
            a();
            Settings.Notify();
        });
    }
}
