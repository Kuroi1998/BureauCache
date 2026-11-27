// BureauCache — BarForm : fond de la barre, uni, en verre ou teinté (T-043, D-019)
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

// Verre : la partie du fond d'écran cachée par la barre, floutée puis voilée de la couleur du thème. La zone réservée à
// la barre ne montre jamais de fenêtre agrandie : c'est toujours le fond d'écran (icônes du bureau cachées par
// BureauCache), qu'on lit dans son fichier plutôt que de capturer l'écran. Image refaite seulement quand le fond
// d'écran, la taille de la barre ou le thème changent, hors du fil d'interface.
partial class BarForm
{
    Bitmap glass;
    string glassKey;
    int glassWork;

    internal static Color TintedBackground { get { return Theme.Mix(Theme.Bg, Theme.Accent, Settings.Dark ? 0.22f : 0.16f); } }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        if (Settings.TaskbarStyle == 1 && glass != null && glass.Width == ClientSize.Width)
        {
            e.Graphics.DrawImageUnscaled(glass, 0, ClientSize.Height - glass.Height);
            return;
        }
        using (SolidBrush b = new SolidBrush(Settings.TaskbarStyle == 2 ? TintedBackground : Theme.Bg))
            e.Graphics.FillRectangle(b, ClientRectangle);
    }

    // Relance la fabrication du verre si quelque chose a changé ; l'image arrive plus tard et la barre se redessine
    void RefreshGlass()
    {
        if (Settings.TaskbarStyle != 1 || Sandbox.Active) { SetBarGlass(null, null); return; }
        if (!IsHandleCreated) return;   // refait à la création de la fenêtre
        Rectangle screen = Screen.PrimaryScreen.Bounds;
        Rectangle area = new Rectangle(Left, screen.Bottom - BarHeight, Width, BarHeight);
        string path = Native.WallpaperPath();
        if (path == null) { SetBarGlass(null, null); return; }
        int style = Native.WallpaperStyle();
        Color tint = Color.FromArgb(Settings.Dark ? 178 : 190, Theme.Bg);
        long stamp = 0;
        try { stamp = File.GetLastWriteTimeUtc(path).Ticks; } catch (Exception) { }
        string key = path + "|" + stamp + "|" + style + "|" + area + "|" + screen + "|" + tint.ToArgb();
        if (key == glassKey) return;
        glassKey = key;
        int work = ++glassWork;
        Task.Factory.StartNew(() => MakeGlass(path, style, screen, area, tint)).ContinueWith(t =>
        {
            Bitmap made = t.Result;
            try
            {
                if (IsDisposed || !IsHandleCreated) { if (made != null) made.Dispose(); return; }
                BeginInvoke((Action)delegate
                {
                    if (work != glassWork || IsDisposed) { if (made != null) made.Dispose(); return; }
                    SetBarGlass(made, key);
                });
            }
            catch (InvalidOperationException) { if (made != null) made.Dispose(); }
        }, TaskScheduler.Default);
    }

    void SetBarGlass(Bitmap bitmap, string key)
    {
        Bitmap old = glass;
        glass = bitmap;
        if (bitmap == null) glassKey = key;
        if (old != null && old != bitmap) old.Dispose();
        Invalidate();
    }

    // Image du fond d'écran sous la zone de la barre (coordonnées de l'écran), floutée et voilée ; null si illisible
    internal static Bitmap MakeGlass(string path, int style, Rectangle screen, Rectangle area, Color tint)
    {
        try
        {
            FileInfo info = new FileInfo(path);
            if (!info.Exists || info.Length > 64L * 1024 * 1024 || area.Width <= 0 || area.Height <= 0) return null;
            using (MemoryStream data = new MemoryStream(File.ReadAllBytes(path)))
            using (Image wallpaper = Image.FromStream(data))
            {
                if ((long)wallpaper.Width * wallpaper.Height > 100000000L) return null;
                RectangleF dest = WallpaperBounds(wallpaper.Size, screen, style);
                int sw = Math.Max(2, area.Width / 24), sh = Math.Max(2, area.Height / 8);
                using (Bitmap small = new Bitmap(sw, sh, PixelFormat.Format32bppRgb))
                {
                    using (Graphics g = Graphics.FromImage(small))
                    {
                        g.Clear(Color.Black);
                        g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                        // Échelle zone de la barre -> petite image ; le fond d'écran suit le même déplacement
                        float kx = (float)sw / area.Width, ky = (float)sh / area.Height;
                        g.DrawImage(wallpaper, new RectangleF((dest.X - area.X) * kx, (dest.Y - area.Y) * ky, dest.Width * kx, dest.Height * ky));
                    }
                    Bitmap result = new Bitmap(area.Width, area.Height, PixelFormat.Format32bppRgb);
                    using (Graphics g = Graphics.FromImage(result))
                    using (ImageAttributes ia = new ImageAttributes())
                    using (SolidBrush veil = new SolidBrush(tint))
                    {
                        ia.SetWrapMode(WrapMode.TileFlipXY);
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.DrawImage(small, new Rectangle(0, 0, area.Width, area.Height), 0, 0, sw, sh, GraphicsUnit.Pixel, ia);
                        g.FillRectangle(veil, 0, 0, area.Width, area.Height);
                    }
                    return result;
                }
            }
        }
        catch (Exception) { return null; }
    }

    // Place de l'image sur l'écran selon la mise en place choisie dans Windows (remplir par défaut)
    internal static RectangleF WallpaperBounds(Size image, Rectangle screen, int style)
    {
        if (image.Width <= 0 || image.Height <= 0) return screen;
        float sx = (float)screen.Width / image.Width, sy = (float)screen.Height / image.Height;
        float scale;
        switch (style)
        {
            case 1: scale = Math.Min(sx, sy); break;                            // ajuster
            case 2: return screen;                                               // étirer
            case 3: scale = 1f; break;                                           // centrer
            default: scale = Math.Max(sx, sy); break;                            // remplir, mosaïque et étendre approchés
        }
        float w = image.Width * scale, h = image.Height * scale;
        return new RectangleF(screen.X + (screen.Width - w) / 2f, screen.Y + (screen.Height - h) / 2f, w, h);
    }

    void DisposeGlass()
    {
        glassWork++;
        if (glass != null) { glass.Dispose(); glass = null; }
    }
}
