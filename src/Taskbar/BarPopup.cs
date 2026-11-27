// BureauCache — cadre commun des panneaux ouverts depuis la barre : notifications et calendrier (T-042, D-018)
using System;
using System.Drawing;
using System.Windows.Forms;

// Une carte dans un panneau sans bordure, au thème de BureauCache, posé au-dessus de son bouton de la barre comme les
// Paramètres rapides. Se ferme à la perte du focus, avec Échap ou par un second clic sur le bouton.
class BarPopup : Form
{
    protected Rectangle Card;
    Rectangle anchor, area;
    bool placed, launching;

    protected static int Margin2 { get { return Ui.S(10); } }
    protected static int Inset { get { return Ui.S(14); } }
    protected static int Gap { get { return Ui.S(8); } }
    protected static int PanelWidth { get { return Math.Max(Ui.S(400), 23 * Ui.Font("Segoe UI", 10f).Height); } }
    protected static int CardWidth { get { return PanelWidth - 2 * Margin2; } }
    protected static int Inner { get { return CardWidth - 2 * Inset; } }
    protected static int ContentLeft { get { return Margin2 + Inset; } }
    protected static int ButtonHeight { get { return Math.Max(Ui.S(42), 2 * Ui.Font("Segoe UI", 10f).Height + Ui.S(6)); } }
    protected static int IconButton { get { return Math.Max(Ui.S(36), Ui.Font("Segoe UI", 10f).Height + Ui.S(16)); } }

    protected BarPopup(string title)
    {
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        DoubleBuffered = true;
        BackColor = Theme.Bg;
        Text = title;
        AccessibleName = title;
    }

    protected override CreateParams CreateParams
    {
        get { CreateParams cp = base.CreateParams; cp.ExStyle |= 0x80 /* WS_EX_TOOLWINDOW */; return cp; }
    }

    protected Label AddLabel(string text, bool emphasis)
    {
        Label label = new Label
        {
            Text = text, AutoEllipsis = true, UseMnemonic = false, TextAlign = ContentAlignment.MiddleLeft,
            Font = Ui.Font(emphasis ? "Segoe UI Semibold" : "Segoe UI", emphasis ? 11.5f : 9.5f),
            ForeColor = emphasis ? Theme.Fg : Theme.FgDim, BackColor = Theme.Card
        };
        Controls.Add(label);
        return label;
    }

    // Bouton plat ; sans texte, c'est une icône seule nommée par AccessibleName
    protected StartTile AddTile(string glyph, string text, Action click)
    {
        StartTile tile = new StartTile(glyph, text, false) { BackColor = Theme.Card };
        if (text.Length == 0) tile.GlyphOnly = true;
        tile.Click += (s, e) => click();
        Controls.Add(tile);
        return tile;
    }

    // Fixe la carte et la taille du panneau, puis le replace au-dessus de son bouton s'il est déjà posé
    protected void FinishLayout(int cardTop, int cardBottom)
    {
        Card = new Rectangle(Margin2, cardTop, CardWidth, cardBottom - cardTop);
        ClientSize = new Size(PanelWidth, cardBottom + Margin2);
        if (placed) PlaceAbove(anchor, area);
        Invalidate();
    }

    public void PlaceAbove(Rectangle anchor, Rectangle area)
    {
        this.anchor = anchor;
        this.area = area;
        placed = true;
        Size content = ClientSize;
        Rectangle bounds = QuickSettingsForm.PopupBounds(anchor, area, content);
        AutoScroll = bounds.Size != content;
        AutoScrollMinSize = AutoScroll ? content : Size.Empty;
        Bounds = bounds;
    }

    // Lien vers Windows : le panneau se ferme d'abord (null : panneau des notifications de Windows, Windows + N)
    protected void Launch(string uri)
    {
        if (Sandbox.Active || launching) return;
        launching = true;
        BeginInvoke((Action)delegate
        {
            Close();
            try
            {
                if (uri == null) Native.PressWinKey(0x4E);
                else ShellUtil.Open(uri, null);
            }
            catch (Exception) { }
        });
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (!Sandbox.Active) Native.SetRoundCorners(Handle);
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        if (Visible) Close();
    }

    protected override bool ProcessCmdKey(ref Message message, Keys key)
    {
        if (key == Keys.Escape) { Close(); return true; }
        return base.ProcessCmdKey(ref message, key);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Rectangle r = Card;
        r.Offset(AutoScrollPosition);
        Ui.Card(e.Graphics, Theme.Card, r, Ui.S(Ui.RadiusCard), false);
        Ui.Edge(e.Graphics, Theme.EdgeHot, new RectangleF(0, 0, ClientSize.Width, ClientSize.Height), Ui.S(Ui.RadiusCard));
    }
}
