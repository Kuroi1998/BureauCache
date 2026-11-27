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
    public event Action InitializationFailed;

    // Vrai quand Windows a accepté la réservation : sans elle, la barre ne doit pas remplacer celle de Windows (BAR-09)
    public bool Registered { get { return appbar; } }

    readonly uint abMsg = Native.RegisterWindowMessage("BureauCache.Barre");
    readonly uint taskbarCreated = Native.RegisterWindowMessage("TaskbarCreated");
    readonly Timer clock = new Timer();
    readonly Timer initialTracking = new Timer { Interval = 120 };
    bool initialTrackingPending = true;
    bool appbar;
    Rectangle requestedBarBounds, approvedBarBounds;
    int hot = -1;   // zone survolée : 0 Démarrer, 1 Paramètres rapides, 2 horloge, Overflow, WindowZone + indice
    readonly Rectangle[] zones = new Rectangle[4];   // Démarrer, réseau/son, horloge, notifications

    // Hauteur selon la taille choisie (T-043) : 56 px, la taille normale, après l'essai de 48 px jugé trop petit (QA-58)
    static readonly int[] Heights = { 46, 56, 66 };
    public static int BarHeight { get { return Ui.S(Heights[Math.Max(0, Math.Min(2, Settings.TaskbarSize))]); } }
    // Échelle des icônes et du texte de la barre par rapport à la taille normale
    static float SizeScale { get { return Heights[Math.Max(0, Math.Min(2, Settings.TaskbarSize))] / 56f; } }
    int appliedHeight;
    bool appliedAutoHide;

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
        appliedHeight = BarHeight;
        appliedAutoHide = Settings.TaskbarAutoHide;
        autoHide.Tick += (o, e) => AutoHideTick();
        ApplyTheme();
        appliedAppearance = new AppearanceSettings();
        InitializeButtonIcons();
        previewDelay.Tick += (o, e) => { previewDelay.Stop(); OpenPreview(previewZone); };
        previewWatch.Tick += (o, e) => WatchPreview();
        clock.Tick += (o, e) => { ScheduleClock(); Invalidate(zones[2]); };
        initialTracking.Tick += (o, e) => FinishInitialTracking();
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
        if ((quickSettings != null && quickSettings.BackColor != Theme.Bg) || (popup != null && popup.BackColor != Theme.Bg)) ClosePanels();
        BackColor = Theme.Bg;
        Invalidate();
    }

    // Le minuteur se recale sur le début de chaque minute (un seul réveil par minute), ou de chaque seconde quand
    // l'horloge les affiche
    void ScheduleClock()
    {
        clock.Stop();
        DateTime now = DateTime.Now;
        clock.Interval = Settings.TaskbarSeconds ? Math.Max(50, 1000 - now.Millisecond + 15)
            : Math.Max(1000, (60 - now.Second) * 1000 - now.Millisecond + 50);
        clock.Start();
    }

    // Réglages de la barre changés (T-043) : nouvelle hauteur réservée à Windows si besoin, horloge et dessin
    public void ApplyLayoutSettings()
    {
        UpdateAutoHide();
        if (BarHeight != appliedHeight || Settings.TaskbarAutoHide != appliedAutoHide)
        {
            appliedHeight = BarHeight;
            appliedAutoHide = Settings.TaskbarAutoHide;
            ClosePanels();
            HidePreview();
            Reposition();
        }
        ScheduleClock();
        RefreshGlass();
        Invalidate();
    }

    // La réservation suit la vie du handle (BAR-10). Jamais en test : elle changerait la zone de travail réelle (BAR-12).
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (Sandbox.Active) return;
        AppBarRegister();
        if (!appbar) return;
        UpdateAutoHide();
        RebuildButtons(); // Les épingles sont visibles dès le premier dessin.
        initialTracking.Start();
    }

    void FinishInitialTracking()
    {
        initialTracking.Stop();
        if (Disposing || IsDisposed || !IsHandleCreated || !appbar) return;
        initialTrackingPending = false;
        try
        {
            StartWindows();
            StartupTimeline.Mark("inventaire_fenetres");
            RefreshGlass();
            ScheduleButtonIcons();
        }
        catch { if (InitializationFailed != null) InitializationFailed(); }
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        initialTracking.Stop();
        initialTrackingPending = true;
        ClosePanels();
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
        StartupTimeline.Mark("barre_reservee");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { clock.Dispose(); DisposeBehavior(); DisposeGlass(); }
        base.Dispose(disposing); // OnHandleDestroyed arrête le suivi et ferme l'aperçu.
        if (disposing)
        {
            initialTracking.Dispose();
            DisposeButtonIcons();
            previewDelay.Dispose();
            previewWatch.Dispose();
            tip.Dispose();
        }
    }

    // Place la barre en bas de l'écran principal et y réserve sa hauteur
    public void Reposition()
    {
        Rectangle s = Screen.PrimaryScreen.Bounds;
        if (!appbar) { ApplyBarBounds(VisibleBounds(new Rectangle(s.Left, s.Bottom - ReservedHeight, s.Width, ReservedHeight))); return; }
        Native.APPBARDATA d = new Native.APPBARDATA();
        d.cbSize = Marshal.SizeOf(d);
        d.hWnd = Handle;
        d.uEdge = Native.ABE_BOTTOM;
        d.rc = new Native.RECT { Left = s.Left, Top = s.Bottom - ReservedHeight, Right = s.Right, Bottom = s.Bottom };
        Native.SHAppBarMessage(Native.ABM_QUERYPOS, ref d);
        d.rc.Top = d.rc.Bottom - ReservedHeight;
        Rectangle requested = Rectangle.FromLTRB(d.rc.Left, d.rc.Top, d.rc.Right, d.rc.Bottom);
        if (requested == requestedBarBounds)
        {
            ApplyBarBounds(VisibleBounds(approvedBarBounds));
            return;
        }
        // ABM_SETPOS déclenche ABN_POSCHANGED. Une position identique ne doit pas relancer la notification.
        requestedBarBounds = approvedBarBounds = requested;
        Native.SHAppBarMessage(Native.ABM_SETPOS, ref d);
        approvedBarBounds = Rectangle.FromLTRB(d.rc.Left, d.rc.Top, d.rc.Right, d.rc.Bottom);
        ApplyBarBounds(VisibleBounds(approvedBarBounds));
        RefreshGlass();
    }

    void ApplyBarBounds(Rectangle bounds)
    {
        // Windows peut notifier la position alors que la barre n'a pas bougé.
        if (Bounds == bounds) return;
        ClosePopup();
        if (quickSettings != null && !quickSettings.ShowingConnections) CloseQuickSettings();
        Bounds = bounds;
        if (quickSettings != null)
        {
            Layout2();
            PlaceQuickSettings(quickSettings);
        }
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
        if (m.Msg == 0x1A /*WM_SETTINGCHANGE*/ && m.WParam.ToInt64() == Native.SPI_SETDESKWALLPAPER) RefreshGlass();
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
        ClosePanels();
        HidePreview();
        TopMost = false;
        Native.SetWindowPos(Handle, Native.HWND_BOTTOM, 0, 0, 0, 0, Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
    }

    // ---- Dessin -------------------------------------------------------------------------------------------------

    // Zones de droite selon les éléments affichés (T-043) ; boutons à gauche ou centrés avec le bouton Démarrer,
    // comme Windows 11, tant qu'ils tiennent avant les zones de droite
    void Layout2()
    {
        int h = ClientSize.Height, pad = Ui.S(4), btn = h - 2 * pad;
        string time, date; ClockText(out time, out date);
        int clockW = Math.Max(TextRenderer.MeasureText(time, ClockFont).Width, date == null ? 0 : TextRenderer.MeasureText(date, ClockFont).Width) + Ui.S(20);
        zones[2] = new Rectangle(ClientSize.Width - Ui.S(8) - clockW, pad, clockW, btn);
        int right = zones[2].Left;
        zones[3] = Settings.TaskbarBell ? new Rectangle(right - Ui.S(4) - Ui.S(32), pad, Ui.S(32), btn) : Rectangle.Empty;
        if (Settings.TaskbarBell) right = zones[3].Left;
        zones[1] = Settings.TaskbarQuick ? new Rectangle(right - Ui.S(4) - Ui.S(64), pad, Ui.S(64), btn) : Rectangle.Empty;
        if (Settings.TaskbarQuick) right = zones[1].Left;
        int limit = right - Ui.S(12), start = Ui.S(8);
        if (Settings.TaskbarCenter)
        {
            int group = btn + Ui.S(6) + buttons.Count * ButtonWidth;
            start = Math.Max(Ui.S(8), Math.Min((ClientSize.Width - group) / 2, limit - group));
        }
        zones[0] = new Rectangle(start, pad, btn, btn);
        LayoutWindows(zones[0].Right + Ui.S(6), limit, pad, btn);
    }

    static Font ClockFont { get { return Ui.Font("Segoe UI", 9.75f * Math.Max(0.9f, SizeScale)); } }

    // Heure (avec les secondes si demandé) et date, ou date null quand elle est masquée
    static void ClockText(out string time, out string date)
    {
        ClockText(DateTime.Now, out time, out date);
    }

    internal static void ClockText(DateTime now, out string time, out string date)
    {
        time = now.ToString(Settings.TaskbarSeconds ? "T" : "t", L.Culture);
        date = Settings.TaskbarDate ? now.ToString("d", L.Culture) : null;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        if (Settings.TaskbarLine) using (Pen line = new Pen(Theme.Line)) g.DrawLine(line, 0, 0, ClientSize.Width, 0);
        if (collapsedBar)
        {
            StartupTimeline.Mark("barre_premier_dessin");
            return;   // les zones gardent leur place de la barre dépliée
        }
        Layout2();
        for (int i = 0; i < zones.Length; i++)
            if (zones[i].IsEmpty) continue;
            else if (i == hot || (i == 1 && quickSettings != null) || (i == popupZone && popup != null)) Ui.Fill(g, Theme.HoverOverlay, zones[i], Ui.S(4));

        int logo = (int)(Ui.S(34) * SizeScale);
        Rectangle z = zones[0];
        Ui.DrawLogo(g, new Rectangle(z.Left + (z.Width - logo) / 2, z.Top + (z.Height - logo) / 2, logo, logo), Theme.Accent);
        PaintWindows(g);

        z = zones[1];
        int halfWidth = z.Width / 2;
        float glyph = 13f * Math.Max(0.9f, SizeScale);
        if (!z.IsEmpty)
        {
            Ui.Glyph(g, Glyphs.Wifi, glyph, Theme.Fg, new Rectangle(z.Left, z.Top, halfWidth, z.Height));
            Ui.Glyph(g, Glyphs.Volume, glyph, Theme.Fg, new Rectangle(z.Left + halfWidth, z.Top, z.Width - halfWidth, z.Height));
        }
        if (!zones[3].IsEmpty) Ui.Glyph(g, Glyphs.Bell, glyph, Theme.Fg, zones[3]);

        string time, date; ClockText(out time, out date);
        z = zones[2];
        int half = z.Height / 2;
        TextFormatFlags f = TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding;
        if (date == null)
            TextRenderer.DrawText(g, time, ClockFont, new Rectangle(z.Left, z.Top + half - ClockFont.Height / 2, z.Width, ClockFont.Height), Theme.Fg, f);
        else
        {
            TextRenderer.DrawText(g, time, ClockFont, new Rectangle(z.Left, z.Top + half - ClockFont.Height, z.Width, ClockFont.Height), Theme.Fg, f);
            TextRenderer.DrawText(g, date, ClockFont, new Rectangle(z.Left, z.Top + half, z.Width, ClockFont.Height), Theme.Fg, f);
        }
        StartupTimeline.Mark("barre_premier_dessin");
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
        if (z != hot) { hot = z; Invalidate(); ShowWindowTip(PanelOpen ? -1 : z); }
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
        if (z != 1 || e.Button != MouseButtons.Left) CloseQuickSettings();
        if ((z != 2 && z != 3) || e.Button != MouseButtons.Left) ClosePopup();
        if (WindowMouseUp(z, e.Button)) return;
        if (e.Button == MouseButtons.Right) { if (MenuRequested != null) MenuRequested(); return; }
        if (e.Button != MouseButtons.Left) return;
        if (z == 1) { ToggleQuickSettings(); return; }
        if (z == 0 && StartClicked != null) StartClicked();
        else if (z == 2 || z == 3) TogglePopup(z);   // horloge : calendrier, cloche : notifications (T-042)
    }
}
