// BureauCache — WeatherPlaceDialog, WeatherDetailDialog
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

// =====================================================================
//  Choix du lieu de la météo : recherche de ville ou position Windows
// =====================================================================
class WeatherPlaceDialog : ThemedDialog
{
    readonly InputBox query;
    readonly ListBox results;
    readonly FlatButton bSearch, bLocate;
    readonly Label status;
    readonly List<WeatherPlace> places = new List<WeatherPlace>();
    bool busy;

    public static void Run()
    {
        using (WeatherPlaceDialog d = new WeatherPlaceDialog()) d.ShowDialog();
    }

    WeatherPlaceDialog() : base(L.T("Lieu de la météo", "Weather location", "Ubicación para el tiempo"), Ui.S(480), Ui.S(430))
    {
        int pad = Ui.S(20), w = ClientSize.Width - 2 * pad;
        AddLabel(L.T("Tape le nom de ta ville, ou laisse Windows trouver ta position.", "Type your city, or let Windows find your location.", "Escribe tu ciudad o deja que Windows detecte tu ubicación."), pad, Ui.S(16))
            .MaximumSize = new Size(w, Ui.S(24));

        query = new InputBox(w - Ui.S(130)) { Location = new Point(pad, Ui.S(44)), BackColor = Theme.Bg };
        query.Box.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; DoSearch(); } };
        Controls.Add(query);
        bSearch = new FlatButton(L.T("Rechercher", "Search", "Buscar"), true, Ui.S(120)) { Location = new Point(pad + w - Ui.S(120), Ui.S(43)), BackColor = Theme.Bg };
        bSearch.Click += (s, e) => DoSearch();
        Controls.Add(bSearch);

        results = new ListBox
        {
            Location = new Point(pad, Ui.S(90)), Size = new Size(w, Ui.S(170)), BorderStyle = BorderStyle.None,
            BackColor = Theme.Surface, ForeColor = Theme.Fg, Font = Ui.Font("Segoe UI", 10f), IntegralHeight = false
        };
        results.DoubleClick += (s, e) => Choose();
        results.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Choose(); } };
        Controls.Add(results);

        bLocate = new FlatButton(L.T("Détecter ma position", "Detect my location", "Detectar mi ubicación"), false) { Location = new Point(pad, Ui.S(270)), BackColor = Theme.Bg };
        bLocate.Click += (s, e) => DoLocate();
        Controls.Add(bLocate);
        status = AddLabel("", pad + bLocate.Width + Ui.S(10), Ui.S(279));
        status.MaximumSize = new Size(w - bLocate.Width - Ui.S(10), 0);

        Label privacy = AddLabel(L.T("La recherche envoie le nom tapé à Open-Meteo. La détection demande la position à Windows, puis envoie les coordonnées arrondies à BigDataCloud pour trouver le nom de la ville. Ensuite, seules les coordonnées du lieu choisi sont envoyées à Open-Meteo, quand le panneau s'ouvre : au plus toutes les 30 minutes (5 minutes après un échec).",
            "Search sends the typed name to Open-Meteo. Detection asks Windows for the location, then sends the rounded coordinates to BigDataCloud to find the city name. Afterwards, only the chosen place's coordinates are sent to Open-Meteo when the panel opens: at most every 30 minutes (5 minutes after a failure).", "La búsqueda envía el nombre a Open-Meteo. La detección pide la ubicación a Windows y envía las coordenadas redondeadas a BigDataCloud para identificar la ciudad. Al abrir el panel, solo se envían a Open-Meteo las coordenadas del lugar elegido como máximo cada 30 minutos, o cada 5 minutos tras un error."), pad, Ui.S(318));
        privacy.MaximumSize = new Size(w, 0);
        privacy.Font = Ui.Font("Segoe UI", 8.25f);
        // Le texte de confidentialité fait 4 à 6 lignes selon la langue : les boutons passent dessous
        ClientSize = new Size(ClientSize.Width, privacy.Top + privacy.GetPreferredSize(new Size(w, 0)).Height + Ui.S(70));

        AddButtons(Choose, L.T("Choisir", "Choose", "Elegir"));
        if (!string.IsNullOrEmpty(Settings.WeatherPlace)) query.Box.Text = Settings.WeatherPlace.Split(',')[0];
    }

    void SetBusy(bool on, string text)
    {
        busy = on;
        bSearch.Enabled = bLocate.Enabled = !on;
        status.Text = text;
    }

    void ShowResults(List<WeatherPlace> found)
    {
        places.Clear();
        places.AddRange(found);
        results.Items.Clear();
        foreach (WeatherPlace p in found) results.Items.Add(p.Label);
        if (results.Items.Count > 0) results.SelectedIndex = 0;
    }

    void DoSearch()
    {
        string q = query.Box.Text.Trim();
        if (busy || q.Length < 2) return;
        SetBusy(true, L.T("Recherche…", "Searching…", "Buscando…"));
        Background.Run(() =>
        {
            List<WeatherPlace> found = null;
            string error = null;
            try { found = Weather.Search(q); } catch (Exception ex) { error = ex.Message; }
            Done(() =>
            {
                if (found != null) ShowResults(found);
                SetBusy(false, error != null ? L.T("Recherche impossible : ", "Search failed: ", "Error de búsqueda: ") + error
                    : found.Count == 0 ? L.T("Aucune ville trouvée.", "No city found.", "No se encontró ninguna ciudad.") : "");
            });
        });
    }

    void DoLocate()
    {
        if (busy) return;
        SetBusy(true, L.T("Demande de la position à Windows…", "Asking Windows for the location…", "Solicitando la ubicación a Windows…"));
        Background.Run(() =>
        {
            WeatherPlace p = null;
            string error = null;
            try { p = Weather.Locate(out error); } catch (Exception ex) { error = ex.Message; }
            Done(() =>
            {
                if (p != null) ShowResults(new List<WeatherPlace> { p });
                SetBusy(false, error ?? "");
            });
        }, true);
    }

    void Done(Action a)
    {
        try { if (!IsDisposed && IsHandleCreated) BeginInvoke(a); } catch { }
    }

    void Choose()
    {
        if (busy) return;
        int i = results.SelectedIndex;
        if (i < 0 || i >= places.Count) { status.Text = L.T("Choisis un lieu dans la liste.", "Choose a place in the list.", "Elige un lugar de la lista."); return; }
        WeatherPlace p = places[i];
        Settings.WeatherPlace = p.Label;
        Settings.WeatherLat = Math.Round(p.Lat, 4);
        Settings.WeatherLon = Math.Round(p.Lon, 4);
        Settings.WeatherOn = true;
        Weather.Reset();
        Settings.Notify();
        DialogResult = DialogResult.OK;
        Close();
    }
}

// =====================================================================
//  Détail de la météo : conditions actuelles et 3 jours
// =====================================================================
class WeatherDetailDialog : ThemedDialog
{
    readonly WeatherReport w;
    readonly Font fBig = Ui.Font("Segoe UI Semibold", 26f);
    readonly Font fText = Ui.Font("Segoe UI", 10f);
    readonly Font fSmall = Ui.Font("Segoe UI", 9f);

    public static void Open(WeatherReport report)
    {
        using (WeatherDetailDialog d = new WeatherDetailDialog(report)) d.ShowDialog();
    }

    WeatherDetailDialog(WeatherReport report) : base(L.T("Météo", "Weather", "Tiempo"), Ui.S(400), Ui.S(360))
    {
        w = report;
        DoubleBuffered = true;
        LinkLabel credit = new LinkLabel
        {
            Text = L.T("Données : ", "Data: ", "Datos: ") + Weather.Attribution, AutoSize = true, Font = fSmall,
            LinkColor = Theme.Accent, ActiveLinkColor = Theme.Accent, BackColor = Theme.Bg, Location = new Point(Ui.S(20), ClientSize.Height - Ui.S(44))
        };
        credit.LinkClicked += (s, e) => { try { ShellUtil.Open(Weather.AttributionUrl, null); } catch { } };
        Controls.Add(credit);
        FlatButton close = new FlatButton(L.T("Fermer", "Close", "Cerrar"), true, Ui.S(100)) { BackColor = Theme.Bg };
        close.Location = new Point(ClientSize.Width - Ui.S(20) - close.Width, ClientSize.Height - Ui.S(18) - close.Height);
        close.Click += (s, e) => Close();
        Controls.Add(close);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        int pad = Ui.S(20), width = ClientSize.Width - 2 * pad;
        TextFormatFlags flags = TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter;
        TextRenderer.DrawText(g, Settings.WeatherPlace, fText, new Rectangle(pad, Ui.S(12), width, Ui.S(22)), Theme.FgDim, flags);
        if (w == null)
        {
            TextRenderer.DrawText(g, L.T("Pas encore de données : réessaie dans un instant.", "No data yet: try again in a moment.", "Todavía no hay datos. Inténtalo de nuevo en un momento."), fText,
                new Rectangle(pad, Ui.S(60), width, Ui.S(24)), Theme.Fg, flags);
            return;
        }
        WeatherIcons.Draw(g, w.Code, w.IsDay, new RectangleF(pad, Ui.S(40), Ui.S(72), Ui.S(72)));
        TextRenderer.DrawText(g, Weather.Temp(w.Temp), fBig, new Rectangle(pad + Ui.S(84), Ui.S(38), Ui.S(130), Ui.S(50)), Theme.Fg, flags);
        TextRenderer.DrawText(g, Weather.Describe(w.Code), fText, new Rectangle(pad + Ui.S(84), Ui.S(84), width - Ui.S(84), Ui.S(22)), Theme.Fg, flags);
        string wind = double.IsNaN(w.Wind) ? "—" : Math.Round(w.Wind).ToString("0", L.Culture) + " " + Weather.WindUnit;
        string extra = L.T("Ressenti ", "Feels like ", "Sensación térmica: ") + Weather.Temp(w.Feels) + "  ·  " + L.T("Vent ", "Wind ", "Viento: ") + wind;
        TextRenderer.DrawText(g, extra, fSmall, new Rectangle(pad, Ui.S(122), width, Ui.S(20)), Theme.FgDim, flags);

        int y = Ui.S(152);
        for (int i = 0; i < w.Days.Count && i < 3; i++)
        {
            WeatherDay d = w.Days[i];
            Rectangle row = new Rectangle(pad, y, width, Ui.S(40));
            Ui.Fill(g, Theme.HoverOverlay, row, Ui.S(8));
            string name = Weather.DayName(w, d.Date);
            TextRenderer.DrawText(g, name, fText, new Rectangle(row.X + Ui.S(12), row.Y, Ui.S(130), row.Height), Theme.Fg, flags);
            WeatherIcons.Draw(g, d.Code, true, new RectangleF(row.X + Ui.S(150), row.Y + Ui.S(5), Ui.S(30), Ui.S(30)));
            TextRenderer.DrawText(g, Weather.Describe(d.Code), fSmall, new Rectangle(row.X + Ui.S(186), row.Y, Ui.S(110), row.Height), Theme.FgDim, flags);
            TextRenderer.DrawText(g, Weather.Temp(d.Min) + " / " + Weather.Temp(d.Max), fText, new Rectangle(row.X, row.Y, row.Width - Ui.S(12), row.Height), Theme.Fg,
                flags | TextFormatFlags.Right);
            y += Ui.S(46);
        }
        TextRenderer.DrawText(g, L.T("Mis à jour à ", "Updated at ", "Actualizado a las ") + w.Fetched.ToString("HH:mm", L.Culture), fSmall,
            new Rectangle(pad, y, width, Ui.S(20)), Theme.FgDim, flags);
    }
}
