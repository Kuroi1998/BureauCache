using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BureauCache.Tests
{
    /// <summary>
    /// Ancienneté des éléments pour le rangement automatique (T-003, R-01).
    /// Les arborescences sont créées dans un dossier temporaire, puis datées du plus profond au plus haut
    /// (créer un élément modifie la date de son dossier parent).
    /// </summary>
    [TestClass]
    public class ArchiverTests
    {
        private static readonly DateTime Old = DateTime.Now.AddDays(-400);
        private static readonly DateTime Limit = DateTime.Now.AddDays(-60);
        private string root;

        [TestInitialize]
        public void Setup()
        {
            root = Path.Combine(Path.GetTempPath(), "BureauCache.Tests." + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
        }

        [TestCleanup]
        public void Cleanup()
        {
            try { Directory.Delete(root, true); } catch { }
        }

        private string NewFile(string relative)
        {
            string path = Path.Combine(root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "x");
            return path;
        }

        // Date tout l'arbre comme ancien, des feuilles vers la racine
        private static void AgeTree(string path)
        {
            if (Directory.Exists(path))
            {
                foreach (string child in Directory.GetFileSystemEntries(path)) AgeTree(child);
                Directory.SetCreationTime(path, Old);
                Directory.SetLastWriteTime(path, Old);
            }
            else
            {
                File.SetCreationTime(path, Old);
                File.SetLastWriteTime(path, Old);
            }
        }

        private static Archiver.Activity Activity(string path, int maxEntries = Archiver.MaxScan)
        {
            return Archiver.ActivityOf(path, Limit, maxEntries);
        }

        [TestMethod]
        public void Fichier_ancien()
        {
            string file = NewFile("ancien.txt");
            AgeTree(file);
            Assert.AreEqual(Archiver.Activity.Old, Activity(file));
        }

        [TestMethod]
        public void Fichier_recent()
        {
            Assert.AreEqual(Archiver.Activity.Recent, Activity(NewFile("recent.txt")));
        }

        [TestMethod]
        public void Fichier_ancien_copie_recemment_est_recent()
        {
            string file = NewFile("copie.txt");
            File.SetLastWriteTime(file, Old);   // la copie garde la date de modification, pas celle de création
            Assert.AreEqual(Archiver.Activity.Recent, Activity(file));
        }

        [TestMethod]
        public void Dossier_entierement_ancien()
        {
            NewFile(@"projet\a.txt");
            NewFile(@"projet\src\b.txt");
            NewFile(@"projet\src\lib\c.txt");
            string folder = Path.Combine(root, "projet");
            AgeTree(folder);
            Assert.AreEqual(Archiver.Activity.Old, Activity(folder));
        }

        // R-01 : une modification récente dans un sous-dossier profond doit garder le dossier sur le bureau
        [TestMethod]
        public void Modification_recente_dans_un_sous_dossier_profond()
        {
            NewFile(@"projet\a.txt");
            string deep = NewFile(@"projet\src\lib\interne\c.txt");
            string folder = Path.Combine(root, "projet");
            AgeTree(folder);
            File.SetLastWriteTime(deep, DateTime.Now);
            Directory.SetLastWriteTime(folder, Old);   // le dossier lui-même ne trahit rien
            Assert.AreEqual(Archiver.Activity.Recent, Activity(folder));
        }

        // R-01 : l'ancien contrôle ne regardait que les 300 premières entrées directes
        [TestMethod]
        public void Modification_recente_au_dela_de_300_entrees()
        {
            for (int i = 0; i < 400; i++) NewFile(@"photos\img" + i.ToString("000") + ".jpg");
            string last = NewFile(@"photos\zzz-derniere.jpg");
            string folder = Path.Combine(root, "photos");
            AgeTree(folder);
            File.SetLastWriteTime(last, DateTime.Now);
            Assert.AreEqual(Archiver.Activity.Recent, Activity(folder));
        }

        [TestMethod]
        public void Dossier_trop_volumineux_non_confirme()
        {
            for (int i = 0; i < 20; i++) NewFile(@"gros\f" + i + ".txt");
            string folder = Path.Combine(root, "gros");
            AgeTree(folder);
            Assert.AreEqual(Archiver.Activity.Unknown, Activity(folder, 10));
            Assert.AreEqual(Archiver.Activity.Old, Activity(folder, 20));
        }

        [TestMethod]
        public void Dossier_vide_ancien()
        {
            string folder = Path.Combine(root, "vide");
            Directory.CreateDirectory(folder);
            AgeTree(folder);
            Assert.AreEqual(Archiver.Activity.Old, Activity(folder));
        }

        [TestMethod]
        public void Element_disparu_non_confirme()
        {
            Assert.AreEqual(Archiver.Activity.Unknown, Activity(Path.Combine(root, "absent")));
        }
    }
}
