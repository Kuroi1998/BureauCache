// BureauCache — BarForm : boutons des fenêtres ouvertes (D-013, T-033)
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

partial class BarForm
{
    const int Overflow = 99, WindowZone = 100;   // codes de zone : « +N », puis 100 + indice du bouton
    WindowList windows;
    uint shellHook;
    readonly List<Rectangle> windowRects = new List<Rectangle>();
    Rectangle overflowRect;
    int shownWindows;
    // Aperçu au survol : il s'ouvre après un court délai, passe tout de suite d'un bouton à l'autre une fois ouvert, et
    // reste tant que la souris est sur un bouton de fenêtre ou sur l'aperçu lui-même
    WindowPreview preview;
    readonly Timer previewDelay = new Timer { Interval = 400 };
    readonly Timer previewWatch = new Timer { Interval = 150 };
    int previewZone = -1;

    // Position du bouton Démarrer à l'écran, pour placer le menu juste au-dessus
    public Rectangle StartButtonBounds { get { return RectangleToScreen(zones[0]); } }

    void StartWindows()
    {
        if (windows != null) return;
        windows = new WindowList(h => h == Handle);
        windows.Changed += Invalidate;
        windows.Changed += OnWindowsChanged;
        previewDelay.Tick += (s, e) => { previewDelay.Stop(); OpenPreview(previewZone); };
        previewWatch.Tick += (s, e) => WatchPreview();
        // Messages du shell : une fenêtre qui clignote (demande d'attention) est signalée par HSHELL_FLASH
        shellHook = Native.RegisterWindowMessage("SHELLHOOK");
        Native.RegisterShellHookWindow(Handle);
        windows.Start();
    }

    void StopWindows()
    {
        if (windows == null) return;
        Native.DeregisterShellHookWindow(Handle);
        HidePreview();
        if (preview != null) { preview.Close(); preview.Dispose(); preview = null; }
        windows.Dispose();
        windows = null;
    }

    void OnShellHook(IntPtr wParam, IntPtr lParam)
    {
        if (windows == null) return;
        int code = (int)(wParam.ToInt64() & 0xFFFF);
        if (code == Native.HSHELL_FLASH) windows.Flash(lParam);
    }

    static int ButtonWidth { get { return Ui.S(44); } }

    void LayoutWindows(int left, int right, int top, int height)
    {
        windowRects.Clear();
        overflowRect = Rectangle.Empty;
        int count = windows == null ? 0 : windows.Items.Count;
        int fit = Math.Max(0, (right - left) / ButtonWidth);
        shownWindows = count <= fit ? count : Math.Max(0, fit - 1);   // une place pour « +N »
        for (int i = 0; i < shownWindows; i++)
            windowRects.Add(new Rectangle(left + i * ButtonWidth, top, ButtonWidth - Ui.S(2), height));
        if (shownWindows < count) overflowRect = new Rectangle(left + shownWindows * ButtonWidth, top, ButtonWidth - Ui.S(2), height);
    }

    int WindowZoneAt(Point p)
    {
        for (int i = 0; i < windowRects.Count; i++) if (windowRects[i].Contains(p)) return WindowZone + i;
        return overflowRect.Contains(p) ? Overflow : -1;
    }

    TaskWindow WindowAtZone(int zone)
    {
        int i = zone - WindowZone;
        return windows != null && i >= 0 && i < shownWindows && i < windows.Items.Count ? windows.Items[i] : null;
    }

    void PaintWindows(Graphics g)
    {
        if (windows == null) return;
        int icon = Ui.S(24);
        for (int i = 0; i < windowRects.Count; i++)
        {
            TaskWindow w = windows.Items[i];
            Rectangle r = windowRects[i];
            bool active = w.Handle == windows.Active;
            if (w.Flashing) Ui.Fill(g, Color.FromArgb(150, 249, 115, 22), r, Ui.S(4));
            else if (active) Ui.Fill(g, Theme.SelOverlay, r, Ui.S(4));
            else if (hot == WindowZone + i) Ui.Fill(g, Theme.HoverOverlay, r, Ui.S(4));
            Rectangle ir = new Rectangle(r.Left + (r.Width - icon) / 2, r.Top + (r.Height - icon) / 2 - Ui.S(2), icon, icon);
            Bitmap b = TaskIcons.For(w.Handle, icon);
            if (b != null) g.DrawImage(b, ir);
            else Ui.Glyph(g, Glyphs.Apps, 13f, Theme.Fg, ir);
            // Trait sous l'icône : long et coloré pour la fenêtre active, court et gris pour les autres
            int lw = active ? Ui.S(16) : Ui.S(6);
            Ui.Fill(g, active ? Theme.Accent : Theme.FgDim, new RectangleF(r.Left + (r.Width - lw) / 2f, r.Bottom - Ui.S(4), lw, Ui.S(3)), Ui.S(1));
        }
        if (!overflowRect.IsEmpty)
        {
            if (hot == Overflow) Ui.Fill(g, Theme.HoverOverlay, overflowRect, Ui.S(4));
            TextRenderer.DrawText(g, "+" + (windows.Items.Count - shownWindows), ClockFont, overflowRect, Theme.Fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    // Appelé quand la zone survolée change
    void ShowWindowTip(int zone)
    {
        previewZone = zone;
        if (WindowAtZone(zone) == null) { previewDelay.Stop(); return; }
        if (preview != null && preview.Visible) OpenPreview(zone);
        else { previewDelay.Stop(); previewDelay.Start(); }
    }

    void OpenPreview(int zone)
    {
        TaskWindow w = WindowAtZone(zone);
        if (w == null || zone != hot) return;
        if (preview == null)
        {
            preview = new WindowPreview();
            preview.ActivateRequested += h => { HidePreview(); WindowList.Activate(h, false); if (windows != null) windows.Refresh(); };
            preview.CloseRequested += h => { HidePreview(); WindowList.Close(h); };
        }
        Rectangle r = RectangleToScreen(windowRects[zone - WindowZone]);
        preview.ShowFor(w.Handle, w.Title, TaskIcons.For(w.Handle, Ui.S(32)), r);
        previewWatch.Start();
    }

    public void HidePreview()
    {
        previewDelay.Stop();
        previewWatch.Stop();
        if (preview != null) preview.HideNow();
    }

    // La souris a quitté les boutons et l'aperçu : il se ferme
    void WatchPreview()
    {
        if (preview == null || !preview.Visible) { previewWatch.Stop(); return; }
        Point p = Cursor.Position;
        if (preview.Bounds.Contains(p)) return;
        Point local = PointToClient(p);
        if (ClientRectangle.Contains(local) && WindowAtZone(WindowZoneAt(local)) != null) return;
        HidePreview();
    }

    // La fenêtre montrée a disparu : l'aperçu aussi
    void OnWindowsChanged()
    {
        if (preview == null || preview.Target == IntPtr.Zero || windows == null) return;
        if (!windows.Items.Exists(w => w.Handle == preview.Target)) HidePreview();
    }

    // Vrai si le clic concernait un bouton de fenêtre (ou « +N ») et a été traité
    bool WindowMouseUp(int zone, MouseButtons button)
    {
        if (zone == Overflow) { ShowOverflowMenu(); return true; }
        TaskWindow w = WindowAtZone(zone);
        if (w == null) return false;
        HidePreview();
        if (button == MouseButtons.Left) { WindowList.Activate(w.Handle, w.Handle == Native.GetForegroundWindow()); windows.Refresh(); }
        else if (button == MouseButtons.Right) ShowWindowMenu(w);
        return true;
    }

    void ShowWindowMenu(TaskWindow w)
    {
        ContextMenuStrip menu = Menus.Create();
        string title = w.Title.Length > 60 ? w.Title.Substring(0, 59) + "…" : w.Title;
        menu.Items.Add(new ToolStripMenuItem(title) { Enabled = false });
        menu.Items.Add(new ToolStripSeparator());
        IntPtr h = w.Handle;
        menu.Items.Add(Menus.Item(L.T("Fermer la fenêtre", "Close window", "Cerrar la ventana"), Glyphs.Delete, () => WindowList.Close(h)));
        ShowMenu(menu);
    }

    void ShowOverflowMenu()
    {
        if (windows == null) return;
        ContextMenuStrip menu = Menus.Create();
        for (int i = shownWindows; i < windows.Items.Count; i++)
        {
            IntPtr h = windows.Items[i].Handle;
            string title = windows.Items[i].Title;
            if (title.Length > 60) title = title.Substring(0, 59) + "…";
            menu.Items.Add(Menus.Item(title, Glyphs.Apps, () => WindowList.Activate(h, false)));
        }
        ShowMenu(menu);
    }

    // Le menu passe au premier plan pour recevoir le clavier et se fermer quand on clique ailleurs
    static void ShowMenu(ContextMenuStrip menu)
    {
        menu.Closed += (s, e) => menu.BeginInvoke(new Action(menu.Dispose));
        menu.Show(Cursor.Position, ToolStripDropDownDirection.AboveRight);
        Native.SetForegroundWindow(menu.Handle);
        menu.Focus();
    }
}
