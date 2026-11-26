// BureauCache — RuleDialog
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

// =====================================================================
//  Créer ou modifier une règle de rangement (D-009)
// =====================================================================
// Nom, filtre (extensions ou mot du nom), âge minimal, destination parmi des dossiers connus et sous-dossier
// facultatif. Un aperçu indique combien de fichiers du bureau correspondent maintenant.
class RuleDialog : ThemedDialog
{
    readonly InputBox name, pattern, sub;
    readonly Segmented filter, days, target;
    readonly Label preview;
    readonly int[] dayChoices;
    public ArchiveRule Result;

    public static ArchiveRule Run(IWin32Window owner, ArchiveRule existing)
    {
        using (RuleDialog d = new RuleDialog(existing))
            return (owner != null ? d.ShowDialog(owner) : d.ShowDialog()) == DialogResult.OK ? d.Result : null;
    }

    RuleDialog(ArchiveRule r) : base(r == null ? L.T("Nouvelle règle", "New rule", "Nueva regla") : L.T("Modifier la règle", "Edit rule", "Editar regla"), Ui.S(560), Ui.S(470))
    {
        bool isNew = r == null;
        r = r ?? new ArchiveRule { Name = L.T("Captures d'écran", "Screenshots", "Capturas de pantalla"), Filter = RuleFilter.NameContains, Pattern = L.T("Capture", "Screenshot", "Captura"), Days = 7, Target = RuleTarget.Pictures, Sub = L.T("Captures", "Screenshots", "Capturas") };
        int pad = Ui.S(20), w = ClientSize.Width - 2 * pad, y = Ui.S(14);

        AddLabel(L.T("Nom", "Name", "Nombre"), pad, y);
        name = Input(r.Name, pad, y + Ui.S(22), w);
        y += Ui.S(66);

        AddLabel(L.T("Fichiers concernés", "Files affected", "Archivos afectados"), pad, y);
        filter = new Segmented(L.T("Extension", "Extension", "Extensión"), L.T("Le nom contient", "Name contains", "El nombre contiene")) { Location = new Point(pad, y + Ui.S(22)), BackColor = Theme.Bg };
        filter.Selected = (int)r.Filter;
        Controls.Add(filter);
        pattern = Input(r.Pattern, filter.Right + Ui.S(10), y + Ui.S(22), w - filter.Width - Ui.S(10));
        y += Ui.S(66);

        AddLabel(L.T("Inutilisés depuis", "Unused for", "Sin usar durante"), pad, y);
        // Un âge enregistré hors des choix (réglages modifiés à la main) devient un choix de plus : ouvrir l'éditeur
        // ne le change pas en silence (QA39-06)
        dayChoices = ArchiveRule.DayChoices.Contains(r.Days) || r.Days < 0 ? ArchiveRule.DayChoices
                   : ArchiveRule.DayChoices.Concat(new[] { r.Days }).OrderBy(d => d).ToArray();
        days = new Segmented(dayChoices.Select(d => d == 0 ? L.T("Dès l'arrivée", "On arrival", "Al llegar") : d + L.T(" j", " d", " d")).ToArray()) { Location = new Point(pad, y + Ui.S(22)), BackColor = Theme.Bg };
        days.Selected = Math.Max(0, Array.IndexOf(dayChoices, Settings.RoundUp(dayChoices, r.Days)));
        Controls.Add(days);
        y += Ui.S(66);

        AddLabel(L.T("Destination", "Destination", "Destino"), pad, y);
        target = new Segmented(Enum.GetValues(typeof(RuleTarget)).Cast<RuleTarget>().Select(t => ArchiveRules.TargetName(t)).ToArray()) { Location = new Point(pad, y + Ui.S(22)), BackColor = Theme.Bg };
        if (target.Width > w) target.Width = w;
        target.Selected = (int)r.Target;
        Controls.Add(target);
        y += Ui.S(66);

        AddLabel(L.T("Sous-dossier (facultatif)", "Subfolder (optional)", "Subcarpeta (opcional)"), pad, y);
        sub = Input(r.Sub, pad, y + Ui.S(22), w);
        y += Ui.S(66);

        preview = AddLabel("", pad, y);
        preview.MaximumSize = new Size(w, 0);
        foreach (Control c in new Control[] { filter, days, target }) ((Segmented)c).Changed += (s, e) => UpdatePreview();
        foreach (InputBox b in new[] { pattern, sub }) b.Box.TextChanged += (s, e) => UpdatePreview();
        UpdatePreview();

        AddButtons(Accept, isNew ? L.T("Créer", "Create", "Crear") : L.T("Enregistrer", "Save", "Guardar"));
        Shown += (s, e) => { name.Box.Focus(); name.Box.SelectAll(); };
    }

    InputBox Input(string text, int x, int y, int width)
    {
        InputBox b = new InputBox(width) { Location = new Point(x, y), BackColor = Theme.Bg };
        b.Box.Text = text;
        b.Box.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Accept(); } };
        Controls.Add(b);
        return b;
    }

    ArchiveRule Current()
    {
        return new ArchiveRule
        {
            Name = name.Box.Text.Trim(), Filter = (RuleFilter)filter.Selected, Pattern = pattern.Box.Text.Trim(),
            Days = dayChoices[days.Selected], Target = (RuleTarget)target.Selected, Sub = sub.Box.Text.Trim()
        };
    }

    // Combien de fichiers du bureau correspondent au filtre, et combien partiraient au prochain rangement
    void UpdatePreview()
    {
        ArchiveRule r = Current();
        string dest = r.Destination();
        int match = 0;
        try
        {
            foreach (string f in Directory.GetFiles(Paths.Desktop))
                if (r.Matches(f)) match++;
        }
        catch { }
        string where = ArchiveRules.TargetName(r.Target) + (r.Sub.Length > 0 ? "\\" + r.Sub : "");
        preview.Text = (match == 0 ? L.T("Aucun fichier du bureau ne correspond pour l'instant.", "No desktop file matches yet.", "Ningún archivo del escritorio coincide por ahora.")
                                   : match + L.T(" fichier(s) du bureau correspondent.", " desktop file(s) match.", " archivo(s) del escritorio coinciden."))
                       + L.T(" Destination : ", " Destination: ", " Destino: ") + where + (dest == null ? L.T(" (introuvable)", " (not found)", " (no encontrada)") : "") + ".";
    }

    void Accept()
    {
        ArchiveRule r = Current();
        string err = r.Validate() ?? (r.Destination() == null ? L.T("Ce dossier est introuvable sur ce PC.", "This folder can't be found on this PC.", "No se encuentra esta carpeta en este equipo.") : null);
        if (err != null) { preview.Text = "⚠ " + err; return; }
        Result = r;
        DialogResult = DialogResult.OK;
        Close();
    }
}
