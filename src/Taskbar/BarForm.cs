// BureauCache — BarForm : barre des tâches BureauCache en bas de l'écran principal (D-013 ; fenêtres : BarForm.Windows.cs)
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// Fenêtre AppBar : Windows réserve sa hauteur, les fenêtres agrandies s'arrêtent au-dessus. Elle ne prend jamais
// le focus (WS_EX_NOACTIVATE) et n'apparaît ni dans Alt+Tab ni dans la liste des fenêtres (WS_EX_TOOLWINDOW).
partial class BarForm : Form
{
    public event Action StartClicked, MenuRequested, ExplorerRestarted;

    // Vrai quand Windows a accepté la réservation : sans elle, la barre ne doit pas remplacer celle de Windows (BAR-09)
    public bool Registered { get { return appbar; } }

    readonly uint abMsg = Native.RegisterWindowMessage("BureauCache.Barre");
    readonly uint taskbarCreated = Native.RegisterWindowMessage("TaskbarCreated");
    readonly Timer clock = new Timer();
    bool appbar;
    Rectangle requestedBarBounds, approvedBarBounds;
    int hot = -1;   // zone survolée : 0 Démarrer, 1 Paramètres rapides, 2 horloge, Overflow, WindowZone + indice
    Rectangle[] zones = new Rectangle[3];

    public static int BarHeight { get { return Ui.S(56); } }   // 48 px à l'essai : icônes jugées trop petites (QA-58)

    public BarForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        DoubleBuffered = true;
        Text = "BureauCache";
        AccessibleName = L.T("Barre des tâches BureauCache", "BureauCache taskbar", "Barra de tareas de BureauCache");
        AccessibleRole = AccessibleRole.ToolBar;
        Rectangle s = Screen.PrimaryScreen.Bounds;
        Bounds = new Rectangle(s.Left, s.Bottom - BarHeight, s.Width, BarHeight);
        ApplyTheme();
        previewDelay.Tick += (o, e) => { previewDelay.Stop(); OpenPreview(previewZone); };
        previewWatch.Tick += (o, e) => WatchPreview();
        clock.Tick += (o, e) => { ScheduleClock(); Invalidate(zones[2]); };
        ScheduleClock();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= 0x80 /*WS_EX_TOOLWINDOW*/ | 0x8 /*WS_EX_TOPMOST*/ | 0x08000000 /*WS_EX_NOACTIVATE*/;
            return cp;
        }
    }

    protected override bool ShowWithoutActivation { get { return true; } }

    public void ApplyTheme()
    {
        BackColor = Theme.Bg;
        Invalidate();
    }

    // Le minuteur se recale sur le début de chaque minute : un seul réveil par minute
    void ScheduleClock()
    {
        clock.Stop();
        DateTime now = DateTime.Now;
        clock.Interval = Math.Max(1000, (60 - now.Second) * 1000 - now.Millisecond + 50);
        clock.Start();
    }

    // La réservation suit la vie du handle (BAR-10). Jamais en test : elle changerait la zone de travail réelle (BAR-12).
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (!Sandbox.Active) { AppBarRegister(); StartWindows(); }
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        StopWindows();
        AppBarRemove();
        base.OnHandleDestroyed(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        clock.Stop();
        AppBarRemove();
        base.OnFormClosed(e);
    }

    void AppBarRegister()
    {
        if (appbar) return;
        Native.APPBARDATA d = new Native.APPBARDATA();
        d.cbSize = Marshal.SizeOf(d);
        d.hWnd = Handle;
        d.uCallbackMessage = abMsg;
        if (Native.SHAppBarMessage(Native.ABM_NEW, ref d) == UIntPtr.Zero) return;
        appbar = true;
        Reposition();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) clock.Dispose();
        base.Dispose(disposing); // OnHandleDestroyed arrête le suivi et ferme l'aperçu.
        if (disposing)
        {
            previewDelay.Dispose();
            previewWatch.Dispose();
            tip.Dispose();
        }
    }

    // Place la barre en bas de l'écran principal et y réserve sa hauteur
    public void Reposition()
    {
        Rectangle s = Screen.PrimaryScreen.Bounds;
        if (!appbar) { Bounds = new Rectangle(s.Left, s.Bottom - BarHeight, s.Width, BarHeight); return; }
        Native.APPBARDATA d = new Native.APPBARDATA();
        d.cbSize = Marshal.SizeOf(d);
        d.hWnd = Handle;
        d.uEdge = Native.ABE_BOTTOM;
        d.rc = new Native.RECT { Left = s.Left, Top = s.Bottom - BarHeight, Right = s.Right, Bottom = s.Bottom };
        Native.SHAppBarMessage(Native.ABM_QUERYPOS, ref d);
        d.rc.Top = d.rc.Bottom - BarHeight;
        Rectangle requested = Rectangle.FromLTRB(d.rc.Left, d.rc.Top, d.rc.Right, d.rc.Bottom);
        if (requested == requestedBarBounds)
        {
            if (Bounds != approvedBarBounds) Bounds = approvedBarBounds;
            return;
        }
        // ABM_SETPOS déclenche ABN_POSCHANGED. Une position identique ne doit pas relancer la notification.
        requestedBarBounds = approvedBarBounds = requested;
        Native.SHAppBarMessage(Native.ABM_SETPOS, ref d);
        approvedBarBounds = Rectangle.FromLTRB(d.rc.Left, d.rc.Top, d.rc.Right, d.rc.Bottom);
        Bounds = approvedBarBounds;
    }

    void AppBarRemove()
    {
        if (!appbar) return;
        Native.APPBARDATA d = new Native.APPBARDATA();
        d.cbSize = Marshal.SizeOf(d);
        d.hWnd = Handle;
        Native.SHAppBarMessage(Native.ABM_REMOVE, ref d);
        appbar = false;
        requestedBarBounds = Rectangle.Empty;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x21 /*WM_MOUSEACTIVATE*/) { m.Result = new IntPtr(3 /*MA_NOACTIVATE*/); return; }
        if (HandleTaskbarList(ref m)) return;   // badges et progressions des applications (D-016)
        if (appbar && m.Msg == (int)abMsg)
        {
            int code = m.WParam.ToInt32();
            if (code == Native.ABN_POSCHANGED) Reposition();
            else if (code == Native.ABN_FULLSCREENAPP) OnFullscreen(m.LParam != IntPtr.Zero);
        }
        else if (m.Msg == (int)shellHook) OnShellHook(m.WParam, m.LParam);
        else if (!Sandbox.Active && taskbarCreated != 0 && m.Msg == (int)taskbarCreated)
        {
            // Explorer a redémarré : il a oublié notre réservation, et sa barre réapparaît. Windows envoie aussi ce
            // message quand l'échelle de l'écran change, réservation conservée : on la rend d'abord, sinon la nouvelle
            // demande serait refusée comme un doublon et la barre BureauCache retirée à tort (CDX-04).
            AppBarRemove();
            requestedBarBounds = Rectangle.Empty;
            AppBarRegister();
            if (windows != null) { Native.RegisterShellHookWindow(Handle); StartBadges(); }   // nouvelle barre d'Explorer : routage à reposer
            if (ExplorerRestarted != null) ExplorerRestarted();
        }
        base.WndProc(ref m);
    }

    // Une application passe en plein écran : la barre descend sous toutes les fenêtres ; elle remonte à la sortie (BAR-10)
    void OnFullscreen(bool entering)
    {
        if (!entering) { TopMost = true; return; }
        HidePreview();
        TopMost = false;
        Native.SetWindowPos(Handle, Native.HWND_BOTTOM, 0, 0, 0, 0, Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
    }

    // ---- Dessin -------------------------------------------------------------------------------------------------

    void Layout2()
    {
        int h = ClientSize.Height, pad = Ui.S(4), btn = h - 2 * pad;
        zones[0] = new Rectangle(Ui.S(8), pad, btn, btn);
        string time, date; ClockText(out time, out date);
        int clockW = Math.Max(TextRenderer.MeasureText(time, ClockFont).Width, TextRenderer.MeasureText(date, ClockFont).Width) + Ui.S(20);
        zones[2] = new Rectangle(ClientSize.Width - Ui.S(8) - clockW, pad, clockW, btn);
        zones[1] = new Rectangle(zones[2].Left - Ui.S(4) - Ui.S(96), pad, Ui.S(96), btn);
        LayoutWindows(zones[0].Right + Ui.S(6), zones[1].Left - Ui.S(12), pad, btn);
    }

    static Font ClockFont { get { return Ui.Font("Segoe UI", 9.75f); } }

    static void ClockText(out string time, out string date)
    {
        DateTime now = DateTime.Now;
        time = now.ToString("t", L.Culture);
        date = now.ToString("d", L.Culture);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Layout2();
        using (Pen line = new Pen(Theme.Line)) g.DrawLine(line, 0, 0, ClientSize.Width, 0);
        for (int i = 0; i < zones.Length; i++)
            if (i == hot) Ui.Fill(g, Theme.HoverOverlay, zones[i], Ui.S(4));

        int logo = Ui.S(34);
        Rectangle z = zones[0];
        Ui.DrawLogo(g, new Rectangle(z.Left + (z.Width - logo) / 2, z.Top + (z.Height - logo) / 2, logo, logo), Theme.Accent);
        PaintWindows(g);

        z = zones[1];
        int third = z.Width / 3;
        Ui.Glyph(g, Glyphs.Wifi, 13f, Theme.Fg, new Rectangle(z.Left, z.Top, third, z.Height));
        Ui.Glyph(g, Glyphs.Volume, 13f, Theme.Fg, new Rectangle(z.Left + third, z.Top, third, z.Height));
        Ui.Glyph(g, Glyphs.Bell, 13f, Theme.Fg, new Rectangle(z.Left + 2 * third, z.Top, third, z.Height));

        string time, date; ClockText(out time, out date);
        z = zones[2];
        int half = z.Height / 2;
        TextFormatFlags f = TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding;
        TextRenderer.DrawText(g, time, ClockFont, new Rectangle(z.Left, z.Top + half - ClockFont.Height, z.Width, ClockFont.Height), Theme.Fg, f);
        TextRenderer.DrawText(g, date, ClockFont, new Rectangle(z.Left, z.Top + half, z.Width, ClockFont.Height), Theme.Fg, f);
    }

    int ZoneAt(Point p)
    {
        for (int i = 0; i < zones.Length; i++) if (zones[i].Contains(p)) return i;
        return WindowZoneAt(p);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (DragMove(e.Location)) return;
        int z = ZoneAt(e.Location);
        if (z != hot) { hot = z; Invalidate(); ShowWindowTip(z); }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (hot != -1) { hot = -1; Invalidate(); ShowWindowTip(-1); }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        int z = ZoneAt(e.Location);
        if (e.Button == MouseButtons.Left)
        {
            bool changed = (pressedButton != null || z >= WindowZone) && !StillPressed(z - WindowZone);
            bool sameZone = pressZone == z;
            pressZone = NoZone;
            if (DragEnd() || changed || !sameZone) return;
        }
        if (WindowMouseUp(z, e.Button)) return;
        if (e.Button == MouseButtons.Right) { if (MenuRequested != null) MenuRequested(); return; }
        if (e.Button != MouseButtons.Left) return;
        if (z == 0 && StartClicked != null) StartClicked();
        else if (z == 1) Native.PressWinKey(0x41);   // Windows + A : Paramètres rapides (réseau, son, batterie)
        else if (z == 2) Native.PressWinKey(0x4E);   // Windows + N : notifications et calendrier
    }
}
