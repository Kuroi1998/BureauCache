// BureauCache — PanelForm (presse-papiers)
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

// Section « Presse-papiers » (D-011) : les textes copiés récemment, en mémoire seulement. Un clic recopie le texte
// et ferme le panneau ; le menu contextuel permet de le retirer de l'historique.
partial class PanelForm
{
    const int ClipShown = 5;

    List<DesktopItem> ClipItems(int px)
    {
        List<DesktopItem> res = new List<DesktopItem>();
        if (!Settings.ClipboardOn) return res;
        foreach (ClipboardHistory.Entry e in ClipboardHistory.Items)
        {
            string text = e.Text;
            res.Add(new DesktopItem
            {
                Kind = ItemKind.Clip, Key = "clip:" + e.Id, Name = ClipboardHistory.Preview(text, 80), Args = e.Id.ToString(),
                Detail = text.Length + L.T(" caractère(s) · copié à ", " character(s) · copied at ", " carácter(es) · copiado a las ") + e.At.ToString("HH:mm"),
                Icon = IconCache.Tile(Glyphs.Copy, Ui.GlyphFont, Settings.Accent, px),
                Run = () => { try { Clipboard.SetText(text); } catch { } }
            });
        }
        return res;
    }

    Group ClipGroup(int px)
    {
        List<DesktopItem> items = ClipItems(px);
        if (items.Count == 0) return null;
        return new Group { Key = "clip", Title = L.T("Presse-papiers", "Clipboard", "Portapapeles"), Glyph = Glyphs.Copy, Items = items.Take(ClipShown).ToList() };
    }

    void RemoveClip(DesktopItem it)
    {
        int id;
        if (it.Args != null && int.TryParse(it.Args, out id)) ClipboardHistory.Remove(id);
    }

    // Appelé par App quand l'historique change : le panneau ouvert se met à jour, le panneau fermé attend sa prochaine ouverture
    public void OnClipboardChanged()
    {
        if (Visible) Rebuild(true);
    }
}
