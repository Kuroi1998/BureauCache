// BureauCache — ajout d'un site aux raccourcis du menu Démarrer (T-044)
using System;
using System.Drawing;
using System.Windows.Forms;

class SiteShortcutDialog : ThemedDialog
{
    readonly InputBox name, address;
    public string SiteName, Address;

    public SiteShortcutDialog() : base(L.T("Ajouter un site", "Add a site", "Añadir un sitio"), Ui.S(440), Ui.S(236))
    {
        int pad = Ui.S(20), w = ClientSize.Width - 2 * pad;
        AddLabel(L.T("Adresse", "Address", "Dirección"), pad, Ui.S(16));
        address = new InputBox(w) { Location = new Point(pad, Ui.S(40)), BackColor = Theme.Bg };
        address.Box.AccessibleName = L.T("Adresse", "Address", "Dirección");
        Controls.Add(address);
        AddLabel(L.T("Nom affiché (facultatif)", "Display name (optional)", "Nombre visible (opcional)"), pad, Ui.S(84));
        name = new InputBox(w) { Location = new Point(pad, Ui.S(108)), BackColor = Theme.Bg };
        name.Box.AccessibleName = L.T("Nom affiché", "Display name", "Nombre visible");
        name.Box.MaxLength = 40;
        Controls.Add(name);
        foreach (InputBox box in new[] { address, name })
            box.Box.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Accept(); } };
        AddButtons(Accept, L.T("Ajouter", "Add", "Añadir"));
        Shown += (s, e) => address.Box.Focus();
    }

    // « exemple.fr » devient « https://exemple.fr » ; seuls http et https sont acceptés
    internal static string Normalize(string text)
    {
        string t = (text ?? string.Empty).Trim();
        if (t.Length == 0) return null;
        if (t.IndexOf("://", StringComparison.Ordinal) < 0) t = "https://" + t;
        return StartShortcuts.Valid(StartShortcuts.Site, t) ? t : null;
    }

    void Accept()
    {
        string url = Normalize(address.Box.Text);
        if (url == null)
        {
            Dialogs.Warn(this, L.T("Cette adresse n'est pas valide. Exemple : https://www.exemple.fr", "This address isn't valid. Example: https://www.example.com",
                "Esta dirección no es válida. Ejemplo: https://www.ejemplo.es"));
            return;
        }
        Address = url;
        SiteName = name.Box.Text.Trim();
        DialogResult = DialogResult.OK;
        Close();
    }
}
