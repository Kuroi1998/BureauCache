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

    public FlatButton(string text, bool primary, int width = 0)
    {
        Text = text;
        AccessibleRole = AccessibleRole.PushButton;
        Primary = primary;
        Font = Ui.Font("Segoe UI Semibold", 9.75f);
        Size = new Size(width > 0 ? width : TextRenderer.MeasureText(text, Font).Width + Ui.S(32), Ui.S(36));
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
        if (SettingsKeyboard && Enabled && (Primary || Danger)) text = SettingsPalette.OnFill(bg);
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
        Ui.Fill(g, contrast ? SystemColors.Window : Theme.Card, new RectangleF(0, 0, Width - 1, Height - 1), Ui.S(10));
        using (var outline = Ui.Round(new RectangleF(0, 0, Width - 1, Height - 1), Ui.S(10)))
        using (Pen stroke = new Pen(line)) g.DrawPath(stroke, outline);
        using (Pen p = new Pen(line))
            foreach (int y in Separators) g.DrawLine(p, Ui.S(16), y, Width - Ui.S(16), y);
    }
}

class BufferedPanel : Panel
{
    public BufferedPanel() { SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); }
}
