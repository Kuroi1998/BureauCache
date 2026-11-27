// BureauCache — panneau de l'horloge de la barre : heure, météo et calendrier (T-042, D-018)
using System;
using System.Drawing;
using System.Windows.Forms;

// L'heure avec les secondes et la date du jour, la météo, puis le mois avec ses flèches et la grille. Resté ouvert à
// minuit, il passe au nouveau jour.
class CalendarForm : BarPopup
{
    readonly Label clock, dayTitle, monthTitle;
    readonly StartTile previous, next;
    internal readonly WeatherStrip WeatherView;
    internal readonly CalendarView Calendar;
    readonly Timer tick = new Timer();
    DateTime shownToday;
    int seconds;

    public CalendarForm() : base(L.T("Calendrier", "Calendar", "Calendario"))
    {
        clock = AddLabel(string.Empty, true);
        clock.Font = Ui.Font("Segoe UI Light", 26f);
        clock.AccessibleRole = AccessibleRole.Clock;
        dayTitle = AddLabel(string.Empty, false);
        dayTitle.Font = Ui.Font("Segoe UI", 10.5f);
        WeatherView = new WeatherStrip();
        WeatherView.Click += (s, e) => OpenWeather();
        Controls.Add(WeatherView);
        monthTitle = AddLabel(string.Empty, true);
        monthTitle.Font = Ui.Font("Segoe UI Semibold", 10.5f);
        previous = AddTile(Glyphs.ChevronUp, string.Empty, () => Calendar.ShowMonth(-1));
        next = AddTile(Glyphs.ChevronDown, string.Empty, () => Calendar.ShowMonth(1));
        previous.AccessibleName = L.T("Mois précédent", "Previous month", "Mes anterior");
        next.AccessibleName = L.T("Mois suivant", "Next month", "Mes siguiente");

        Calendar = new CalendarView();
        Calendar.MonthChanged += (s, e) => monthTitle.Text = CalendarMonth.MonthTitle(Calendar.Month, L.Culture);
        Controls.Add(Calendar);
        monthTitle.Text = CalendarMonth.MonthTitle(Calendar.Month, L.Culture);
        shownToday = DateTime.Today;
        dayTitle.Text = CalendarMonth.DayTitle(shownToday, L.Culture);
        clock.Text = ClockText(DateTime.Now);
        tick.Tick += (s, e) => Tick();

        int x = ContentLeft, y = Margin2 + Ui.S(8);
        // Hauteur et largeur prévues pour l'heure la plus large (« 23:58:58 », « 10:58:58 PM »)
        int clockHeight = TextRenderer.MeasureText(ClockText(new DateTime(2000, 1, 1, 23, 58, 58)), clock.Font).Height;
        clock.Bounds = new Rectangle(x - Ui.S(2), y, Inner, clockHeight);
        y = clock.Bottom;
        dayTitle.Bounds = new Rectangle(x, y, Inner, dayTitle.Font.Height + Ui.S(6));
        y = dayTitle.Bottom + Ui.S(10);
        WeatherView.Bounds = new Rectangle(x, y, Inner, WeatherStrip.PreferredHeight);
        y = WeatherView.Bottom + Ui.S(10);
        next.Bounds = new Rectangle(x + Inner - IconButton, y, IconButton, IconButton);
        previous.Bounds = new Rectangle(next.Left - Ui.S(4) - IconButton, y, IconButton, IconButton);
        monthTitle.Bounds = new Rectangle(x, y, previous.Left - Gap - x, IconButton);
        y = next.Bottom + Ui.S(4);
        Calendar.Bounds = new Rectangle(x - Ui.S(4), y, Inner + Ui.S(8), Calendar.PreferredHeight);
        FinishLayout(Margin2, Calendar.Bottom + Ui.S(8));
        ActiveControl = Calendar;
    }

    // Heure longue de la langue de BureauCache : « 16:54:23 », « 4:54:23 PM », « 16:54:23 »
    internal static string ClockText(DateTime time) { return time.ToString("T", L.Culture); }

    // Chaque seconde, calée sur le changement de seconde de l'horloge du PC
    void Tick()
    {
        DateTime now = DateTime.Now;
        clock.Text = ClockText(now);
        tick.Interval = Math.Max(50, 1000 - now.Millisecond + 15);
        if (now.Date != shownToday) NewDay(now.Date);
        if (++seconds % 60 == 0) WeatherView.RefreshIfDue();
    }

    // Panneau resté ouvert à minuit : la date du jour et le cercle d'aujourd'hui suivent
    void NewDay(DateTime today)
    {
        bool showingToday = Calendar.Selected == shownToday;
        shownToday = today;
        dayTitle.Text = CalendarMonth.DayTitle(shownToday, L.Culture);
        if (showingToday) Calendar.SetSelected(shownToday);
        Calendar.Invalidate();
    }

    void OpenWeather()
    {
        if (Sandbox.Active) return;
        WeatherReport report = Weather.Current;
        bool configured = Weather.Configured;
        BeginInvoke((Action)delegate
        {
            Close();
            if (!configured) WeatherPlaceDialog.Run();
            else if (report != null) WeatherDetailDialog.Open(report);
        });
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (Sandbox.Active) return;
        WeatherView.RefreshIfDue();
        tick.Interval = Math.Max(50, 1000 - DateTime.Now.Millisecond + 15);
        tick.Start();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) tick.Dispose();
        base.Dispose(disposing);
    }
}
