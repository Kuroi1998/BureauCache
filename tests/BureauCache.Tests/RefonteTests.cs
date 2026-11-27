using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

// T-031, refonte (D-012) : étape 1, défauts visibles sur les captures du chef de projet.
[TestClass]
public class RefonteTests
{
    // Largeur mesurée simplement : 10 px par caractère
    static int M(string s) { return s.Length * 10; }

    [TestMethod]
    public void TileNamesNeverBreakInsideAWord()
    {
        CollectionAssert.AreEqual(new[] { "Steam" }, ItemsView.SplitTileName("Steam", 80, M));
        // « Win-Debloat-Tools-main » : coupure après un tiret, jamais « Win-Debloat-To / ols-main »
        string[] lines = ItemsView.SplitTileName("Win-Debloat-Tools-main", 120, M);
        Assert.AreEqual(2, lines.Length);
        Assert.AreEqual("Win-Debloat-", lines[0]);
        Assert.AreEqual("Tools-main", lines[1]);
        // Un seul long mot : une ligne, que le dessin termine par « … » (pas « ParalivesPhotoM / ode »)
        CollectionAssert.AreEqual(new[] { "ParalivesPhotoMode" }, ItemsView.SplitTileName("ParalivesPhotoMode", 100, M));
        // Plusieurs mots : la coupure tombe à l'espace la plus lointaine qui tient
        CollectionAssert.AreEqual(new[] { "Assassin's Creed", "Shadows" }, ItemsView.SplitTileName("Assassin's Creed Shadows", 170, M));
        CollectionAssert.AreEqual(new[] { "RatioMaster-1.9.", "1" }, ItemsView.SplitTileName("RatioMaster-1.9.1", 160, M));
        CollectionAssert.AreEqual(new[] { "" }, ItemsView.SplitTileName(null, 80, M));
    }

    [TestMethod]
    public void PanelShowsCityAndCountryOnly()
    {
        Assert.AreEqual("Saint-Ghislain, Belgique", Weather.ShortPlace("Saint-Ghislain, wallonne, Région, Belgique"), "Lieu enregistré par la 3.10.");
        Assert.AreEqual("Mons, Belgique", Weather.ShortPlace("Mons, Région wallonne, Belgique"));
        Assert.AreEqual("Paris, France", Weather.ShortPlace("Paris, France"));
        Assert.AreEqual("Lima", Weather.ShortPlace("Lima"));
        Assert.AreEqual("", Weather.ShortPlace(null));
    }

    static void RunSta(Action action)
    {
        Exception error = null;
        Thread t = new Thread(() => { try { action(); } catch (Exception ex) { error = ex; } });
        t.IsBackground = true;
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        Assert.IsTrue(t.Join(10000));
        if (error != null) ExceptionDispatchInfo.Capture(error).Throw();
    }

    [TestMethod]
    public void ClipboardGroupIsAListAndRecentsSkipSystemItems()
    {
        RunSta(() =>
        {
            bool clip = Settings.ClipboardOn, recents = Settings.ShowRecents;
            Dictionary<string, long[]> usage = Settings.Usage;
            long since = Settings.RecentsSince;
            ClipboardHistory.Clear();
            try
            {
                Settings.ClipboardOn = true;
                ClipboardHistory.Add("un texte");
                using (PanelForm panel = new PanelForm())
                {
                    BindingFlags priv = BindingFlags.Instance | BindingFlags.NonPublic;
                    Group g = (Group)typeof(PanelForm).GetMethod("ClipGroup", priv).Invoke(panel, new object[] { 16 });
                    Assert.IsTrue(g.AsList, "Le presse-papiers est une liste compacte, même en vue grille.");

                    // La Corbeille ouverte récemment n'apparaît pas dans les Récents (elle a déjà son widget)
                    DesktopItem bin = new DesktopItem { Key = "::corbeille", Name = "Corbeille", IsSystem = true };
                    DesktopItem doc = new DesktopItem { Key = @"C:\Bureau\rapport.docx", Name = "rapport.docx" };
                    panel.All = new List<DesktopItem> { bin, doc };
                    Settings.ShowRecents = true;
                    Settings.RecentsSince = 0;
                    Settings.Usage = new Dictionary<string, long[]>(StringComparer.OrdinalIgnoreCase);
                    Settings.Usage[bin.Key] = new long[] { 3, DateTime.Now.Ticks };
                    Settings.Usage[doc.Key] = new long[] { 1, DateTime.Now.Ticks };
                    List<Group> groups = (List<Group>)typeof(PanelForm).GetMethod("BuildGroups", priv).Invoke(panel, null);
                    Group rec = groups.First(x => x.Key == "recent");
                    CollectionAssert.AreEqual(new[] { "rapport.docx" }, rec.Items.Select(i => i.Name).ToArray());
                }
            }
            finally
            {
                ClipboardHistory.Clear();
                Settings.ClipboardOn = clip; Settings.ShowRecents = recents; Settings.Usage = usage; Settings.RecentsSince = since;
            }
        });
    }
}
