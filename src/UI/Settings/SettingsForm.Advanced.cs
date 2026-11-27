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
            row.Value.Text = counts[row.Key.Name].Count() + T(" élément(s)", " item(s)", " elemento(s)");
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
        cardSections[customCard].Rows.Clear();
        foreach (CustomCat cc in Settings.CustomCats.ToList())
        {
            CustomCat c = cc;
            FlatButton del = Button(T("Supprimer", "Delete", "Eliminar"), false, S(100), () =>
            {
                if (!Ask(T("Supprimer la catégorie « " + c.Name + " » ?\nSes éléments retournent dans leur catégorie automatique.",
                           "Delete the \"" + c.Name + "\" category?\nIts items go back to their automatic category.", "¿Eliminar la categoría «" + c.Name + "»?\nSus elementos volverán a la categoría automática."))) return;
                Settings.DeleteCategory(c);
                Settings.Notify();
            });
            FlatButton edit = Button(T("Modifier", "Edit", "Editar"), false, S(100), () => { if (CategoryDialog.Run(c) != null) Settings.Notify(); });
            del.Danger = true;
            customCounts[c] = Row(c.Name, "", del, null, new GlyphTileControl(c.Glyph, c.Color), edit);
        }
        FlatButton add = Button(T("Nouvelle catégorie", "New category", "Nueva categoría"), true, S(170), () => { if (CategoryDialog.Run(null) != null) Settings.Notify(); });
        Row(Settings.CustomCats.Count == 0 ? T("Aucune catégorie personnalisée", "No custom category", "Sin categorías personalizadas") : T("Ajouter une catégorie", "Add a category", "Añadir una categoría"),
            T("Ex. : Travail, Cours, Mods… avec ton icône et ta couleur.", "E.g. Work, School, Mods… with your own icon and color.", "Por ejemplo, Trabajo, Estudios o Mods, con tu propio icono y color."), add, null);
        Style(customCard);
        cur = saved;
    }

    void BuildAdvanced()
    {
        NewPage(T("Avancé", "Advanced", "Opciones avanzadas"), T("Sauvegardes et mises à jour.", "Backups and updates.", "Copias de seguridad y actualizaciones."));
        Subtab(T("Sauvegarde", "Backups", "Copias"));
        Section(T("SAUVEGARDE", "BACKUP", "COPIA DE SEGURIDAD"));
        lExport = Row(T("Exporter mes réglages", "Export my settings", "Exportar mi configuración"), T("Réglages, favoris, catégories et ordre.", "Settings, favorites, categories and order.", "Configuración, favoritos, categorías y orden."),
            Button(T("Exporter…", "Export…", "Exportar…"), false, S(130), Export), null);
        lImport = Row(T("Importer des réglages", "Import settings", "Importar configuración"), T("Remplace les réglages actuels.", "Replaces the current settings.", "Sustituye la configuración actual."),
            Button(T("Importer…", "Import…", "Importar…"), false, S(130), Import), null);
        lRestore = Row(T("Restaurer une sauvegarde", "Restore a backup", "Restaurar una copia de seguridad"),
            T("Une copie automatique par jour, sur les " + SettingsBackup.Kept + " derniers jours.", "One automatic copy per day, for the last " + SettingsBackup.Kept + " days.", "Una copia automática al día, durante los últimos " + SettingsBackup.Kept + " días."),
            Button(T("Restaurer…", "Restore…", "Restaurar…"), false, S(130), RestoreBackup), null);

        Subtab(T("Mises à jour", "Updates", "Actualización"));
        Section(T("MISES À JOUR", "UPDATES", "ACTUALIZACIONES"));
        repo = new InputBox(S(230));
        repo.Box.Leave += (s, e) => { if (repo.Box.Text.Trim() != Settings.UpdateRepo) { Settings.UpdateRepo = repo.Box.Text.Trim(); ResetUpdateButton(); Settings.Notify(); } };
        repo.Box.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; ActiveControl = null; } };
        repo.Box.HandleCreated += (s, e) => Native.SendMessage(repo.Box.Handle, 0x1501, (IntPtr)1, Updater.DefaultRepo);
        Row(T("Dépôt GitHub", "GitHub repository", "Repositorio de GitHub"), T("Laisse vide pour le dépôt officiel.", "Leave empty for the official repository.", "Deja este campo vacío para usar el repositorio oficial."), repo, null);
        tAutoUpd = NewToggle(v => Settings.AutoUpdate = v);
        Row(T("Vérifier chaque jour", "Check daily", "Comprobar a diario"), null, tAutoUpd, null);
        bCheck = Button(T("Vérifier", "Check", "Comprobar"), true, S(170), CheckUpdate);
        lUpdate = Row(T("Version ", "Version ", "Versión ") + Installer.Version, "-", bCheck, null);

        // Le nettoyage rapide n'était accessible que par un clic droit sur la tuile Disque (Gemini B2)
        Subtab(T("Entretien", "Maintenance", "Mantenimiento"));
        Section(T("ENTRETIEN", "MAINTENANCE", "MANTENIMIENTO"));
        Row(T("Nettoyage rapide", "Quick clean-up", "Limpieza rápida"),
            T("Caches et fichiers temporaires de ton compte, avec les tailles avant de supprimer. Aussi par clic droit sur la tuile Disque.",
              "Caches and temporary files of your account, with sizes before deleting. Also by right-clicking the Disk tile.",
              "Cachés y archivos temporales de tu cuenta, con los tamaños antes de borrar. También con clic derecho en el mosaico Disco."),
            Button(T("Analyser…", "Scan…", "Analizar…"), false, S(130), () => CleanupDialog.Run(this)), null);

        Section(T("SÉCURITÉ", "SAFETY", "SEGURIDAD"));
        Note(T("Si BureauCache se ferme de façon inattendue, un petit processus de surveillance réaffiche automatiquement tes icônes du bureau.",
               "If BureauCache closes unexpectedly, a small watchdog process automatically brings your desktop icons back.", "Si BureauCache se cierra de forma inesperada, un proceso de supervisión restaura los iconos del escritorio."));
    }

    void Export()
    {
        Begin(lExport);
        if (!Settings.Save())
        {
            Dialogs.Warn(this, T("Impossible de sauvegarder les réglages.", "Could not save settings.", "No se pudo guardar la configuración."));
            return;
        }
        using (SaveFileDialog d = new SaveFileDialog())
        {
            d.FileName = T("BureauCache-reglages", "BureauCache-settings", "BureauCache-configuracion") + ".ini";
            d.Filter = T("Réglages BureauCache", "BureauCache settings", "Configuración de BureauCache") + " (*.ini)|*.ini";
            if (d.ShowDialog(this) != DialogResult.OK) return;
            try { Settings.Flush(); FileOps.CopyFile(Settings.FilePath, d.FileName); Done(lExport, T("Réglages exportés dans ", "Settings exported to ", "Configuración exportada en ") + System.IO.Path.GetFileName(d.FileName) + "."); }
            catch (Exception ex) { Dialogs.Warn(this, ex.Message); }
        }
    }

    void Import()
    {
        Begin(lImport);
        using (OpenFileDialog d = new OpenFileDialog())
        {
            d.Filter = T("Réglages BureauCache", "BureauCache settings", "Configuración de BureauCache") + " (*.ini)|*.ini";
            if (d.ShowDialog(this) != DialogResult.OK) return;
            // Fichier vérifié avant tout ; import transactionnel et réversible, réussite annoncée seulement une fois
            // les réglages enregistrés (D-007, D007-10)
            if (!SettingsBackup.IsValid(d.FileName))
            {
                Dialogs.Warn(this, T("Fichier de réglages invalide ou trop volumineux.", "Invalid or oversized settings file.", "Archivo de configuración no válido o demasiado grande."));
                return;
            }
            // Pas de confirmation : l'import est réversible par « Restaurer une sauvegarde… » (Gemini A1, A6)
            List<string> kept = new List<string>();
            if (!SettingsBackup.ImportFrom(d.FileName, kept))
            {
                Dialogs.Warn(this, T("Les réglages n'ont pas pu être importés.", "The settings couldn't be imported.", "No se pudo importar la configuración."));
                return;
            }
            L.Refresh();
            Settings.Notify();
            if (!IsDisposed) Done(lImport, T("Réglages importés. Les précédents sont dans « Restaurer une sauvegarde… ».", "Settings imported. The previous ones are in \"Restore a backup…\".", "Configuración importada. La anterior está en «Restaurar una copia de seguridad…».") +
                (kept.Count == 0 ? "" : " " + T("Gardés tels qu'ils étaient sur ce PC : ", "Kept as they were on this PC: ", "Se conservan como estaban en este equipo: ") + string.Join(", ", kept.ToArray()) + "."));
        }
    }

    void RestoreBackup()
    {
        Begin(lRestore);
        if (!BackupDialog.Run(this)) return;
        L.Refresh();
        Settings.Notify();
        if (!IsDisposed) Done(lRestore, T("Réglages restaurés. « Avant une restauration » permet de revenir en arrière.", "Settings restored. \"Before a restore\" lets you go back.", "Configuración restaurada. «Antes de una restauración» permite volver atrás."));
    }

    // Résultat d'une action affiché sous son réglage, au lieu d'une boîte à valider (Gemini A2, A6). Le texte
    // d'origine est gardé, et revient au début de la tentative suivante (QA34-06).
    readonly Dictionary<Label, string> doneOriginal = new Dictionary<Label, string>();

    void Done(Label description, string text)
    {
        if (description == null || description.IsDisposed) return;
        if (!doneOriginal.ContainsKey(description)) doneOriginal[description] = description.Text;
        description.Text = "✓ " + text;
    }

    void Begin(Label description)
    {
        string original;
        if (description != null && !description.IsDisposed && doneOriginal.TryGetValue(description, out original)) description.Text = original;
    }

    void ResetUpdateButton()
    {
        bCheck.Text = T("Vérifier", "Check", "Comprobar");
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
            lUpdate.Text = T("Dépôt invalide (format : utilisateur/projet).", "Invalid repository (format: owner/name).", "Repositorio no válido (formato: propietario/nombre).");
            return;
        }
        bCheck.Enabled = false;
        lUpdate.Text = T("Vérification…", "Checking…", "Comprobando…");
        Background.Run(() =>
        {
            Updater.Release release = Updater.Check();
            // Le résultat est retenu tout de suite, avant l'affichage : une fenêtre détruite entre-temps
            // (changement de langue) ne le perd plus, et l'icône ou la fenêtre suivante l'affichent (R-32, Q24-09)
            bool accepted = Updater.AcceptCheck(release);
            try
            {
                BeginInvoke(new Action(() =>
                {
                    bCheck.Enabled = true;
                    ResetUpdateButton();
                    if (!accepted) { ResetUpdateButton(); UpdateTexts(); return; }
                    if (release.Available)
                    {
                        lUpdate.Text = T("Version ", "Version ", "Versión ") + release.Version + T(" disponible.", " is available.", " está disponible.");
                        bCheck.Text = T("Installer la ", "Install ", "Instalar ") + release.Version;
                    }
                    else if (release.Error != null) lUpdate.Text = release.Error;
                    else if (release.Incomplete) lUpdate.Text = Updater.IncompleteText(release);
                    else lUpdate.Text = T("Tu as la dernière version.", "You're up to date.", "Tienes la última versión.");
                }));
            }
            catch { }
        });
    }

    FlatButton ResetButton(string text, Action a)
    {
        return Button(text, false, S(130), () =>
        {
            if (!Ask(T("Confirmer : ", "Confirm: ", "Confirmar: ") + text.ToLower(L.Culture) + " ?")) return;
            a();
            Settings.Notify();
        });
    }
}
