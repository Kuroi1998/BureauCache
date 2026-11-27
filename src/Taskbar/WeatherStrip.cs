// BureauCache — météo du panneau de l'horloge (T-042) : la même que la tuile du panneau latéral
using System;
using System.Drawing;
using System.Windows.Forms;

// Icône, température, description, lieu et minimale / maximale du jour. Clic : détails de la météo, ou choix de la
// ville quand elle n'est pas encore réglée. Les données viennent de Weather (Open-Meteo, déjà utilisé par le panneau).
class WeatherStrip : Control
{
    bool hover;

    public WeatherStrip()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                 ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        BackColor = Theme.Card;
        Cursor = Cursors.Hand;
        TabStop = true;
        AccessibleRole = AccessibleRole.PushButton;
        UpdateName();
    }

    static Font Big { get { return Ui.Font("Segoe UI Semibold", 15f); } }
    static Font Small { get { return Ui.Font("Segoe UI", 9.5f); } }
    public static int PreferredHeight { get { return Math.Max(Ui.S(58), 2 * Small.Height + Ui.S(18)); } }

    // Texte affiché quand il n'y a pas de relevé, ou null
    internal static string Message()
    {
        if (!Weather.Configured) return L.T("Météo : choisir une ville…", "Weather: choose a city…", "Tiempo: elige una ciudad…");
        if (Weather.Current != null) return null;
        return Weather.Failed ? L.T("Météo indisponible (nouvel essai dans 5 min)", "Weather unavailable (retry in 5 min)", "Tiempo no disponible (reintento en 5 min)")
            : L.T("Météo : chargement…", "Weather: loading…", "Cargando el tiempo…");
    }

    static string Place()
    {
        return string.IsNullOrEmpty(Settings.WeatherShort) ? Weather.ShortPlace(Settings.WeatherPlace) : Settings.WeatherShort;
    }

    public void UpdateName()
    {
        WeatherReport w = Weather.Current;
        string message = Message();
        AccessibleName = message ?? (L.T("Météo", "Weather", "Tiempo") + " : " + Weather.Temp(w.Temp) + ", " + Weather.Describe(w.Code) + ", " + Place());
        Invalidate();
    }

    // Relevé ancien de plus de 30 min : nouvelle lecture en arrière-plan, l'affichage suit à son arrivée
    public void RefreshIfDue()
    {
        if (Sandbox.Active || !Weather.Due) return;
        Weather.RefreshAsync(() =>
        {
            try { if (IsHandleCreated && !IsDisposed) BeginInvoke(new Action(UpdateName)); }
            catch (InvalidOperationException) { }   // panneau fermé entre-temps
        });
        UpdateName();
    }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = false; Invalidate(); }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

    protected override bool IsInputKey(Keys keyData) { return keyData == Keys.Enter || keyData == Keys.Space || base.IsInputKey(keyData); }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) { e.Handled = e.SuppressKeyPress = true; OnClick(EventArgs.Empty); return; }
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(BackColor);
        Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
        Ui.Fill(g, hover || Focused ? Theme.Hover : Theme.Surface, r, Ui.S(Ui.RadiusControl));
        if (Focused) Ui.Edge(g, Theme.Accent, r, Ui.S(Ui.RadiusControl));
        TextFormatFlags left = TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine;
        WeatherReport w = Weather.Current;
        string message = Message();
        if (message != null)
        {
            Ui.Glyph(g, Weather.Configured ? Glyphs.Globe : Glyphs.Search, 12f, Theme.FgDim, new Rectangle(Ui.S(8), 0, Ui.S(36), Height));
            TextRenderer.DrawText(g, message, Small, new Rectangle(Ui.S(48), 0, Width - Ui.S(56), Height), Theme.FgDim, left);
            return;
        }
        int icon = Math.Min(Height - Ui.S(12), Ui.S(44));
        WeatherIcons.Draw(g, w.Code, w.IsDay, new RectangleF(Ui.S(8), (Height - icon) / 2f, icon, icon));
        string temp = Weather.Temp(w.Temp);
        int x = Ui.S(14) + icon, tw = TextRenderer.MeasureText(temp, Big).Width;
        TextRenderer.DrawText(g, temp, Big, new Rectangle(x, 0, tw + Ui.S(4), Height), Theme.Fg, left);
        x += tw + Ui.S(8);
        string range = w.Days.Count > 0 ? Weather.Temp(w.Days[0].Min) + " / " + Weather.Temp(w.Days[0].Max) : string.Empty;
        int rw = range.Length > 0 ? TextRenderer.MeasureText(range, Small).Width + Ui.S(12) : 0;
        int half = Height / 2;
        TextRenderer.DrawText(g, Weather.Describe(w.Code), Small, new Rectangle(x, half - Small.Height - Ui.S(1), Width - x - rw, Small.Height + Ui.S(2)), Theme.Fg, left);
        TextRenderer.DrawText(g, Place(), Small, new Rectangle(x, half + Ui.S(1), Width - x - rw, Small.Height + Ui.S(2)), Theme.FgDim, left);
        if (rw > 0)
            TextRenderer.DrawText(g, range, Small, new Rectangle(Width - rw, 0, rw - Ui.S(8), Height), Theme.FgDim,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.NoPrefix);
    }
}
