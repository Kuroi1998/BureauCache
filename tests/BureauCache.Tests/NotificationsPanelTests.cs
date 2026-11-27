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
    public void ArrowKeysContinueFromTheDisplayedMonth()
    {
        RunSta(() =>
        {
            using (var view = new CalendarView())
            {
                view.SetSelected(new DateTime(2026, 1, 31));
                view.ShowMonth(1);
                Assert.AreEqual(new DateTime(2026, 1, 31), view.Selected);
                view.HandleKey(Keys.Left);
                Assert.AreEqual(new DateTime(2026, 2, 27), view.Selected);
                Assert.AreEqual(new DateTime(2026, 2, 1), view.Month);
            }
        });
    }

    [TestMethod]
    public void CalendarClicksFollowThePaintedColumnBoundaries()
    {
        RunSta(() =>
        {
            using (var view = new CalendarView { Width = 351 })
            {
                var month = new DateTime(2026, 11, 15);
                var days = CalendarMonth.Days(month, CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek);
                var cellBounds = typeof(CalendarView).GetMethod("CellBounds", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var mouseDown = typeof(CalendarView).GetMethod("OnMouseDown", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                for (int i = 8; i < 14; i++)
                {
                    view.SetSelected(month);
                    var cell = (Rectangle)cellBounds.Invoke(view, new object[] { i });
                    mouseDown.Invoke(view, new object[] { new MouseEventArgs(MouseButtons.Left, 1, cell.Left, cell.Top, 0) });
                    Assert.AreEqual(days[i], view.Selected);
                }
            }
        });
    }

    [TestMethod]
    public void CalendarPanelFitsItsTextsInEveryLanguage()
    {
        RunSta(() =>
        {
            foreach (Func<LanguageScope> scope in new Func<LanguageScope>[] { LanguageScope.French, LanguageScope.English, LanguageScope.Spanish })
                using (scope())
                using (CalendarForm form = new CalendarForm())
                {
                    form.Location = new Point(-4000, -4000);   // affiché hors écran : Visible a alors un sens
                    form.Show();
                    foreach (StartTile tile in form.Controls.OfType<StartTile>().Where(t => t.Visible))
                    {
                        Assert.IsTrue(form.ClientRectangle.Contains(tile.Bounds), L.CurrentLanguage + " : " + tile.Text);
                        Assert.IsTrue(tile.GlyphOnly && !string.IsNullOrEmpty(tile.AccessibleName), "Flèches nommées pour les lecteurs d'écran.");
                    }
                    foreach (Label label in form.Controls.OfType<Label>().Where(l => l.Visible))
                    {
                        Assert.IsTrue(form.ClientRectangle.Contains(label.Bounds), label.Text);
                        Size need = TextRenderer.MeasureText(label.Text, label.Font);
                        Assert.IsTrue(need.Width <= label.Width && need.Height <= label.Height, L.CurrentLanguage + " : texte rogné « " + label.Text + " »");
                    }
                    Assert.IsTrue(form.ClientRectangle.Contains(form.Calendar.Bounds));
                    Assert.AreEqual(2, form.Controls.OfType<StartTile>().Count(), "Le calendrier n'a que ses deux flèches.");
                    Assert.IsFalse(form.Controls.OfType<NotificationList>().Any(), "Pas de notifications dans le panneau de l'horloge.");
                }
        });
    }

    [TestMethod]
    public void NotificationsPanelHasNoCalendar()
    {
        RunSta(() =>
        {
            using (NotificationsForm form = new NotificationsForm())
                Assert.IsFalse(form.Controls.OfType<CalendarView>().Any(), "Pas de calendrier dans le panneau de la cloche.");
        });
    }

    [TestMethod]
    public void ClockShowsSecondsInEveryLanguage()
    {
        DateTime time = new DateTime(2026, 11, 27, 16, 54, 7);
        using (LanguageScope.French()) Assert.AreEqual("16:54:07", CalendarForm.ClockText(time));
        using (LanguageScope.English()) Assert.AreEqual("4:54:07 PM", CalendarForm.ClockText(time));
        using (LanguageScope.Spanish()) Assert.AreEqual("16:54:07", CalendarForm.ClockText(time));
    }

    [TestMethod]
    public void CalendarPanelShowsWeatherAboveTheMonth()
    {
        RunSta(() =>
        {
            using (CalendarForm form = new CalendarForm())
            {
                Assert.IsTrue(form.WeatherView.Bottom < form.Calendar.Top, "Météo au-dessus du calendrier.");
                Assert.IsTrue(form.ClientRectangle.Contains(form.WeatherView.Bounds));
                Assert.IsFalse(string.IsNullOrEmpty(form.WeatherView.AccessibleName), "Météo nommée pour les lecteurs d'écran.");
                if (!Weather.Configured) Assert.IsNotNull(WeatherStrip.Message(), "Sans ville : invitation à en choisir une.");
            }
        });
    }
}
