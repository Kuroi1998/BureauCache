using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BureauCache.Tests;

// T-042 : panneau Notifications et calendrier de la barre (D-018)
[TestClass]
public class NotificationsPanelTests
{
    static void RunSta(Action body)
    {
        Exception error = null;
        Thread t = new Thread(() => { try { body(); } catch (Exception ex) { error = ex; } });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
        if (error != null) ExceptionDispatchInfo.Capture(error).Throw();
    }

    [TestMethod]
    public void MonthGridMatchesWindowsForNovember2026()
    {
        DateTime[] days = CalendarMonth.Days(new DateTime(2026, 11, 15), DayOfWeek.Monday);
        Assert.AreEqual(42, days.Length);
        Assert.AreEqual(new DateTime(2026, 10, 26), days[0], "Comme la capture du panneau Windows : lundi 26 octobre.");
        Assert.AreEqual(new DateTime(2026, 12, 6), days[41]);
        for (int i = 1; i < days.Length; i++) Assert.AreEqual(days[i - 1].AddDays(1), days[i]);
    }

    [TestMethod]
    public void WeekCanStartOnSunday()
    {
        DateTime[] days = CalendarMonth.Days(new DateTime(2026, 11, 1), DayOfWeek.Sunday);
        Assert.AreEqual(new DateTime(2026, 11, 1), days[0], "Le 1er novembre 2026 est un dimanche : pas de jour d'octobre.");
        days = CalendarMonth.Days(new DateTime(2026, 2, 1), DayOfWeek.Monday);
        Assert.AreEqual(new DateTime(2026, 1, 26), days[0]);
    }

    [TestMethod]
    public void DayNamesFollowLanguageAndFirstDay()
    {
        CollectionAssert.AreEqual(new[] { "lu", "ma", "me", "je", "ve", "sa", "di" },
            CalendarMonth.DayNames(CultureInfo.GetCultureInfo("fr-FR"), DayOfWeek.Monday));
        CollectionAssert.AreEqual(new[] { "Su", "Mo", "Tu", "We", "Th", "Fr", "Sa" },
            CalendarMonth.DayNames(CultureInfo.GetCultureInfo("en-US"), DayOfWeek.Sunday));
        Assert.AreEqual(7, CalendarMonth.DayNames(CultureInfo.GetCultureInfo("es-ES"), DayOfWeek.Monday).Distinct().Count(),
            "Sept colonnes distinctes en espagnol aussi.");
    }

    [TestMethod]
    public void MonthNavigationCrossesYearsAndStaysBounded()
    {
        Assert.AreEqual(new DateTime(2027, 1, 1), CalendarMonth.AddMonths(new DateTime(2026, 12, 31), 1));
        Assert.AreEqual(new DateTime(2025, 12, 1), CalendarMonth.AddMonths(new DateTime(2026, 1, 15), -1));
        Assert.AreEqual(new DateTime(1900, 1, 1), CalendarMonth.AddMonths(new DateTime(1900, 1, 1), -5));
        Assert.AreEqual(new DateTime(9998, 12, 1), CalendarMonth.AddMonths(new DateTime(9998, 12, 1), 3));
    }

    [TestMethod]
    public void TitlesInThreeLanguages()
    {
        DateTime friday = new DateTime(2026, 11, 27);
        using (LanguageScope.French())
        {
            Assert.AreEqual("vendredi 27 novembre", CalendarMonth.DayTitle(friday, L.Culture));
            Assert.AreEqual("novembre 2026", CalendarMonth.MonthTitle(friday, L.Culture));
        }
        using (LanguageScope.English())
        {
            Assert.AreEqual("Friday, November 27", CalendarMonth.DayTitle(friday, L.Culture));
            Assert.AreEqual("November 2026", CalendarMonth.MonthTitle(friday, L.Culture));
        }
        using (LanguageScope.Spanish())
        {
            Assert.AreEqual("viernes, 27 de noviembre", CalendarMonth.DayTitle(friday, L.Culture));
            Assert.AreEqual("noviembre de 2026", CalendarMonth.MonthTitle(friday, L.Culture));
        }
    }

    [TestMethod]
    public void KeyboardMovesTheSelectionAndTheMonth()
    {
        RunSta(() =>
        {
            using (CalendarView view = new CalendarView())
            {
                view.SetSelected(new DateTime(2026, 11, 30));
                Assert.IsTrue(view.HandleKey(Keys.Right));
                Assert.AreEqual(new DateTime(2026, 12, 1), view.Selected);
                Assert.AreEqual(new DateTime(2026, 12, 1), view.Month, "Le mois suit le jour choisi.");
                view.SetSelected(new DateTime(2027, 1, 31));
                Assert.IsTrue(view.HandleKey(Keys.PageDown));
                Assert.AreEqual(new DateTime(2027, 2, 28), view.Selected, "31 janvier + 1 mois : dernier jour de février.");
                Assert.IsTrue(view.HandleKey(Keys.Up));
                Assert.AreEqual(new DateTime(2027, 2, 21), view.Selected);
                Assert.IsTrue(view.HandleKey(Keys.Home));
                Assert.AreEqual(DateTime.Today, view.Selected);
                Assert.IsFalse(view.HandleKey(Keys.A));
                view.ShowMonth(-1);
                Assert.AreEqual(DateTime.Today, view.Selected, "Les flèches du mois ne changent pas le jour choisi.");
            }
        });
    }

    [TestMethod]
    public void PanelFitsItsTextsInEveryLanguageAndCollapses()
    {
        RunSta(() =>
        {
            foreach (Func<LanguageScope> scope in new Func<LanguageScope>[] { LanguageScope.French, LanguageScope.English, LanguageScope.Spanish })
                using (scope())
                using (NotificationsForm form = new NotificationsForm(true))
                {
                    bool wasCollapsed = form.Collapsed;
                    if (wasCollapsed) form.ToggleCollapse();
                    try
                    {
                        foreach (StartTile tile in form.Controls.OfType<StartTile>())
                        {
                            Assert.IsTrue(form.ClientRectangle.Contains(tile.Bounds), L.CurrentLanguage + " : " + tile.Text);
                            if (!tile.GlyphOnly) Assert.IsTrue(tile.TextFits, L.CurrentLanguage + " : " + tile.Text);
                            else Assert.IsFalse(string.IsNullOrEmpty(tile.AccessibleName), "Bouton icône seule sans nom.");
                        }
                        foreach (Label label in form.Controls.OfType<Label>())
                        {
                            Assert.IsTrue(form.ClientRectangle.Contains(label.Bounds), label.Text);
                            Size need = TextRenderer.MeasureText(label.Text, label.Font, new Size(label.Width, int.MaxValue), TextFormatFlags.WordBreak);
                            Assert.IsTrue(need.Height <= label.Height && (need.Width <= label.Width || label.Text.Contains(" ")),
                                L.CurrentLanguage + " : texte rogné « " + label.Text + " »");
                        }
                        Assert.IsTrue(form.ClientRectangle.Contains(form.Calendar.Bounds));
                        int open = form.ClientSize.Height;
                        form.ToggleCollapse();
                        Assert.IsFalse(form.Calendar.Visible);
                        Assert.IsTrue(form.ClientSize.Height < open, "Replié, le panneau rapetisse.");
                        form.ToggleCollapse();
                        Assert.AreEqual(open, form.ClientSize.Height);
                    }
                    finally { if (wasCollapsed != form.Collapsed) form.ToggleCollapse(); }
                }
        });
    }
}
