// BureauCache — BarForm : boutons des applications épinglées et des fenêtres ouvertes (D-013, D-014)
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

partial class BarForm
{
    const int Overflow = 99, WindowZone = 100;   // codes de zone : « +N », puis 100 + indice du bouton
    WindowList windows;
    uint shellHook;
    List<BarButton> buttons = new List<BarButton>();
    readonly List<Rectangle> buttonRects = new List<Rectangle>();
    Rectangle overflowRect;
    int shown, buttonsLeft;
    // Aperçu au survol : il s'ouvre après un court délai, passe tout de suite d'un bouton à l'autre une fois ouvert, et
    // reste tant que la souris est sur un bouton ou sur l'aperçu lui-même. Une épingle fermée n'a qu'une infobulle.
    WindowPreview preview;
    readonly Timer previewDelay = new Timer { Interval = 400 };
    readonly Timer previewWatch = new Timer { Interval = 150 };
    readonly ToolTip tip = new ToolTip();
    int previewZone = -1;

    // Position du bouton Démarrer à l'écran, pour placer le menu juste au-dessus
    public Rectangle StartButtonBounds { get { return RectangleToScreen(zones[0]); } }

    void StartWindows()
    {
        if (windows != null) return;
        windows = new WindowList(h => h == Handle);
        windows.Changed += RebuildButtons;
        windows.Changed += OnWindowsChanged;
        // Messages du shell : une fenêtre qui clignote (demande d'attention) est signalée par HSHELL_FLASH
        shellHook = Native.RegisterWindowMessage("SHELLHOOK");
        Native.RegisterShellHookWindow(Handle);
        windows.Start();
        StartBadges();
    }

    void StopWindows()
    {
        if (windows == null) return;
        Native.DeregisterShellHookWindow(Handle);
        HidePreview();
        if (preview != null) { preview.Close(); preview.Dispose(); preview = null; }
        windows.Dispose();
        windows = null;
        StopBadges();
    }

    public void RetryWindowTracking()
    {
        if (windows != null && windows.NeedsRefresh) windows.Refresh();
    }

    void OnShellHook(IntPtr wParam, IntPtr lParam)
    {
        if (windows == null) return;
        int code = (int)(wParam.ToInt64() & 0xFFFF);
        if (code == Native.HSHELL_FLASH) windows.Flash(lParam);
    }

    // Liste des boutons : épingles puis fenêtres (D-014). Pendant un glisser-déposer, la liste en cours est gardée jusqu'au lâcher.
    public void RebuildButtons()
    {
        if (dragging) return;   // la fin du glissement reconstruit la liste
        buttons = TaskbarModel.Build(Settings.TaskbarPins, windows != null ? windows.Items : new List<TaskWindow>());
        TaskIcons.PruneKeys(new HashSet<string>(buttons.Where(b => b.Key != null).Select(b => b.Key), StringComparer.OrdinalIgnoreCase));
        Invalidate();
    }

    static int ButtonWidth { get { return Ui.S(56); } }

    void LayoutWindows(int left, int right, int top, int height)
    {
        buttonRects.Clear();
        overflowRect = Rectangle.Empty;
        buttonsLeft = left;
        int count = buttons.Count;
        int fit = Math.Max(0, (right - left) / ButtonWidth);
        shown = count <= fit ? count : Math.Max(0, fit - 1);   // une place pour « +N »
        for (int i = 0; i < shown; i++)
            buttonRects.Add(new Rectangle(left + i * ButtonWidth, top, ButtonWidth - Ui.S(2), height));
        if (shown < count) overflowRect = new Rectangle(left + shown * ButtonWidth, top, ButtonWidth - Ui.S(2), height);
    }

    int WindowZoneAt(Point p)
    {
        for (int i = 0; i < buttonRects.Count; i++) if (buttonRects[i].Contains(p)) return WindowZone + i;
        return overflowRect.Contains(p) ? Overflow : -1;
    }

    BarButton ButtonAtZone(int zone)
    {
        int i = zone - WindowZone;
        return i >= 0 && i < shown && i < buttons.Count ? buttons[i] : null;
    }

    Bitmap IconOf(BarButton b, int px)
    {
        Bitmap img = b.Window != null ? TaskIcons.For(b.Window.Handle, px) : null;
        return img ?? TaskIcons.ForKey(b.Key, px);
    }

    void PaintWindows(Graphics g)
    {
        int icon = Ui.S(32);
        IntPtr active = windows != null ? windows.Active : IntPtr.Zero;
        for (int i = 0; i < buttonRects.Count && i < buttons.Count; i++)
        {
            BarButton b = buttons[i];
            Rectangle r = buttonRects[i];
            bool isActive = b.Window != null && b.Window.Handle == active;
            bool lifted = dragging && i == dragIndex;
            if (lifted) Ui.Fill(g, Theme.SelOverlay, r, Ui.S(4));
            else if (b.Window != null && b.Window.Flashing) Ui.Fill(g, Color.FromArgb(150, 249, 115, 22), r, Ui.S(4));
            else if (isActive) Ui.Fill(g, Theme.SelOverlay, r, Ui.S(4));
            else if (hot == WindowZone + i) Ui.Fill(g, Theme.HoverOverlay, r, Ui.S(4));
            Rectangle ir = new Rectangle(r.Left + (r.Width - icon) / 2, r.Top + (r.Height - icon) / 2 - Ui.S(2), icon, icon);
            Bitmap img = IconOf(b, icon);
            if (img != null) g.DrawImage(img, ir);
            else Ui.Glyph(g, Glyphs.Apps, 17f, Theme.Fg, ir);
            PaintBadge(g, r, ir, b.Window != null ? b.Window.Handle : IntPtr.Zero);
            // Trait sous l'icône : long et coloré pour la fenêtre active, court et gris pour une fenêtre ouverte,
            // aucun pour une épingle dont l'application est fermée
            if (b.Window == null) continue;
            int lw = isActive ? Ui.S(16) : Ui.S(6);
            Ui.Fill(g, isActive ? Theme.Accent : Theme.FgDim, new RectangleF(r.Left + (r.Width - lw) / 2f, r.Bottom - Ui.S(4), lw, Ui.S(3)), Ui.S(1));
        }
        if (!overflowRect.IsEmpty)
        {
            if (hot == Overflow) Ui.Fill(g, Theme.HoverOverlay, overflowRect, Ui.S(4));
            TextRenderer.DrawText(g, "+" + (buttons.Count - shown), ClockFont, overflowRect, Theme.Fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    // Appelé quand la zone survolée change
    void ShowWindowTip(int zone)
    {
        previewZone = zone;
        tip.Hide(this);
        BarButton b = ButtonAtZone(zone);
        if (b == null || dragging) { previewDelay.Stop(); return; }
        if (b.Window == null)
        {
            HidePreview();
            Rectangle r = buttonRects[zone - WindowZone];
            tip.Show(b.Name, this, r.Left, -Ui.S(34), 3000);
            return;
        }
        if (preview != null && preview.Visible) OpenPreview(zone);
        else { previewDelay.Stop(); previewDelay.Start(); }
    }

    void OpenPreview(int zone)
    {
        BarButton b = ButtonAtZone(zone);
        if (b == null || b.Window == null || zone != hot || dragging) return;
        if (preview == null)
        {
            preview = new WindowPreview();
            preview.ActivateRequested += h => { HidePreview(); WindowList.Activate(h, false); if (windows != null) windows.Refresh(); };
            preview.CloseRequested += h => { HidePreview(); WindowList.Close(h, ReportCloseError); };
        }
        Rectangle r = RectangleToScreen(buttonRects[zone - WindowZone]);
        preview.ShowFor(b.Window.Handle, b.Window.Title, IconOf(b, Ui.S(32)), r);
        previewWatch.Start();
    }

    public void HidePreview()
    {
        previewDelay.Stop();
        previewWatch.Stop();
        tip.Hide(this);
        if (preview != null) preview.HideNow();
    }

    // La souris a quitté les boutons et l'aperçu : il se ferme
    void WatchPreview()
    {
        if (preview == null || !preview.Visible) { previewWatch.Stop(); return; }
        Point p = Cursor.Position;
        if (preview.Bounds.Contains(p)) return;
        Point local = PointToClient(p);
        BarButton b = ClientRectangle.Contains(local) ? ButtonAtZone(WindowZoneAt(local)) : null;
        if (b != null && b.Window != null) return;
        for (int i = 0; i < shown && i < buttons.Count; i++)
        {
            TaskWindow window = buttons[i].Window;
            if (window != null && window.Handle == preview.Target &&
                PreviewGap(RectangleToScreen(buttonRects[i]), preview.Bounds).Contains(p)) return;
        }
        HidePreview();
    }

    internal static Rectangle PreviewGap(Rectangle button, Rectangle previewBounds)
    {
        int left = Math.Max(button.Left, previewBounds.Left);
        int right = Math.Min(button.Right, previewBounds.Right);
        if (left >= right || previewBounds.Bottom >= button.Top) return Rectangle.Empty;
        return Rectangle.FromLTRB(left, previewBounds.Bottom, right, button.Top);
    }

    // La fenêtre montrée a disparu : l'aperçu aussi
    void OnWindowsChanged()
    {
        if (windows != null) ForgetBadges(new HashSet<IntPtr>(windows.Items.Select(w => w.Handle)));
        if (preview == null || preview.Target == IntPtr.Zero || windows == null) return;
        if (!windows.Items.Exists(w => w.Handle == preview.Target)) HidePreview();
    }

    // Vrai si le clic concernait un bouton (ou « +N ») et a été traité
    bool WindowMouseUp(int zone, MouseButtons button)
    {
        if (zone == Overflow) { ShowOverflowMenu(); return true; }
        BarButton b = ButtonAtZone(zone);
        if (b == null) return false;
        HidePreview();
        bool newWindow = button == MouseButtons.Middle || (button == MouseButtons.Left && (ModifierKeys & Keys.Shift) != 0);
        if (newWindow || (button == MouseButtons.Left && b.Window == null)) { Launch(b.Key); return true; }
        if (button == MouseButtons.Left)
        {
            WindowList.Activate(b.Window.Handle, b.Window.Handle == Native.GetForegroundWindow());
            windows.Refresh();
        }
        else if (button == MouseButtons.Right) ShowButtonMenu(b);
        return true;
    }

    static void Launch(string key)
    {
        try { TaskbarModel.Launch(key); } catch { }
    }

    void ShowButtonMenu(BarButton b)
    {
        ContextMenuStrip menu = Menus.Create();
        string title = b.Window != null ? b.Window.Title : b.Name;
        if (title.Length > 60) title = title.Substring(0, 59) + "…";
        menu.Items.Add(new ToolStripMenuItem(title) { Enabled = false });
        menu.Items.Add(new ToolStripSeparator());
        string key = b.Key, name = b.Name;
        if (TaskbarModel.CanLaunch(key))
            menu.Items.Add(Menus.Item(b.Window != null ? L.T("Nouvelle fenêtre", "New window", "Nueva ventana") : L.T("Ouvrir", "Open", "Abrir"),
                Glyphs.Open, () => Launch(key)));
        bool pinned = TaskbarModel.ValidKey(key) && TaskbarModel.IsPinned(Settings.TaskbarPins, key);
        // Épingler seulement ce que Windows sait relancer ; une épingle devenue introuvable peut toujours être détachée
        if (TaskbarModel.ValidKey(key) && (pinned || TaskbarModel.CanLaunch(key)))
        {
            menu.Items.Add(Menus.Item(pinned ? L.T("Détacher de la barre des tâches", "Unpin from taskbar", "Desanclar de la barra de tareas")
                                             : L.T("Épingler à la barre des tâches", "Pin to taskbar", "Anclar a la barra de tareas"),
                pinned ? Glyphs.Pinned : Glyphs.Pin, () => SetPinned(key, name, !pinned)));
        }
        if (b.Window != null)
        {
            IntPtr h = b.Window.Handle;
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Menus.Item(L.T("Fermer la fenêtre", "Close window", "Cerrar la ventana"), Glyphs.Delete, () => WindowList.Close(h, ReportCloseError)));
        }
        ShowMenu(menu);
    }

    // Fermeture refusée par Windows (CDX-10) : le plus souvent, une fenêtre lancée en administrateur
    static void ReportCloseError(int error)
    {
        Dialogs.Warn(null, error == 5
            ? L.T("Cette fenêtre a été ouverte en tant qu'administrateur : BureauCache ne peut pas la fermer. Ferme-la depuis l'application elle-même.",
                  "This window was opened as administrator, so BureauCache can't close it. Close it from the app itself.",
                  "Esta ventana se abrió como administrador, así que BureauCache no puede cerrarla. Ciérrala desde la propia aplicación.")
            : L.T("Windows a refusé de fermer cette fenêtre (erreur " + error + ").", "Windows refused to close this window (error " + error + ").",
                  "Windows se negó a cerrar esta ventana (error " + error + ")."));
    }

    // Épingler ajoute l'application à la fin des épingles ; détacher la retire (ses fenêtres ouvertes restent)
    void SetPinned(string key, string name, bool pin)
    {
        Settings.TaskbarPins = pin ? TaskbarModel.WithPin(Settings.TaskbarPins, key, name) : TaskbarModel.Without(Settings.TaskbarPins, key);
        Settings.SaveQuiet();
        RebuildButtons();
    }

    void ShowOverflowMenu()
    {
        ContextMenuStrip menu = Menus.Create();
        for (int i = shown; i < buttons.Count; i++)
        {
            BarButton b = buttons[i];
            string title = b.Window != null ? b.Window.Title : b.Name;
            if (title.Length > 60) title = title.Substring(0, 59) + "…";
            menu.Items.Add(Menus.Item(title, Glyphs.Apps, () =>
            {
                if (b.Window != null) WindowList.Activate(b.Window.Handle, false); else Launch(b.Key);
            }));
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
