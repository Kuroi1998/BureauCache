using System;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;

// T-033 et T-034 : fenêtres ouvertes dans la barre, menu Démarrer (compte et alimentation). Aucune action réelle :
// le bac à sable bloque l'activation, la fermeture, le verrouillage, la déconnexion, la veille et l'arrêt.
[TestClass]
public class TaskbarWindowsStartTests
{
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
            Assert.AreEqual(8, tiles.Length, "3 raccourcis, 2 actions de compte, 3 d'alimentation.");
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
    public void WindowListDoesNotHookWindowsInTests()
    {
        using (WindowList list = new WindowList(h => false))
        {
            list.Start();   // bac à sable : aucun crochet posé, aucune énumération
            Assert.AreEqual(0, list.Items.Count);
        }
    }
}
