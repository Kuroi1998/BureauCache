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

    // R-23 : un favori n'est jamais compté
    [TestMethod]
    public void R23_FavoritesAreNeverCounted()
    {
        List<string> candidates = Archiver.Candidates();
        if (candidates.Count == 0) Assert.Inconclusive("Aucun élément ancien sur ce bureau.");
        var favorites = new HashSet<string>(candidates, StringComparer.OrdinalIgnoreCase);
        var usage = new Dictionary<string, long[]>(StringComparer.OrdinalIgnoreCase);
        Assert.AreEqual(0, Archiver.CountCandidates(favorites, usage, Archiver.Limit, Archiver.Folder));
    }
}
