// BureauCache — FlatControl, Toggle, Segmented, Slider, Swatches
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

// =====================================================================
//  Contrôles dessinés à la main
// =====================================================================
abstract partial class FlatControl : Control
{
    protected bool SettingsKeyboard { get; private set; }

    public void EnableSettingsKeyboard()
    {
        SettingsKeyboard = true;
        SetStyle(ControlStyles.Selectable, true);
        TabStop = true;
    }

    protected override bool IsInputKey(Keys keyData)
    {
        if (SettingsKeyboard && (keyData == Keys.Left || keyData == Keys.Right || keyData == Keys.Home || keyData == Keys.End)) return true;
        return base.IsInputKey(keyData);
    }

    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected void DrawSettingsFocus(Graphics g)
    {
        if (SettingsKeyboard && Focused) ControlPaint.DrawFocusRectangle(g, Rectangle.Inflate(ClientRectangle, -1, -1),
            SettingsHighContrast ? SystemColors.WindowText : Theme.Fg, BackColor);
    }

    protected bool SettingsHighContrast { get { return SettingsKeyboard && SystemInformation.HighContrast; } }

    protected FlatControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Cursor = Cursors.Hand;
    }
    protected Graphics Prep(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        return g;
    }
}

class Toggle : FlatControl
{
    bool on;
    float pos;
    readonly System.Windows.Forms.Timer anim = new System.Windows.Forms.Timer();
    public event EventHandler Changed;

    public Toggle()
    {
        AccessibleRole = AccessibleRole.CheckButton;
        Size = new Size(Ui.S(44), Ui.S(24));
        TabStop = true;
        anim.Interval = 12;
        anim.Tick += (s, e) =>
        {
            float target = on ? 1f : 0f;
            pos += (target - pos) * 0.45f;
            if (Math.Abs(target - pos) < 0.02f) { pos = target; anim.Stop(); }
            Invalidate();
        };
    }

    protected override AccessibleRole FlatRole { get { return AccessibleRole.CheckButton; } }
    protected override AccessibleStates FlatState { get { return on ? AccessibleStates.Checked : AccessibleStates.None; } }
    protected override string FlatDefaultAction { get { return on ? L.T("Désactiver", "Turn off", "Desactivar") : L.T("Activer", "Turn on", "Activar"); } }

    public bool Checked
    {
        get { return on; }
        set
        {
            bool animate = on != value && Visible && IsHandleCreated && Settings.Animations;
            on = value;
            if (animate) anim.Start();
            else { anim.Stop(); pos = on ? 1 : 0; Invalidate(); }
        }
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        Checked = !Checked;
        if (Changed != null) Changed(this, EventArgs.Empty);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space) { e.SuppressKeyPress = true; OnClick(EventArgs.Empty); }
        // Gauche : désactivé, Droite : activé (les flèches étaient capturées sans effet, Q30-02)
        else if (SettingsKeyboard && (e.KeyCode == Keys.Left || e.KeyCode == Keys.Right))
        {
            e.SuppressKeyPress = true;
            if (Checked != (e.KeyCode == Keys.Right)) OnClick(EventArgs.Empty);
        }
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = Prep(e);
        RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        Color track = SettingsHighContrast ? (on ? SystemColors.Highlight : SystemColors.Control) : Theme.Mix(Theme.Track, Theme.Accent, pos);
        Ui.Fill(g, track, r, r.Height / 2);
        if (SettingsHighContrast)
            using (var path = Ui.Round(r, r.Height / 2))
            using (Pen pen = new Pen(SystemColors.WindowText)) g.DrawPath(pen, path);
        float pad = Ui.S(3);
        float d = r.Height - pad * 2;
        float x = r.X + pad + pos * (r.Width - d - pad * 2);
        Color thumb = SettingsHighContrast ? (on ? SystemColors.HighlightText : SystemColors.ControlText) : Color.White;
        using (SolidBrush b = new SolidBrush(thumb)) g.FillEllipse(b, x, r.Y + pad, d, d);
        DrawSettingsFocus(g);
    }

    protected override void Dispose(bool disposing) { if (disposing) anim.Dispose(); base.Dispose(disposing); }
}

class Segmented : FlatControl
{
    readonly string[] opts;
    int sel, hover = -1;
    public event EventHandler Changed;

    public Segmented(params string[] options)
    {
        opts = options;
        Font = Ui.Font("Segoe UI", 9.5f);
        int w = 0;
        foreach (string o in opts) w = Math.Max(w, TextRenderer.MeasureText(o, Font).Width);
        Size = new Size((w + Ui.S(22)) * opts.Length + Ui.S(6), Ui.S(32));
    }

    protected override AccessibleRole FlatRole { get { return AccessibleRole.ComboBox; } }
    protected override string FlatValue { get { return sel >= 0 && sel < opts.Length ? opts[sel] : null; } }
    protected override string FlatDefaultAction { get { return L.T("Choix suivant", "Next choice", "Opción siguiente"); } }
    protected override void FlatDoDefault()
    {
        sel = (sel + 1) % opts.Length; Invalidate();
        if (Changed != null) Changed(this, EventArgs.Empty);
    }

    public int Selected
    {
        get { return sel; }
        set { sel = Math.Max(0, Math.Min(opts.Length - 1, value)); Invalidate(); }
    }

    float SegW { get { return (Width - Ui.S(6)) / (float)opts.Length; } }
    int IndexAt(int x) { return Math.Max(0, Math.Min(opts.Length - 1, (int)((x - Ui.S(3)) / SegW))); }

    protected override void OnMouseMove(MouseEventArgs e) { int h = IndexAt(e.X); if (h != hover) { hover = h; Invalidate(); } base.OnMouseMove(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = -1; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            int i = IndexAt(e.X);
            if (i != sel) { sel = i; Invalidate(); if (Changed != null) Changed(this, EventArgs.Empty); }
        }
        base.OnMouseDown(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        int before = sel;
        if (e.KeyCode == Keys.Left) Selected = sel - 1;
        else if (e.KeyCode == Keys.Right) Selected = sel + 1;
        else if (e.KeyCode == Keys.Home) Selected = 0;
        else if (e.KeyCode == Keys.End) Selected = opts.Length - 1;
        else { base.OnKeyDown(e); return; }
        e.SuppressKeyPress = true;
        if (before != sel && Changed != null) Changed(this, EventArgs.Empty);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = Prep(e);
        Ui.Fill(g, SettingsHighContrast ? SystemColors.Control : Theme.Surface, new RectangleF(0, 0, Width - 1, Height - 1), Ui.S(8));
        float sw = SegW, pad = Ui.S(3);
        for (int i = 0; i < opts.Length; i++)
        {
            RectangleF r = new RectangleF(pad + i * sw, pad, sw, Height - pad * 2 - 1);
            if (i == sel) Ui.Fill(g, SettingsHighContrast ? SystemColors.Highlight : Theme.Accent, r, Ui.S(6));
            else if (i == hover && !SettingsHighContrast) Ui.Fill(g, Theme.Hover, r, Ui.S(6));
            Color text = SettingsHighContrast ? (i == sel ? SystemColors.HighlightText : SystemColors.ControlText)
                : (i == sel ? (SettingsKeyboard ? SettingsPalette.OnFill(Theme.Accent) : Color.White) : Theme.Fg);
            TextRenderer.DrawText(g, opts[i], Font, Rectangle.Round(r), text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        }
        DrawSettingsFocus(g);
    }
}

class Slider : FlatControl
{
    public int Min, Max, Step = 1;
    int val;
    bool drag;
    public event EventHandler Changed;

    public Slider(int min, int max, int step)
    {
        AccessibleRole = AccessibleRole.Slider;
        Min = min; Max = max; Step = step;
        Size = new Size(Ui.S(170), Ui.S(26));
    }

    protected override AccessibleRole FlatRole { get { return AccessibleRole.Slider; } }
    protected override string FlatValue { get { return val.ToString(); } }

    public int Value
    {
        get { return val; }
        set { val = Math.Max(Min, Math.Min(Max, value)); Invalidate(); }
    }

    float Pad { get { return Math.Min(Ui.S(9), Math.Max(0, (Width - 1) / 2f)); } }

    void SetFromX(int x)
    {
        float t = (x - Pad) / (Width - Pad * 2);
        t = Math.Max(0, Math.Min(1, t));
        int v = Min + (int)Math.Round(t * (Max - Min) / Step) * Step;
        v = Math.Max(Min, Math.Min(Max, v));
        if (v != val) { val = v; Invalidate(); if (Changed != null) Changed(this, EventArgs.Empty); }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left) { drag = true; SetFromX(e.X); }
        base.OnMouseDown(e);
    }
    protected override void OnMouseMove(MouseEventArgs e) { if (drag) SetFromX(e.X); base.OnMouseMove(e); }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left) EndDrag();
        base.OnMouseUp(e);
    }

    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);
        if (!Capture) EndDrag();
    }

    void EndDrag()
    {
        if (!drag) return;
        drag = false;
        Invalidate();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        int before = val;
        if (e.KeyCode == Keys.Left) Value -= Step;
        else if (e.KeyCode == Keys.Right) Value += Step;
        else if (e.KeyCode == Keys.Home) Value = Min;
        else if (e.KeyCode == Keys.End) Value = Max;
        else { base.OnKeyDown(e); return; }
        e.SuppressKeyPress = true;
        if (before != val && Changed != null) Changed(this, EventArgs.Empty);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = Prep(e);
        float cy = Height / 2f, th = Ui.S(4);
        float t = (val - Min) / (float)(Max - Min);
        float kx = Pad + t * (Width - Pad * 2);
        Color track = SettingsHighContrast ? SystemColors.ControlText : Theme.Track;
        Color accent = SettingsHighContrast ? SystemColors.Highlight : Theme.Accent;
        Ui.Fill(g, track, new RectangleF(Pad, cy - th / 2, Width - Pad * 2, th), th / 2);
        Ui.Fill(g, accent, new RectangleF(Pad, cy - th / 2, Math.Max(th, kx - Pad), th), th / 2);
        float kr = Ui.S(8);
        using (SolidBrush b = new SolidBrush(accent)) g.FillEllipse(b, kx - kr, cy - kr, kr * 2, kr * 2);
        float ir = drag ? Ui.S(3) : Ui.S(4);
        using (SolidBrush b = new SolidBrush(SettingsHighContrast ? SystemColors.HighlightText : Color.White)) g.FillEllipse(b, kx - ir, cy - ir, ir * 2, ir * 2);
        DrawSettingsFocus(g);
    }
}

class Swatches : FlatControl
{
    public static readonly Color[] Presets = {
        Color.FromArgb(59, 130, 246), Color.FromArgb(139, 92, 246), Color.FromArgb(236, 72, 153),
        Color.FromArgb(239, 68, 68), Color.FromArgb(249, 115, 22), Color.FromArgb(22, 163, 74),
        Color.FromArgb(13, 148, 136)
    };
    public Color Selected;
    public event EventHandler Changed;
    int D { get { return Math.Min(Ui.S(22), Math.Max(1, (Width - Ui.S(6)) / Presets.Length - Gap)); } }
    int Gap { get { return Math.Min(Ui.S(8), Math.Max(1, Width / (Presets.Length * 3))); } }

    public Swatches()
    {
        Size = new Size(Presets.Length * Ui.S(30) + Ui.S(6), Ui.S(28));
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            for (int i = 0; i < Presets.Length; i++)
            {
                Rectangle bounds = SwatchBounds(i);
                float radius = bounds.Width / 2f;
                float dx = e.X - bounds.X - radius, dy = e.Y - bounds.Y - radius;
                if (dx * dx + dy * dy > radius * radius) continue;
                SelectColor(i);
                break;
            }
        }
        base.OnMouseDown(e);
    }

    Rectangle SwatchBounds(int index)
    {
        return new Rectangle(Ui.S(3) + index * (D + Gap), (Height - D) / 2, D, D);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = Prep(e);
        for (int i = 0; i < Presets.Length; i++)
        {
            Rectangle bounds = SwatchBounds(i);
            using (SolidBrush b = new SolidBrush(Presets[i])) g.FillEllipse(b, bounds);
            if (Presets[i].ToArgb() == Selected.ToArgb())
            {
                using (Pen p = new Pen(SettingsHighContrast ? SystemColors.WindowText : Theme.Fg, Ui.S(2)))
                    g.DrawEllipse(p, Rectangle.Inflate(bounds, Ui.S(2), Ui.S(2)));
                float dot = Math.Min(D / 2f, Ui.S(6));
                using (SolidBrush w = new SolidBrush(SettingsPalette.OnFill(Presets[i])))
                    g.FillEllipse(w, bounds.X + (D - dot) / 2, bounds.Y + (D - dot) / 2, dot, dot);
            }
        }
        DrawSettingsFocus(g);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        int current = Array.FindIndex(Presets, color => color.ToArgb() == Selected.ToArgb());
        int next;
        if (e.KeyCode == Keys.Left) next = Math.Max(0, current - 1);
        else if (e.KeyCode == Keys.Right) next = Math.Min(Presets.Length - 1, current + 1);
        else if (e.KeyCode == Keys.Home) next = 0;
        else if (e.KeyCode == Keys.End) next = Presets.Length - 1;
        else { base.OnKeyDown(e); return; }
        e.SuppressKeyPress = true;
        SelectColor(next);
    }

    void SelectColor(int index)
    {
        Color color = Presets[index];
        if (color.ToArgb() == Selected.ToArgb()) return;
        Selected = color;
        Invalidate();
        if (Changed != null) Changed(this, EventArgs.Empty);
    }
}
