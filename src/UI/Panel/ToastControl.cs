// BureauCache — ToastControl
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

// =====================================================================
//  Notification en bas du panneau, avec bouton « Annuler »
// =====================================================================
class ToastControl : FlatControl
{
    readonly PanelForm owner;
    string text;
    Action undo, commit;
    DateTime start, until;
    Rectangle rcUndo;
    bool hoverUndo;
    readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 50 };
    readonly Font fText = Ui.Font("Segoe UI", 9.75f);
    readonly Font fBtn = Ui.Font("Segoe UI Semibold", 9.75f);

    public ToastControl(PanelForm owner)
    {
        this.owner = owner;
        Visible = false;
        Cursor = Cursors.Default;
        timer.Tick += (s, e) => { if (DateTime.Now >= until) Flush(); else Invalidate(); };
    }

    public bool CanUndo { get { return Visible && undo != null; } }

    public void Show(string message, Action undoAction, Action commitAction)
    {
        Flush();
        text = message;
        undo = undoAction;
        commit = commitAction;
        start = DateTime.Now;
        until = start.AddSeconds(undoAction != null ? 7 : 3);
        Visible = true;
        BringToFront();
        timer.Start();
        Invalidate();
    }

    // Termine l'action (ex. : envoie vraiment le fichier à la corbeille)
    public void Flush()
    {
        timer.Stop();
        Visible = false;
        undo = null;
        Action c = commit;
        commit = null;
        if (c != null) try { c(); } catch { }
    }

    public void Undo()
    {
        if (undo == null) return;
        Action u = undo;
        undo = null;
        commit = null;
        timer.Stop();
        Visible = false;
        try { u(); } catch (Exception ex) { Dialogs.Info(null, ex.Message); }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        bool h = rcUndo.Contains(e.Location);
        if (h != hoverUndo) { hoverUndo = h; Invalidate(); }
        Cursor = h ? Cursors.Hand : Cursors.Default;
        base.OnMouseMove(e);
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (rcUndo.Contains(e.Location)) Undo();
        else Flush();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        owner.PaintBg(g, ClientRectangle, Location);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
        Ui.Fill(g, Theme.Card, r, Ui.S(10));
        using (GraphicsPath p = Ui.Round(r, Ui.S(10)))
        using (Pen pen = new Pen(Theme.Line)) g.DrawPath(pen, p);
        int right = Width - Ui.S(12);
        rcUndo = Rectangle.Empty;
        if (undo != null)
        {
            string u = L.T("Annuler", "Undo");
            int w = TextRenderer.MeasureText(u, fBtn).Width + Ui.S(16);
            rcUndo = new Rectangle(right - w, (Height - Ui.S(30)) / 2, w, Ui.S(30));
            if (hoverUndo) Ui.Fill(g, Theme.HoverOverlay, rcUndo, Ui.S(6));
            TextRenderer.DrawText(g, u, fBtn, rcUndo, Theme.Accent, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            right = rcUndo.Left - Ui.S(6);
        }
        Ui.Glyph(g, undo != null ? Glyphs.Undo : Glyphs.Check, 10f, Theme.Accent, new Rectangle(Ui.S(8), 0, Ui.S(28), Height));
        TextRenderer.DrawText(g, text, fText, new Rectangle(Ui.S(38), 0, right - Ui.S(38), Height), Theme.Fg,
            TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        double total = (until - start).TotalMilliseconds, left = Math.Max(0, (until - DateTime.Now).TotalMilliseconds);
        int bw = (int)((Width - Ui.S(24)) * left / total);
        Ui.Fill(g, Color.FromArgb(160, Theme.Accent), new Rectangle(Ui.S(12), Height - Ui.S(5), Math.Max(Ui.S(2), bw), Ui.S(2)), Ui.S(1));
    }
}
