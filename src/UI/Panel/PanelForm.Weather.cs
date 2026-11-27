// BureauCache — PanelForm (tuile météo et nettoyage)
using System;
using System.Drawing;
using System.Windows.Forms;

partial class PanelForm
{
    Rectangle rcWeather;
    readonly Font fWeather = Ui.Font("Segoe UI Semibold", 13f);

    // Tuile météo sous les widgets (si activée) ; renvoie le haut de la liste
    int LayoutWeather(int x0, int cw, int top)
    {
        if (!Settings.WeatherOn) { rcWeather = Rectangle.Empty; return top; }
        int y = Settings.Widgets ? top : S(104);
        rcWeather = new Rectangle(x0 + S(12), y, cw - S(24), S(46));
        return rcWeather.Bottom + S(8);
    }

    void DrawWeather(Graphics g)
    {
        if (rcWeather.IsEmpty) return;
        Ui.Card(g, hoverBtn == 6 ? Theme.SelOverlay : Theme.HoverOverlay, rcWeather, S(Ui.RadiusCard), hoverBtn == 6);
        WeatherReport w = Weather.Current;
        TextFormatFlags left = TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis;
        if (!Weather.Configured || w == null)
        {
            Ui.Glyph(g, Glyphs.Search, 10f, Theme.FgDim, new Rectangle(rcWeather.X + S(8), rcWeather.Y, S(28), rcWeather.Height));
            string msg = !Weather.Configured ? L.T("Météo : choisir une ville…", "Weather: choose a city…", "Tiempo: elige una ciudad…")
                : Weather.Failed ? L.T("Météo indisponible (nouvel essai dans 5 min)", "Weather unavailable (retry in 5 min)", "Tiempo no disponible (reintento en 5 min)")
                : L.T("Météo : chargement…", "Weather: loading…", "Cargando el tiempo…");
            TextRenderer.DrawText(g, msg, fSmall, new Rectangle(rcWeather.X + S(40), rcWeather.Y, rcWeather.Width - S(48), rcWeather.Height), Theme.FgDim, left);
            return;
        }
        WeatherIcons.Draw(g, w.Code, w.IsDay, new RectangleF(rcWeather.X + S(6), rcWeather.Y + S(5), S(36), S(36)));
        string temp = Weather.Temp(w.Temp);
        int tw = TextRenderer.MeasureText(temp, fWeather).Width;
        TextRenderer.DrawText(g, temp, fWeather, new Rectangle(rcWeather.X + S(46), rcWeather.Y, tw + S(4), rcWeather.Height), Theme.Fg, left);
        int x = rcWeather.X + S(50) + tw;
        string range = w.Days.Count > 0 ? Weather.Temp(w.Days[0].Min) + " / " + Weather.Temp(w.Days[0].Max) : "";
        int rw = range.Length > 0 ? TextRenderer.MeasureText(range, fSmall).Width + S(10) : 0;
        Rectangle text = new Rectangle(x, rcWeather.Y + S(4), rcWeather.Right - x - rw, S(20));
        TextRenderer.DrawText(g, Weather.Describe(w.Code), fSmall, text, Theme.Fg, left);
        text.Y += S(18);
        TextRenderer.DrawText(g, Weather.ShortPlace(Settings.WeatherPlace), fSmall, text, Theme.FgDim, left);
        if (rw > 0)
            TextRenderer.DrawText(g, range, fSmall, new Rectangle(rcWeather.Right - rw, rcWeather.Y, rw - S(6), rcWeather.Height), Theme.FgDim,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.NoPrefix);
    }

    void RefreshWeatherIfDue()
    {
        if (!Weather.Due) return;
        Weather.RefreshAsync(() =>
        {
            try { if (IsHandleCreated) BeginInvoke(new Action(() => { if (Visible) Invalidate(rcWeather); })); } catch { }
        });
        if (Visible) Invalidate(rcWeather);
    }

    void WeatherClick(MouseButtons button, Point location)
    {
        if (button == MouseButtons.Left)
        {
            HidePanel();
            if (!Weather.Configured) WeatherPlaceDialog.Run();
            else WeatherDetailDialog.Open(Weather.Current);
            return;
        }
        Menus.Clear(menu.Items);
        menu.Items.Add(Menus.Item(L.T("Actualiser", "Refresh", "Actualizar"), Glyphs.Restart, () => { Weather.Reset(); RefreshWeatherIfDue(); }));
        menu.Items.Add(Menus.Item(L.T("Changer de lieu…", "Change location…", "Cambiar ubicación…"), Glyphs.Globe, () => { HidePanel(); WeatherPlaceDialog.Run(); }));
        menu.Items.Add(Menus.Item(L.T("Masquer la météo", "Hide weather", "Ocultar el tiempo"), Glyphs.Eye, () => { Settings.WeatherOn = false; Weather.Reset(); Settings.Notify(); }));
        ShowPanelMenu(this, location);
    }

    void DiskMenu(Point location)
    {
        Menus.Clear(menu.Items);
        menu.Items.Add(Menus.Item(L.T("Ouvrir Ce PC", "Open This PC", "Abrir Este equipo"), Glyphs.Open, () => { HidePanel(); try { ShellUtil.Open("explorer.exe", "shell:MyComputerFolder"); } catch { } }));
        menu.Items.Add(Menus.Item(L.T("Nettoyer…", "Clean up…", "Limpiar…"), Glyphs.Delete, () =>
        {
            HidePanel();
            CleanupDialog.Run();
            RefreshWidgetData();
        }));
        ShowPanelMenu(this, location);
    }
}
