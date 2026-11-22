// BureauCache — FlatControl, Toggle, Segmented, Slider, Swatches
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

// =====================================================================
//  Contrôles dessinés à la main
// =====================================================================
abstract class FlatControl : Control
{
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
        if (e.KeyCode == Keys.Space) OnClick(EventArgs.Empty);
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = Prep(e);
        RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        Ui.Fill(g, Theme.Mix(Theme.Track, Theme.Accent, pos), r, r.Height / 2);
        float pad = Ui.S(3);
        float d = r.Height - pad * 2;
        float x = r.X + pad + pos * (r.Width - d - pad * 2);
        using (SolidBrush b = new SolidBrush(Color.White)) g.FillEllipse(b, x, r.Y + pad, d, d);
    }
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
        int i = IndexAt(e.X);
        if (i != sel) { sel = i; Invalidate(); if (Changed != null) Changed(this, EventArgs.Empty); }
        base.OnMouseDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = Prep(e);
        Ui.Fill(g, Theme.Surface, new RectangleF(0, 0, Width - 1, Height - 1), Ui.S(8));
        float sw = SegW, pad = Ui.S(3);
        for (int i = 0; i < opts.Length; i++)
        {
            RectangleF r = new RectangleF(pad + i * sw, pad, sw, Height - pad * 2 - 1);
            if (i == sel) Ui.Fill(g, Theme.Accent, r, Ui.S(6));
            else if (i == hover) Ui.Fill(g, Theme.Hover, r, Ui.S(6));
            TextRenderer.DrawText(g, opts[i], Font, Rectangle.Round(r), i == sel ? Color.White : Theme.Fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
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
        Min = min; Max = max; Step = step;
        Size = new Size(Ui.S(170), Ui.S(26));
    }

    public int Value
    {
        get { return val; }
        set { val = Math.Max(Min, Math.Min(Max, value)); Invalidate(); }
    }

    float Pad { get { return Ui.S(9); } }

    void SetFromX(int x)
    {
        float t = (x - Pad) / (Width - Pad * 2);
        t = Math.Max(0, Math.Min(1, t));
        int v = Min + (int)Math.Round(t * (Max - Min) / Step) * Step;
        v = Math.Max(Min, Math.Min(Max, v));
        if (v != val) { val = v; Invalidate(); if (Changed != null) Changed(this, EventArgs.Empty); }
    }

    protected override void OnMouseDown(MouseEventArgs e) { drag = true; SetFromX(e.X); base.OnMouseDown(e); }
    protected override void OnMouseMove(MouseEventArgs e) { if (drag) SetFromX(e.X); base.OnMouseMove(e); }
    protected override void OnMouseUp(MouseEventArgs e) { drag = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        int v = Math.Max(Min, Math.Min(Max, val + (e.Delta > 0 ? Step : -Step)));
        if (v != val) { val = v; Invalidate(); if (Changed != null) Changed(this, EventArgs.Empty); }
        base.OnMouseWheel(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = Prep(e);
        float cy = Height / 2f, th = Ui.S(4);
        float t = (val - Min) / (float)(Max - Min);
        float kx = Pad + t * (Width - Pad * 2);
        Ui.Fill(g, Theme.Track, new RectangleF(Pad, cy - th / 2, Width - Pad * 2, th), th / 2);
        Ui.Fill(g, Theme.Accent, new RectangleF(Pad, cy - th / 2, Math.Max(th, kx - Pad), th), th / 2);
        float kr = Ui.S(8);
        using (SolidBrush b = new SolidBrush(Theme.Accent)) g.FillEllipse(b, kx - kr, cy - kr, kr * 2, kr * 2);
        float ir = drag ? Ui.S(3) : Ui.S(4);
        using (SolidBrush b = new SolidBrush(Color.White)) g.FillEllipse(b, kx - ir, cy - ir, ir * 2, ir * 2);
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
    int D { get { return Ui.S(22); } }
    int Gap { get { return Ui.S(8); } }

    public Swatches()
    {
        Size = new Size(Presets.Length * (D + Gap) - Gap + Ui.S(6), D + Ui.S(6));
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        int i = (e.X - Ui.S(3)) / (D + Gap);
        if (i >= 0 && i < Presets.Length)
        {
            Selected = Presets[i];
            Invalidate();
            if (Changed != null) Changed(this, EventArgs.Empty);
        }
        base.OnMouseDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = Prep(e);
        for (int i = 0; i < Presets.Length; i++)
        {
            float x = Ui.S(3) + i * (D + Gap), y = Ui.S(3);
            using (SolidBrush b = new SolidBrush(Presets[i])) g.FillEllipse(b, x, y, D, D);
            if (Presets[i].ToArgb() == Selected.ToArgb())
            {
                using (Pen p = new Pen(Theme.Fg, Ui.S(2))) g.DrawEllipse(p, x - Ui.S(2), y - Ui.S(2), D + Ui.S(4), D + Ui.S(4));
                using (SolidBrush w = new SolidBrush(Color.White)) g.FillEllipse(w, x + D / 2f - Ui.S(3), y + D / 2f - Ui.S(3), Ui.S(6), Ui.S(6));
            }
        }
    }
}
