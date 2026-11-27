// BureauCache — ItemsView (noms des tuiles, lignes du presse-papiers)
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

// Refonte, étape 1 (D-012, T-031)
partial class ItemsView
{
    const TextFormatFlags LineFlags = TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;

    // Coupures déjà calculées : le nom et la largeur ne changent pas d'une image à l'autre
    readonly Dictionary<string, string[]> tileLines = new Dictionary<string, string[]>();

    // Nom d'une tuile sur deux lignes au plus. La coupure tombe après une espace, un tiret, un tiret bas ou un point,
    // jamais au milieu d'un mot (« ParalivesPhotoM / ode ») ; ce qui dépasse finit par « … ».
    internal static string[] SplitTileName(string name, int width, Func<string, int> measure)
    {
        if (string.IsNullOrEmpty(name)) return new[] { "" };
        if (measure(name) <= width) return new[] { name };
        int best = -1;
        for (int i = 1; i < name.Length; i++)
        {
            char c = name[i - 1];
            if (c != ' ' && c != '-' && c != '_' && c != '.') continue;
            if (measure(name.Substring(0, i).TrimEnd()) <= width) best = i;
            else break;
        }
        // Pas de coupure possible avant la largeur (un seul long mot) : une ligne, terminée par « … »
        if (best <= 0) return new[] { name };
        string first = name.Substring(0, best).TrimEnd(), rest = name.Substring(best).TrimStart();
        return rest.Length == 0 ? new[] { first } : new[] { first, rest };
    }

    void DrawTileName(Graphics g, string name, Rectangle r, Color fg)
    {
        string key = r.Width + "|" + name;
        string[] lines;
        if (!tileLines.TryGetValue(key, out lines))
        {
            if (tileLines.Count > 2000) tileLines.Clear();
            Font f = fGrid;
            lines = SplitTileName(name, r.Width, t => TextRenderer.MeasureText(t, f, Size.Empty, LineFlags).Width);
            tileLines[key] = lines;
        }
        int lh = fGrid.Height;
        for (int i = 0; i < lines.Length && (i + 1) * lh <= r.Height + S(2); i++)
            TextRenderer.DrawText(g, lines[i], fGrid, new Rectangle(r.X, r.Y + i * lh, r.Width, lh), fg,
                LineFlags | TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis);
    }

    // Une ligne du presse-papiers : icône discrète, aperçu du texte, détail (taille, heure) et, au survol,
    // un bouton « Copier » à droite (un clic n'importe où sur la ligne copie aussi)
    void DrawClipItem(Graphics g, DesktopItem it, Rectangle r, bool selected, bool hot)
    {
        Rectangle bg = new Rectangle(r.X, r.Y + S(2), r.Width, r.Height - S(4));
        Ui.Card(g, selected ? Theme.SelOverlay : hot && !dragging ? Theme.HoverOverlay : Color.FromArgb(Settings.Dark ? 14 : 10, Theme.Fg), bg, S(Ui.RadiusControl), selected || hot);
        if (selected) Ui.Fill(g, Theme.Accent, new RectangleF(bg.X, bg.Y + S(10), S(3), bg.Height - S(20)), S(2));
        Ui.Glyph(g, Glyphs.Copy, 11f, Theme.FgDim, new Rectangle(r.X + S(10), r.Y, S(22), r.Height));
        int tx = r.X + S(40), right = r.Right - S(10);
        if (hot && !dragging)
        {
            string copy = L.T("Copier", "Copy", "Copiar");
            Size cs = TextRenderer.MeasureText(copy, fDetail);
            Rectangle pill = new Rectangle(right - cs.Width - S(12), r.Y + (r.Height - S(24)) / 2, cs.Width + S(12), S(24));
            Ui.Fill(g, Theme.Accent, pill, S(6));
            TextRenderer.DrawText(g, copy, fDetail, pill, SettingsPalette.OnFill(Theme.Accent), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            right = pill.Left - S(8);
        }
        TextRenderer.DrawText(g, it.Name, fItem, new Rectangle(tx, r.Y + S(3), right - tx, r.Height / 2), Theme.Fg,
            TextFormatFlags.Bottom | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        TextRenderer.DrawText(g, it.Detail ?? "", fDetail, new Rectangle(tx, r.Y + r.Height / 2 + S(1), right - tx, r.Height / 2 - S(4)), Theme.FgDim,
            TextFormatFlags.Top | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }
}
