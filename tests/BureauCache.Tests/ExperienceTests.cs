using System;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;

// T-030 : expérience issue de l'audit T-028 (GEM-07, GEM-08, GEM-10, GEM-11, GEM-12, GEM-14).
[TestClass]
public class ExperienceTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    static void RunSta(Action action)
    {
        Exception error = null;
        Thread t = new Thread(() => { try { action(); } catch (Exception ex) { error = ex; } });
        t.IsBackground = true;
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        Assert.IsTrue(t.Join(10000), "Le scénario WinForms doit se terminer en dix secondes.");
        if (error != null) ExceptionDispatchInfo.Capture(error).Throw();
    }

    static T Field<T>(object o, string name) { return (T)o.GetType().GetField(name, Private).GetValue(o); }
    static void Key(Form f, Keys k) { f.GetType().GetMethod("OnKeyDown", Private).Invoke(f, new object[] { new KeyEventArgs(k) }); }

    [TestMethod]
    public void WelcomeButtonsAreKeyboardReachableAndEnterDoesNotSkipTheirAction()
    {
        RunSta(() =>
        {
            using (WelcomeForm w = new WelcomeForm(null))
            {
                IntPtr h = w.Handle;
                FlatButton skip = Field<FlatButton>(w, "bSkip"), next = Field<FlatButton>(w, "bNext");
                Assert.IsTrue(skip.TabStop && next.TabStop, "GEM-08 : les boutons sont atteignables à la tabulation.");

                // Focus sur « Passer » : Entrée appartient au bouton, la page ne change pas
                w.ActiveControl = skip;
                Key(w, Keys.Enter);
                Assert.AreEqual(0, Field<int>(w, "page"), "GEM-08 : Entrée sur « Passer » ne doit pas déclencher « Suivant ».");

                // Focus sur le choix du bord : les flèches changent le choix, pas la page
                w.ActiveControl = Field<Segmented>(w, "side");
                Key(w, Keys.Right);
                Assert.AreEqual(0, Field<int>(w, "page"));

                // Focus sur « Suivant » non pris par un autre contrôle : → avance toujours
                w.ActiveControl = next;
                Key(w, Keys.Right);
                Assert.AreEqual(1, Field<int>(w, "page"));
            }
        });
    }

    [TestMethod]
    public void WelcomeMentionsF1()
    {
        RunSta(() =>
        {
            using (WelcomeForm w = new WelcomeForm(null))
            {
                string[] texts = (string[])typeof(WelcomeForm).GetProperty("Texts", Private).GetValue(w, null);
                Assert.IsTrue(texts.Any(t => t.Contains("F1")), "GEM-14 : F1 est signalé dans les Premiers pas.");
            }
        });
    }

    [TestMethod]
    public void CustomControlsExposeRoleNameAndState()
    {
        RunSta(() =>
        {
            using (Form f = new Form())
            {
                Toggle t = new Toggle { AccessibleName = "Cacher les icônes" };
                Segmented s = new Segmented("Gauche", "Droite");
                Slider sl = new Slider(0, 10, 1);
                FlatButton b = new FlatButton("Nettoyer", true, 100);
                f.Controls.AddRange(new Control[] { t, s, sl, b });
                IntPtr h = f.Handle;

                Assert.AreEqual(AccessibleRole.CheckButton, t.AccessibilityObject.Role);
                Assert.AreEqual("Cacher les icônes", t.AccessibilityObject.Name);
                Assert.IsFalse((t.AccessibilityObject.State & AccessibleStates.Checked) != 0);
                t.Checked = true;
                Assert.IsTrue((t.AccessibilityObject.State & AccessibleStates.Checked) != 0, "GEM-07 : l'état coché est exposé.");

                s.Selected = 1;
                Assert.AreEqual("Droite", s.AccessibilityObject.Value);
                s.AccessibilityObject.DoDefaultAction();
                Assert.AreEqual(0, s.Selected, "L'action par défaut passe au choix suivant.");

                sl.Value = 7;
                Assert.AreEqual(AccessibleRole.Slider, sl.AccessibilityObject.Role);
                Assert.AreEqual("7", sl.AccessibilityObject.Value);

                Assert.AreEqual(AccessibleRole.PushButton, b.AccessibilityObject.Role);
                Assert.AreEqual("Nettoyer", b.AccessibilityObject.Name);
            }
        });
    }

    static double Luminance(Color c)
    {
        Func<int, double> lin = v => { double x = v / 255.0; return x <= 0.04045 ? x / 12.92 : Math.Pow((x + 0.055) / 1.055, 2.4); };
        return 0.2126 * lin(c.R) + 0.7152 * lin(c.G) + 0.0722 * lin(c.B);
    }

    static double Contrast(Color a, Color b)
    {
        double la = Luminance(a), lb = Luminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    [TestMethod]
    public void SecondaryTextMeetsAaContrastInBothThemes()
    {
        int mode = Settings.ThemeMode;
        try
        {
            foreach (int m in new[] { 1, 2 })   // 1 = sombre, 2 = clair
            {
                Settings.ThemeMode = m;
                foreach (Color bg in new[] { Theme.Bg, Theme.Card, Theme.Surface, Theme.Side })
                    Assert.IsTrue(Contrast(Theme.FgDim, bg) >= 4.5, "GEM-10 : texte secondaire à " + Contrast(Theme.FgDim, bg).ToString("0.00") + ":1 (thème " + m + ").");
            }
        }
        finally { Settings.ThemeMode = mode; }
    }

    [TestMethod]
    public void HighContrastModeUsesSystemColors()
    {
        try
        {
            Theme.HighContrastOverride = true;
            Assert.AreEqual(SystemColors.Window, Theme.Bg);
            Assert.AreEqual(SystemColors.WindowText, Theme.Fg);
            Assert.AreEqual(SystemColors.Highlight, Theme.Accent);
            Theme.HighContrastOverride = false;
            Assert.AreNotEqual(SystemColors.Highlight, Theme.Accent == SystemColors.Highlight ? Color.Empty : Theme.Accent, "Hors contraste élevé, l'accent de BureauCache revient.");
        }
        finally { Theme.HighContrastOverride = null; }
    }

    [TestMethod]
    public void NetworkErrorsAreTranslated()
    {
        string offline = Http.FriendlyError(new WebException("The remote name could not be resolved: 'api.github.com'", WebExceptionStatus.NameResolutionFailure));
        Assert.IsFalse(offline.Contains("remote name"), "GEM-12 : pas de texte système brut.");
        Assert.IsFalse(Http.FriendlyError(new WebException("x", WebExceptionStatus.Timeout)).Contains("x"));
        Assert.AreEqual("Message de BureauCache", Http.FriendlyError(new System.IO.IOException("Message de BureauCache")), "Les messages déjà rédigés restent.");
    }

    [TestMethod]
    public void CleanupFooterGrowsWithAMultiLineStatus()
    {
        RunSta(() =>
        {
            ConstructorInfo ctor = typeof(CleanupDialog).GetConstructor(Private, null, Type.EmptyTypes, null);
            using (Form d = (Form)ctor.Invoke(null))
            {
                IntPtr h = d.Handle;
                Label status = Field<Label>(d, "status");
                int before = d.ClientSize.Height;
                FlatButton button = d.Controls.OfType<FlatButton>().First();
                status.Text = string.Join("\n", Enumerable.Repeat("Cierra Edge, Chrome, Discord y Steam para limpiar su caché.", 6).ToArray());
                Assert.IsTrue(status.Bottom <= d.ClientSize.Height, "GEM-11 : le texte d'état reste entièrement visible.");
                Assert.IsTrue(d.ClientSize.Height > before);
                Assert.IsTrue(button.Bottom <= d.ClientSize.Height, "Les boutons descendent avec le pied de page.");
            }
        });
    }
}
