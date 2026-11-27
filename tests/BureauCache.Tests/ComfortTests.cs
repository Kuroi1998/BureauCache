using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;

// T-023 : détail météo non modal, lieu cherché dans les Paramètres, clavier (F6) dans le panneau
[TestClass]
public class ComfortTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;

    [TestMethod]
    public void WeatherDetailIsNonModalAndUnique()
    {
        RunSta(() =>
        {
            FieldInfo current = typeof(WeatherDetailDialog).GetField("current", StaticPrivate);
            WeatherDetailDialog.Open(null);   // revient tout de suite : la fenêtre ne bloque pas
            var first = (Form)current.GetValue(null);
            Assert.IsNotNull(first);
            Assert.IsTrue(first.Visible && !first.Modal);
            WeatherDetailDialog.Open(null);
            var second = (Form)current.GetValue(null);
            Assert.AreNotSame(first, second, "Une seule fenêtre à la fois : la précédente est fermée.");
            Assert.IsTrue(first.IsDisposed);
            second.Close();
            Application.DoEvents();
            Assert.IsTrue(second.IsDisposed);
            Assert.IsNull(current.GetValue(null));
        });
    }

    [TestMethod]
    public void PlaceMenuAlwaysOffersDetectionAndListsResults()
    {
        RunSta(() =>
        {
            using (var form = new SettingsForm(new SettingsActions()))
            {
                IntPtr h = form.Handle;
                MethodInfo show = typeof(SettingsForm).GetMethod("ShowPlaces", Private);
                FieldInfo search = typeof(SettingsForm).GetField("placeSearch", Private);
                search.SetValue(form, 1);
                var places = new List<WeatherPlace> { new WeatherPlace { Name = "Mons", Country = "Belgique", Lat = 50.45, Lon = 3.95 } };
                show.Invoke(form, new object[] { 1, places, null });
                var menu = (ContextMenuStrip)typeof(SettingsForm).GetField("placeMenu", Private).GetValue(form);
                var items = menu.Items.OfType<ToolStripMenuItem>().ToList();
                Assert.AreEqual(2, items.Count, "Un résultat puis « Détecter ma position ».");
                StringAssert.Contains(items[0].Text, "Mons");
                menu.Close();
                show.Invoke(form, new object[] { 0, new List<WeatherPlace>(), null });   // recherche dépassée : ignorée
                Assert.AreEqual(2, menu.Items.OfType<ToolStripMenuItem>().Count());
            }
        });
    }

    [TestMethod]
    public void F6CyclesThroughTopHotspotsAndEscapeLeaves()
    {
        RunSta(() =>
        {
            using (var panel = new PanelForm())
            {
                IntPtr h = panel.Handle;
                MethodInfo move = typeof(PanelForm).GetMethod("MoveChromeFocus", Private);
                MethodInfo key = typeof(PanelForm).GetMethod("HandleChromeKey", Private);
                FieldInfo focus = typeof(PanelForm).GetField("chromeFocus", Private);
                var order = (List<int>)typeof(PanelForm).GetMethod("ChromeOrder", Private).Invoke(panel, null);
                Assert.IsTrue(order.Contains(1) && order.Contains(5) && order.Contains(0));
                move.Invoke(panel, new object[] { 1 });
                Assert.AreEqual(order[0], (int)focus.GetValue(panel));
                move.Invoke(panel, new object[] { -1 });   // depuis le premier, un pas en arrière mène au dernier
                Assert.AreEqual(order[order.Count - 1], (int)focus.GetValue(panel), "Le parcours boucle.");
                Assert.IsTrue((bool)key.Invoke(panel, new object[] { Keys.Escape, false }));
                Assert.AreEqual(-1, (int)focus.GetValue(panel));
                move.Invoke(panel, new object[] { 1 });
                Assert.IsFalse((bool)key.Invoke(panel, new object[] { Keys.A, false }), "Une autre touche rend la main.");
                Assert.AreEqual(-1, (int)focus.GetValue(panel));
            }
        });
    }

    static void RunSta(Action action)
    {
        Exception error = null;
        var thread = new Thread(() => { try { action(); } catch (Exception ex) { error = ex; } });
        thread.IsBackground = true;
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.IsTrue(thread.Join(20000), "Le scénario WinForms a dépassé 20 secondes.");
        if (error != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
    }
}
