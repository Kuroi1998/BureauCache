// BureauCache — ThemedDialog, RenameDialog, CategoryDialog
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

// Base des petites boîtes de dialogue
class ThemedDialog : Form
{
    public ThemedDialog(string title, int w, int h)
    {
        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.None;
        ClientSize = new Size(w, h);
        BackColor = Theme.Bg;
        Icon = Ui.MakeIcon(Settings.Accent);
    }

    protected Label AddLabel(string text, int x, int y)
    {
        Label l = new Label { Text = text, AutoSize = true, Font = Ui.Font("Segoe UI", 9.75f), ForeColor = Theme.FgDim, BackColor = Theme.Bg, Location = new Point(x, y) };
        Controls.Add(l);
        return l;
    }

    protected void AddButtons(Action ok, string okText)
    {
        FlatButton bOk = new FlatButton(okText, true, Ui.S(120));
        FlatButton bCancel = new FlatButton(L.T("Annuler", "Cancel", "Cancelar"), false, Ui.S(100));
        bOk.BackColor = bCancel.BackColor = Theme.Bg;
        bCancel.Location = new Point(ClientSize.Width - Ui.S(20) - bCancel.Width, ClientSize.Height - Ui.S(18) - bCancel.Height);
        bOk.Location = new Point(bCancel.Left - Ui.S(8) - bOk.Width, bCancel.Top);
        bOk.Click += (s, e) => ok();
        bCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
        Controls.Add(bOk);
        Controls.Add(bCancel);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Native.SetDarkTitleBar(Handle, Settings.Dark);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }
}

class RenameDialog : ThemedDialog
{
    readonly InputBox input;
    public string Value;

    public RenameDialog(string current, bool selectWithoutExt) : base(L.T("Renommer", "Rename", "Cambiar nombre"), Ui.S(420), Ui.S(156))
    {
        AddLabel(L.T("Nouveau nom", "New name", "Nuevo nombre"), Ui.S(20), Ui.S(16));
        input = new InputBox(Ui.S(380)) { Location = new Point(Ui.S(20), Ui.S(42)), BackColor = Theme.Bg };
        input.Box.Text = current;
        input.Box.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Accept(); } };
        Controls.Add(input);
        AddButtons(Accept, L.T("Renommer", "Rename", "Cambiar nombre"));
        Shown += (s, e) =>
        {
            input.Box.Focus();
            int dot = selectWithoutExt ? current.LastIndexOf('.') : -1;
            if (dot > 0) input.Box.Select(0, dot); else input.Box.SelectAll();
        };
    }

    void Accept()
    {
        string v = input.Box.Text.Trim();
        if (!FileOps.IsValidName(v))
        {
            Dialogs.Warn(this, L.T("Ce nom n'est pas valide.", "This name is not valid.", "Este nombre no es válido."));
            return;
        }
        Value = v;
        DialogResult = DialogResult.OK;
        Close();
    }
}

class CategoryDialog : ThemedDialog
{
    readonly InputBox input;
    readonly GlyphPicker picker;
    readonly Swatches colors;
    readonly CustomCat editing;
    public string CatName, Glyph;
    public Color CatColor;

    public CategoryDialog(CustomCat existing)
        : base(existing == null ? L.T("Nouvelle catégorie", "New category", "Nueva categoría") : L.T("Modifier la catégorie", "Edit category", "Editar categoría"), Ui.S(420), Ui.S(330))
    {
        editing = existing;
        AddLabel(L.T("Nom", "Name", "Nombre"), Ui.S(20), Ui.S(14));
        input = new InputBox(Ui.S(380)) { Location = new Point(Ui.S(20), Ui.S(38)), BackColor = Theme.Bg };
        input.Box.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Accept(); } };
        Controls.Add(input);
        AddLabel(L.T("Icône", "Icon", "Icono"), Ui.S(20), Ui.S(84));
        picker = new GlyphPicker(Catalog.PickerGlyphs) { Location = new Point(Ui.S(20), Ui.S(108)), BackColor = Theme.Bg };
        Controls.Add(picker);
        AddLabel(L.T("Couleur", "Color", "Color"), Ui.S(20), Ui.S(186));
        colors = new Swatches { Location = new Point(Ui.S(17), Ui.S(208)), BackColor = Theme.Bg };
        colors.Selected = Swatches.Presets[CustomCountColor()];
        Controls.Add(colors);
        if (existing != null)
        {
            input.Box.Text = existing.Name;
            picker.Selected = existing.Glyph;
            colors.Selected = existing.Color;
        }
        AddButtons(Accept, existing == null ? L.T("Créer", "Create", "Crear") : L.T("Enregistrer", "Save", "Guardar"));
        Shown += (s, e) => { input.Box.Focus(); input.Box.SelectAll(); };
    }

    static int CustomCountColor() { return Settings.CustomCats.Count % Swatches.Presets.Length; }

    void Accept()
    {
        string v = input.Box.Text.Trim();
        string err = null;
        if (v.Length == 0) err = L.T("Donne un nom à la catégorie.", "Give the category a name.", "Dale un nombre a la categoría.");
        else if (v.IndexOfAny(new[] { '|', '>', '=' }) >= 0) err = L.T("Le nom ne peut pas contenir | > ou =.", "The name can't contain | > or =.", "El nombre no puede contener | > ni =.");
        else if (Catalog.BuiltIn.Any(b => string.Equals(b, v, StringComparison.OrdinalIgnoreCase) || string.Equals(L.Cat(b), v, StringComparison.OrdinalIgnoreCase)))
            err = L.T("Ce nom est déjà utilisé par une catégorie automatique.", "This name is used by a built-in category.", "Este nombre pertenece a una categoría integrada.");
        else
        {
            CustomCat other = Settings.FindCat(v);
            if (other != null && other != editing) err = L.T("Une catégorie porte déjà ce nom.", "A category already has this name.", "Ya existe una categoría con ese nombre.");
        }
        if (err != null) { Dialogs.Warn(this, err); return; }
        CatName = v;
        Glyph = picker.Selected;
        CatColor = colors.Selected;
        DialogResult = DialogResult.OK;
        Close();
    }

    // Crée ou modifie une catégorie ; renvoie son nom ou null
    public static string Run(CustomCat existing)
    {
        using (CategoryDialog d = new CategoryDialog(existing))
        {
            if (d.ShowDialog() != DialogResult.OK) return null;
            if (existing == null)
                Settings.CustomCats.Add(new CustomCat { Name = d.CatName, Glyph = d.Glyph, Color = d.CatColor });
            else
            {
                if (existing.Name != d.CatName) Settings.RenameCategory(existing, d.CatName);
                existing.Glyph = d.Glyph;
                existing.Color = d.CatColor;
            }
            return d.CatName;
        }
    }
}
