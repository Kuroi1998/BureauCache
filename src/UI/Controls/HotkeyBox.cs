// BureauCache — HotkeyBox
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

class HotkeyBox : FlatControl
{
    public int Mods, Key;
    bool capturing;
    string live;
    public static bool Capturing;
    public event EventHandler Changed;

    public HotkeyBox()
    {
        SetStyle(ControlStyles.Selectable, true);
        TabStop = true;
        Font = Ui.Font("Segoe UI Semibold", 9.75f);
        Size = new Size(Ui.S(210), Ui.S(34));
    }

    public static string Format(int mods, int key)
    {
        if (key == 0) return L.T("Aucun", "None");
        List<string> p = new List<string>();
        if ((mods & 2) != 0) p.Add("Ctrl");
        if ((mods & 1) != 0) p.Add("Alt");
        if ((mods & 4) != 0) p.Add(L.T("Maj", "Shift"));
        if ((mods & 8) != 0) p.Add("Win");
        p.Add(KeyName((Keys)key));
        return string.Join(" + ", p);
    }

    static string ModsOnly(int mods)
    {
        List<string> p = new List<string>();
        if ((mods & 2) != 0) p.Add("Ctrl");
        if ((mods & 1) != 0) p.Add("Alt");
        if ((mods & 4) != 0) p.Add(L.T("Maj", "Shift"));
        if ((mods & 8) != 0) p.Add("Win");
        return string.Join(" + ", p);
    }

    static string KeyName(Keys k)
    {
        if (k >= Keys.D0 && k <= Keys.D9) return ((int)(k - Keys.D0)).ToString();
        if (k >= Keys.NumPad0 && k <= Keys.NumPad9) return L.T("Pavé ", "Num ") + (int)(k - Keys.NumPad0);
        switch (k)
        {
            case Keys.Space: return L.T("Espace", "Space");
            case Keys.Return: return L.T("Entrée", "Enter");
            case Keys.Tab: return "Tab";
            case Keys.Up: return L.T("Haut", "Up");
            case Keys.Down: return L.T("Bas", "Down");
            case Keys.Left: return L.T("Gauche", "Left");
            case Keys.Right: return L.T("Droite", "Right");
            case Keys.Oemtilde: return "²";
        }
        return k.ToString();
    }

    protected override bool IsInputKey(Keys keyData) { return true; }
    protected override void OnGotFocus(EventArgs e) { capturing = true; Capturing = true; live = null; Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { capturing = false; Capturing = false; Invalidate(); base.OnLostFocus(e); }
    protected override void OnMouseDown(MouseEventArgs e) { Focus(); base.OnMouseDown(e); }

    int CurrentMods(KeyEventArgs e)
    {
        int m = 0;
        if (e.Control) m |= 2;
        if (e.Alt) m |= 1;
        if (e.Shift) m |= 4;
        if ((Native.GetKeyState(0x5B) & 0x8000) != 0 || (Native.GetKeyState(0x5C) & 0x8000) != 0) m |= 8;
        return m;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        e.Handled = true;
        e.SuppressKeyPress = true;
        Keys k = e.KeyCode;
        int m = CurrentMods(e);
        if (k == Keys.Escape && m == 0) { Parent.Focus(); return; }
        if (k == Keys.ControlKey || k == Keys.ShiftKey || k == Keys.Menu || k == Keys.LWin || k == Keys.RWin)
        {
            live = m == 0 ? null : ModsOnly(m) + " + …";
            Invalidate();
            return;
        }
        bool fkey = k >= Keys.F1 && k <= Keys.F24;
        if (m == 0 && !fkey) { live = L.T("Ajoute Ctrl, Alt ou Maj", "Add Ctrl, Alt or Shift"); Invalidate(); return; }
        Mods = m;
        Key = (int)k;
        live = null;
        if (Changed != null) Changed(this, EventArgs.Empty);
        Parent.Focus();
    }

    protected override void WndProc(ref Message m)
    {
        if (capturing && (m.Msg == 0x105 || m.Msg == 0x106)) return;
        base.WndProc(ref m);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = Prep(e);
        RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        Ui.Fill(g, Theme.Surface, r, Ui.S(8));
        if (capturing)
            using (GraphicsPath p = Ui.Round(r, Ui.S(8)))
            using (Pen pen = new Pen(Theme.Accent, Ui.S(2))) g.DrawPath(pen, p);
        string txt = capturing ? (live ?? L.T("Appuie sur tes touches…", "Press your keys…")) : Format(Mods, Key);
        TextRenderer.DrawText(g, txt, Font, ClientRectangle, capturing && live == null ? Theme.FgDim : Theme.Fg,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }
}
