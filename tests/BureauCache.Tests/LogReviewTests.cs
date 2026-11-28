using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

// T-048 : journal des avertissements. Le contenu sensible utilisé ici est entièrement fictif.
[TestClass]
public class LogReviewTests
{
    string previousDir, folder;
    bool previousBlocked;

    [TestInitialize]
    public void Isolate()
    {
        previousDir = Settings.DirOverride;
        previousBlocked = Settings.SaveBlocked;
        folder = Path.Combine(Path.GetTempPath(), "BureauCache.Log-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        Settings.DirOverride = folder;
        Settings.SaveBlocked = false;
    }

    [TestCleanup]
    public void Restore()
    {
        Settings.SaveBlocked = previousBlocked;
        Settings.DirOverride = previousDir;
        try { Directory.Delete(folder, true); } catch (IOException) { }
    }

    string LogPath { get { return Path.Combine(folder, Log.FileName); } }

    [TestMethod]
    public void UnContexteInconnuNEcritRien()
    {
        Log.Warn("texte.libre.quelconque", new IOException("secret fictif"));
        Assert.IsFalse(File.Exists(LogPath));
    }

    [TestMethod]
    public void UneLigneNeContientQueLHeureLeContexteEtLaCategorie()
    {
        const string secret = @"C:\Utilisateur\fictif\mot-de-passe-fictif";
        Log.Warn("reglages.enregistrement", new IOException(secret));

        string text = File.ReadAllText(LogPath);
        StringAssert.StartsWith(text, "utc,contexte,cause" + "\r\n");
        StringAssert.Contains(text, ",reglages.enregistrement,io\r\n");
        Assert.IsFalse(text.Contains(secret));
    }

    [TestMethod]
    public void LaCategorieDependDuTypeDexception()
    {
        Log.Warn("maj.nettoyage", new UnauthorizedAccessException("fictif"));
        Log.Warn("maj.nettoyage", new InvalidOperationException("fictif"));
        string text = File.ReadAllText(LogPath);
        StringAssert.Contains(text, ",maj.nettoyage,acces\r\n");
        StringAssert.Contains(text, ",maj.nettoyage,exception\r\n");
    }

    [TestMethod]
    public void LeJournalNeDepassePasLaLimiteEtRecommence()
    {
        // Un fichier presque plein : la ligne ajoutée doit compter dans la limite (T-048)
        File.WriteAllBytes(LogPath, new byte[64 * 1024 - 10]);
        Log.Warn("maj.nettoyage", new UnauthorizedAccessException("fictif"));

        string text = File.ReadAllText(LogPath);
        StringAssert.StartsWith(text, "utc,contexte,cause" + "\r\n");
        StringAssert.Contains(text, ",maj.nettoyage,acces\r\n");
        Assert.IsTrue(new FileInfo(LogPath).Length <= 64 * 1024);
    }

    [TestMethod]
    public void RienNEstEcritQuandLesReglagesSontBloques()
    {
        Settings.SaveBlocked = true;
        Log.Warn("reglages.enregistrement", new IOException("fictif"));
        Assert.IsFalse(File.Exists(LogPath));
    }
}
