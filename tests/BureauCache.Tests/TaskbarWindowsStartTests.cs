using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;

// T-033 et T-034 : fenêtres ouvertes dans la barre, menu Démarrer (compte et alimentation). Aucune action réelle :
// le bac à sable bloque l'activation, la fermeture, le verrouillage, la déconnexion, la veille et l'arrêt.
[TestClass]
public class TaskbarWindowsStartTests
{
    [TestMethod]
    public void StartMenuStaysInsideAShortWorkingAreaAndKeepsAllTiles()
    {
        using (StartMenuForm menu = new StartMenuForm())
        {
            Size content = menu.ClientSize;
            Rectangle area = new Rectangle(-700, 80, 620, 320);
            Rectangle start = new Rectangle(-120, 390, 40, 50);
            menu.PlaceAbove(start, area);
            Assert.IsTrue(area.Contains(menu.Bounds));
            Assert.IsTrue(menu.AutoScroll);
            Assert.AreEqual(content, menu.AutoScrollMinSize);
            Assert.IsTrue(menu.ClientSize.Height < content.Height);
        }
    }
    const long ToolWindow = 0x80, AppWindow = 0x40000, NoActivate = 0x08000000, Child = 0x40000000;

    static bool Q(bool visible = true, bool cloaked = false, long style = 0, long ex = 0, bool owned = false, int title = 5, string cls = "Notepad")
    {
        return WindowList.Qualifies(visible, cloaked, style, ex, owned, title, cls);
    }

    [TestMethod]
    public void OrdinaryTopLevelWindowGetsAButton()
    {
        Assert.IsTrue(Q());
    }

    [TestMethod]
    public void HiddenCloakedChildUntitledWindowsAreSkipped()
    {
        Assert.IsFalse(Q(visible: false));
        Assert.IsFalse(Q(cloaked: true), "Autre bureau virtuel ou application du Store suspendue.");
        Assert.IsFalse(Q(style: Child));
        Assert.IsFalse(Q(title: 0));
    }

    [TestMethod]
    public void ToolAndOwnedWindowsAreSkippedUnlessTheyAskForAButton()
    {
        Assert.IsFalse(Q(ex: ToolWindow), "Panneau, menus, barres flottantes.");
        Assert.IsFalse(Q(ex: NoActivate));
        Assert.IsFalse(Q(owned: true), "Boîte de dialogue : son propriétaire a déjà un bouton.");
        Assert.IsTrue(Q(ex: AppWindow | ToolWindow, owned: true), "WS_EX_APPWINDOW demande explicitement un bouton.");
        Assert.IsTrue(Q(ex: AppWindow, title: 0));
    }

    [TestMethod]
    public void ShellWindowsNeverAppear()
    {
        foreach (string cls in new[] { "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Windows.UI.Core.CoreWindow" })
            Assert.IsFalse(Q(ex: AppWindow, cls: cls), cls);
    }

    [TestMethod]
    public void ShutdownFlagsFollowWindows()
    {
        Assert.AreEqual(Native.SHUTDOWN_RESTART, SessionActions.ShutdownFlags(true, false, true), "Pas d'arrêt rapide pour un redémarrage.");
        Assert.AreEqual(Native.SHUTDOWN_POWEROFF, SessionActions.ShutdownFlags(false, false, false));
        Assert.AreEqual(Native.SHUTDOWN_POWEROFF | Native.SHUTDOWN_HYBRID, SessionActions.ShutdownFlags(false, false, true), "Démarrage rapide.");
        Assert.AreEqual(Native.SHUTDOWN_RESTART | Native.SHUTDOWN_INSTALL_UPDATES, SessionActions.ShutdownFlags(true, true, false), "Mettre à jour et redémarrer.");
        Assert.AreEqual(Native.SHUTDOWN_POWEROFF | Native.SHUTDOWN_INSTALL_UPDATES | Native.SHUTDOWN_HYBRID, SessionActions.ShutdownFlags(false, true, true));
    }

    [TestMethod]
    public void InitialsComeFromTheDisplayName()
    {
        Assert.AreEqual("SL", SessionActions.Initials("Serguei Lété"));
        Assert.AreEqual("JP", SessionActions.Initials("jean-pierre martin"));
        Assert.AreEqual("A", SessionActions.Initials("admin"));
        Assert.AreEqual("?", SessionActions.Initials("  "));
        Assert.AreEqual("?", SessionActions.Initials(null));
    }

    [TestMethod]
    public void SessionAndWindowActionsAreBlockedInTests()
    {
        Assert.IsTrue(Sandbox.Active);
        Assert.ThrowsException<InvalidOperationException>(() => SessionActions.Lock());
        Assert.ThrowsException<InvalidOperationException>(() => SessionActions.SignOut());
        Assert.ThrowsException<InvalidOperationException>(() => SessionActions.Sleep());
        Assert.ThrowsException<InvalidOperationException>(() => SessionActions.Restart());
        Assert.ThrowsException<InvalidOperationException>(() => SessionActions.PowerOff());
        Assert.ThrowsException<InvalidOperationException>(() => WindowList.Activate(new IntPtr(1), false));
        Assert.ThrowsException<InvalidOperationException>(() => WindowList.Close(new IntPtr(1)));
    }

    [TestMethod]
    public void StartMenuOffersAccountAndPowerAsAccessibleButtons()
    {
        using (StartMenuForm menu = new StartMenuForm())
        {
            StartTile[] tiles = menu.Controls.OfType<StartTile>().ToArray();
            Assert.AreEqual(11, tiles.Length, "6 raccourcis, 2 actions de compte, 3 d'alimentation.");
            string[] names = tiles.Select(t => t.Text).ToArray();
            foreach (string expected in new[] { L.T("Verrouiller", "Lock", "Bloquear"), L.T("Se déconnecter", "Sign out", "Cerrar sesión"),
                                                L.T("Mettre en veille", "Sleep", "Suspender") })
                CollectionAssert.Contains(names, expected);
            Assert.IsTrue(names.Any(n => n.Contains(L.T("redémarrer", "restart", "reiniciar")) || n == L.T("Redémarrer", "Restart", "Reiniciar")));
            Assert.IsTrue(tiles.All(t => t.TabStop), "Chaque tuile est atteignable au clavier.");
            Assert.IsTrue(tiles.All(t => t.AccessibilityObject.Role == AccessibleRole.PushButton), "Lue comme un bouton.");
        }
    }

    [TestMethod]
    public void TypingInTheStartMenuSendsTheTextToThePanelSearch()
    {
        using (StartMenuForm menu = new StartMenuForm())
        {
            string received = null;
            menu.SearchRequested += t => received = t;
            TextBox box = menu.Controls.OfType<TextBox>().Single();
            box.Text = "   ";
            Assert.IsNull(received, "Des espaces seuls ne lancent rien.");
            int calls = 0;
            menu.SearchRequested += t => calls++;
            box.Text = "calc";
            Assert.AreEqual("calc", received);
            try { box.Text = "calcu"; } catch (ObjectDisposedException) { }
            Assert.AreEqual(1, calls, "QA-66 : une seconde frappe rapide ne relance pas la recherche.");
        }
    }

    [TestMethod]
    public void StartMenuIsLargeEnoughForTranslatedLabels()
    {
        using (StartMenuForm menu = new StartMenuForm())
            foreach (StartTile t in menu.Controls.OfType<StartTile>())
            {
                // Le texte doit tenir en entier dans la zone où la tuile le dessine (essai : « Applications et
                // recherche » et « Gestionnaire des tâches » coupés en bas, alors que l'ancien test passait)
                Assert.IsTrue(t.TextFits, t.Text);
                Assert.IsTrue(t.TextArea.Bottom <= t.Height, t.Text);
                Assert.IsTrue(t.Height >= Ui.S(44), t.Text);
            }
    }

    [TestMethod]
    public void LongestTranslatedLabelsFitInEveryLanguage()
    {
        Func<IDisposable>[] scopes = { () => BureauCache.Tests.LanguageScope.French(), () => BureauCache.Tests.LanguageScope.English(), () => BureauCache.Tests.LanguageScope.Spanish() };
        foreach (Func<IDisposable> scope in scopes)
            using (scope())
            using (StartMenuForm menu = new StartMenuForm())
                foreach (StartTile t in menu.Controls.OfType<StartTile>())
                    Assert.IsTrue(t.TextFits, L.CurrentLanguage + " : " + t.Text);
    }

    [TestMethod]
    public void PreviewDoesNothingInTests()
    {
        using (WindowPreview p = new WindowPreview())
        {
            p.ShowFor(new IntPtr(1), "Titre", null, new System.Drawing.Rectangle(0, 0, 40, 40));
            Assert.IsFalse(p.Visible, "Bac à sable : aucune vignette DWM ni fenêtre affichée.");
            Assert.AreEqual(IntPtr.Zero, p.Target);
        }
    }

    [TestMethod]
    public void WindowListDoesNotHookWindowsInTests()
    {
        using (WindowList list = new WindowList(h => false))
        {
            list.Start();   // bac à sable : aucun crochet posé, aucune énumération
            Assert.AreEqual(0, list.Items.Count);
        }
    }
}
