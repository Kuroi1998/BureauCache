using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class ShellReviewTests
{
    const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;
    const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;

    // Public pour que le binder dynamique de ShellUtil puisse appeler DoIt.
    public sealed class VerbProbe
    {
        readonly Action action;
        public VerbProbe(Action action) { this.action = action; }
        public void DoIt() { action(); }
    }

    [TestMethod]
    [TestCategory("Review")]
    [Timeout(20000)]
    public void Q28_01_WorkerCanDeliverAfterItsMessageLoopHasExited()
    {
        var control = typeof(ShellWorker).GetField("invoker", StaticPrivate);
        object previous = control.GetValue(null);
        control.SetValue(null, null);
        Thread worker = null;
        try
        {
            OnWorker(() =>
            {
                worker = Thread.CurrentThread;
                Application.ExitThread();
            });
            Assert.IsTrue(worker.Join(5000), "Q28-01 : la première boucle doit s'arrêter.");
            OnWorker(() => Assert.AreNotSame(worker, Thread.CurrentThread,
                "Q28-01 : la demande doit reprendre sur un fil vivant."));
        }
        finally
        {
            object replacement = control.GetValue(null);
            if (replacement != null && !ReferenceEquals(previous, replacement))
            {
                var invoker = (Control)replacement;
                if (!invoker.IsDisposed && invoker.IsHandleCreated)
                {
                    try { invoker.BeginInvoke(new Action(Application.ExitThread)); }
                    catch (InvalidOperationException) { }
                }
            }
            control.SetValue(null, previous);
        }
    }

    [TestMethod]
    [TestCategory("Review")]
    [Timeout(20000)]
    public void Q28_02_AnOldMenuIdCannotInvokeAVerbInTheNextSession()
    {
        int calls = 0;
        WithVerbs(new VerbProbe(() => calls++), id =>
        {
            OnWorker(() => typeof(ShellWorker).GetField("session", StaticPrivate).SetValue(null, id + 1));
            ShellWorker.Invoke(id, 0);
            OnWorker(() => Assert.AreEqual(0, calls, "Q28-02 : l'ancien ID a exécuté le verbe de la nouvelle session."));
            ShellWorker.Invoke(id + 1, 0);
            OnWorker(() => Assert.AreEqual(1, calls, "Q28-02 : le choix de la session courante doit fonctionner."));
        });
    }

    [TestMethod]
    [TestCategory("Review")]
    [Timeout(20000)]
    public void Q28_03_ACommandMustNotEnterWhileTheFirstCommandPumpsMessages()
    {
        using (var entered = new ManualResetEvent(false))
        using (var queued = new ManualResetEvent(false))
        using (var completed = new ManualResetEvent(false))
        {
            bool running = false, nested = false;
            var verb = new VerbProbe(() =>
            {
                if (running) { nested = true; return; }
                running = true;
                entered.Set();
                try { if (queued.WaitOne(5000)) Application.DoEvents(); }
                finally { running = false; completed.Set(); }
            });
            WithVerbs(verb, id =>
            {
                ShellWorker.Invoke(id, 0);
                try
                {
                    Assert.IsTrue(entered.WaitOne(5000), "Q28-03 : le premier verbe doit démarrer.");
                    ShellWorker.Invoke(id, 0);
                }
                finally { queued.Set(); }
                Assert.IsTrue(completed.WaitOne(5000));
                OnWorker(() => Assert.IsFalse(nested,
                    "Q28-03 : la seconde commande est entrée pendant la boucle imbriquée du premier verbe."));
            });
        }
    }

    [TestMethod]
    [TestCategory("Review")]
#if !WINDOWS_INTERACTIVE_TESTS
    [Ignore("Q28-04 : ouvre et ferme un vrai menu WinForms sur le bureau interactif.")]
#endif
    [TestCategory("WindowsInteractive")]
    public void Q28_04_ClosingTheSubmenuDiscardsItsPendingResult()
    {
        RunSta(() =>
        {
            using (var panel = new PanelForm())
            using (var hold = new ManualResetEvent(false))
            {
                Post(() => hold.WaitOne(5000));
                try
                {
                    panel.Show();
                    var missing = new DesktopItem
                    {
                        Kind = ItemKind.Recent,
                        Target = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "absent.txt")
                    };
                    typeof(PanelForm).GetMethod("ShowItemMenu", InstancePrivate)
                        .Invoke(panel, new object[] { missing, new Point(5, 5) });
                    var menu = (ContextMenuStrip)typeof(PanelForm).GetField("menu", InstancePrivate).GetValue(panel);
                    var more = (ToolStripMenuItem)menu.Items[menu.Items.Count - 1];
                    more.ShowDropDown();
                    Assert.IsTrue(more.DropDown.Visible);
                    more.HideDropDown();
                    ToolStripItem[] afterClose = more.DropDownItems.Cast<ToolStripItem>().ToArray();
                    hold.Set();
                    OnWorker(() => { });
                    Application.DoEvents();
                    Assert.IsFalse(more.DropDown.Visible);
                    CollectionAssert.AreEqual(afterClose, more.DropDownItems.Cast<ToolStripItem>().ToArray(),
                        "Q28-04 : une réponse tardive a modifié le contenu du sous-menu déjà fermé.");
                }
                finally { hold.Set(); }
            }
        });
    }

    [TestMethod]
    [TestCategory("Review")]
    public void Q28_05_DisplayRebuildKeepsTheUncommittedRepositoryEdit()
    {
        RunSta(() =>
        {
            using (var panel = new PanelForm())
            using (var previous = new SettingsForm(new SettingsActions()))
            {
                previous.SelectPage(5);
                TextBox input = RepositoryBox(previous);
                const string draft = "owner/uncommitted-review-draft";
                input.Text = draft;
                input.Select(6, 11);
                Assert.AreNotEqual(Settings.UpdateRepo, input.Text);
                App app = AppWithSettings(panel, previous);
                SettingsForm replacement = null;
                try
                {
                    SignalDisplayChange(app);
                    replacement = WaitForReplacement(app, previous);
                    Assert.AreEqual(5, replacement.CurrentPage);
                    TextBox target = RepositoryBox(replacement);
                    Assert.AreEqual(draft, target.Text, "Q28-05 : la saisie en cours a disparu.");
                    Assert.AreEqual(6, target.SelectionStart, "Q28-05 : la position d'édition a disparu.");
                    Assert.AreEqual(11, target.SelectionLength);
                }
                finally { if (replacement != null) replacement.Dispose(); }
            }
        });
    }

    [TestMethod]
    [TestCategory("Review")]
    public void Q28_06_DisplayRebuildMovesSettingsOntoAConnectedScreen()
    {
        RunSta(() =>
        {
            using (var panel = new PanelForm())
            using (var previous = new SettingsForm(new SettingsActions()))
            {
                previous.StartPosition = FormStartPosition.Manual;
                Rectangle desktop = SystemInformation.VirtualScreen;
                previous.Location = new Point(desktop.Right + 500, desktop.Bottom + 500);
                Assert.IsFalse(Screen.AllScreens.Any(screen => screen.WorkingArea.IntersectsWith(previous.Bounds)));
                App app = AppWithSettings(panel, previous);
                SettingsForm replacement = null;
                try
                {
                    SignalDisplayChange(app);
                    replacement = WaitForReplacement(app, previous);
                    Assert.IsTrue(Screen.AllScreens.Any(screen => screen.WorkingArea.IntersectsWith(replacement.Bounds)),
                        "Q28-06 : la fenêtre reconstruite reste hors de tous les écrans connectés.");
                }
                finally { if (replacement != null) replacement.Dispose(); }
            }
        });
    }

    [TestMethod]
    [TestCategory("Review")]
    public void Q28_07_RepeatedIdenticalDisplayNotificationKeepsTheReplacement()
    {
        RunSta(() =>
        {
            using (var panel = new PanelForm())
            using (var previous = new SettingsForm(new SettingsActions()))
            {
                App app = AppWithSettings(panel, previous);
                SignalDisplayChange(app);
                SignalDisplayChange(app);
                using (SettingsForm replacement = WaitForReplacement(app, previous))
                {
                    SignalDisplayChange(app);
                    Application.DoEvents();
                    Assert.AreSame(replacement, typeof(App).GetField("settingsForm", InstancePrivate).GetValue(app),
                        "Q28-07 : une notification identique a recréé une deuxième fenêtre.");
                }
            }
        });
    }

    static App AppWithSettings(PanelForm panel, SettingsForm settings)
    {
        // Initialise uniquement le parcours de reconstruction. Le constructeur
        // normal installe les raccourcis, l'icône et les surveillances du bureau.
        var app = (App)FormatterServices.GetUninitializedObject(typeof(App));
        IntPtr handle = panel.Handle;
        typeof(App).GetField("panel", InstancePrivate).SetValue(app, panel);
        typeof(App).GetField("settingsForm", InstancePrivate).SetValue(app, settings);
        typeof(App).GetField("screens", InstancePrivate).SetValue(app, Screen.AllScreens);
        typeof(App).GetField("screenSignature", InstancePrivate).SetValue(app, "previous-topology");
        return app;
    }

    static void SignalDisplayChange(App app)
    {
        typeof(App).GetMethod("OnDisplaysChanged", InstancePrivate).Invoke(app, null);
    }

    static SettingsForm WaitForReplacement(App app, SettingsForm previous)
    {
        var field = typeof(App).GetField("settingsForm", InstancePrivate);
        var clock = Stopwatch.StartNew();
        while (clock.ElapsedMilliseconds < 5000)
        {
            Application.DoEvents();
            var current = (SettingsForm)field.GetValue(app);
            if (current != null && !ReferenceEquals(previous, current)) return current;
            Thread.Sleep(10);
        }
        Assert.Fail("La reconstruction des Paramètres n'a pas abouti.");
        return null;
    }

    static TextBox RepositoryBox(SettingsForm form)
    {
        return ((InputBox)typeof(SettingsForm).GetField("repo", InstancePrivate).GetValue(form)).Box;
    }

    static void WithVerbs(VerbProbe probe, Action<int> test)
    {
        var session = typeof(ShellWorker).GetField("session", StaticPrivate);
        var verbs = typeof(ShellWorker).GetField("verbs", StaticPrivate);
        int previousId = 0;
        object previousVerbs = null;
        OnWorker(() =>
        {
            previousId = (int)session.GetValue(null);
            previousVerbs = verbs.GetValue(null);
            session.SetValue(null, previousId + 1);
            verbs.SetValue(null, new List<object> { probe });
        });
        try { test(previousId + 1); }
        finally
        {
            OnWorker(() =>
            {
                session.SetValue(null, previousId);
                verbs.SetValue(null, previousVerbs);
            });
        }
    }

    static void Post(Action action)
    {
        typeof(ShellWorker).GetMethod("Post", StaticPrivate).Invoke(null, new object[] { action });
    }

    static void OnWorker(Action action)
    {
        var done = new System.Threading.Tasks.TaskCompletionSource<Exception>();
        Post(() =>
        {
            try { action(); done.SetResult(null); }
            catch (Exception ex) { done.SetResult(ex); }
        });
        Assert.IsTrue(done.Task.Wait(5000), "Le fil STA n'a pas exécuté la demande.");
        if (done.Task.Result != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(done.Task.Result).Throw();
    }

    static void RunSta(Action action)
    {
        Exception error = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { error = ex; }
        });
        thread.IsBackground = true;
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.IsTrue(thread.Join(12000), "Le scénario WinForms a dépassé douze secondes.");
        if (error != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
    }
}
