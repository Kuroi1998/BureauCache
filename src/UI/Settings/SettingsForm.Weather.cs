// BureauCache — SettingsForm (lieu de la météo)
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

// T-023 (Gemini A8) : le lieu de la météo se cherche directement dans les Paramètres, sans fenêtre à part.
// On tape une ville puis Entrée (ou « Rechercher ») : les résultats s'affichent dans un menu sous le champ, avec
// « Détecter ma position » en dernier. Choisir un lieu active la météo.
partial class SettingsForm
{
    InputBox placeBox;
    FlatButton bPlace;
    ContextMenuStrip placeMenu;
    int placeSearch;   // seule la dernière recherche lancée peut afficher son résultat

    void BuildPlaceRow()
    {
        placeBox = new InputBox(S(190));
        placeBox.Box.TextChanged += (s, e) =>
        {
            placeSearch++;
            if (placeMenu != null && placeMenu.Visible) placeMenu.Close();
            UpdateWeatherPlaceText();
        };
        placeBox.Box.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; SearchPlace(); } };
        placeBox.Box.HandleCreated += (s, e) => Native.SendMessage(placeBox.Box.Handle, 0x1501, (IntPtr)1, T("Ville…", "City…", "Ciudad…"));
        bPlace = Button(T("Rechercher", "Search", "Buscar"), false, S(110), SearchPlace);
        lWeatherPlace = Row(T("Lieu de la météo", "Weather location", "Ubicación para el tiempo"), "-", bPlace, null, null, placeBox,
            T("ville localisation", "city location", "ciudad meteorología"));
    }

    void UpdateWeatherPlaceText()
    {
        if (lWeatherPlace != null)
            lWeatherPlace.Text = string.IsNullOrEmpty(Settings.WeatherPlace)
                ? T("Aucun lieu choisi.", "No place chosen.", "Ningún lugar seleccionado.")
                : Settings.WeatherPlace;
    }

    // Météo activée sans lieu : on invite à chercher juste en dessous
    void AskForPlace()
    {
        // Différé : l'interrupteur déclenche ensuite un rechargement de la page qui réécrirait ce texte
        Post(AskForPlaceNow);
    }

    void AskForPlaceNow()
    {
        if (IsDisposed || Weather.Configured) return;
        lWeatherPlace.Text = T("Cherche ta ville ci-dessous : choisir un lieu active la météo.", "Search for your city below: choosing a place turns the weather on.", "Busca tu ciudad abajo: elegir un lugar activa el tiempo.");
        placeBox.Box.Focus();
    }

    void SearchPlace()
    {
        string q = placeBox.Box.Text.Trim();
        int my = ++placeSearch;
        if (q.Length < 2) { ShowPlaces(my, new List<WeatherPlace>(), null); return; }   // menu réduit à « Détecter »
        lWeatherPlace.Text = T("Recherche…", "Searching…", "Buscando…");
        Background.Run(() =>
        {
            List<WeatherPlace> found = null;
            string error = null;
            try { found = Weather.Search(q); } catch (Exception ex) { error = Http.FriendlyError(ex); }
            Post(() => ShowPlaces(my, found ?? new List<WeatherPlace>(), error));
        });
    }

    void ShowPlaces(int my, List<WeatherPlace> found, string error)
    {
        if (my != placeSearch || IsDisposed) return;
        UpdateTexts();
        if (error != null) lWeatherPlace.Text = T("Recherche impossible : ", "Search failed: ", "Error de búsqueda: ") + error;
        else if (found.Count == 0 && placeBox.Box.Text.Trim().Length >= 2) lWeatherPlace.Text = T("Aucune ville trouvée.", "No city found.", "No se encontró ninguna ciudad.");
        if (placeMenu == null) placeMenu = Menus.Create();
        Menus.Clear(placeMenu.Items);
        foreach (WeatherPlace p in found)
        {
            WeatherPlace place = p;
            placeMenu.Items.Add(Menus.Item(place.Label, Glyphs.Globe, () => ChoosePlace(place)));
        }
        if (found.Count > 0) placeMenu.Items.Add(new ToolStripSeparator());
        placeMenu.Items.Add(Menus.Item(T("Détecter ma position", "Detect my location", "Detectar mi ubicación"), Glyphs.Flag, DetectPlace));
        // Fenêtre fermée ou page changée pendant la recherche : pas de menu flottant seul à l'écran (QA37-01)
        if (Visible && placeBox.Visible && WindowState != FormWindowState.Minimized) placeMenu.Show(placeBox, new Point(0, placeBox.Height));
    }

    void DetectPlace()
    {
        int my = ++placeSearch;
        lWeatherPlace.Text = T("Demande de la position à Windows…", "Asking Windows for the location…", "Solicitando la ubicación a Windows…");
        Background.Run(() =>
        {
            WeatherPlace p = null;
            string error = null;
            try { p = Weather.Locate(out error); } catch (Exception ex) { error = ex.Message; }
            Post(() =>
            {
                if (my != placeSearch || IsDisposed) return;
                if (p != null) ChoosePlace(p);
                else { UpdateTexts(); lWeatherPlace.Text = error ?? T("Position introuvable.", "Location not found.", "Ubicación no encontrada."); }
            });
        }, true);
    }

    void ChoosePlace(WeatherPlace p)
    {
        WeatherPlaceDialog.Apply(p);   // enregistre et active la météo ; Notify recharge la page
        placeBox.Box.Text = "";
        UpdateTexts();
    }

    void Post(Action a)
    {
        try { if (!IsDisposed && IsHandleCreated) BeginInvoke(a); } catch { }
    }

    void DisposePlaceMenu()
    {
        if (placeMenu != null) placeMenu.Dispose();
    }
}
