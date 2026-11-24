using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;

// QA-30 : image figée du panneau après un changement de réglage
[TestClass]
public class InterfaceTests
{
    [DllImport("user32.dll")] static extern bool GetWindowDisplayAffinity(IntPtr hWnd, out uint affinity);

    [TestMethod]
    public void QA30_HiddenPanelIsParkedOffScreenAndKeepsItsAffinity()
    {
        RunSta(() =>
        {
            bool anim = Settings.Animations, capture = Settings.HidePanelInCapture;
            try
            {
                Settings.Animations = false;
                Settings.HidePanelInCapture = false;
                using (var panel = new PanelForm())
                {
                    panel.ShowPanel(Screen.PrimaryScreen);
                    Pump(200);
                    panel.HidePanel();
                    Pump(200);
                    Assert.IsFalse(panel.Visible);
                    Assert.IsFalse(Screen.AllScreens.Any(s => s.Bounds.IntersectsWith(panel.Bounds)),
                        "Le panneau caché doit être rangé hors de tous les écrans : " + panel.Bounds);

                    uint before, after;
                    GetWindowDisplayAffinity(panel.Handle, out before);
                    Settings.HidePanelInCapture = true;
                    panel.OnSettingsChanged();
                    GetWindowDisplayAffinity(panel.Handle, out after);
                    Assert.AreEqual(before, after, "Un changement de réglage ne doit pas toucher la fenêtre cachée.");

                    panel.ShowPanel(Screen.PrimaryScreen);
                    Pump(200);
                    Assert.IsTrue(Screen.PrimaryScreen.Bounds.IntersectsWith(panel.Bounds), "Le panneau doit revenir à l'écran.");
                    GetWindowDisplayAffinity(panel.Handle, out after);
                    if (Native.CaptureExclusionSupported)
                        Assert.AreEqual(Native.WDA_EXCLUDEFROMCAPTURE, after, "L'affinité doit être appliquée à l'affichage.");
                    panel.HidePanel();
                }
            }
            finally
            {
                Settings.Animations = anim;
                Settings.HidePanelInCapture = capture;
            }
        });
    }

    // Effacer l'historique des récents ne retire pas la protection contre le rangement, et n'altère pas un
    // instantané d'annulation pris avant
    [TestMethod]
    public void QA30_ClearingRecentsKeepsUsageDatesAndSnapshots()
    {
        var usage = Settings.Usage;
        long since = Settings.RecentsSince;
        try
        {
            long used = DateTime.Now.AddDays(-1).Ticks;
            Settings.Usage = new System.Collections.Generic.Dictionary<string, long[]>(StringComparer.OrdinalIgnoreCase);
            Settings.Usage["C:\\Bureau\\rapport.docx"] = new long[] { 4, used };
            Settings.Snap snap = Settings.Snapshot();
            Settings.ClearRecents();
            Assert.AreEqual(used, Settings.Usage["C:\\Bureau\\rapport.docx"][1], "La date d'usage protège encore le fichier du rangement.");
            Assert.AreEqual(0, Settings.Usage["C:\\Bureau\\rapport.docx"][0]);
            Assert.IsTrue(Settings.RecentsSince > used, "Les récents ne montrent plus les usages antérieurs.");
            Settings.Restore(snap);
            Assert.AreEqual(4, Settings.Usage["C:\\Bureau\\rapport.docx"][0], "L'instantané d'annulation ne doit pas être modifié.");
        }
        finally { Settings.Usage = usage; Settings.RecentsSince = since; }
    }

    static void Pump(int ms)
    {
        var clock = Stopwatch.StartNew();
        while (clock.ElapsedMilliseconds < ms) { Application.DoEvents(); Thread.Sleep(10); }
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
