using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

// T-057 : le dossier d'installation peut être choisi par l'utilisateur. Seul un dossier qui ne contient que des
// fichiers de BureauCache peut être supprimé à la désinstallation.
[TestClass]
public class InstallerFolderTests
{
    string folder;

    [TestInitialize]
    public void Isolate()
    {
        folder = Path.Combine(Path.GetTempPath(), "BureauCache.Dossier-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
    }

    [TestCleanup]
    public void Clean()
    {
        try { Directory.Delete(folder, true); } catch (IOException) { }
    }

    [TestMethod]
    public void UnDossierAvecSeulementNosFichiersPeutEtreSupprime()
    {
        File.WriteAllText(Path.Combine(folder, "BureauCache.exe"), "fictif");
        File.WriteAllText(Path.Combine(folder, "BureauCache.exe.old"), "fictif");
        File.WriteAllText(Path.Combine(folder, "unins000.exe"), "fictif");
        File.WriteAllText(Path.Combine(folder, "unins000.dat"), "fictif");
        Assert.IsTrue(Installer.ContainsOnlyOwnFiles(folder));
    }

    [TestMethod]
    public void UnFichierEtrangerEmpecheLaSuppression()
    {
        File.WriteAllText(Path.Combine(folder, "BureauCache.exe"), "fictif");
        File.WriteAllText(Path.Combine(folder, "mes-notes.txt"), "fictif");
        Assert.IsFalse(Installer.ContainsOnlyOwnFiles(folder));
    }

    [TestMethod]
    public void UnSousDossierEmpecheLaSuppression()
    {
        File.WriteAllText(Path.Combine(folder, "BureauCache.exe"), "fictif");
        Directory.CreateDirectory(Path.Combine(folder, "Documents"));
        Assert.IsFalse(Installer.ContainsOnlyOwnFiles(folder));
    }

    [TestMethod]
    public void UnDossierVideEstAccepte()
    {
        Assert.IsTrue(Installer.ContainsOnlyOwnFiles(folder));
    }
}
