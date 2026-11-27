// BureauCache — panneau des paramètres rapides de la barre des tâches (T-040)
using System;
using System.Drawing;
using System.Windows.Forms;

partial class QuickSettingsForm : Form
{
    readonly Timer refresh = new Timer { Interval = 75 };
    readonly ToolTip mediaTip = new ToolTip();
    readonly IntPtr monitor;
    readonly Label battery, mediaTitle, mediaArtist;
    readonly PictureBox artwork;
    readonly StartTile previous, playback, next, saver;
    readonly QuickLevel volume, brightness;
    bool launching;

    static int Pad { get { return Ui.S(16); } }
    static int Gap { get { return Ui.S(8); } }
    static int PanelWidth { get { return Math.Max(Ui.S(520), 27 * Ui.Font("Segoe UI", 10f).Height); } }
    static int Inner { get { return PanelWidth - 2 * Pad; } }

    public QuickSettingsForm(IntPtr monitor)
    {
        this.monitor = monitor;
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        DoubleBuffered = true;
        BackColor = Theme.Bg;
        Text = L.T("Paramètres rapides", "Quick settings", "Configuración rápida");
        AccessibleName = Text;

        LabelAt(Text, Pad, Pad, Inner, 27, true);
        LabelAt(L.T("Connexions et réglages", "Connections and settings", "Conexiones y ajustes"), Pad, Ui.S(47), Inner, 22, false);
        int row = Ui.S(72), rowHeight = Ui.S(46) + 2 * Ui.Font("Segoe UI", 10f).Height, cell = (Inner - 2 * Gap) / 3;
        AddTile(Glyphs.Wifi, "Wi-Fi", TileBounds(0, row, cell, rowHeight), true, () => ShowConnections(true));
        AddTile(Glyphs.Bluetooth, "Bluetooth", TileBounds(1, row, cell, rowHeight), true, () => ShowConnections(false));
        AddLink(Glyphs.Airplane, L.T("Mode avion", "Airplane mode", "Modo avión"), "ms-settings:network-airplanemode", TileBounds(2, row, cell, rowHeight));
        row += rowHeight + Gap;
        saver = AddLink(Glyphs.Battery, L.T("Énergie", "Power", "Energía"), "ms-settings:powersleep", TileBounds(0, row, cell, rowHeight));
        AddLink(Glyphs.NightLight, L.T("Éclairage nocturne", "Night light", "Luz nocturna"), "ms-settings:nightlight", TileBounds(1, row, cell, rowHeight));
        AddLink(Glyphs.Accessibility, L.T("Accessibilité", "Accessibility", "Accesibilidad"), "ms-settings:easeofaccess", TileBounds(2, row, cell, rowHeight));

        row += rowHeight + Ui.S(15);
        volume = AddLevel(Glyphs.Volume, L.T("Volume", "Volume", "Volumen"), row, true);
        row += Ui.S(60);
        brightness = AddLevel(Glyphs.Brightness, L.T("Luminosité", "Brightness", "Brillo"), row, false);
        row += Ui.S(64);

        artwork = new PictureBox { Bounds = new Rectangle(Pad, row, Ui.S(54), Ui.S(54)), SizeMode = PictureBoxSizeMode.Zoom, BackColor = Theme.Surface };
        artwork.Paint += (s, e) =>
        {
            if (artwork.Image == null) Ui.Glyph(e.Graphics, Glyphs.Music, 19f, Theme.FgDim, artwork.ClientRectangle);
        };
        Controls.Add(artwork);
        mediaTitle = LabelAt(L.T("Lecture multimédia", "Media playback", "Reproducción multimedia"), artwork.Right + Ui.S(10), row + Ui.S(4), Inner - Ui.S(64), 44, true);
        mediaArtist = LabelAt(string.Empty, mediaTitle.Left, mediaTitle.Bottom, mediaTitle.Width, 23, false);
        row = Math.Max(artwork.Bottom, mediaArtist.Bottom) + Gap;
        int mediaButtonHeight = Math.Max(Ui.S(42), 2 * Ui.Font("Segoe UI", 10f).Height + Ui.S(6));
        previous = AddTile(Glyphs.Previous, L.T("Précédent", "Previous", "Anterior"), TileBounds(0, row, cell, mediaButtonHeight), false, () => SendMedia(-1));
        playback = AddTile(Glyphs.Play, L.T("Lecture / Pause", "Play / Pause", "Reproducir / Pausar"), TileBounds(1, row, cell, mediaButtonHeight), false, () => SendMedia(0));
        next = AddTile(Glyphs.Next, L.T("Suivant", "Next", "Siguiente"), TileBounds(2, row, cell, mediaButtonHeight), false, () => SendMedia(1));
        previous.Enabled = playback.Enabled = next.Enabled = false;

        row += mediaButtonHeight + Gap;
        battery = LabelAt(string.Empty, Pad, row, Inner / 2, 36, false);
        AddLink(Glyphs.Settings, L.T("Tous les paramètres", "All settings", "Toda la configuración"), "ms-settings:",
            new Rectangle(battery.Right, row, Inner - battery.Width, Ui.S(36)), null, false);
        ClientSize = new Size(PanelWidth, battery.Bottom + Pad);

        refresh.Tick += (s, e) => RefreshControls();
    }

    protected override CreateParams CreateParams
    {
        get { CreateParams cp = base.CreateParams; cp.ExStyle |= 0x80 /* WS_EX_TOOLWINDOW */; return cp; }
    }

    public void PlaceAbove(Rectangle anchor, Rectangle area)
    {
        Size content = ClientSize;
        Rectangle bounds = PopupBounds(anchor, area, content);
        if (bounds.Size != content)
        {
            AutoScroll = true;
            AutoScrollMinSize = content;
        }
        Bounds = bounds;
    }

    internal static Rectangle PopupBounds(Rectangle anchor, Rectangle area, Size content)
    {
        int bottom = Math.Max(area.Top + 1, Math.Min(area.Bottom, anchor.Top - Gap));
        int height = Math.Min(content.Height, bottom - area.Top);
        int width = Math.Min(area.Width, content.Width + (height < content.Height ? SystemInformation.VerticalScrollBarWidth : 0));
        int left = Math.Max(area.Left, Math.Min(anchor.Right - width, area.Right - width));
        return new Rectangle(left, bottom - height, width, height);
    }

    Label LabelAt(string text, int x, int y, int width, int height, bool emphasis)
    {
        Label label = new Label
        {
            Bounds = new Rectangle(x, y, width, Ui.S(height)),
            Text = text, AutoEllipsis = true, UseMnemonic = false, TextAlign = ContentAlignment.MiddleLeft,
            Font = Ui.Font(emphasis ? "Segoe UI Semibold" : "Segoe UI", emphasis ? 12f : 9.5f),
            ForeColor = emphasis ? Theme.Fg : Theme.FgDim, BackColor = Theme.Bg
        };
        Controls.Add(label);
        return label;
    }

    static Rectangle TileBounds(int col, int y, int w, int h) { return new Rectangle(Pad + col * (w + Gap), y, w, h); }

    StartTile AddTile(string glyph, string text, Rectangle bounds, bool stacked, Action click)
    {
        StartTile tile = new StartTile(glyph, text, stacked) { Bounds = bounds };
        tile.Click += (s, e) => click();
        Controls.Add(tile);
        return tile;
    }

    StartTile AddLink(string glyph, string text, string uri, Rectangle bounds, string fallback = null, bool stacked = true)
    {
        return AddTile(glyph, text, bounds, stacked, () => Launch(uri, fallback));
    }

    void Launch(string uri, string fallback)
    {
        if (Sandbox.Active || launching) return;
        launching = true;
        BeginInvoke((Action)delegate
        {
            Close();
            try { ShellUtil.Open(uri, null); }
            catch (Exception)
            {
                try { ShellUtil.Open(fallback ?? "ms-settings:", null); }
                catch (Exception) { }
            }
        });
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (Sandbox.Active) return;
        Native.SetRoundCorners(Handle);
        RefreshControls();
        refresh.Start();
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        // Une invite Windows ou un changement de focus ne doit pas faire perdre la liste de connexions.
        if (Visible && connectionPage == null) Close();
        else if (connectionPassword != null) connectionPassword.Clear();   // la page reste, pas le secret (CDX-47)
    }

    protected override bool ProcessCmdKey(ref Message message, Keys key)
    {
        if (key == Keys.Escape) { if (connectionPage != null) HideConnections(); else Close(); return true; }
        return base.ProcessCmdKey(ref message, key);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Ui.Edge(e.Graphics, Theme.EdgeHot, new RectangleF(0, 0, ClientSize.Width, ClientSize.Height), Ui.S(Ui.RadiusCard));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !IsDisposed)
        {
            refresh.Dispose();
            CancelConnections();
            mediaTip.Dispose();
            FlushLevel(volume);
            FlushLevel(brightness);
            DisposeMedia();
        }
        base.Dispose(disposing);
    }
}
