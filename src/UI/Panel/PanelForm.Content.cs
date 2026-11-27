// BureauCache — PanelForm (recherche dans le contenu)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// Groupe « Dans le contenu » (D-010) : 300 ms après la dernière frappe, l'index Windows est interrogé sur un fil
// d'arrière-plan. Un ticket écarte les réponses arrivées après une nouvelle frappe, et les fichiers déjà
// trouvés par leur nom ne sont pas répétés.
partial class PanelForm
{
    const int ContentShown = 8;
    readonly System.Windows.Forms.Timer contentTimer = new System.Windows.Forms.Timer { Interval = 300 };
    bool contentHooked;
    string contentAsked, contentFor;
    List<DesktopItem> contentItems = new List<DesktopItem>();
    int contentTicket;

    Group ContentGroup(string q, List<Group> shown)
    {
        if (!Settings.SearchContent || !ContentSearch.Applies(q) || shown.Any(g => g.Key == "s:calc"))
        {
            CancelContentSearch();
            return null;
        }
        if (!contentHooked)
        {
            contentHooked = true;
            contentTimer.Tick += (s, e) => StartContentSearch();
            Disposed += (s, e) => contentTimer.Dispose();
        }
        // Nouveau texte : la recherche en cours ne pourra plus remplir le cache (ticket avancé tout de suite)
        if (q != contentAsked) { contentAsked = q; contentTicket++; contentTimer.Stop(); contentTimer.Start(); }
        if (q != contentFor || contentItems.Count == 0) return null;

        HashSet<string> already = new HashSet<string>(shown.SelectMany(g => g.Items).Select(i => i.Key ?? ""), StringComparer.OrdinalIgnoreCase);
        Dictionary<string, DesktopItem> byKey = new Dictionary<string, DesktopItem>(StringComparer.OrdinalIgnoreCase);
        foreach (DesktopItem it in All) byKey[it.Key] = it;
        Group g2 = new Group { Key = "s:content", Title = L.T("Dans le contenu", "In file contents", "En el contenido"), Glyph = Glyphs.Page };
        foreach (DesktopItem hit in contentItems)
        {
            if (already.Contains(hit.Key)) continue;
            DesktopItem known;
            g2.Items.Add(byKey.TryGetValue(hit.Key, out known) ? known : hit);
            if (g2.Items.Count >= ContentShown) break;
        }
        return g2.Items.Count > 0 ? g2 : null;
    }

    // Appelé à chaque reconstruction dont le texte ne mène pas à une recherche dans le contenu (vide, = ? >) :
    // le minuteur est arrêté et toute réponse en vol est écartée (QA42-02)
    void CancelContentSearch()
    {
        contentTimer.Stop();
        if (contentAsked != null) contentTicket++;
        contentAsked = null;
    }

    void StartContentSearch()
    {
        contentTimer.Stop();
        string q = contentAsked;
        if (q == null) return;
        int ticket = ++contentTicket, px = ListPx;
        Background.Run(() =>
        {
            List<DesktopItem> items = new List<DesktopItem>();
            try
            {
                foreach (string p in ContentSearch.Find(q, ContentSearch.MaxResults))
                    items.Add(new DesktopItem
                    {
                        Kind = ItemKind.Recent, Key = p, Name = Path.GetFileName(p), Target = p,
                        Detail = Path.GetDirectoryName(p), Icon = IconCache.ForFile(p, px)
                    });
            }
            catch { }
            Post(() =>
            {
                if (ticket != contentTicket || IsDisposed) return;
                contentFor = q;
                contentItems = items;
                if (items.Count > 0 && search.Text.Trim() == q) Rebuild(true);
            });
        }, true);
    }
}
