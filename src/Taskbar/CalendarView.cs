// BureauCache — grille du mois dans le panneau Notifications et calendrier (T-042, D-018)
using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

// Jours de la semaine, puis 6 semaines. Aujourd'hui en couleur d'accent, jour choisi cerclé. Clavier : flèches,
// Page précédente / suivante (mois), Origine (aujourd'hui).
class CalendarView : Control
{
    DateTime month, selected;
    DateTime[] days;
    string[] dayNames;
    readonly DayOfWeek firstDay;
    int hover = -1;

    public event EventHandler MonthChanged;

    public CalendarView()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                 ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        // Le premier jour de la semaine suit le format régional de Windows ; les noms suivent la langue de BureauCache
        firstDay = CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
        dayNames = CalendarMonth.DayNames(L.Culture, firstDay);
        Font = Ui.Font("Segoe UI", 10f);
        BackColor = Theme.Card;
        TabStop = true;
        AccessibleRole = AccessibleRole.Table;
        SetSelected(DateTime.Today);
    }

    public DateTime Month { get { return month; } }
    public DateTime Selected { get { return selected; } }

    int Cell { get { return Math.Max(Ui.S(38), Font.Height + Ui.S(18)); } }
    int Header { get { return Math.Max(Ui.S(34), Font.Height + Ui.S(14)); } }
    public int PreferredHeight { get { return Header + 6 * Cell; } }

    public void ShowMonth(int delta)
    {
        DateTime target = CalendarMonth.AddMonths(month, delta);
        if (target == month) return;
        SetMonth(target);
    }

    public void SetSelected(DateTime day)
    {
        selected = CalendarMonth.Clamp(day);
        DateTime first = new DateTime(selected.Year, selected.Month, 1);
        if (first != month) SetMonth(first);
        else Invalidate();
        AccessibleName = CalendarMonth.DayTitle(selected, L.Culture) + " " + selected.Year.ToString(L.Culture);
        AccessibilityNotifyClients(AccessibleEvents.NameChange, -1);
    }

    void SetMonth(DateTime first)
    {
        month = first;
        days = CalendarMonth.Days(month, firstDay);
        hover = -1;
        Invalidate();
        if (MonthChanged != null) MonthChanged(this, EventArgs.Empty);
    }

    // Vrai si la touche a été traitée
    internal bool HandleKey(Keys key)
    {
        switch (key)
        {
            case Keys.Left: SetSelected(Step(-1)); return true;
            case Keys.Right: SetSelected(Step(1)); return true;
            case Keys.Up: SetSelected(Step(-7)); return true;
            case Keys.Down: SetSelected(Step(7)); return true;
            case Keys.PageUp: SetSelected(SameDay(-1)); return true;
            case Keys.PageDown: SetSelected(SameDay(1)); return true;
            case Keys.Home: SetSelected(DateTime.Today); return true;
        }
        return false;
    }

    DateTime Step(int count)
    {
        try { return selected.AddDays(count); }
        catch (ArgumentOutOfRangeException) { return selected; }
    }

    // Même jour un mois plus tôt ou plus tard (31 janvier + 1 mois : 28 ou 29 février)
    DateTime SameDay(int delta)
    {
        DateTime first = CalendarMonth.AddMonths(month, delta);
        return first.AddDays(Math.Min(selected.Day, DateTime.DaysInMonth(first.Year, first.Month)) - 1);
    }

    protected override bool IsInputKey(Keys keyData)
    {
        switch (keyData)
        {
            case Keys.Left: case Keys.Right: case Keys.Up: case Keys.Down:
            case Keys.PageUp: case Keys.PageDown: case Keys.Home: return true;
        }
        return base.IsInputKey(keyData);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (HandleKey(e.KeyData)) { e.Handled = true; return; }
        base.OnKeyDown(e);
    }

    Rectangle CellBounds(int index)
    {
        int col = index % 7, row = index / 7;
        int left = col * Width / 7, right = (col + 1) * Width / 7;
        return new Rectangle(left, Header + row * Cell, right - left, Cell);
    }

    int IndexAt(Point p)
    {
        if (p.Y < Header || p.X < 0 || p.X >= Width) return -1;
        int row = (p.Y - Header) / Cell, col = Math.Min(6, p.X * 7 / Math.Max(1, Width));
        return row >= 0 && row < 6 ? row * 7 + col : -1;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int i = IndexAt(e.Location);
        if (i != hover) { hover = i; Invalidate(); }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (hover != -1) { hover = -1; Invalidate(); }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        int i = IndexAt(e.Location);
        if (i >= 0 && e.Button == MouseButtons.Left) SetSelected(days[i]);
    }

    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(BackColor);
        TextFormatFlags center = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
        for (int c = 0; c < 7; c++)
        {
            Rectangle cell = CellBounds(c);
            TextRenderer.DrawText(g, dayNames[c], Font, new Rectangle(cell.Left, 0, cell.Width, Header), Theme.Fg, center);
        }
        DateTime today = DateTime.Today;
        int disc = Math.Min(Cell, Width / 7) - Ui.S(6);
        for (int i = 0; i < CalendarMonth.Cells; i++)
        {
            Rectangle cell = CellBounds(i);
            Rectangle round = new Rectangle(cell.Left + (cell.Width - disc) / 2, cell.Top + (cell.Height - disc) / 2, disc, disc);
            DateTime day = days[i];
            bool isToday = day == today, isSelected = day == selected;
            Color text = day.Month == month.Month ? Theme.Fg : Theme.FgDim;
            if (isToday)
            {
                using (SolidBrush b = new SolidBrush(Theme.Accent)) g.FillEllipse(b, round);
                text = Theme.SelText(true, Color.White);
            }
            else if (i == hover)
                using (SolidBrush b = new SolidBrush(Theme.HoverOverlay)) g.FillEllipse(b, round);
            if (isSelected && (Focused || !isToday))
            {
                Rectangle ring = round;
                if (isToday) ring.Inflate(Ui.S(2), Ui.S(2));
                using (Pen p = new Pen(isToday ? Theme.Fg : Theme.Accent, Math.Max(1f, Ui.S(2)))) g.DrawEllipse(p, ring);
            }
            TextRenderer.DrawText(g, day.Day.ToString(L.Culture), Font, cell, text, center);
        }
    }
}
