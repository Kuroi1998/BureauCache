// BureauCache — PanelForm
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;

// =====================================================================
//  Panneau latéral
// =====================================================================
partial class PanelForm : Form
{
    public readonly ItemsView View;
    readonly TextBox search;
    readonly PreviewForm preview = new PreviewForm();
    readonly Font fTitle = Ui.Font("Segoe UI Semibold", 13f);
    readonly Font fCount = Ui.Font("Segoe UI", 10f);
    readonly Font fClock = Ui.Font("Segoe UI Semibold", 12f);
    readonly Font fSmall = Ui.Font("Segoe UI", 8.25f);
    public List<DesktopItem> All = new List<DesktopItem>(), Apps = new List<DesktopItem>(), Recent = new List<DesktopItem>();
    List<DesktopItem> winSettings = new List<DesktopItem>(), commands = new List<DesktopItem>();
    int catalogPx, catalogLanguage, catalogAccent;
    bool catalogHidesIcons;
    public volatile bool Dirty = true;
    public bool Busy;
    public event EventHandler SettingsRequested;
    Bitmap glass;
    Rectangle rcGear, rcViewBtn, rcPin, rcSearch, rcClock, rcDisk, rcBin;
    int hoverBtn = -1;
    readonly ToastControl toast;
    bool resizing, hoverGrip;
    int resizeStartX, resizeStartW;
    bool appbar;
    Screen pinScreen;
    readonly uint abMsg = Native.RegisterWindowMessage("BureauCacheAppBar");
    readonly ContextMenuStrip menu;
    bool showingPanelPopup;
    bool loadedSystem, loadedApps, loadedRecents;
    int loadedPx;
    bool loading, loadPending, loadedOnce;

    // widgets
    readonly System.Windows.Forms.Timer widgetTimer = new System.Windows.Forms.Timer();
    readonly System.Windows.Forms.Timer blurTimer = new System.Windows.Forms.Timer();
    long diskFree, diskTotal, binCount;
    int widgetTicks;

    // animation
    readonly System.Windows.Forms.Timer anim = new System.Windows.Forms.Timer();
    readonly Stopwatch sw = new Stopwatch();
    bool opening;
    int fromLeft, toLeft, openLeft;
    double fromOp, toOp;

    static int S(int v) { return Ui.S(v); }
    public int IconPx { get { return S(Settings.GridView ? 48 : 32); } }
    public bool IsOpen { get { return Visible && opening; } }
    int ListPx { get { return S(32); } }

    public PanelForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        KeyPreview = true;
        AutoScaleMode = AutoScaleMode.None;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

        search = new TextBox { BorderStyle = BorderStyle.None, Font = Ui.Font("Segoe UI", 10.5f), MaxLength = 256 };   // SEC-03
        search.TextChanged += (s, e) =>
        {
            bool chips = ChipsVisible;
            if (chips != chipsShown) { chipsShown = chips; LayoutChrome(); }
            Rebuild(false); if (search.Text.Trim().Length > 0) View.SelectFirst(); Invalidate(rcSearch);
        };
        search.KeyDown += SearchKeyDown;
        search.HandleCreated += (s, e) => SetCue();

        View = new ItemsView(this);
        View.ItemActivated += it => Launch(it, null);
        View.ItemContext += ShowItemMenu;
        View.ItemDropped += HandleDrop;
        View.FilesDropped += AddFilesToDesktop;
        View.HoverItem += (it, r) =>
        {
            if (!IsOpen || !Settings.HoverPreview || Busy || MenuOpen) return;
            if (it.Kind == ItemKind.Desktop && it.IsSystem) return;
            if (it.Kind != ItemKind.Desktop && it.Kind != ItemKind.Recent && it.Kind != ItemKind.App) return;
            showingPanelPopup = true;
            try { preview.ShowFor(this, it, r, Settings.RightSide); }
            finally { showingPanelPopup = false; }
        };
        View.HoverEnd += () => preview.HidePreview();

        toast = new ToastControl(this);
        Controls.Add(toast);
        Controls.Add(View);
        Controls.Add(search);

        menu = Menus.Create();
        menu.Opening += (s, e) => View.CancelHover();

        anim.Interval = 10;
        anim.Tick += AnimTick;
        widgetTimer.Interval = 1000;
        widgetTimer.Tick += (s, e) => { if (++widgetTicks % 15 == 0) RefreshWidgetData(); Invalidate(rcClock); };
        blurTimer.Interval = 120;
        blurTimer.Tick += (s, e) => RefreshGlass();
        RefreshCatalog();
        ApplyTheme();
    }

    void SetCue()
    {
        Native.SendMessage(search.Handle, 0x1501, (IntPtr)1, L.T("Rechercher partout…   (F1 : aide)", "Search everything…   (F1: help)", "Buscar en todo…   (F1: ayuda)"));
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= 0x80;
            cp.ClassStyle |= 0x20000;
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Native.SetRoundCorners(Handle);
        appliedAffinity = uint.MaxValue;   // nouveau handle : aucune affinité encore donnée à Windows
        ApplyCaptureSettings();
    }

    public void RefreshCatalog()
    {
        int px = ListPx, language = L.CurrentLanguage, accent = Settings.Accent.ToArgb();
        bool hidesIcons = Settings.HideIcons;
        bool refreshNames = catalogPx != px || catalogLanguage != language;
        if (refreshNames) winSettings = SystemCatalog.WindowsSettings(px);
        if (refreshNames || catalogAccent != accent || catalogHidesIcons != hidesIcons)
            commands = SystemCatalog.Commands(px, () => { if (SettingsRequested != null) SettingsRequested(this, EventArgs.Empty); });
        if (catalogLanguage != language && search.IsHandleCreated) SetCue();
        catalogPx = px;
        catalogLanguage = language;
        catalogAccent = accent;
        catalogHidesIcons = hidesIcons;
    }

    public void ApplyTheme()
    {
        search.BackColor = Theme.Surface;
        search.ForeColor = Theme.Fg;
        BackColor = Theme.Bg;
        LayoutChrome();
        Invalidate(true);
    }

    // Bande de redimensionnement le long du bord intérieur
    int Grip { get { return S(6); } }
    int X0 { get { return Settings.RightSide ? Grip : 0; } }
    int CW { get { return Width - Grip; } }
    bool InGrip(Point p) { return Settings.RightSide ? p.X < Grip : p.X >= Width - Grip; }

    void LayoutChrome()
    {
        int x0 = X0, cw = CW;
        rcGear = new Rectangle(x0 + cw - S(12) - S(34), S(12), S(34), S(34));
        rcViewBtn = new Rectangle(rcGear.X - S(38), S(12), S(34), S(34));
        rcPin = new Rectangle(rcViewBtn.X - S(38), S(12), S(34), S(34));
        toast.Bounds = new Rectangle(x0 + S(10), Height - S(64), cw - S(20), S(52));
        rcSearch = new Rectangle(x0 + S(12), S(58), cw - S(24), S(38));
        int th = search.PreferredHeight;
        // Les pastilles ne prennent de la place que champ vide : dès qu'on tape, le champ récupère la largeur (QA36-01)
        search.Bounds = new Rectangle(rcSearch.X + S(36), rcSearch.Y + (rcSearch.Height - th) / 2 + S(1), rcSearch.Width - S(46) - (ChipsVisible ? ChipsWidth : 0), th);
        LayoutChips();
        int top = S(106);
        if (Settings.Widgets)
        {
            int gap = S(8), w = (cw - S(24) - gap * 2) / 3, y = S(104);
            rcClock = new Rectangle(x0 + S(12), y, w, S(46));
            rcDisk = new Rectangle(rcClock.Right + gap, y, w, S(46));
            rcBin = new Rectangle(rcDisk.Right + gap, y, cw - S(12) - (rcDisk.Right + gap - x0), S(46));
            top = S(158);
        }
        else rcClock = rcDisk = rcBin = Rectangle.Empty;
        top = LayoutWeather(x0, cw, top);
        View.Bounds = new Rectangle(x0, top, cw, Math.Max(0, Height - top));
    }

    protected override void OnResize(EventArgs e) { base.OnResize(e); LayoutChrome(); }

    public void PaintBg(Graphics g, Rectangle area, Point offset)
    {
        if (glass != null && ClientSize.Width > 0 && ClientSize.Height > 0)
        {
            // En capture visible, conserve l'image prise à l'ouverture pendant un redimensionnement.
            float sx = (float)glass.Width / ClientSize.Width, sy = (float)glass.Height / ClientSize.Height;
            var source = new RectangleF((offset.X + area.X) * sx, (offset.Y + area.Y) * sy, area.Width * sx, area.Height * sy);
            g.DrawImage(glass, area, source, GraphicsUnit.Pixel);
        }
        else
            using (SolidBrush b = new SolidBrush(Theme.Bg)) g.FillRectangle(b, area);
    }

    protected override void OnPaintBackground(PaintEventArgs e) { PaintBg(e.Graphics, ClientRectangle, Point.Empty); }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        int x0 = X0;
        string title = L.T("Bureau", "Desktop", "Escritorio");
        TextRenderer.DrawText(g, title, fTitle, new Point(x0 + S(16), S(16)), Theme.Fg, TextFormatFlags.NoPrefix);
        int tw = TextRenderer.MeasureText(title, fTitle).Width;
        string count = loading && !loadedOnce ? L.T("Chargement…", "Loading…", "Cargando…") : All.Count + L.T(" éléments", " items", " elementos");
        Rectangle countRect = new Rectangle(x0 + S(16) + tw, S(14), rcPin.X - S(4) - (x0 + S(16) + tw), S(30));
        if (countRect.Width > S(30))
            TextRenderer.DrawText(g, count, fCount, countRect, Theme.FgDim, TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        if (hoverBtn == 0) Ui.Fill(g, Theme.HoverOverlay, rcViewBtn, S(8));
        if (hoverBtn == 1) Ui.Fill(g, Theme.HoverOverlay, rcGear, S(8));
        if (hoverBtn == 5 || Settings.Pinned) Ui.Fill(g, Settings.Pinned ? Theme.SelOverlay : Theme.HoverOverlay, rcPin, S(8));
        Ui.Glyph(g, Settings.Pinned ? Glyphs.Pinned : Glyphs.Pin, 11f, hoverBtn == 5 || Settings.Pinned ? Theme.Accent : Theme.FgDim, rcPin);
        Ui.Glyph(g, Settings.GridView ? Glyphs.List : Glyphs.Grid, 11f, hoverBtn == 0 ? Theme.Accent : Theme.FgDim, rcViewBtn);
        Ui.Glyph(g, Glyphs.Settings, 11f, hoverBtn == 1 ? Theme.Accent : Theme.FgDim, rcGear);

        Ui.Fill(g, Theme.Surface, rcSearch, S(8));
        Ui.Glyph(g, Glyphs.Search, 10f, Theme.FgDim, new Rectangle(rcSearch.X + S(6), rcSearch.Y, S(28), rcSearch.Height));
        DrawChips(g);

        if (Settings.Widgets) { DrawWidgets(g); DrawTileMenuHints(g); }
        if (Settings.WeatherOn) DrawWeather(g);
        DrawChromeFocus(g);   // en dernier : le cadre de focus passe au-dessus des tuiles

        int bw = hoverGrip || resizing ? S(4) : S(2);
        using (SolidBrush b = new SolidBrush(Theme.Accent))
            g.FillRectangle(b, Settings.RightSide ? 0 : Width - bw, 0, bw, Height);
        if (hoverGrip || resizing)
        {
            Rectangle h = new Rectangle(Settings.RightSide ? S(1) : Width - S(5), Height / 2 - S(24), S(4), S(48));
            Ui.Fill(g, Color.White, h, S(2));
        }
    }

    void DrawWidgets(Graphics g)
    {
        Rectangle[] rs = { rcClock, rcDisk, rcBin };
        for (int i = 0; i < 3; i++) Ui.Fill(g, hoverBtn == i + 2 && i > 0 ? Theme.SelOverlay : Theme.HoverOverlay, rs[i], S(8));
        DateTime now = DateTime.Now;
        TextRenderer.DrawText(g, now.ToString("HH:mm", L.Culture), fClock, new Rectangle(rcClock.X, rcClock.Y + S(3), rcClock.Width, S(24)), Theme.Fg,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        TextRenderer.DrawText(g, now.ToString("ddd d MMM", L.Culture), fSmall, new Rectangle(rcClock.X, rcClock.Y + S(25), rcClock.Width, S(18)), Theme.FgDim,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);

        Ui.Glyph(g, Glyphs.Drive, 9f, Theme.FgDim, new Rectangle(rcDisk.X + S(6), rcDisk.Y + S(4), S(18), S(20)));
        TextRenderer.DrawText(g, diskTotal > 0 ? L.Size(diskFree) : "—", fSmall, new Rectangle(rcDisk.X + S(24), rcDisk.Y + S(4), rcDisk.Width - S(28), S(20)), Theme.Fg,
            TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        Rectangle bar = new Rectangle(rcDisk.X + S(8), rcDisk.Y + S(28), rcDisk.Width - S(16), S(5));
        Ui.Fill(g, Theme.Track, bar, S(2));
        if (diskTotal > 0)
        {
            float used = 1f - diskFree / (float)diskTotal;
            Color c = used > 0.9f ? Theme.Danger : Theme.Accent;
            Ui.Fill(g, c, new RectangleF(bar.X, bar.Y, Math.Max(S(4), bar.Width * used), bar.Height), S(2));
        }
        TextRenderer.DrawText(g, L.T("libres", "free", "libre"), fSmall, new Rectangle(rcDisk.X, rcDisk.Y + S(32), rcDisk.Width, S(14)), Theme.FgDim,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix);

        Ui.Glyph(g, Glyphs.Delete, 9f, Theme.FgDim, new Rectangle(rcBin.X + S(6), rcBin.Y + S(4), S(18), S(20)));
        string bt = binCount == 0 ? L.T("vide", "empty", "vacía") : binCount.ToString();
        TextRenderer.DrawText(g, bt, fSmall, new Rectangle(rcBin.X + S(24), rcBin.Y + S(4), rcBin.Width - S(28), S(20)), Theme.Fg,
            TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(g, L.T("corbeille", "bin", "papelera"), fSmall, new Rectangle(rcBin.X, rcBin.Y + S(26), rcBin.Width, S(18)), Theme.FgDim,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
    }

    void UpdateWidgetTimer(bool open)
    {
        bool enabled = open && (Settings.Widgets || Settings.WeatherOn);
        if (enabled && !widgetTimer.Enabled)
        {
            widgetTicks = 0;
            RefreshWidgetData();
        }
        widgetTimer.Enabled = enabled;
    }

    void RefreshWidgetData()
    {
        try
        {
            DriveInfo d = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory));
            diskFree = d.AvailableFreeSpace;
            diskTotal = d.TotalSize;
        }
        catch { }
        long size;
        Native.RecycleBinInfo(out binCount, out size);
        if (Visible) Invalidate(Rectangle.Union(rcDisk, rcBin));
        RefreshWeatherIfDue();
    }

    int HitButton(Point p)
    {
        int chip = HitChip(p);
        if (chip >= 0) return chip;
        if (rcViewBtn.Contains(p)) return 0;
        if (rcGear.Contains(p)) return 1;
        if (rcPin.Contains(p)) return 5;
        if (Settings.Widgets)
        {
            if (rcClock.Contains(p)) return 2;
            if (rcDisk.Contains(p)) return 3;
            if (rcBin.Contains(p)) return 4;
        }
        if (Settings.WeatherOn && rcWeather.Contains(p)) return 6;
        return -1;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left && InGrip(e.Location))
        {
            resizing = true;
            Busy = true;
            resizeStartX = Cursor.Position.X;
            resizeStartW = Width;
            preview.HidePreview();
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (resizing)
        {
            int dx = Cursor.Position.X - resizeStartX;
            int w = Math.Max(S(260), Math.Min(S(560), resizeStartW + (Settings.RightSide ? -dx : dx)));
            if (w != Width)
            {
                int right = Right;
                Width = w;
                if (Settings.RightSide) { Left = right - w; openLeft = Left; }
                Invalidate(true);
            }
            return;
        }
        bool g = InGrip(e.Location);
        if (g != hoverGrip) { hoverGrip = g; Invalidate(); }
        int h = g ? -1 : HitButton(e.Location);
        if (h != hoverBtn) { hoverBtn = h; Invalidate(); }
        UpdateTip(h);
        Cursor = g ? Cursors.SizeWE : h >= 0 && h != 2 ? Cursors.Hand : rcSearch.Contains(e.Location) ? Cursors.IBeam : Cursors.Default;
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        FinishResize();
    }

    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);
        if (!Capture) FinishResize();
    }

    void FinishResize()
    {
        if (!resizing) return;
        resizing = false;
        Busy = false;
        ApplyWidth((int)Math.Round(Width / Ui.Scale / 10.0) * 10);
        // Le glissement a déjà fixé Width : si l'arrondi tombe juste, ApplyPanelGeometry ne voit aucun
        // changement et la réservation du panneau épinglé garderait l'ancienne largeur (R-27)
        AppBarSetPos();
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        if (InGrip(e.Location)) ApplyWidth(340);
    }
}
