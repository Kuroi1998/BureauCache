using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class AuditRefonteReviewTests
{
    const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [TestInitialize]
    public void RequireIsolation()
    {
        Assert.IsTrue(Sandbox.Active);
        Assert.IsNotNull(Paths.DesktopOverride);
        Assert.IsNotNull(Settings.DirOverride);
        Sandbox.GuardPath(Paths.DesktopOverride, "audit desktop");
        Sandbox.GuardPath(Settings.DirOverride, "audit settings");
    }

    [TestMethod]
    public void QA52_01_ImportPreservesActiveLocalArchivePolicy()
    {
        List<ArchiveRule> previousRules = Settings.ArchiveRules;
        bool previousArchiveOn = Settings.ArchiveOn;
        List<string> previousLog = Settings.ArchiveLog;
        string previousRepo = Settings.UpdateRepo;
        bool previousUpdates = Settings.AutoUpdate, previousClipboard = Settings.ClipboardOn;
        try
        {
            Settings.ArchiveOn = true;
            ArchiveRule rule = new ArchiveRule { Name = "Local", Pattern = "txt", Days = 30 };
            Settings.ArchiveRules = new List<ArchiveRule> { rule };

            // Exercise only the import policy. Settings.Load reads the real theme from HKCU.
            Type policy = typeof(SettingsBackup).GetNestedType("LocalChoices", BindingFlags.NonPublic);
            object local = policy.GetMethod("Capture").Invoke(null, null);
            Settings.ArchiveRules = new List<ArchiveRule>();
            policy.GetMethod("Reapply").Invoke(local, new object[] { new List<string>() });

            Assert.IsTrue(Settings.ArchiveOn);
            Assert.AreEqual(1, Settings.ArchiveRules.Count, "Portable preferences must not replace active local routing rules.");
            Assert.AreEqual(rule.Serialize(), Settings.ArchiveRules[0].Serialize());
        }
        finally
        {
            Settings.ArchiveRules = previousRules;
            Settings.ArchiveOn = previousArchiveOn;
            Settings.ArchiveLog = previousLog;
            Settings.UpdateRepo = previousRepo;
            Settings.AutoUpdate = previousUpdates;
            Settings.ClipboardOn = previousClipboard;
        }
    }

    [TestMethod]
    public void QA52_02_WelcomeToggleHandlesEnterExactlyOnce()
    {
        RunSta(delegate
        {
            using (KeyboardToggle toggle = new KeyboardToggle())
            {
                toggle.EnableSettingsKeyboard();
                int changes = 0;
                toggle.Changed += delegate { changes++; };
                toggle.Press(Keys.Space);
                Assert.IsTrue(toggle.Checked);
                Assert.AreEqual(1, changes);
                bool enterIsInput = toggle.TakesInput(Keys.Enter);
                KeyEventArgs enter = toggle.Press(Keys.Enter);
                Assert.AreEqual(2, changes, "Enter must activate the focused toggle exactly once.");
                Assert.IsFalse(toggle.Checked);
                Assert.IsTrue(enterIsInput && enter.Handled);
            }
        });
    }

    [TestMethod]
    public void QA52_03_RepaintingDoesNotRecomputeAccessibleValue()
    {
        RunSta(delegate
        {
            using (AccessibleProbe control = new AccessibleProbe())
            {
                Assert.AreNotEqual(IntPtr.Zero, control.Handle);
                control.Invalidate();
                control.ValueReads = 0;
                for (int frame = 0; frame < 12; frame++) control.Invalidate();
                Assert.AreEqual(0, control.ValueReads, "Painting an unchanged control should not query its accessible value.");
            }
        });
    }

    [TestMethod]
    public void QA52_04_GridDoesNotOfferClipboardAsDropTarget()
    {
        RunSta(delegate
        {
            bool previousGrid = Settings.GridView;
            try
            {
                Settings.GridView = true;
                using (ItemsView view = new ItemsView(null))
                {
                    view.Size = new Size(340, 330);
                    Group desktop = new Group { Key = "qa52:desktop" };
                    desktop.Items.Add(new DesktopItem { Key = "qa52:file", Kind = ItemKind.Desktop });
                    Group clipboard = new Group { Key = "clip", AsList = true };
                    clipboard.Items.Add(new DesktopItem { Key = "qa52:clip", Kind = ItemKind.Clip });
                    view.SetGroups(new List<Group> { desktop, clipboard }, false);

                    IList rows = (IList)ViewField("rows").GetValue(view);
                    ViewField("dragRow").SetValue(view, rows[0]);
                    MethodInfo compute = typeof(ItemsView).GetMethod("ComputeDrop", PrivateInstance);
                    compute.Invoke(view, new object[] { CenterOf(rows[0]) });
                    Assert.AreSame(desktop, ViewField("dropGroup").GetValue(view));
                    compute.Invoke(view, new object[] { CenterOf(rows[1]) });
                    Assert.IsNull(ViewField("dropGroup").GetValue(view), "Clipboard rows cannot accept a reordered file.");
                }
            }
            finally { Settings.GridView = previousGrid; }
        });
    }

    [TestMethod]
    public void QA52_05_WeatherShortLabelKeepsCommaInsideCityName()
    {
        WeatherPlace place = new WeatherPlace { Name = "Ville, secteur Nord", Admin1 = "Province", Country = "Belgique" };
        Assert.AreEqual("Ville, secteur Nord, Province, Belgique", place.Label);
        // Tri de QA52-05 (Claude) : le libellé seul est ambigu (une virgule peut être dans le nom comme dans la région,
        // cas réel « Saint-Ghislain, wallonne, Région, Belgique ») ; la forme courte est construite à partir des champs
        // et enregistrée au choix du lieu (Settings.WeatherShort).
        Assert.AreEqual("Ville, secteur Nord, Belgique", place.Short);
    }

    [TestMethod]
    public void QA52_06_ExpiredDownloadDoesNotSucceedAtEndOfStream()
    {
        Stopwatch clock = Stopwatch.StartNew();
        Assert.IsTrue(SpinWait.SpinUntil(delegate { return clock.ElapsedMilliseconds > 0; }, 1000));
        using (MemoryStream input = new MemoryStream())
        using (MemoryStream output = new MemoryStream())
        {
            Updater.CopyLimited(input, output, 32, clock, 60000);
            Assert.ThrowsException<IOException>(delegate { Updater.CopyLimited(input, output, 32, clock, 0); });
        }
    }

    [TestMethod]
    public void QA52_07_HighContrastSelectionUsesSystemHighlight()
    {
        bool? previousContrast = Theme.HighContrastOverride;
        try
        {
            Theme.HighContrastOverride = true;
            Assert.AreEqual(SystemColors.Highlight.ToArgb(), Theme.SelOverlay.ToArgb(),
                "The selected row must use the user's opaque system highlight color.");
        }
        finally { Theme.HighContrastOverride = previousContrast; }
    }

    [TestMethod]
    public void QA52_08_AccessibleListExposesItsSelectedItem()
    {
        RunSta(delegate
        {
            using (ItemsView view = new ItemsView(null))
            {
                view.Size = new Size(340, 330);
                Group group = new Group { Key = "qa52:results" };
                group.Items.Add(new DesktopItem { Key = "qa52:report", Name = "Rapport" });
                view.SetGroups(new List<Group> { group }, false);
                view.SelectFirst();

                AccessibleObject list = view.AccessibilityObject;
                Assert.AreEqual(AccessibleRole.List, list.Role);
                Assert.AreEqual(1, list.GetChildCount(), "The populated list must expose an accessible item.");
                Assert.AreEqual("Rapport", list.GetSelected().Name);
            }
        });
    }

    [TestMethod]
    public void QA52_09_DisablingClipboardCancelsPendingRetry()
    {
        RunSta(delegate
        {
            ClipboardWatcher watcher = new ClipboardWatcher();
            FieldInfo retryField = typeof(ClipboardWatcher).GetField("retry", PrivateInstance);
            FieldInfo listeningField = typeof(ClipboardWatcher).GetField("listening", PrivateInstance);
            FieldInfo entriesField = typeof(ClipboardHistory).GetField("entries", BindingFlags.Static | BindingFlags.NonPublic);
            List<ClipboardHistory.Entry> entries = (List<ClipboardHistory.Entry>)entriesField.GetValue(null);
            List<ClipboardHistory.Entry> previous = ClipboardHistory.Items;
            using (System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 150 })
            {
                try
                {
                    // Seed only the pending retry. No clipboard listener or native handle is created.
                    listeningField.SetValue(watcher, true);
                    retryField.SetValue(watcher, timer);
                    timer.Start();
                    watcher.Enable(false);
                    System.Windows.Forms.Timer pending = (System.Windows.Forms.Timer)retryField.GetValue(watcher);
                    Assert.IsTrue(pending == null || !pending.Enabled, "Disabling clipboard monitoring must cancel its pending read.");
                }
                finally
                {
                    watcher.Close();
                    lock (entries)
                    {
                        entries.Clear();
                        entries.AddRange(previous);
                    }
                }
            }
        });
    }

    static FieldInfo ViewField(string name) { return typeof(ItemsView).GetField(name, PrivateInstance); }

    static Point CenterOf(object row)
    {
        Rectangle r = (Rectangle)row.GetType().GetField("R").GetValue(row);
        return new Point(r.Left + r.Width / 2, r.Top + r.Height / 2);
    }

    static void RunSta(Action test)
    {
        Exception error = null;
        Thread thread = new Thread(delegate()
        {
            try { test(); }
            catch (Exception ex) { error = ex; }
        });
        thread.IsBackground = true;
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.IsTrue(thread.Join(10000), "The UI test did not finish.");
        if (error != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
    }

    sealed class KeyboardToggle : Toggle
    {
        public bool TakesInput(Keys key) { return IsInputKey(key); }

        public KeyEventArgs Press(Keys key)
        {
            KeyEventArgs args = new KeyEventArgs(key);
            OnKeyDown(args);
            return args;
        }
    }

    sealed class AccessibleProbe : FlatControl
    {
        public int ValueReads;
        protected override string FlatValue
        {
            get { ValueReads++; return "unchanged"; }
        }
    }
}
