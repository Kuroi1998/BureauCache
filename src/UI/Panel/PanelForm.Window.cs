// BureauCache — PanelForm (partie 5)
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;

partial class PanelForm
{
    // ---------- Effet verre ----------
    // Images de travail réutilisées d'une capture à l'autre (le flou en direct en refait une toutes les 120 ms)
    Bitmap capBuf, smallBuf, spareBuf;

    static Bitmap Reuse(ref Bitmap buf, int w, int h, PixelFormat fmt)
    {
        if (buf == null || buf.Width != w || buf.Height != h)
        {
            if (buf != null) buf.Dispose();
            buf = new Bitmap(w, h, fmt);
        }
        return buf;
    }

    Bitmap MakeGlass(Rectangle area)
    {
        try
        {
            Bitmap cap = Reuse(ref capBuf, area.Width, area.Height, PixelFormat.Format32bppRgb);
            using (Graphics g = Graphics.FromImage(cap)) g.CopyFromScreen(area.Location, Point.Empty, area.Size);
            int sw2 = Math.Max(2, area.Width / 22), sh2 = Math.Max(2, area.Height / 22);
            Bitmap small = Reuse(ref smallBuf, sw2, sh2, PixelFormat.Format32bppRgb);
            using (Graphics g = Graphics.FromImage(small))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                g.DrawImage(cap, new Rectangle(0, 0, sw2, sh2));
            }
            Bitmap res = spareBuf != null && spareBuf.Width == area.Width && spareBuf.Height == area.Height
                ? spareBuf : new Bitmap(area.Width, area.Height, PixelFormat.Format32bppRgb);
            spareBuf = null;
            using (Graphics g = Graphics.FromImage(res))
            using (ImageAttributes ia = new ImageAttributes())
            {
                ia.SetWrapMode(WrapMode.TileFlipXY);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(small, new Rectangle(0, 0, area.Width, area.Height), 0, 0, sw2, sh2, GraphicsUnit.Pixel, ia);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(Settings.Dark ? 196 : 200, Theme.Bg))) g.FillRectangle(b, 0, 0, area.Width, area.Height);
            }
            return res;
        }
        catch { return null; }
    }

    bool liveBlurActive;

    void RefreshGlass()
    {
        if (!Visible || !liveBlurActive) { blurTimer.Stop(); return; }
        Bitmap g = MakeGlass(new Rectangle(openLeft, Top, Width, Height));
        if (g == null) return;
        Bitmap old = glass;
        glass = g;
        if (spareBuf != null) spareBuf.Dispose();
        spareBuf = old; // l'image précédente servira pour la prochaine capture
        Invalidate(true);
    }

    // ---------- Affichage / animations ----------
    public void ShowPanel(Screen scr)
    {
        if (IsOpen) { Activate(); return; }
        if (Visible) { StartAnim(true); Activate(); search.Focus(); return; }
        if (Dirty) RequestLoad();
        Rectangle wa = scr.WorkingArea;
        Width = S(Settings.PanelWidth);
        Height = wa.Height;
        Top = wa.Top;
        openLeft = Settings.RightSide ? wa.Right - Width : wa.Left;
        closedLeft = Settings.RightSide ? wa.Right - Width + S(40) : wa.Left - S(40);
        if (glass != null) { glass.Dispose(); glass = null; }
        liveBlurActive = Settings.Glass && Settings.LiveBlur;
        // Exclut le panneau des captures : permet de capturer ce qu'il y a derrière en continu
        try { Native.SetWindowDisplayAffinity(Handle, liveBlurActive ? 0x11u : 0u); } catch { liveBlurActive = false; }
        if (Settings.Glass) glass = MakeGlass(new Rectangle(openLeft, Top, Width, Height));
        if (Settings.Widgets) { RefreshWidgetData(); widgetTimer.Start(); }
        ApplyTheme();
        if (search.Text.Length > 0) search.Text = "";
        else Rebuild(true);
        Left = Settings.Animations ? closedLeft : openLeft;
        Opacity = Settings.Animations ? 0 : 1;
        Show();
        Activate();
        Native.SetForegroundWindow(Handle);
        search.Focus();
        StartAnim(true);
        if (liveBlurActive) blurTimer.Start();
    }

    public void HidePanel()
    {
        preview.HidePreview();
        if (Settings.Pinned && appbar) return;
        if (!Visible || !opening) return;
        StartAnim(false);
    }

    void StartAnim(bool open)
    {
        opening = open;
        fromLeft = Left;
        toLeft = open ? openLeft : closedLeft;
        fromOp = Opacity;
        toOp = open ? 1 : 0;
        if (!Settings.Animations) { Left = open ? openLeft : closedLeft; Opacity = 1; if (!open) Finish(); anim.Stop(); return; }
        sw.Restart();
        anim.Start();
    }

    void Finish()
    {
        Hide();
        widgetTimer.Stop();
        blurTimer.Stop();
    }

    void AnimTick(object sender, EventArgs e)
    {
        double dur = opening ? 180.0 : 120.0;
        double t = Math.Min(1, sw.ElapsedMilliseconds / dur);
        double k = 1 - Math.Pow(1 - t, 3);
        Left = (int)Math.Round(fromLeft + (toLeft - fromLeft) * k);
        Opacity = fromOp + (toOp - fromOp) * k;
        if (t >= 1)
        {
            anim.Stop();
            if (!opening) { Finish(); Opacity = 1; }
        }
    }

    public bool MenuOpen { get { return menu.Visible; } }
}
