// BureauCache — Icons, IconCache
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// =====================================================================
//  Icônes, miniatures et cache
// =====================================================================
static class Icons
{
    static Guid IID_IImageList = new Guid("46EB5926-582E-4017-9FDF-E8998DAA0950");

    public static Bitmap ForPath(string path, int px)
    {
        Native.SHFILEINFO fi = new Native.SHFILEINFO();
        IntPtr r = Native.SHGetFileInfo(path, 0, ref fi, (uint)Marshal.SizeOf(fi), 0x4000);
        if (r == IntPtr.Zero) return null;
        return FromSysIndex(fi.iIcon, px);
    }

    public static Bitmap Stock(uint id, int px)
    {
        Native.SHSTOCKICONINFO si = new Native.SHSTOCKICONINFO();
        si.cbSize = (uint)Marshal.SizeOf(si);
        if (Native.SHGetStockIconInfo(id, 0x4000, ref si) != 0) return null;
        return FromSysIndex(si.iSysIconIndex, px);
    }

    static Bitmap FromSysIndex(int idx, int px)
    {
        int list = px <= 32 ? 0 : px <= 48 ? 2 : 4;
        Bitmap b = FromList(list, idx);
        if (list == 4 && b != null && MostlyEmpty(b)) { b.Dispose(); b = FromList(2, idx); }
        return b;
    }

    static Bitmap FromList(int list, int idx)
    {
        IntPtr himl;
        Guid g = IID_IImageList;
        if (Native.SHGetImageList(list, ref g, out himl) != 0 || himl == IntPtr.Zero) return null;
        try { return FromHIcon(Native.ImageList_GetIcon(himl, idx, 1)); }
        finally { Marshal.Release(himl); }
    }

    // Image (miniature ou icône) d'un élément du shell, avec transparence
    public static Bitmap ShellImage(string parsingName, int px, bool thumbnailOnly)
    {
        Native.IShellItemImageFactory f = null;
        try
        {
            Native.SHCreateItemFromParsingName(parsingName, IntPtr.Zero, typeof(Native.IShellItemImageFactory).GUID, out f);
            IntPtr hb;
            Native.SIZE sz = new Native.SIZE { cx = px, cy = px };
            int hr = f.GetImage(sz, thumbnailOnly ? 0x8 : 0x0, out hb);
            if (hr != 0)
            {
                if (hb != IntPtr.Zero) Native.DeleteObject(hb);
                return null;
            }
            if (hb == IntPtr.Zero) return null;
            return FromHBitmap(hb, PixelFormat.Format32bppPArgb);
        }
        catch { return null; }
        finally { if (f != null) Marshal.ReleaseComObject(f); }
    }

    static Bitmap FromHBitmap(IntPtr hb, PixelFormat alphaFormat)
    {
        try
        {
            using (Bitmap src = Image.FromHbitmap(hb))
            {
                if (Image.GetPixelFormatSize(src.PixelFormat) == 32)
                {
                    Rectangle rc = new Rectangle(0, 0, src.Width, src.Height);
                    BitmapData d = src.LockBits(rc, ImageLockMode.ReadOnly, src.PixelFormat);
                    try
                    {
                        using (Bitmap wrap = new Bitmap(d.Width, d.Height, d.Stride, alphaFormat, d.Scan0))
                            if (HasAlpha(wrap)) return new Bitmap(wrap);
                    }
                    finally { src.UnlockBits(d); }
                }
                return new Bitmap(src);
            }
        }
        finally { Native.DeleteObject(hb); }
    }

    public static Bitmap FromHIcon(IntPtr h)
    {
        if (h == IntPtr.Zero) return null;
        try
        {
            Native.ICONINFO ii;
            if (Native.GetIconInfo(h, out ii))
            {
                if (ii.hbmMask != IntPtr.Zero) Native.DeleteObject(ii.hbmMask);
                if (ii.hbmColor != IntPtr.Zero)
                {
                    Bitmap b = FromHBitmap(ii.hbmColor, PixelFormat.Format32bppArgb);
                    if (b != null && b.PixelFormat == PixelFormat.Format32bppArgb) return b;
                    if (b != null) b.Dispose();
                }
            }
            using (Icon ic = Icon.FromHandle(h)) return ic.ToBitmap();
        }
        catch { return null; }
        finally { Native.DestroyIcon(h); }
    }

    // Lit le canal alpha en une seule copie mémoire (GetPixel pixel par pixel est ~100 fois plus lent)
    static byte[] AlphaChannel(Bitmap b, out int stride)
    {
        BitmapData d = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            // Copie ligne par ligne : selon l'image, les lignes peuvent être rangées de bas en haut (pas négatif)
            int row = Math.Abs(d.Stride);
            stride = row;
            byte[] buf = new byte[row * d.Height];
            for (int y = 0; y < d.Height; y++)
                Marshal.Copy(new IntPtr(d.Scan0.ToInt64() + (long)y * d.Stride), buf, y * row, row);
            return buf;
        }
        finally { b.UnlockBits(d); }
    }

    static bool HasAlpha(Bitmap b)
    {
        int stride;
        byte[] px = AlphaChannel(b, out stride);
        for (int i = 3; i < px.Length; i += 4) if (px[i] > 0) return true;
        return false;
    }

    // Une icône « jumbo » qui n'existe qu'en petit est dessinée dans un coin : on la détecte
    static bool MostlyEmpty(Bitmap b)
    {
        int stride, maxX = 0, maxY = 0;
        byte[] px = AlphaChannel(b, out stride);
        for (int y = 0; y < b.Height; y += 2)
            for (int x = 0; x < b.Width; x += 2)
                if (px[y * stride + x * 4 + 3] > 16) { if (x > maxX) maxX = x; if (y > maxY) maxY = y; }
        return maxX < b.Width / 2 && maxY < b.Height / 2;
    }

    // Petite tuile colorée avec un glyphe (paramètres, commandes, calcul, web)
    public static Bitmap Tile(string glyph, string font, Color c, int px)
    {
        Bitmap b = new Bitmap(px, px);
        using (Graphics g = Graphics.FromImage(b))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            Ui.Fill(g, c, new RectangleF(px * 0.06f, px * 0.06f, px * 0.88f, px * 0.88f), px * 0.22f);
            bool glyphFont = font == Ui.GlyphFont;
            using (Font f = new Font(font, px * (glyphFont ? 0.40f : 0.5f), glyphFont ? FontStyle.Regular : FontStyle.Bold, GraphicsUnit.Pixel))
            using (SolidBrush br = new SolidBrush(Color.White))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString(glyph, f, br, new RectangleF(0, glyphFont ? 0 : -px * 0.04f, px, px), sf);
        }
        return b;
    }
}

static class IconCache
{
    static readonly Dictionary<string, Bitmap> map = new Dictionary<string, Bitmap>(StringComparer.OrdinalIgnoreCase);
    static readonly object lk = new object();

    public static Bitmap Get(string key, Func<Bitmap> make)
    {
        lock (lk) { Bitmap b; if (map.TryGetValue(key, out b)) return b; }
        Bitmap nb = null;
        try { nb = make(); } catch { }
        lock (lk)
        {
            Bitmap b;
            if (map.TryGetValue(key, out b)) { if (nb != null) nb.Dispose(); return b; }
            if (nb != null) map[key] = nb;
            return nb;
        }
    }

    // Libère toutes les icônes d'une taille donnée (les clés contiennent « |taille| » ou finissent par « |taille »)
    public static void RemoveSize(int px)
    {
        string mid = "|" + px + "|", end = "|" + px;
        List<Bitmap> dead = new List<Bitmap>();
        lock (lk)
        {
            foreach (string k in map.Keys.Where(k => k.Contains(mid) || k.EndsWith(end)).ToList())
            {
                if (map[k] != null) dead.Add(map[k]);
                map.Remove(k);
            }
        }
        foreach (Bitmap b in dead) b.Dispose();
    }

    public static Bitmap ForFile(string path, int px)
    {
        long stamp = 0;
        try { stamp = File.GetLastWriteTimeUtc(path).Ticks; } catch { }
        string key = path + "|" + px + "|" + stamp;
        RetireOlderVersions(path + "|" + px + "|", key);
        return Get(key, () => Icons.ForPath(path, px));
    }

    // Un fichier modifié change de clé (sa date fait partie de la clé) : l'image de l'ancienne version restait en
    // mémoire jusqu'à la fermeture (CDX-29). Elle sort du cache tout de suite, mais n'est libérée qu'après la
    // publication des nouvelles listes (ReleaseRetired, sur le fil d'interface) : l'ancienne liste peut encore la dessiner.
    static readonly List<Bitmap> retired = new List<Bitmap>();

    static void RetireOlderVersions(string prefix, string current)
    {
        lock (lk)
        {
            foreach (string k in map.Keys.Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                                                     !string.Equals(k, current, StringComparison.OrdinalIgnoreCase)).ToList())
            {
                if (map[k] != null) retired.Add(map[k]);
                map.Remove(k);
            }
        }
    }

    public static void ReleaseRetired()
    {
        List<Bitmap> dead;
        lock (lk) { dead = retired.ToList(); retired.Clear(); }
        foreach (Bitmap b in dead) b.Dispose();
    }

    public static Bitmap Tile(string glyph, string font, Color c, int px)
    {
        return Get("tile|" + glyph + "|" + font + "|" + c.ToArgb() + "|" + px, () => Icons.Tile(glyph, font, c, px));
    }
}
