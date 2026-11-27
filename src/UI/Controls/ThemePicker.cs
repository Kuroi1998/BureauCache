// BureauCache — choix d'un thème prêt dans les Paramètres : six cartes avec un aperçu de leurs couleurs (T-045)
using System;
using System.Drawing;
using System.Windows.Forms;

class ThemePicker : FlatControl
{
    public int Selected = -1;   // thème prêt correspondant aux réglages, -1 si l'utilisateur a composé le sien
    public event EventHandler Changed;
    int hover = -1;
    const int Columns = 3, Rows = 2;

    protected override AccessibleRole FlatRole { get { return AccessibleRole.List; } }
    protected override string FlatValue { get { return Selected >= 0 ? Palettes.Presets[Selected].Name : L.T("Personnalisé", "Custom", "Personalizado"); } }

    public ThemePicker()
    {
        Size = new Size(Ui.S(372), 2 * CardHeight + Ui.S(8));
    }

    static int CardHeight { get { return Math.Max(Ui.S(74), Ui.Font("Segoe UI", 9f).Height + Ui.S(52)); } }

    internal Rectangle CardBounds(int index)
    {
        int gap = Ui.S(8), w = (Width - (Columns - 1) * gap) / Columns;
        int col = index % Columns, row = index / Columns;
        return new Rectangle(col * (w + gap), row * (CardHeight + gap), w, CardHeight);
    }

    int CardAt(Point p)
    {
        for (int i = 0; i < Palettes.Presets.Length; i++) if (CardBounds(i).Contains(p)) return i;
        return -1;
    }

    protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); int h = CardAt(e.Location); if (h != hover) { hover = h; Invalidate(); } }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); if (hover != -1) { hover = -1; Invalidate(); } }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left) { int i = CardAt(e.Location); if (i >= 0) Choose(i); }
        base.OnMouseDown(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        int current = Math.Max(0, Selected), next;
        if (e.KeyCode == Keys.Left) next = Math.Max(0, current - 1);
        else if (e.KeyCode == Keys.Right) next = Math.Min(Palettes.Presets.Length - 1, current + 1);
        else if (e.KeyCode == Keys.Up) next = Math.Max(0, current - Columns);
        else if (e.KeyCode == Keys.Down) next = Math.Min(Palettes.Presets.Length - 1, current + Columns);
        else { base.OnKeyDown(e); return; }
        e.SuppressKeyPress = true;
        Choose(next);
    }

    void Choose(int index)
    {
        if (index == Selected) return;
        Selected = index;
        Invalidate();
        AnnounceAccessibleChange();
        if (Changed != null) Changed(this, EventArgs.Empty);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = Prep(e);
        Font font = Ui.Font("Segoe UI", 9f);
        for (int i = 0; i < Palettes.Presets.Length; i++)
        {
            Palettes.Preset p = Palettes.Presets[i];
            Rectangle r = CardBounds(i);
            bool dark = p.Mode == 1 || (p.Mode == 0 && Settings.SystemDark);
            Palettes.Palette palette = Palettes.All[p.Palette];
            Color bg = Palettes.Apply(dark ? Color.FromArgb(24, 24, 28) : Color.FromArgb(243, 243, 247), palette, dark);
            Color card = Palettes.Apply(dark ? Color.FromArgb(46, 46, 54) : Color.FromArgb(255, 255, 255), palette, dark);
            Color fg = dark ? Color.FromArgb(236, 236, 241) : Color.FromArgb(24, 24, 28);
            Ui.Fill(g, bg, r, Ui.S(Ui.RadiusControl));
            // Aperçu : une carte, un trait de texte et la pastille d'accent
            Rectangle mini = new Rectangle(r.Left + Ui.S(8), r.Top + Ui.S(8), r.Width - Ui.S(16), Ui.S(26));
            Ui.Fill(g, card, mini, Ui.S(4));
            using (SolidBrush a = new SolidBrush(p.Accent)) g.FillEllipse(a, mini.Left + Ui.S(6), mini.Top + Ui.S(6), Ui.S(14), Ui.S(14));
            Ui.Fill(g, Color.FromArgb(90, fg), new Rectangle(mini.Left + Ui.S(26), mini.Top + Ui.S(10), mini.Width / 2, Ui.S(6)), Ui.S(3));
            TextRenderer.DrawText(g, p.Name, font, new Rectangle(r.Left + Ui.S(6), mini.Bottom + Ui.S(2), r.Width - Ui.S(12), r.Bottom - mini.Bottom - Ui.S(4)), fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            Color edge = i == Selected ? Theme.Accent : i == hover ? Theme.EdgeHot : Theme.Edge;
            Ui.Edge(g, edge, new RectangleF(r.X, r.Y, r.Width - 1, r.Height - 1), Ui.S(Ui.RadiusControl));
            if (i == Selected) Ui.Edge(g, edge, new RectangleF(r.X + 1, r.Y + 1, r.Width - 3, r.Height - 3), Ui.S(Ui.RadiusControl));
        }
        DrawSettingsFocus(g);
    }
}
