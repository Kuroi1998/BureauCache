// BureauCache — GlyphPicker, GlyphTileControl, FlatButton, InputBox, Card, BufferedPanel, SideNav
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
        if (i >= 0 && i < glyphs.Length) { Selected = glyphs[i]; Invalidate(); }
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
        Primary = primary;
        Font = Ui.Font("Segoe UI Semibold", 9.75f);
        Size = new Size(width > 0 ? width : TextRenderer.MeasureText(text, Font).Width + Ui.S(32), Ui.S(36));
    }

    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = Prep(e);
        RectangleF r = new RectangleF(0, 0, Width - 1, Height - 1);
        Color fill = Primary ? Theme.Accent : Danger ? Theme.Danger : Theme.Surface;
        Color bg = Primary || Danger ? (hover ? Theme.Mix(fill, Color.Black, 0.12f) : fill) : (hover ? Theme.Hover : Theme.Surface);
        if (!Enabled) bg = Theme.Surface;
        Ui.Fill(g, bg, r, Ui.S(8));
        TextRenderer.DrawText(g, Text, Font, ClientRectangle, !Enabled ? Theme.FgDim : Primary || Danger ? Color.White : Theme.Fg,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
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

    public void ApplyTheme()
    {
        Box.BackColor = Theme.Surface;
        Box.ForeColor = Theme.Fg;
        Invalidate();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        Ui.Fill(e.Graphics, Theme.Surface, new RectangleF(0, 0, Width - 1, Height - 1), Ui.S(8));
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
        g.Clear(Theme.Bg);
        Ui.Fill(g, Theme.Card, new RectangleF(0, 0, Width - 1, Height - 1), Ui.S(10));
        using (Pen p = new Pen(Theme.Line))
            foreach (int y in Separators) g.DrawLine(p, Ui.S(16), y, Width - Ui.S(16), y);
    }
}

class BufferedPanel : Panel
{
    public BufferedPanel() { SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); }
}

class SideNav : FlatControl
{
    readonly string[] names, glyphs;
    int sel, hover = -1;
    public event Action<int> SelectedChanged;
    readonly Font fName = Ui.Font("Segoe UI Semibold", 12.5f);
    readonly Font fVer = Ui.Font("Segoe UI", 8.75f);
    readonly Font fItem = Ui.Font("Segoe UI", 10.5f);

    public SideNav(string[] names, string[] glyphs) { this.names = names; this.glyphs = glyphs; }

    Rectangle ItemRect(int i) { return new Rectangle(Ui.S(12), Ui.S(92) + i * Ui.S(42), Width - Ui.S(24), Ui.S(38)); }
    public int Selected { get { return sel; } set { sel = value; Invalidate(); } }

    int HitTest(Point p)
    {
        for (int i = 0; i < names.Length; i++) if (ItemRect(i).Contains(p)) return i;
        return -1;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        int h = HitTest(e.Location);
        if (h != hover) { hover = h; Invalidate(); }
        Cursor = h >= 0 ? Cursors.Hand : Cursors.Default;
        base.OnMouseMove(e);
    }
    protected override void OnMouseLeave(EventArgs e) { hover = -1; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        int h = HitTest(e.Location);
        if (h >= 0 && h != sel) { sel = h; Invalidate(); if (SelectedChanged != null) SelectedChanged(h); }
        base.OnMouseDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = Prep(e);
        Ui.DrawLogo(g, new Rectangle(Ui.S(20), Ui.S(24), Ui.S(38), Ui.S(38)), Settings.Accent);
        TextRenderer.DrawText(g, "BureauCache", fName, new Point(Ui.S(68), Ui.S(22)), Theme.Fg, TextFormatFlags.NoPrefix);
        TextRenderer.DrawText(g, "Version " + Installer.Version, fVer, new Point(Ui.S(70), Ui.S(46)), Theme.FgDim, TextFormatFlags.NoPrefix);
        for (int i = 0; i < names.Length; i++)
        {
            Rectangle r = ItemRect(i);
            if (i == sel)
            {
                Ui.Fill(g, Theme.SelOverlay, r, Ui.S(8));
                Ui.Fill(g, Theme.Accent, new RectangleF(r.X, r.Y + Ui.S(10), Ui.S(3), r.Height - Ui.S(20)), Ui.S(2));
            }
            else if (i == hover) Ui.Fill(g, Theme.HoverOverlay, r, Ui.S(8));
            Ui.Glyph(g, glyphs[i], 11f, i == sel ? Theme.Accent : Theme.Fg, new Rectangle(r.X + Ui.S(8), r.Y, Ui.S(32), r.Height));
            TextRenderer.DrawText(g, names[i], fItem, new Rectangle(r.X + Ui.S(46), r.Y, r.Width - Ui.S(46), r.Height), Theme.Fg,
                TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
        using (Pen p = new Pen(Theme.Line)) g.DrawLine(p, Width - 1, 0, Width - 1, Height);
    }
}
