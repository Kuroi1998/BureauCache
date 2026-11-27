// BureauCache — mois affiché par le calendrier de la barre (T-042, D-018)
using System;
using System.Globalization;

// Calculs sans interface, pour les tests : 6 semaines complètes, comme le calendrier de Windows
static class CalendarMonth
{
    public const int Cells = 42;
    const int FirstYear = 1900, LastYear = 9998;

    // Les 42 jours affichés : la semaine du 1er du mois commence au premier jour de la semaine de la région
    internal static DateTime[] Days(DateTime month, DayOfWeek firstDay)
    {
        DateTime first = new DateTime(month.Year, month.Month, 1);
        int offset = ((int)first.DayOfWeek - (int)firstDay + 7) % 7;
        DateTime start = first.AddDays(-offset);
        DateTime[] days = new DateTime[Cells];
        for (int i = 0; i < Cells; i++) days[i] = start.AddDays(i);
        return days;
    }

    // En-têtes des colonnes (« lu », « Mo », « L »), dans l'ordre de la semaine affichée
    internal static string[] DayNames(CultureInfo culture, DayOfWeek firstDay)
    {
        string[] names = culture.DateTimeFormat.ShortestDayNames;
        string[] result = new string[7];
        for (int i = 0; i < 7; i++) result[i] = names[((int)firstDay + i) % 7];
        return result;
    }

    // Premier jour du mois décalé de delta mois, borné à des années qu'un calendrier de bureau peut afficher
    internal static DateTime AddMonths(DateTime month, int delta)
    {
        long index = (long)month.Year * 12 + month.Month - 1 + delta;
        index = Math.Max((long)FirstYear * 12, Math.Min((long)LastYear * 12 + 11, index));
        return new DateTime((int)(index / 12), (int)(index % 12) + 1, 1);
    }

    internal static DateTime Clamp(DateTime day)
    {
        if (day.Year < FirstYear) return new DateTime(FirstYear, 1, 1);
        if (day.Year > LastYear) return new DateTime(LastYear, 12, 31);
        return day.Date;
    }

    // « novembre 2026 », « November 2026 », « noviembre de 2026 »
    internal static string MonthTitle(DateTime month, CultureInfo culture)
    {
        return month.ToString(L.T("MMMM yyyy", "MMMM yyyy", "MMMM 'de' yyyy"), culture);
    }

    // « vendredi 27 novembre », « Friday, November 27 », « viernes, 27 de noviembre »
    internal static string DayTitle(DateTime day, CultureInfo culture)
    {
        return day.ToString(L.T("dddd d MMMM", "dddd, MMMM d", "dddd, d 'de' MMMM"), culture);
    }
}
