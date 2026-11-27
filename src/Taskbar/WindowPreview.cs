// BureauCache — WindowPreview : aperçu en direct d'une fenêtre au survol de son bouton (D-013, T-033)
using System;
using System.Drawing;
using System.Windows.Forms;

// Vignette DWM (l'image vivante de la fenêtre, composée par Windows), titre et bouton de fermeture. La fenêtre
// d'aperçu ne prend jamais le focus : un clic active la fenêtre montrée, la croix la ferme.
class WindowPreview : Form
{
    public event Action<IntPtr> ActivateRequested, CloseRequested;

    IntPtr thumb, target;
    string title = "";
    Bitmap icon;
    Rectangle thumbArea, closeRect;
    bool closeHot;

    static int MaxW { get { return Ui.S(300); } }
    static int MaxH { get { return Ui.S(190); } }
    static int HeaderH { get { return Ui.S(36); } }
    static int Pad { get { return Ui.S(10); } }

    public IntPtr Target { get { return Visible ? target : IntPtr.Zero; } }

    public WindowPreview()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        DoubleBuffered = true;
        BackColor = Theme.Bg;
        Size = new Size(MaxW + 2 * Pad, HeaderH + MaxH + Pad);
    }

    protected override CreateParams CreateParams
    {
        get { CreateParams cp = base.CreateParams; cp.ExStyle |= 0x80 /*TOOLWINDOW*/ | 0x8 /*TOPMOST*/ | 0x08000000 /*NOACTIVATE*/; return cp; }
    }

    protected override bool ShowWithoutActivation { get { return true; } }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x21 /*WM_MOUSEACTIVATE*/) { m.Result = new IntPtr(3 /*MA_NOACTIVATE*/); return; }
        base.WndProc(ref m);
    }

    // anchor : bouton de la fenêtre, en coordonnées écran ; l'aperçu s'ouvre centré au-dessus
    public void ShowFor(IntPtr hwnd, string text, Bitmap image, Rectangle anchor)
    {
        if (Sandbox.Active) return;
        BackColor = Theme.Bg;
        IntPtr unused = Handle;
        Release();
        target = hwnd;
        title = text ?? "";
        // Copie propre : l'image de la barre peut être libérée pendant que l'aperçu est ouvert
        if (icon != null) icon.Dispose();
        icon = image != null ? new Bitmap(image) : null;
        AccessibleName = title;

        // Taille de la source, réduite pour tenir dans le cadre en gardant ses proportions
        Size src = Size.Empty;
        if (Native.DwmRegisterThumbnail(Handle, hwnd, out thumb) == 0)
        {
            Native.SIZE s;
            if (Native.DwmQueryThumbnailSourceSize(thumb, out s) == 0 && s.cx > 0 && s.cy > 0) src = new Size(s.cx, s.cy);
        }
        int w = MaxW, h = MaxH;
        if (!src.IsEmpty)
        {
            double k = Math.Min((double)MaxW / src.Width, (double)MaxH / src.Height);
            w = Math.Max(Ui.S(120), (int)(src.Width * k));
            h = Math.Max(Ui.S(60), (int)(src.Height * k));
        }
        Size = new Size(w + 2 * Pad, HeaderH + h + Pad);
        thumbArea = new Rectangle(Pad, HeaderH, w, h);
        closeRect = new Rectangle(Width - Ui.S(34), Ui.S(4), Ui.S(28), Ui.S(28));

        Rectangle wa = Screen.FromRectangle(anchor).WorkingArea;
        int x = anchor.Left + anchor.Width / 2 - Width / 2;
        x = Math.Max(wa.Left + Ui.S(4), Math.Min(x, wa.Right - Width - Ui.S(4)));
        Location = new Point(x, anchor.Top - Height - Ui.S(8));

        if (thumb != IntPtr.Zero && !src.IsEmpty)
        {
            Native.DWM_THUMBNAIL_PROPERTIES p = new Native.DWM_THUMBNAIL_PROPERTIES();
            p.dwFlags = Native.DWM_TNP_RECTDESTINATION | Native.DWM_TNP_VISIBLE | Native.DWM_TNP_OPACITY | Native.DWM_TNP_SOURCECLIENTAREAONLY;
            p.rcDestination = new Native.RECT { Left = thumbArea.Left, Top = thumbArea.Top, Right = thumbArea.Right, Bottom = thumbArea.Bottom };
            p.opacity = 255;
            p.fVisible = true;
            p.fSourceClientAreaOnly = false;
            Native.DwmUpdateThumbnailProperties(thumb, ref p);
        }
        if (!Visible) Show();
        Native.SetRoundCorners(Handle);
        Invalidate();
    }

    public void HideNow()
    {
        Release();
        target = IntPtr.Zero;
        if (Visible) Hide();
    }

    void Release()
    {
        if (thumb != IntPtr.Zero) { Native.DwmUnregisterThumbnail(thumb); thumb = IntPtr.Zero; }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        Ui.Edge(g, Theme.EdgeHot, new RectangleF(0, 0, Width, Height), Ui.S(Ui.RadiusCard));
        int iconPx = Ui.S(16);
        Rectangle ir = new Rectangle(Pad, (HeaderH - iconPx) / 2, iconPx, iconPx);
        if (icon != null) g.DrawImage(icon, ir); else Ui.Glyph(g, Glyphs.Apps, 9f, Theme.Fg, ir);
        TextRenderer.DrawText(g, title, Ui.Font("Segoe UI", 9.75f), new Rectangle(ir.Right + Ui.S(8), 0, closeRect.Left - ir.Right - Ui.S(12), HeaderH),
            Theme.Fg, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        if (closeHot) Ui.Fill(g, Color.FromArgb(196, 43, 28), closeRect, Ui.S(4));
        Ui.Glyph(g, "", 9f, closeHot ? Color.White : Theme.Fg, closeRect);   // croix (ChromeClose)
        // Sous la vignette : l'icône en grand, visible si Windows n'a pas d'image à montrer (fenêtre réduite, par exemple)
        int big = Ui.S(48);
        Rectangle br = new Rectangle(thumbArea.Left + (thumbArea.Width - big) / 2, thumbArea.Top + (thumbArea.Height - big) / 2, big, big);
        if (icon != null) g.DrawImage(icon, br); else Ui.Glyph(g, Glyphs.Apps, 24f, Theme.FgDim, br);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        bool hot = closeRect.Contains(e.Location);
        if (hot != closeHot) { closeHot = hot; Invalidate(closeRect); }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (closeHot) { closeHot = false; Invalidate(closeRect); }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left || target == IntPtr.Zero) return;
        IntPtr h = target;
        if (closeRect.Contains(e.Location)) { if (CloseRequested != null) CloseRequested(h); }
        else if (ActivateRequested != null) ActivateRequested(h);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        Release();
        if (icon != null) { icon.Dispose(); icon = null; }
        base.OnFormClosed(e);
    }
}
