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
            Bitmap replacement = new Bitmap(w, h, fmt);
            if (buf != null) buf.Dispose();
            buf = replacement;
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
            Bitmap res = Reuse(ref spareBuf, area.Width, area.Height, PixelFormat.Format32bppRgb);
            using (Graphics g = Graphics.FromImage(res))
            using (ImageAttributes ia = new ImageAttributes())
            {
                ia.SetWrapMode(WrapMode.TileFlipXY);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(small, new Rectangle(0, 0, area.Width, area.Height), 0, 0, sw2, sh2, GraphicsUnit.Pixel, ia);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(Settings.Dark ? 196 : 200, Theme.Bg))) g.FillRectangle(b, 0, 0, area.Width, area.Height);
            }
            spareBuf = null;
            return res;
        }
        catch { return null; }
    }

    bool liveBlurActive, excludedFromCapture, layoutRightSide;

    uint appliedAffinity = uint.MaxValue;   // dernière affinité donnée à Windows pour ce handle

    void ApplyCaptureSettings()
    {
        uint affinity = Settings.HidePanelInCapture ? Native.WDA_EXCLUDEFROMCAPTURE : Native.WDA_NONE;
        // Panneau caché : l'affinité n'est pas touchée. La retoucher sur une fenêtre cachée faisait réapparaître
        // son image figée à l'écran après un changement de réglage (QA-30) ; elle est appliquée à l'affichage.
        if (IsHandleCreated && Visible && affinity != appliedAffinity)
            appliedAffinity = Native.SetWindowDisplayAffinity(Handle, affinity) ? affinity : uint.MaxValue;
        excludedFromCapture = appliedAffinity == Native.WDA_EXCLUDEFROMCAPTURE && Native.CaptureExclusionSupported;
        liveBlurActive = excludedFromCapture && Settings.Glass && Settings.LiveBlur && !appbar;
        blurTimer.Enabled = Visible && liveBlurActive;
        if (!Settings.Glass) SetGlass(null);
    }

    void SetGlass(Bitmap bitmap)
    {
        Bitmap old = glass;
        glass = bitmap;
        if (old != null) old.Dispose();
    }
    int ClosedLeft { get { return openLeft + (Settings.RightSide ? S(40) : -S(40)); } }

    void RefreshGlass()
    {
        if (!Visible || !liveBlurActive) { blurTimer.Stop(); return; }
        if (anim.Enabled || View.IsScrolling) return;
        Bitmap g = MakeGlass(new Rectangle(openLeft, Top, Width, Height));
        if (g == null) return;
        Bitmap old = glass;
        glass = g;
        if (spareBuf != null) spareBuf.Dispose();
        spareBuf = old; // l'image précédente servira pour la prochaine capture
        Invalidate(true);
    }

    // ---------- Affichage / animations ----------
    void ApplyPanelGeometry()
    {
        int width = S(Settings.PanelWidth);
        bool rightSide = Settings.RightSide;
        if (!IsOpen || (Width == width && layoutRightSide == rightSide)) return;

        Screen screen = Screen.FromControl(this);
        layoutRightSide = rightSide;
        if (appbar)
        {
            Width = width;
            AppBarSetPos();
        }
        else
        {
            Rectangle work = screen.WorkingArea;
            openLeft = rightSide ? work.Right - width : work.Left;
            anim.Stop();
            Bounds = new Rectangle(openLeft, Top, width, Height);
            Opacity = 1;
        }
        if (!liveBlurActive) RefreshStaticGlass();
    }

    public void ShowPanel(Screen scr)
    {
        if (IsOpen) { Activate(); return; }
        if (Visible)
        {
            StartAnim(true);
            ApplyCaptureSettings();
            ApplyPanelGeometry();
            UpdateWidgetTimer(true);
            Activate();
            search.Focus();
            return;
        }
        if (Dirty) RequestLoad();
        Rectangle wa = scr.WorkingArea;
        Width = S(Settings.PanelWidth);
        Height = wa.Height;
        Top = wa.Top;
        layoutRightSide = Settings.RightSide;
        openLeft = layoutRightSide ? wa.Right - Width : wa.Left;
        // Le panneau est encore masqué : la photo du fond ne le contient pas.
        SetGlass(Settings.Glass ? MakeGlass(new Rectangle(openLeft, Top, Width, Height)) : null);
        ApplyCaptureSettings();
        ApplyTheme();
        if (search.Text.Length > 0) search.Text = "";
        else Rebuild(true);
        Left = Settings.Animations ? ClosedLeft : openLeft;
        Opacity = Settings.Animations ? 0 : 1;
        Show();
        ApplyCaptureSettings();
        UpdateWidgetTimer(true);
        Activate();
        Native.SetForegroundWindow(Handle);
        search.Focus();
        StartAnim(true);
    }

    public DateTime LastHidden = DateTime.MinValue;

    public void HidePanel()
    {
        View.CancelHover();
        if (Settings.Pinned && appbar) return;
        if (!Visible || !opening) return;
        View.StopScroll();
        LastHidden = DateTime.Now;
        StartAnim(false);
    }

    void StartAnim(bool open)
    {
        opening = open;
        fromLeft = Left;
        toLeft = open ? openLeft : ClosedLeft;
        fromOp = Opacity;
        toOp = open ? 1 : 0;
        if (!Settings.Animations) { Left = toLeft; Opacity = 1; if (!open) Finish(); anim.Stop(); return; }
        sw.Restart();
        anim.Start();
    }

    void Finish()
    {
        Hide();
        // Rangé hors de tous les écrans : si le compositeur de Windows garde une image de la fenêtre cachée,
        // elle n'est plus visible sur le bord (QA-30). ShowPanel replace la fenêtre avant de l'afficher.
        Rectangle all = SystemInformation.VirtualScreen;
        Location = new Point(all.Left - Width - S(200), Top);
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

    public bool MenuOpen { get { return showingPanelPopup || menu.Visible; } }
}
