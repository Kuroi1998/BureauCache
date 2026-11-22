// BureauCache — PreviewForm
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;

// =====================================================================
//  Aperçu au survol
// =====================================================================
class PreviewForm : Form
{
    Bitmap img, placeholder;
    string title, line1, line2;
    int token;
    readonly Font fTitle = Ui.Font("Segoe UI Semibold", 10.5f);
    readonly Font fLine = Ui.Font("Segoe UI", 9f);

    public PreviewForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        AutoScaleMode = AutoScaleMode.None;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        Size = new Size(Ui.S(280), Ui.S(290));
    }

    protected override bool ShowWithoutActivation { get { return true; } }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= 0x80 | 0x08000000 | 0x20 | 0x8; // TOOLWINDOW | NOACTIVATE | TRANSPARENT | TOPMOST
            cp.ClassStyle |= 0x20000;
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Native.SetRoundCorners(Handle); }

    static string Describe(DesktopItem it, out string second)
    {
        second = null;
        try
        {
            if (it.Kind == ItemKind.App && it.Args != null) return L.T("Application du Microsoft Store", "Microsoft Store app");
            string p = it.Target;
            string ext = Path.GetExtension(p).ToLowerInvariant();
            if (Directory.Exists(p))
            {
                int n = 0;
                try { n = Directory.EnumerateFileSystemEntries(p).Take(1000).Count(); } catch { }
                second = L.T("Modifié le ", "Modified ") + Directory.GetLastWriteTime(p).ToString("g", L.Culture);
                return L.T("Dossier · ", "Folder · ") + (n >= 1000 ? "999+" : n.ToString()) + (n > 1 ? L.T(" éléments", " items") : L.T(" élément", " item"));
            }
            FileInfo fi = new FileInfo(p);
            second = L.T("Modifié le ", "Modified ") + fi.LastWriteTime.ToString("g", L.Culture);
            if (ext == ".lnk")
            {
                string t = ShellUtil.ResolveLnk(p);
                return string.IsNullOrEmpty(t) ? L.T("Raccourci", "Shortcut") : L.T("Raccourci vers ", "Shortcut to ") + t;
            }
            if (ext == ".url")
            {
                string u = ShellUtil.ReadUrl(p);
                return u ?? L.T("Lien", "Link");
            }
            return L.Size(fi.Length) + " · " + (ext.Length > 1 ? ext.Substring(1).ToUpperInvariant() : L.T("Fichier", "File"));
        }
        catch { return ""; }
    }

    public void ShowFor(DesktopItem it, Rectangle anchor, bool rightSide)
    {
        int my = ++token;
        title = it.Name;
        line1 = Describe(it, out line2);
        placeholder = it.Icon;
        if (img != null) { img.Dispose(); img = null; }
        Rectangle wa = Screen.FromRectangle(anchor).WorkingArea;
        int x = rightSide ? anchor.Left - Width - Ui.S(14) : anchor.Right + Ui.S(14);
        int y = Math.Max(wa.Top + Ui.S(8), Math.Min(wa.Bottom - Height - Ui.S(8), anchor.Top + anchor.Height / 2 - Height / 2));
        Location = new Point(x, y);
        if (!Visible) Show();
        Invalidate();
        if (!it.HasPath && !(it.Kind == ItemKind.App && it.Args != null)) return;
        string parsing = it.HasPath ? it.Target : it.Args;
        bool thumbs = it.HasPath;
        int px = Ui.S(256);
        Background.Run(() =>
        {
            Bitmap b = thumbs ? Icons.ShellImage(parsing, px, true) : null;
            if (b == null) b = Icons.ShellImage(parsing, px, false);
            try
            {
                BeginInvoke(new Action(() =>
                {
                    if (my == token && Visible) { img = b; Invalidate(); }
                    else if (b != null) b.Dispose();
                }));
            }
            catch { if (b != null) b.Dispose(); }
        }, true);
    }

    public void HidePreview()
    {
        token++;
        if (Visible) Hide();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(Theme.Card);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        int pad = Ui.S(12);
        Rectangle area = new Rectangle(pad, pad, Width - pad * 2, Ui.S(180));
        Ui.Fill(g, Theme.Surface, area, Ui.S(8));
        Bitmap b = img ?? placeholder;
        if (b != null)
        {
            float maxW = area.Width - Ui.S(8), maxH = area.Height - Ui.S(8);
            float sc = Math.Min(maxW / b.Width, maxH / b.Height);
            if (img == null) sc = Math.Min(sc, Ui.S(64) / (float)b.Width);
            int w = (int)(b.Width * sc), h = (int)(b.Height * sc);
            g.DrawImage(b, new Rectangle(area.X + (area.Width - w) / 2, area.Y + (area.Height - h) / 2, w, h));
        }
        int y = area.Bottom + Ui.S(10);
        TextRenderer.DrawText(g, title, fTitle, new Rectangle(pad, y, Width - pad * 2, Ui.S(22)), Theme.Fg,
            TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        y += Ui.S(24);
        if (!string.IsNullOrEmpty(line1))
            TextRenderer.DrawText(g, line1, fLine, new Rectangle(pad, y, Width - pad * 2, Ui.S(18)), Theme.FgDim,
                TextFormatFlags.PathEllipsis | TextFormatFlags.NoPrefix);
        y += Ui.S(20);
        if (!string.IsNullOrEmpty(line2))
            TextRenderer.DrawText(g, line2, fLine, new Rectangle(pad, y, Width - pad * 2, Ui.S(18)), Theme.FgDim,
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        using (Pen p = new Pen(Theme.Line)) g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
    }
}
