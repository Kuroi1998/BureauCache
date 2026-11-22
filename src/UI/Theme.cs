// BureauCache — Theme, Ui, MenuColors, ThemedRenderer, Menus
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

// =====================================================================
//  Thème et dessin
// =====================================================================
static class Theme
{
    static Color D(int r, int g, int b, int lr, int lg, int lb)
    { return Settings.Dark ? Color.FromArgb(r, g, b) : Color.FromArgb(lr, lg, lb); }
    public static Color Bg { get { return D(24, 24, 28, 243, 243, 247); } }
    public static Color Side { get { return D(31, 31, 37, 233, 233, 239); } }
    public static Color Card { get { return D(36, 36, 42, 255, 255, 255); } }
    public static Color Surface { get { return D(46, 46, 54, 232, 232, 238); } }
    public static Color Hover { get { return D(54, 54, 63, 222, 222, 230); } }
    public static Color Line { get { return D(50, 50, 58, 228, 228, 235); } }
    public static Color Track { get { return D(72, 72, 84, 200, 200, 210); } }
    public static Color Fg { get { return D(236, 236, 241, 24, 24, 28); } }
    public static Color FgDim { get { return D(150, 150, 162, 105, 105, 115); } }
    public static Color Danger { get { return Color.FromArgb(239, 68, 68); } }
    public static Color Accent { get { return Settings.Accent; } }
    public static Color HoverOverlay { get { return Settings.Dark ? Color.FromArgb(26, 255, 255, 255) : Color.FromArgb(16, 0, 0, 0); } }
    public static Color SelOverlay { get { return Settings.Dark ? Color.FromArgb(44, 255, 255, 255) : Color.FromArgb(28, 0, 0, 0); } }

    public static Color Mix(Color a, Color b, float t)
    {
        return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
    }
}

static class Ui
{
    public static float Scale = 1f;
    public static int S(int v) { return (int)Math.Round(v * Scale); }
    public const string GlyphFont = "Segoe MDL2 Assets";

    public static GraphicsPath Round(RectangleF r, float rad)
    {
        GraphicsPath p = new GraphicsPath();
        float d = Math.Min(rad * 2, Math.Min(r.Width, r.Height));
        if (d <= 0) { p.AddRectangle(r); return p; }
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public static void Fill(Graphics g, Color c, RectangleF r, float rad)
    {
        SmoothingMode old = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (GraphicsPath p = Round(r, rad))
        using (SolidBrush b = new SolidBrush(c)) g.FillPath(b, p);
        g.SmoothingMode = old;
    }

    public static void DrawLogo(Graphics g, Rectangle r, Color accent)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Fill(g, accent, r, r.Width * 0.24f);
        float u = r.Width / 32f;
        for (int i = 0; i < 3; i++)
        {
            float y = r.Y + (8 + i * 6.5f) * u;
            Fill(g, Color.White, new RectangleF(r.X + 7 * u, y, 4.5f * u, 4.5f * u), 1.2f * u);
            Fill(g, Color.FromArgb(210, 255, 255, 255), new RectangleF(r.X + 14 * u, y + 1.2f * u, 11 * u, 2.2f * u), 1.1f * u);
        }
    }

    public static Icon MakeIcon(Color accent)
    {
        using (Bitmap b = new Bitmap(32, 32))
        {
            using (Graphics g = Graphics.FromImage(b)) DrawLogo(g, new Rectangle(1, 1, 30, 30), accent);
            IntPtr h = b.GetHicon();
            Icon tmp = Icon.FromHandle(h);
            Icon copy = (Icon)tmp.Clone();
            tmp.Dispose();
            Native.DestroyIcon(h);
            return copy;
        }
    }

    // Polices partagées : une seule instance par nom/taille/style au lieu d'en recréer à chaque dessin
    static readonly Dictionary<string, Font> fonts = new Dictionary<string, Font>();

    public static Font Font(string name, float pt, FontStyle style = FontStyle.Regular)
    {
        string key = name + "|" + pt.ToString(CultureInfo.InvariantCulture) + "|" + (int)style;
        lock (fonts)
        {
            Font f;
            if (!fonts.TryGetValue(key, out f)) fonts[key] = f = new Font(name, pt, style);
            return f;
        }
    }

    public static void Glyph(Graphics g, string glyph, float pt, Color c, Rectangle r)
    {
        TextRenderer.DrawText(g, glyph, Font(GlyphFont, pt), r, c, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    public static Bitmap GlyphImage(string glyph, Color c)
    {
        int s = S(16);
        Bitmap b = new Bitmap(s, s);
        using (Graphics g = Graphics.FromImage(b))
        using (SolidBrush br = new SolidBrush(c))
        using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
        {
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            g.DrawString(glyph, Font(GlyphFont, 10f), br, new RectangleF(0, 0, s, s), sf);
        }
        return b;
    }

    public static void WriteIco(string path)
    {
        int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 128, 256 };
        List<byte[]> pngs = new List<byte[]>();
        foreach (int s in sizes)
            using (Bitmap b = new Bitmap(s, s))
            {
                using (Graphics g = Graphics.FromImage(b))
                {
                    int pad = s >= 32 ? Math.Max(1, s / 32) : 0;
                    DrawLogo(g, new Rectangle(pad, pad, s - pad * 2, s - pad * 2), Color.FromArgb(59, 130, 246));
                }
                using (MemoryStream ms = new MemoryStream()) { b.Save(ms, ImageFormat.Png); pngs.Add(ms.ToArray()); }
            }
        using (BinaryWriter w = new BinaryWriter(File.Create(path)))
        {
            w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                w.Write((byte)0); w.Write((byte)0);
                w.Write((short)1); w.Write((short)32);
                w.Write(pngs[i].Length); w.Write(offset);
                offset += pngs[i].Length;
            }
            foreach (byte[] p in pngs) w.Write(p);
        }
    }
}

class MenuColors : ProfessionalColorTable
{
    public override Color MenuItemSelected { get { return Theme.Hover; } }
    public override Color MenuItemBorder { get { return Theme.Hover; } }
    public override Color MenuBorder { get { return Theme.Line; } }
    public override Color ToolStripDropDownBackground { get { return Theme.Card; } }
    public override Color ImageMarginGradientBegin { get { return Theme.Card; } }
    public override Color ImageMarginGradientMiddle { get { return Theme.Card; } }
    public override Color ImageMarginGradientEnd { get { return Theme.Card; } }
    public override Color SeparatorDark { get { return Theme.Line; } }
    public override Color SeparatorLight { get { return Theme.Card; } }
    public override Color MenuItemSelectedGradientBegin { get { return Theme.Hover; } }
    public override Color MenuItemSelectedGradientEnd { get { return Theme.Hover; } }
    public override Color MenuItemPressedGradientBegin { get { return Theme.Hover; } }
    public override Color MenuItemPressedGradientEnd { get { return Theme.Hover; } }
    public override Color CheckBackground { get { return Theme.Hover; } }
    public override Color CheckSelectedBackground { get { return Theme.Hover; } }
    public override Color CheckPressedBackground { get { return Theme.Hover; } }
}

class ThemedRenderer : ToolStripProfessionalRenderer
{
    public ThemedRenderer() : base(new MenuColors()) { RoundedEdges = false; }
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? Theme.Fg : Theme.FgDim;
        base.OnRenderItemText(e);
    }
    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = Theme.Fg;
        base.OnRenderArrow(e);
    }
}

static class Menus
{
    public static ContextMenuStrip Create()
    {
        ContextMenuStrip m = new ContextMenuStrip();
        m.Renderer = new ThemedRenderer();
        m.Font = Ui.Font("Segoe UI", 9.75f);
        m.ShowImageMargin = true;
        return m;
    }

    public static ToolStripMenuItem Item(string text, string glyph, Action click)
    {
        ToolStripMenuItem it = new ToolStripMenuItem(text);
        if (glyph != null) it.Image = Ui.GlyphImage(glyph, Theme.Fg);
        if (click != null) it.Click += (s, e) => click();
        it.Padding = new Padding(0, Ui.S(2), 0, Ui.S(2));
        return it;
    }

    public static ToolStripMenuItem Sub(ContextMenuStrip owner, string text, string glyph)
    {
        ToolStripMenuItem it = Item(text, glyph, null);
        ((ToolStripDropDownMenu)it.DropDown).Renderer = owner.Renderer;
        return it;
    }
}
