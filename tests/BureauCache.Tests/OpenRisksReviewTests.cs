using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class OpenRisksReviewTests
{
    [TestMethod]
    [TestCategory("Review")]
    public void Q26_05_ImportingLegacyScreenIndexClearsPreviousDeviceName()
    {
        string path = Path.GetTempFileName();
        string name = Settings.ScreenName;
        int index = Settings.ScreenIdx;
        bool hideIcons = Settings.HideIcons, hideCapture = Settings.HidePanelInCapture;
        try
        {
            Settings.ScreenName = @"\\.\DISPLAY17";
            File.WriteAllText(path, "HideIcons=" + (hideIcons ? "1" : "0") + "\r\nScreenIdx=0\r\n");
            Assert.IsTrue(Settings.Load(path));
            Assert.AreEqual(0, Settings.ScreenIdx);
            Assert.AreNotEqual(@"\\.\DISPLAY17", Settings.ScreenName,
                "Q26-05 : un ancien INI garde le nom du moniteur de la configuration précédente.");
        }
        finally
        {
            Settings.ScreenName = name;
            Settings.ScreenIdx = index;
            Settings.HideIcons = hideIcons;
            Settings.HidePanelInCapture = hideCapture;
            File.Delete(path);
        }
    }

    [TestMethod]
    [TestCategory("Review")]
#if !WINDOWS_INTERACTIVE_TESTS
    [Ignore("Q26-06 : la construction et la fermeture de la fenêtre d'aperçu exigent une session WinForms interactive.")]
#endif
    [TestCategory("WindowsInteractive")]
    public void Q26_06_DisposingPreviewReleasesItsOwnedBitmap()
    {
        RunSta(() =>
        {
            using (var bitmap = new Bitmap(16, 16))
            {
                using (var preview = new PreviewForm())
                {
                    typeof(PreviewForm).GetField("img", BindingFlags.Instance | BindingFlags.NonPublic)
                        .SetValue(preview, bitmap);
                }
                try { bitmap.GetPixel(0, 0); }
                catch (ArgumentException) { return; }
                Assert.Fail("Q26-06 : la miniature appartenant à l'aperçu reste utilisable après sa destruction.");
            }
        });
    }

    // Q26-08 : la date limite capturée est celle utilisée ; une limite très ancienne ne laisse aucun candidat.
    // Lecture seule du vrai bureau : rien n'est déplacé.
    [TestMethod]
    [TestCategory("Review")]
    public void Q26_08_CountUsesTheCapturedCutoff()
    {
        var favorites = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var usage = new System.Collections.Generic.Dictionary<string, long[]>(StringComparer.OrdinalIgnoreCase);
        DateTime veryOld = new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Assert.AreEqual(0, Archiver.CountCandidates(favorites, usage, veryOld, Archiver.Folder));
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
        Assert.IsTrue(thread.Join(10000));
        if (error != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
    }
}
