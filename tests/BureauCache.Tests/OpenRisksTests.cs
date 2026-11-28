using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;

// Risques ouverts du registre QA traités par T-017
[TestClass]
public class OpenRisksTests
{
    // R-10 : l'écran choisi est retrouvé par son nom, et un écran débranché rend le bord actif partout
    [TestMethod]
    public void R10_ChosenScreenFollowsDeviceName()
    {
        int idx = Settings.ScreenIdx;
        string name = Settings.ScreenName;
        try
        {
            Screen[] all = Screen.AllScreens;
            Settings.ScreenIdx = -1; Settings.ScreenName = "";
            Assert.AreEqual(-1, Settings.ChosenScreen(all));

            Settings.ScreenIdx = 0; Settings.ScreenName = all[0].DeviceName;
            Assert.AreEqual(0, Settings.ChosenScreen(all));

            Settings.ScreenIdx = 0; Settings.ScreenName = @"\\.\DISPLAY-DEBRANCHE";
            Assert.AreEqual(-1, Settings.ChosenScreen(all), "Écran absent : tous les écrans, pas celui qui a pris son indice.");

            Settings.ScreenIdx = 0; Settings.ScreenName = "";   // réglage d'avant 3.8 : seul l'indice est connu
            Assert.AreEqual(0, Settings.ChosenScreen(all));
            Settings.ScreenIdx = all.Length;
            Assert.AreEqual(-1, Settings.ChosenScreen(all));
        }
        finally { Settings.ScreenIdx = idx; Settings.ScreenName = name; }
    }

    // R-23 : le décompte de fond, sur copies des réglages, donne le même résultat que l'évaluation complète.
    // Lecture seule du vrai bureau : rien n'est déplacé.
    [TestMethod]
    public void R23_BackgroundCountMatchesCandidates()
    {
        var favorites = new HashSet<string>(Settings.Favorites, StringComparer.OrdinalIgnoreCase);
        var usage = new Dictionary<string, long[]>(Settings.Usage, StringComparer.OrdinalIgnoreCase);
        Assert.AreEqual(Archiver.Candidates().Count, Archiver.CountCandidates(favorites, usage, Archiver.Limit, Archiver.Folder));
    }

    // R-23 : un favori n'est jamais compté. Fixture propre (T-055) : un fichier ancien dans le bureau de test,
    // sous bac à sable, au lieu de dépendre du contenu du vrai bureau (auparavant : Assert.Inconclusive).
    [TestMethod]
    public void R23_FavoritesAreNeverCounted()
    {
        string file = System.IO.Path.Combine(Paths.Desktop, "ancien-" + Guid.NewGuid().ToString("N") + ".txt");
        System.IO.File.WriteAllText(file, "fictif");
        // Toutes les dates anciennes : l'ancienneté retient la plus récente des trois
        System.IO.File.SetCreationTimeUtc(file, DateTime.UtcNow.AddDays(-400));
        System.IO.File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddDays(-400));
        System.IO.File.SetLastAccessTimeUtc(file, DateTime.UtcNow.AddDays(-400));
        List<string> savedFavorites = Settings.Favorites;
        try
        {
            Settings.Favorites = new List<string>();
            List<string> candidates = Archiver.Candidates();
            Assert.IsTrue(candidates.Exists(c => string.Equals(c, file, StringComparison.OrdinalIgnoreCase)),
                "Le fichier ancien doit être candidat avant d'être mis en favori.");
            var favorites = new HashSet<string>(new[] { file }, StringComparer.OrdinalIgnoreCase);
            var usage = new Dictionary<string, long[]>(StringComparer.OrdinalIgnoreCase);
            Assert.AreEqual(0, Archiver.CountCandidates(favorites, usage, Archiver.Limit, Archiver.Folder));
        }
        finally
        {
            Settings.Favorites = savedFavorites;
            try { System.IO.File.Delete(file); } catch (System.IO.IOException) { }
        }
    }
}
