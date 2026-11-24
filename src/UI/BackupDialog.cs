// BureauCache — BackupDialog
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

// =====================================================================
//  Restaurer une sauvegarde des réglages (D-007)
// =====================================================================
class BackupDialog : ThemedDialog
{
    readonly List<SettingsBackup.Entry> entries = SettingsBackup.List();
    readonly ListBox list;
    readonly Label status;

    // Vrai si des réglages ont été restaurés
    public static bool Run(IWin32Window owner)
    {
        using (BackupDialog d = new BackupDialog()) return d.ShowDialog(owner) == DialogResult.OK;
    }

    BackupDialog() : base(L.T("Restaurer une sauvegarde", "Restore a backup", "Restaurar una copia de seguridad"), Ui.S(480), Ui.S(330))
    {
        int pad = Ui.S(20), w = ClientSize.Width - 2 * pad;
        Label intro = AddLabel(L.T("BureauCache garde une copie de tes réglages par jour, sur les " + SettingsBackup.Kept + " derniers jours. Tes réglages actuels sont mis de côté avant la restauration : tu pourras revenir en arrière.",
            "BureauCache keeps one copy of your settings per day, for the last " + SettingsBackup.Kept + " days. Your current settings are set aside before restoring, so you can go back.",
            "BureauCache guarda una copia diaria de tu configuración durante los últimos " + SettingsBackup.Kept + " días. Tu configuración actual se aparta antes de restaurar, así que podrás volver atrás."), pad, Ui.S(16));
        intro.MaximumSize = new Size(w, 0);
        int y = Ui.S(16) + intro.GetPreferredSize(new Size(w, 0)).Height + Ui.S(12);

        list = new ListBox
        {
            Location = new Point(pad, y), Size = new Size(w, Ui.S(150)), BorderStyle = BorderStyle.None,
            BackColor = Theme.Surface, ForeColor = Theme.Fg, Font = Ui.Font("Segoe UI", 10f), IntegralHeight = false,
            HorizontalScrollbar = true   // libellés longs (espagnol) lisibles en entier (D007-08)
        };
        foreach (SettingsBackup.Entry e in entries) list.Items.Add(Describe(e));
        if (list.Items.Count > 0) list.SelectedIndex = 0;
        list.DoubleClick += (s, e) => Restore();
        list.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Restore(); } };
        Controls.Add(list);

        status = AddLabel(entries.Count == 0
            ? L.T("Aucune sauvegarde pour l'instant : la première sera faite au prochain changement de réglage.",
                  "No backup yet: the first one will be made at the next settings change.",
                  "Todavía no hay copias: la primera se hará en el próximo cambio de configuración.")
            : "", pad, list.Bottom + Ui.S(8));
        status.MaximumSize = new Size(w, 0);
        ClientSize = new Size(ClientSize.Width, list.Bottom + Ui.S(8) + Math.Max(Ui.S(20), status.GetPreferredSize(new Size(w, 0)).Height) + Ui.S(70));
        AddButtons(Restore, L.T("Restaurer", "Restore", "Restaurar"));
        // Rien à restaurer : le bouton est grisé (D007-08)
        FlatButton ok = Controls.OfType<FlatButton>().First(b => b.Primary);
        ok.Enabled = list.SelectedIndex >= 0;
        list.SelectedIndexChanged += (s, e) => ok.Enabled = list.SelectedIndex >= 0;
        Shown += (s, e) => list.Focus();
    }

    static string Describe(SettingsBackup.Entry e)
    {
        string when;
        if (e.BeforeRestore)
            when = L.T("Avant la dernière restauration", "Before the last restore", "Antes de la última restauración") + " (" + e.Date.ToString("g", L.Culture) + ")";
        else if (e.Date.Date == DateTime.Today) when = L.T("Aujourd'hui", "Today", "Hoy");
        else if (e.Date.Date == DateTime.Today.AddDays(-1)) when = L.T("Hier", "Yesterday", "Ayer");
        else when = e.Date.ToString("D", L.Culture);
        return when + "  ·  " + e.Favorites + L.T(" favori(s), ", " favorite(s), ", " favorito(s), ") +
               e.Categories + L.T(" catégorie(s)", " category(ies)", " categoría(s)");
    }

    void Restore()
    {
        int i = list.SelectedIndex;
        if (i < 0 || i >= entries.Count) return;
        if (!Dialogs.Ask(this, L.T("Remplacer tes réglages actuels par cette sauvegarde ?\n\n", "Replace your current settings with this backup?\n\n", "¿Sustituir tu configuración actual por esta copia?\n\n") + list.Items[i]))
            return;
        if (!SettingsBackup.Restore(entries[i]))
        {
            Dialogs.Warn(this, L.T("Cette sauvegarde n'a pas pu être restaurée.", "This backup could not be restored.", "No se pudo restaurar esta copia."));
            return;
        }
        DialogResult = DialogResult.OK;
        Close();
    }
}
