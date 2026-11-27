// BureauCache — PanelForm (indices : infobulles, pastilles de recherche, menus des tuiles)
using System;
using System.Drawing;
using System.Windows.Forms;

// T-022 (Gemini B1, B2, B3) : rendre visibles des fonctions qu'on ne découvrait que par hasard.
partial class PanelForm
{
    // Pastilles du champ de recherche : = calcul, ? web, > commandes système. Elles n'apparaissent que champ vide.
    static readonly string[] ChipPrefixes = { "=", "?", ">" };
    static readonly string[] ChipGlyphs = { Glyphs.Calculator, Glyphs.Help, Glyphs.Terminal };
    const int ChipFirst = 7;   // codes de HitButton : 7, 8 et 9
    readonly Rectangle[] rcChips = new Rectangle[3];
    ToolTip tips;
    bool chipsShown = true;
    int tipFor = -1;

    int ChipsWidth { get { return S(26) * 3 + S(4) * 2 + S(6); } }

    void LayoutChips()
    {
        int w = S(26), h = S(24), gap = S(4);
        int x = rcSearch.Right - S(6) - (w * 3 + gap * 2), y = rcSearch.Y + (rcSearch.Height - h) / 2;
        for (int i = 0; i < 3; i++) rcChips[i] = new Rectangle(x + i * (w + gap), y, w, h);
    }

    bool ChipsVisible { get { return search.Text.Length == 0; } }

    int HitChip(Point p)
    {
        if (!ChipsVisible) return -1;
        for (int i = 0; i < 3; i++) if (rcChips[i].Contains(p)) return ChipFirst + i;
        return -1;
    }

    void DrawChips(Graphics g)
    {
        if (!ChipsVisible) return;
        for (int i = 0; i < 3; i++)
        {
            bool hot = hoverBtn == ChipFirst + i;
            // Icônes au lieu des caractères = ? > : calculatrice, aide, commande ; l'infobulle donne le préfixe (D-012)
            Ui.Card(g, hot ? Theme.SelOverlay : Theme.HoverOverlay, rcChips[i], S(6), hot);
            Ui.Glyph(g, ChipGlyphs[i], 9f, hot ? Theme.Accent : Theme.FgDim, rcChips[i]);
        }
    }

    // Un clic sur une pastille écrit son préfixe et place le curseur derrière, prêt pour la suite
    void ClickChip(int code)
    {
        string prefix = ChipPrefixes[code - ChipFirst];
        search.Text = prefix == "=" ? "=" : prefix + " ";
        search.Focus();
        search.SelectionStart = search.Text.Length;
    }

    // Petit « … » sur les tuiles qui ont un menu au clic droit (disque : nettoyage rapide ; corbeille : vider)
    void DrawTileMenuHints(Graphics g)
    {
        if (!Settings.Widgets) return;
        if (hoverBtn == 3) Ui.Glyph(g, Glyphs.More, 8f, Theme.FgDim, new Rectangle(rcDisk.Right - S(18), rcDisk.Y + S(2), S(16), S(14)));
        if (hoverBtn == 4) Ui.Glyph(g, Glyphs.More, 8f, Theme.FgDim, new Rectangle(rcBin.Right - S(18), rcBin.Y + S(2), S(16), S(14)));
    }

    string TipText(int h)
    {
        switch (h)
        {
            case 0: return Settings.GridView ? L.T("Afficher en liste", "Show as a list", "Ver como lista") : L.T("Afficher en grille", "Show as a grid", "Ver como cuadrícula");
            case 1: return L.T("Paramètres", "Settings", "Configuración");
            case 5: return Settings.Pinned ? L.T("Détacher le panneau", "Unpin the panel", "Desfijar el panel")
                                           : L.T("Épingler : le panneau reste ouvert et les fenêtres lui laissent la place", "Pin: the panel stays open and windows make room for it", "Fijar: el panel permanece abierto y las ventanas le dejan espacio");
            case 2: return DateTime.Now.ToString("D", L.Culture);
            case 3: return L.T("Clic : Ce PC · Clic droit : nettoyage rapide", "Click: This PC · Right-click: quick clean-up", "Clic: Este equipo · Clic derecho: limpieza rápida");
            case 4: return L.T("Clic : ouvrir la corbeille · Clic droit : la vider", "Click: open the Recycle Bin · Right-click: empty it", "Clic: abrir la papelera · Clic derecho: vaciarla");
            case 6: return L.T("Clic : prévisions sur 3 jours · Clic droit : actualiser ou changer de lieu", "Click: 3-day forecast · Right-click: refresh or change place", "Clic: previsión de 3 días · Clic derecho: actualizar o cambiar de lugar") + " · Open-Meteo.com (CC BY 4.0)";   // attribution (GEM-05)
            case 7: return L.T("= : calculer (ex. = 12*7)", "= : calculate (e.g. = 12*7)", "= : calcular (p. ej. = 12*7)");
            case 8: return L.T("? puis un mot : chercher sur le web (« ? » seul affiche l'aide)", "? then a word: search the web (\"?\" alone shows help)", "? y una palabra: buscar en la web («?» solo muestra la ayuda)");
            case 9: return L.T("> : commandes système (verrouiller, veille…)", "> : system commands (lock, sleep…)", "> : comandos del sistema (bloquear, suspender…)");
        }
        return null;
    }

    string tipShown;

    void UpdateTip(int h)
    {
        // Le texte compte aussi : épingler sans bouger la souris change l'infobulle de l'épingle (QA36-03)
        string text = TipText(h) ?? "";
        if (h == tipFor && text == tipShown) return;
        tipFor = h;
        tipShown = text;
        if (tips == null)
        {
            tips = new ToolTip { InitialDelay = 500, ReshowDelay = 200, AutoPopDelay = 8000, ShowAlways = true };
            Disposed += (s, e) => tips.Dispose();
        }
        tips.SetToolTip(this, text);
    }

    // ---------- Clavier : F6 parcourt les pastilles, boutons et tuiles du haut (Gemini B7) ----------
    int chromeFocus = -1;

    System.Collections.Generic.List<int> ChromeOrder()
    {
        var order = new System.Collections.Generic.List<int>();
        if (ChipsVisible) { order.Add(7); order.Add(8); order.Add(9); }
        order.Add(5); order.Add(0); order.Add(1);
        if (Settings.Widgets) { order.Add(3); order.Add(4); }
        if (Settings.WeatherOn && !rcWeather.IsEmpty) order.Add(6);
        return order;
    }

    Rectangle HotspotRect(int code)
    {
        switch (code)
        {
            case 0: return rcViewBtn;
            case 1: return rcGear;
            case 3: return rcDisk;
            case 4: return rcBin;
            case 5: return rcPin;
            case 6: return rcWeather;
            case 7: case 8: case 9: return rcChips[code - ChipFirst];
        }
        return Rectangle.Empty;
    }

    void MoveChromeFocus(int step)
    {
        var order = ChromeOrder();
        int i = order.IndexOf(chromeFocus);
        i = i < 0 ? (step > 0 ? 0 : order.Count - 1) : (i + step + order.Count) % order.Count;
        SetChromeFocus(order[i]);
    }

    void SetChromeFocus(int code)
    {
        chromeFocus = code;
        Invalidate();
        if (code < 0) { if (tips != null) tips.Hide(this); return; }
        Rectangle r = HotspotRect(code);
        UpdateTip(-1);
        if (tips != null) tips.Show(TipText(code) ?? "", this, r.X, r.Bottom + S(4), 4000);
    }

    // Vrai si la touche est consommée. Toute autre touche rend la main au comportement normal du panneau.
    bool HandleChromeKey(Keys k, bool shift)
    {
        int code = chromeFocus;
        if (k == Keys.Left || k == Keys.Right) { MoveChromeFocus(k == Keys.Left ? -1 : 1); return true; }
        if (k == Keys.Escape) { SetChromeFocus(-1); return true; }
        if (k == Keys.Enter || k == Keys.Space || k == Keys.Apps || (k == Keys.F10 && shift))
        {
            SetChromeFocus(-1);
            Rectangle r = HotspotRect(code);
            MouseButtons button = k == Keys.Apps || k == Keys.F10 ? MouseButtons.Right : MouseButtons.Left;
            OnMouseClick(new MouseEventArgs(button, 1, r.X + r.Width / 2, r.Y + r.Height / 2, 0));
            return true;
        }
        SetChromeFocus(-1);
        return false;
    }

    void DrawChromeFocus(Graphics g)
    {
        if (chromeFocus < 0) return;
        Rectangle r = HotspotRect(chromeFocus);
        if (r.IsEmpty) { chromeFocus = -1; return; }
        using (Pen p = new Pen(Theme.Accent, S(2))) g.DrawRectangle(p, Rectangle.Inflate(r, S(1), S(1)));
    }
}
