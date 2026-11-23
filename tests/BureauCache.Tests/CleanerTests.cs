using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BureauCache.Tests
{
    /// <summary>
    /// Nettoyage (D-005) : règles de sécurité éprouvées sur des arborescences temporaires.
    /// La « racine autorisée » du test joue le rôle de %LOCALAPPDATA%, un dossier voisin celui de tes documents.
    /// </summary>
    [TestClass]
    public class CleanerTests
    {
        private string sandbox, allowed, outside;
        private string[] roots, forbidden;

        [TestInitialize]
        public void Setup()
        {
            sandbox = Path.Combine(Path.GetTempPath(), "BureauCache.Tests." + Guid.NewGuid().ToString("N"));
            allowed = Directory.CreateDirectory(Path.Combine(sandbox, "profil")).FullName;
            outside = Directory.CreateDirectory(Path.Combine(sandbox, "documents")).FullName;
            roots = new[] { allowed + "\\" };
            forbidden = new[] { Path.Combine(allowed, "interdit") + "\\" };
        }

        [TestCleanup]
        public void Cleanup()
        {
            foreach (string link in Directory.GetDirectories(sandbox, "*", SearchOption.AllDirectories)
                     .Where(d => (File.GetAttributes(d) & FileAttributes.ReparsePoint) != 0).OrderByDescending(d => d.Length))
                try { Directory.Delete(link, false); } catch { }
            try { Directory.Delete(sandbox, true); } catch { }
        }

        private static string Write(string path, DateTime? age = null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, new string('x', 100));
            if (age.HasValue) { File.SetLastWriteTimeUtc(path, age.Value); File.SetCreationTimeUtc(path, age.Value); }
            return path;
        }

        private static void Junction(string link, string target)
        {
            var psi = new ProcessStartInfo("cmd.exe", "/c mklink /J \"" + link + "\" \"" + target + "\"") { CreateNoWindow = true, UseShellExecute = false };
            using (var p = Process.Start(psi)) p.WaitForExit(5000);
            if (!Directory.Exists(link)) Assert.Inconclusive("Jonction impossible à créer ici.");
        }

        private CleanCategory Category(params CleanTarget[] targets)
        {
            var c = new CleanCategory { Id = "test", Checked = true };
            c.Targets.AddRange(targets);
            return c;
        }

        [TestMethod]
        public void Chemins_autorises_et_interdits()
        {
            Assert.IsTrue(Cleaner.IsAllowed(Path.Combine(allowed, "cache"), roots, forbidden));
            Assert.IsTrue(Cleaner.IsAllowed(allowed, roots, forbidden));
            Assert.IsFalse(Cleaner.IsAllowed(outside, roots, forbidden));
            Assert.IsFalse(Cleaner.IsAllowed(Path.Combine(allowed, @"interdit\x"), roots, forbidden));
            Assert.IsFalse(Cleaner.IsAllowed(Path.Combine(allowed, @"..\documents"), roots, forbidden));   // remontée
            Assert.IsFalse(Cleaner.IsAllowed(allowed + "-voisin", roots, forbidden));                     // préfixe trompeur
            Assert.IsFalse(Cleaner.IsAllowed("\0invalide", roots, forbidden));
        }

        [TestMethod]
        public void Racines_reelles_hors_de_Windows_et_Program_Files()
        {
            foreach (string r in Cleaner.Roots)
                Assert.IsTrue(Cleaner.IsAllowed(r, Cleaner.Roots, Cleaner.Forbidden) || r.StartsWith(Environment.GetEnvironmentVariable("OneDrive") ?? "\0"), r);
            Assert.IsFalse(Cleaner.IsAllowed(Environment.GetFolderPath(Environment.SpecialFolder.Windows), Cleaner.Roots, Cleaner.Forbidden));
            Assert.IsFalse(Cleaner.IsAllowed(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Cleaner.Roots, Cleaner.Forbidden));
            Assert.IsFalse(Cleaner.IsAllowed(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), Cleaner.Roots, Cleaner.Forbidden));
        }

        [TestMethod]
        public void Supprime_les_fichiers_du_cache_et_les_dossiers_vides_mais_garde_la_racine()
        {
            string cache = Path.Combine(allowed, "Cache_Data");
            Write(Path.Combine(cache, "a.bin"));
            Write(Path.Combine(cache, @"sous\b.bin"));
            CleanResult r = Cleaner.Clean(new[] { Category(new CleanTarget { Dir = cache }) }, roots, forbidden);
            Assert.AreEqual(2, r.Deleted);
            Assert.AreEqual(200, r.Freed);
            Assert.IsTrue(Directory.Exists(cache), "la racine du cache reste");
            Assert.IsFalse(Directory.Exists(Path.Combine(cache, "sous")), "les sous-dossiers vides sont retirés");
        }

        [TestMethod]
        public void Fichiers_recents_ignores_selon_l_age_minimal()
        {
            string temp = Path.Combine(allowed, "Temp");
            string vieux = Write(Path.Combine(temp, "vieux.tmp"), DateTime.UtcNow.AddDays(-3));
            string recent = Write(Path.Combine(temp, "recent.tmp"));
            string copieRecente = Write(Path.Combine(temp, "copie.tmp"), DateTime.UtcNow.AddDays(-3));
            File.SetCreationTimeUtc(copieRecente, DateTime.UtcNow);                                    // ancien mais apporté récemment
            Cleaner.Clean(new[] { Category(new CleanTarget { Dir = temp, MinAge = TimeSpan.FromHours(24) }) }, roots, forbidden);
            Assert.IsFalse(File.Exists(vieux));
            Assert.IsTrue(File.Exists(recent));
            Assert.IsTrue(File.Exists(copieRecente));
        }

        // Le piège mortel des nettoyeurs : suivre une jonction et supprimer ce qu'elle désigne
        [TestMethod]
        public void Ne_suit_jamais_une_jonction()
        {
            string precieux = Write(Path.Combine(outside, "precieux.docx"));
            string cache = Path.Combine(allowed, "Cache");
            Write(Path.Combine(cache, "a.bin"));
            Junction(Path.Combine(cache, "lien-vers-documents"), outside);
            CleanResult r = Cleaner.Clean(new[] { Category(new CleanTarget { Dir = cache }) }, roots, forbidden);
            Assert.IsTrue(File.Exists(precieux), "le fichier désigné par la jonction doit rester intact");
            Assert.AreEqual(1, r.Deleted);
        }

        [TestMethod]
        public void Cible_qui_est_elle_meme_une_jonction_ignoree()
        {
            string precieux = Write(Path.Combine(outside, "precieux.docx"));
            string lien = Path.Combine(allowed, "Cache-lien");
            Junction(lien, outside);
            Cleaner.Clean(new[] { Category(new CleanTarget { Dir = lien }) }, roots, forbidden);
            Assert.IsTrue(File.Exists(precieux));
        }

        [TestMethod]
        public void Cible_hors_des_racines_ignoree()
        {
            string precieux = Write(Path.Combine(outside, "precieux.docx"));
            CleanResult r = Cleaner.Clean(new[] { Category(new CleanTarget { Dir = outside }) }, roots, forbidden);
            Assert.IsTrue(File.Exists(precieux));
            Assert.AreEqual(0, r.Deleted);
        }

        // Un fichier OneDrive « en ligne uniquement » ne doit être ni lu ni supprimé
        [TestMethod]
        public void Fichier_en_ligne_uniquement_ignore()
        {
            string cache = Path.Combine(allowed, "Cache");
            string nuage = Write(Path.Combine(cache, "nuage.bin"));
            File.SetAttributes(nuage, FileAttributes.Offline);
            Write(Path.Combine(cache, "local.bin"));
            CleanResult r = Cleaner.Clean(new[] { Category(new CleanTarget { Dir = cache }) }, roots, forbidden);
            Assert.IsTrue(File.Exists(nuage));
            Assert.AreEqual(1, r.Deleted);
        }

        [TestMethod]
        public void Motif_et_dossier_sans_recursion()
        {
            string explorer = Path.Combine(allowed, "Explorer");
            string thumb = Write(Path.Combine(explorer, "thumbcache_256.db"));
            string autre = Write(Path.Combine(explorer, "iconcache_256.db"));
            string profond = Write(Path.Combine(explorer, @"sous\thumbcache_32.db"));
            Cleaner.Clean(new[] { Category(new CleanTarget { Dir = explorer, Pattern = "thumbcache_*.db", Recurse = false }) }, roots, forbidden);
            Assert.IsFalse(File.Exists(thumb));
            Assert.IsTrue(File.Exists(autre));
            Assert.IsTrue(File.Exists(profond));
        }

        [TestMethod]
        public void Fichier_verrouille_laisse_en_place_et_compte()
        {
            string cache = Path.Combine(allowed, "Cache");
            string ouvert = Write(Path.Combine(cache, "ouvert.bin"));
            Write(Path.Combine(cache, "libre.bin"));
            CleanResult r;
            using (new FileStream(ouvert, FileMode.Open, FileAccess.Read, FileShare.None))
                r = Cleaner.Clean(new[] { Category(new CleanTarget { Dir = cache }) }, roots, forbidden);
            Assert.IsTrue(File.Exists(ouvert));
            Assert.AreEqual(1, r.Deleted);
            Assert.AreEqual(1, r.Skipped);
        }

        // Revue QA-23 : retirer l'attribut modifierait aussi les autres noms d'un lien physique : le fichier est laissé
        [TestMethod]
        public void Fichier_en_lecture_seule_laisse_en_place()
        {
            string cache = Path.Combine(allowed, "Cache");
            string ro = Write(Path.Combine(cache, "ro.bin"));
            File.SetAttributes(ro, FileAttributes.ReadOnly);
            Assert.AreEqual(0, Cleaner.Clean(new[] { Category(new CleanTarget { Dir = cache }) }, roots, forbidden).Deleted);
            Assert.IsTrue(File.Exists(ro));
            Assert.AreEqual(FileAttributes.ReadOnly, File.GetAttributes(ro) & FileAttributes.ReadOnly);
            File.SetAttributes(ro, FileAttributes.Normal);
        }

        // Revue QA-23 : un PARENT de la cible qui est une jonction emmènerait le nettoyage ailleurs
        [TestMethod]
        public void Parent_de_la_cible_en_jonction_ignore()
        {
            string precieux = Write(Path.Combine(outside, @"Cache\precieux.docx"));
            Junction(Path.Combine(allowed, "Appli"), outside);
            string cible = Path.Combine(allowed, @"Appli\Cache");   // le chemin lui-même n'est pas un lien
            Assert.IsTrue(Cleaner.CrossesLink(cible, roots));
            CleanResult r = Cleaner.Clean(new[] { Category(new CleanTarget { Dir = cible }) }, roots, forbidden);
            Assert.IsTrue(File.Exists(precieux));
            Assert.AreEqual(0, r.Deleted);
            Assert.IsFalse(Cleaner.CrossesLink(Path.Combine(allowed, "Cache"), roots));
        }

        // Revue QA-23 : %TEMP% n'est plus une racine ; il n'est nettoyé que s'il est dans AppData\Local
        [TestMethod]
        public void Temp_n_est_pas_une_racine_autonome()
        {
            string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd('\\') + "\\";
            Assert.IsFalse(Cleaner.Roots.Any(r => string.Equals(r, temp, StringComparison.OrdinalIgnoreCase)));
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            Assert.AreEqual(temp.StartsWith(local + "\\", StringComparison.OrdinalIgnoreCase), Cleaner.IsAllowed(temp, Cleaner.Roots, Cleaner.Forbidden));
            foreach (string r in Cleaner.Roots) Assert.IsFalse(r.StartsWith(@"\\"), "racine réseau : " + r);
        }

        [TestMethod]
        [DataRow("thumbcache_256.db", "thumbcache_*.db", true)]
        [DataRow("THUMBCACHE_idx.DB", "thumbcache_*.db", true)]
        [DataRow("thumbcache.db", "thumbcache_*.db", false)]
        [DataRow("iconcache_256.db", "thumbcache_*.db", false)]
        [DataRow("x.db", "x.db", true)]
        public void Motifs(string name, string pattern, bool expected)
        {
            Assert.AreEqual(expected, Cleaner.Matches(name, pattern));
        }

        // Les catégories réelles ne visent que des dossiers autorisés, et seules les sûres sont cochées par défaut
        [TestMethod]
        public void Categories_reelles_sures()
        {
            var cats = Cleaner.Categories();
            foreach (var c in cats)
                foreach (var t in c.Targets)
                    Assert.IsTrue(Cleaner.IsAllowed(t.Dir, Cleaner.Roots, Cleaner.Forbidden), c.Id + " : " + t.Dir);
            CollectionAssert.AreEquivalent(new[] { "temp", "browsers", "apps" }, cats.Where(c => c.Checked).Select(c => c.Id).ToArray());
            Assert.IsTrue(cats.Single(c => c.Id == "temp").Targets.All(t => t.MinAge >= TimeSpan.FromHours(24)));
            foreach (var t in cats.SelectMany(c => c.Targets))
            {
                string d = t.Dir.ToLowerInvariant();
                foreach (string interdit in new[] { "cookies", "login data", "local storage", "indexeddb", "history", "bookmarks", "\\storage", "places.sqlite" })
                    Assert.IsFalse(d.Contains(interdit), "chemin sensible : " + t.Dir);
            }
        }
    }
}
