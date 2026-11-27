using System;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

// T-038 : visite guidée mise à jour (6 écrans, barre des tâches, petits plus)
[TestClass]
public class WelcomeTourTests
{
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    static void RunSta(Action body)
    {
        Exception error = null;
        Thread t = new Thread(() => { try { body(); } catch (Exception ex) { error = ex; } });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
        if (error != null) ExceptionDispatchInfo.Capture(error).Throw();
    }

    static string[] Strings(WelcomeForm w, string property) { return (string[])typeof(WelcomeForm).GetProperty(property, Private).GetValue(w, null); }

    [TestMethod]
    public void TourHasSixScreensWithTitleAndText()
    {
        RunSta(() =>
        {
            using (WelcomeForm w = new WelcomeForm(null))
            {
                Assert.AreEqual(6, w.PageCount);
                Assert.AreEqual(6, Strings(w, "Titles").Length);
                Assert.AreEqual(6, Strings(w, "Texts").Length);
            }
        });
    }

    [TestMethod]
    public void TourPresentsTheNewFeatures()
    {
        RunSta(() =>
        {
            using (BureauCache.Tests.LanguageScope.French())
            using (WelcomeForm w = new WelcomeForm(null))
            {
                string all = string.Join(" ", Strings(w, "Titles").Concat(Strings(w, "Texts")).Concat(
                    ((string[][])typeof(WelcomeForm).GetProperty("Tips", Private).GetValue(w, null)).SelectMany(t => t)));
                foreach (string feature in new[] { "barre des tâches", "menu Démarrer", "épingl", "aperçu", "Rétablir la barre", "Météo",
                                                   "presse-papiers", "Gestionnaire des tâches", "Rangement automatique", "texte des documents", "F1" })
                    StringAssert.Contains(all, feature, "La visite doit présenter : " + feature);
            }
        });
    }

    [TestMethod]
    public void TipsNeverOverlapThePageDotsInAnyLanguage()
    {
        RunSta(() =>
        {
            Func<IDisposable>[] scopes = { () => BureauCache.Tests.LanguageScope.French(), () => BureauCache.Tests.LanguageScope.English(), () => BureauCache.Tests.LanguageScope.Spanish() };
            foreach (Func<IDisposable> scope in scopes)
                using (scope())
                using (WelcomeForm w = new WelcomeForm(null))
                    for (int p = 0; p < w.PageCount; p++)
                        Assert.IsTrue(w.TipsBottom(p) <= w.DotsTop, L.CurrentLanguage + ", écran " + (p + 1) + " : " + w.TipsBottom(p) + " > " + w.DotsTop);
        });
    }

    [TestMethod]
    public void TaskbarOptionUsesTheSameTextsAsSettings()
    {
        RunSta(() =>
        {
            using (WelcomeForm w = new WelcomeForm(null))
            {
                System.Windows.Forms.Label label = w.Controls.OfType<System.Windows.Forms.Label>().FirstOrDefault(l => l.Text == TaskbarConsent.Label);
                Assert.IsNotNull(label, "Même libellé que dans les Paramètres.");
                Assert.IsFalse(Settings.TaskbarOn, "Ouvrir la visite n'active rien.");
            }
        });
    }
}
