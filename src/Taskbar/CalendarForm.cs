// BureauCache — panneau calendrier ouvert par l'horloge de la barre (T-042, D-018)
using System;
using System.Drawing;
using System.Windows.Forms;

// Date du jour, mois avec ses flèches, puis la grille. Resté ouvert à minuit, il passe au nouveau jour.
class CalendarForm : BarPopup
{
    readonly Label dayTitle, monthTitle;
    readonly StartTile previous, next;
    internal readonly CalendarView Calendar;
    readonly Timer midnight = new Timer { Interval = 30000 };
    DateTime shownToday;

    public CalendarForm() : base(L.T("Calendrier", "Calendar", "Calendario"))
    {
        dayTitle = AddLabel(string.Empty, true);
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
        midnight.Tick += (s, e) => CheckDate();

        int x = ContentLeft, y = Margin2 + Ui.S(10);
        dayTitle.Bounds = new Rectangle(x, y, Inner, Ui.S(30));
        y = dayTitle.Bottom + Ui.S(6);
        next.Bounds = new Rectangle(x + Inner - IconButton, y, IconButton, IconButton);
        previous.Bounds = new Rectangle(next.Left - Ui.S(4) - IconButton, y, IconButton, IconButton);
        monthTitle.Bounds = new Rectangle(x, y, previous.Left - Gap - x, IconButton);
        y = next.Bottom + Ui.S(4);
        Calendar.Bounds = new Rectangle(x - Ui.S(4), y, Inner + Ui.S(8), Calendar.PreferredHeight);
        FinishLayout(Margin2, Calendar.Bottom + Ui.S(8));
        ActiveControl = Calendar;
    }

    // Panneau resté ouvert à minuit : la date du jour et le cercle d'aujourd'hui suivent
    void CheckDate()
    {
        if (DateTime.Today == shownToday) return;
        bool showingToday = Calendar.Selected == shownToday;
        shownToday = DateTime.Today;
        dayTitle.Text = CalendarMonth.DayTitle(shownToday, L.Culture);
        if (showingToday) Calendar.SetSelected(shownToday);
        Calendar.Invalidate();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (!Sandbox.Active) midnight.Start();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) midnight.Dispose();
        base.Dispose(disposing);
    }
}
