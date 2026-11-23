// BureauCache — SettingsForm (partie 3)
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

partial class SettingsForm
{
    readonly Dictionary<CustomCat, Label> customCounts = new Dictionary<CustomCat, Label>();

    void RebuildCustoms()
    {
        string sig = string.Join("|", Settings.CustomCats.Select(c => c.Name + c.Glyph + c.Color.ToArgb()));
        if (sig != customSig || Settings.CustomCats.Any(c => !customCounts.ContainsKey(c)))
        {
            BuildCustomRows();
            customSig = sig;
        }
        if (customCounts.Count == 0) return;
        var counts = Settings.CatOverride.Values.ToLookup(name => name, StringComparer.OrdinalIgnoreCase);
        foreach (var row in customCounts)
            row.Value.Text = counts[row.Key.Name].Count() + T(" élément(s)", " item(s)");
    }

    void BuildCustomRows()
    {
        Card saved = cur;
        cur = customCard;
        foreach (Control c in customCard.Controls.Cast<Control>().ToList())
        {
            Label label = c as Label;
            if (label != null) dim.Remove(label);
            c.Dispose();
        }
        customCard.Separators.Clear();
        customCounts.Clear();
        cardRows[customCard].Clear();
        foreach (CustomCat cc in Settings.CustomCats.ToList())
        {
            CustomCat c = cc;
            FlatButton del = Button(T("Supprimer", "Delete"), false, S(100), () =>
            {
                if (!Ask(T("Supprimer la catégorie « " + c.Name + " » ?\nSes éléments retournent dans leur catégorie automatique.",
                           "Delete the \"" + c.Name + "\" category?\nIts items go back to their automatic category."))) return;
                Settings.DeleteCategory(c);
                Settings.Notify();
            });
            FlatButton edit = Button(T("Modifier", "Edit"), false, S(100), () => { if (CategoryDialog.Run(c) != null) Settings.Notify(); });
            del.Danger = true;
            customCounts[c] = Row(c.Name, "", del, null, new GlyphTileControl(c.Glyph, c.Color), edit);
        }
        FlatButton add = Button(T("Nouvelle catégorie", "New category"), true, S(170), () => { if (CategoryDialog.Run(null) != null) Settings.Notify(); });
        Row(Settings.CustomCats.Count == 0 ? T("Aucune catégorie personnalisée", "No custom category") : T("Ajouter une catégorie", "Add a category"),
            T("Ex. : Travail, Cours, Mods… avec ton icône et ta couleur.", "E.g. Work, School, Mods… with your own icon and color."), add, null);
        Style(customCard);
        cur = saved;
        ArrangePages();
    }

    void BuildAdvanced()
    {
        NewPage(T("Avancé", "Advanced"), T("Sauvegardes et mises à jour.", "Backups and updates."));
        Section(T("SAUVEGARDE", "BACKUP"));
        Row(T("Exporter mes réglages", "Export my settings"), T("Réglages, favoris, catégories et ordre.", "Settings, favorites, categories and order."),
            Button(T("Exporter…", "Export…"), false, S(130), Export), null);
        Row(T("Importer des réglages", "Import settings"), T("Remplace les réglages actuels.", "Replaces the current settings."),
            Button(T("Importer…", "Import…"), false, S(130), Import), null);

        Section(T("MISES À JOUR", "UPDATES"));
        repo = new InputBox(S(230));
        repo.Box.Leave += (s, e) => { if (repo.Box.Text.Trim() != Settings.UpdateRepo) { Settings.UpdateRepo = repo.Box.Text.Trim(); ResetUpdateButton(); Settings.Notify(); } };
        repo.Box.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; ActiveControl = null; } };
        repo.Box.HandleCreated += (s, e) => Native.SendMessage(repo.Box.Handle, 0x1501, (IntPtr)1, Updater.DefaultRepo);
        Row(T("Dépôt GitHub", "GitHub repository"), T("Laisse vide pour le dépôt officiel.", "Leave empty for the official repository."), repo, null);
        tAutoUpd = NewToggle(v => Settings.AutoUpdate = v);
        Row(T("Vérifier chaque jour", "Check daily"), null, tAutoUpd, null);
        bCheck = Button(T("Vérifier", "Check"), true, S(170), CheckUpdate);
        lUpdate = Row(T("Version ", "Version ") + Installer.Version, "-", bCheck, null);

        Section(T("SÉCURITÉ", "SAFETY"));
        Note(T("Si BureauCache se ferme de façon inattendue, un petit processus de surveillance réaffiche automatiquement tes icônes du bureau.",
               "If BureauCache closes unexpectedly, a small watchdog process automatically brings your desktop icons back."));
    }

    void Export()
    {
        if (!Settings.Save())
        {
            Dialogs.Warn(this, T("Impossible de sauvegarder les réglages.", "Could not save settings."));
            return;
        }
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
                if (!Ask(T("Remplacer tes réglages actuels par ceux de ce fichier ?", "Replace your current settings with this file?"))) return;
                if (!Settings.Load(d.FileName))
                    throw new IOException(T("Fichier de réglages invalide ou trop volumineux.", "Invalid or oversized settings file."));
                L.Refresh();
                Settings.Notify();
                Dialogs.Info(this, T("Réglages importés.", "Settings imported."));
            }
            catch (Exception ex) { Dialogs.Info(this, ex.Message); }
        }
    }

    void ResetUpdateButton()
    {
        bCheck.Text = T("Vérifier", "Check");
        lUpdate.Text = "-";
    }

    void CheckUpdate()
    {
        if (Updater.AvailableRelease != null && string.Equals(repo.Box.Text.Trim(), Settings.UpdateRepo, StringComparison.Ordinal))
        {
            if (act.InstallUpdate != null) act.InstallUpdate();
            return;
        }
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
            Updater.Release release = Updater.Check();
            try
            {
                BeginInvoke(new Action(() =>
                {
                    bCheck.Enabled = true;
                    ResetUpdateButton();
                    if (!Updater.AcceptCheck(release)) { ResetUpdateButton(); UpdateTexts(); return; }
                    if (release.Available)
                    {
                        lUpdate.Text = T("Version ", "Version ") + release.Version + T(" disponible.", " is available.");
                        bCheck.Text = T("Installer la ", "Install ") + release.Version;
                    }
                    else if (release.Error != null) lUpdate.Text = release.Error;
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
