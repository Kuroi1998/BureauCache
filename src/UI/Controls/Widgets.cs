// BureauCache — GlyphPicker, GlyphTileControl, FlatButton, InputBox, Card, BufferedPanel
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

class GlyphPicker : FlatControl
{
    readonly string[] glyphs;
    public string Selected;
    const int Cols = 10;
    int Cell { get { return Ui.S(34); } }

    public GlyphPicker(string[] glyphs)
    {
        this.glyphs = glyphs;
        Selected = glyphs[0];
        int rows = (glyphs.Length + Cols - 1) / Cols;
        Size = new Size(Cols * Cell, rows * Cell);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        int i = (e.Y / Cell) * Cols + e.X / Cell;
        if (e.Button == MouseButtons.Left && i >= 0 && i < glyphs.Length) { Selected = glyphs[i]; Invalidate(); }
        base.OnMouseDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = Prep(e);
        for (int i = 0; i < glyphs.Length; i++)
        {
            Rectangle r = new Rectangle((i % Cols) * Cell + Ui.S(2), (i / Cols) * Cell + Ui.S(2), Cell - Ui.S(4), Cell - Ui.S(4));
            bool sel = glyphs[i] == Selected;
            Ui.Fill(g, sel ? Theme.Accent : Theme.Surface, r, Ui.S(6));
            Ui.Glyph(g, glyphs[i], 11f, sel ? Color.White : Theme.Fg, r);
        }
        DrawSettingsFocus(g);
    }

    // Flèches pour se déplacer dans la grille, Début et Fin pour la première et la dernière icône
    protected override bool IsInputKey(Keys keyData)
    {
        return SettingsKeyboard && (keyData == Keys.Up || keyData == Keys.Down) || base.IsInputKey(keyData);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        int i = Array.IndexOf(glyphs, Selected), next;
        if (e.KeyCode == Keys.Left) next = i - 1;
        else if (e.KeyCode == Keys.Right) next = i + 1;
        else if (e.KeyCode == Keys.Up) next = i - Cols;
        else if (e.KeyCode == Keys.Down) next = i + Cols;
        else if (e.KeyCode == Keys.Home) next = 0;
        else if (e.KeyCode == Keys.End) next = glyphs.Length - 1;
        else { base.OnKeyDown(e); return; }
        e.SuppressKeyPress = true;
        if (next >= 0 && next < glyphs.Length) { Selected = glyphs[next]; Invalidate(); }
    }
}

class GlyphTileControl : FlatControl
{
    public string Glyph;
    public Color TileColor;
    public GlyphTileControl(string glyph, Color c) { Glyph = glyph; TileColor = c; Size = new Size(Ui.S(30), Ui.S(30)); Cursor = Cursors.Default; }
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = Prep(e);
        Ui.Fill(g, TileColor, new RectangleF(0, 0, Width - 1, Height - 1), Ui.S(7));
        Ui.Glyph(g, Glyph, 10f, Color.White, ClientRectangle);
    }
}

class FlatButton : FlatControl
{
    public bool Primary, Danger;
    bool hover;

    protected override AccessibleRole FlatRole { get { return AccessibleRole.PushButton; } }
    protected override string FlatDefaultAction { get { return L.T("Appuyer", "Press", "Pulsar"); } }

    public FlatButton(string text, bool primary, int width = 0)
    {
        Text = text;
        AccessibleRole = AccessibleRole.PushButton;
        Primary = primary;
        Font = Ui.Font("Segoe UI Semibold", 9.75f);
        // Largeur demandée, mais jamais moins que le texte traduit : l'espagnol est 30 à 45 % plus long (Gemini B10)
        Size = new Size(Math.Max(width, TextRenderer.MeasureText(text, Font).Width + Ui.S(32)), Ui.S(36));
    }

    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }

    protected override bool IsInputKey(Keys keyData)
    {
        return SettingsKeyboard && keyData == Keys.Enter || base.IsInputKey(keyData);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (SettingsKeyboard && (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter))
        {
            e.SuppressKeyPress = true;
            OnClick(EventArgs.Empty);
        }
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = Prep(e);
        RectangleF r = new RectangleF(0, 0, Width - 1, Height - 1);
        Color fill = Primary ? Theme.Accent : Danger ? Theme.Danger : Theme.Surface;
        Color bg = Primary || Danger ? (hover ? Theme.Mix(fill, Color.Black, 0.12f) : fill) : (hover ? Theme.Hover : Theme.Surface);
        if (!Enabled) bg = Theme.Surface;
        Color text = !Enabled ? Theme.FgDim : Primary || Danger ? Color.White : Theme.Fg;
        if (Enabled && (Primary || Danger)) text = SettingsPalette.OnFill(bg);   // accent clair : texte sombre (GEM-10)
        if (SettingsHighContrast)
        {
            bg = Primary && Enabled ? SystemColors.Highlight : SystemColors.Control;
            text = !Enabled ? SystemColors.GrayText : Primary ? SystemColors.HighlightText : SystemColors.ControlText;
        }
        Ui.Fill(g, bg, r, Ui.S(8));
        if (SettingsHighContrast)
            using (var path = Ui.Round(r, Ui.S(8)))
            using (Pen pen = new Pen(text)) g.DrawPath(pen, path);
        TextRenderer.DrawText(g, Text, Font, ClientRectangle, text,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        DrawSettingsFocus(g);
    }
}

class InputBox : Panel
{
    public readonly TextBox Box;

    public InputBox(int width)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Size = new Size(width, Ui.S(34));
        Padding = new Padding(Ui.S(10), Ui.S(8), Ui.S(10), 0);
        Box = new TextBox { BorderStyle = BorderStyle.None, Dock = DockStyle.Fill, Font = Ui.Font("Segoe UI", 10f) };
        Controls.Add(Box);
        Cursor = Cursors.IBeam;
        Click += (s, e) => Box.Focus();
        ApplyTheme();
    }

    public void ApplyTheme(bool highContrast = false)
    {
        Box.BackColor = highContrast ? SystemColors.Control : Theme.Surface;
        Box.ForeColor = highContrast ? SystemColors.ControlText : Theme.Fg;
        Invalidate();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        var bounds = new RectangleF(0, 0, Width - 1, Height - 1);
        Ui.Fill(e.Graphics, Box.BackColor, bounds, Ui.S(8));
        if (SystemInformation.HighContrast)
            using (var path = Ui.Round(bounds, Ui.S(8)))
            using (Pen pen = new Pen(SystemColors.WindowText)) e.Graphics.DrawPath(pen, path);
    }
}

class Card : Panel
{
    public readonly List<int> Separators = new List<int>();

    public Card()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        if (Width < 2 || Height < 2) return;
        bool contrast = SystemInformation.HighContrast;
        Color line = contrast ? SystemColors.WindowText : Theme.Line;
        g.Clear(contrast ? SystemColors.Window : Theme.Bg);
        // Carte des Paramètres : même rayon et même bordure fine que les cartes du panneau (D-012)
        RectangleF box = new RectangleF(0, 0, Width, Height);
        Ui.Fill(g, contrast ? SystemColors.Window : Theme.Card, box, Ui.S(Ui.RadiusCard));
        Ui.Edge(g, contrast ? SystemColors.WindowText : Theme.Edge, box, Ui.S(Ui.RadiusCard));
        using (Pen p = new Pen(line))
            foreach (int y in Separators) g.DrawLine(p, Ui.S(16), y, Width - Ui.S(16), y);
    }
}

class BufferedPanel : Panel
{
    public BufferedPanel() { SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); }
}
