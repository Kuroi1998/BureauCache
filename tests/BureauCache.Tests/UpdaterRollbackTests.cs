using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BureauCache.Tests
{
    /// <summary>
    /// Retour arrière après une mise à jour (T-006, R-07), sur de faux exécutables dans un dossier temporaire.
    /// Le renommage d'un exe réellement en cours d'exécution reste à vérifier à la main.
    /// </summary>
    [TestClass]
    public class UpdaterRollbackTests
    {
        private string dir, exe;

        [TestInitialize]
        public void Setup()
        {
            dir = Path.Combine(Path.GetTempPath(), "BureauCache.Tests." + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            exe = Path.Combine(dir, "BureauCache.exe");
        }

        [TestCleanup]
        public void Cleanup()
        {
            try { Directory.Delete(dir, true); } catch { }
        }

        private void Write(string suffix, string content) { File.WriteAllText(exe + suffix, content); }
        private string Read(string suffix) { return File.ReadAllText(exe + suffix); }
        private bool Exists(string suffix) { return File.Exists(exe + suffix); }

        [TestMethod]
        public void Demarrage_normal_sans_ancienne_version()
        {
            Write("", "v2");
            Assert.AreEqual(Updater.StartState.Normal, Updater.BeginStart(exe));
            Assert.IsFalse(Exists(".trial"));
        }

        [TestMethod]
        public void Demarrage_normal_efface_les_restes()
        {
            Write("", "v2");
            Write(".trial", "");
            Write(".bad", "v3");
            Assert.AreEqual(Updater.StartState.Normal, Updater.BeginStart(exe));
            Assert.IsFalse(Exists(".trial"));
            Assert.IsFalse(Exists(".bad"));
        }

        [TestMethod]
        public void Premier_demarrage_apres_mise_a_jour_est_a_l_essai()
        {
            Write("", "v2");
            Write(".old", "v1");
            Assert.AreEqual(Updater.StartState.Trial, Updater.BeginStart(exe));
            Assert.IsTrue(Exists(".trial"));
            Assert.AreEqual("v2", Read(""));
            Assert.AreEqual("v1", Read(".old"));
        }

        [TestMethod]
        public void Demarrage_confirme_efface_l_ancienne_version()
        {
            Write("", "v2");
            Write(".old", "v1");
            Updater.BeginStart(exe);
            Updater.ConfirmStart(exe);
            Assert.IsFalse(Exists(".old"));
            Assert.IsFalse(Exists(".trial"));
            Assert.AreEqual(Updater.StartState.Normal, Updater.BeginStart(exe));
        }

        // Le premier essai a planté avant la confirmation : au démarrage suivant, retour à v1
        [TestMethod]
        public void Essai_non_confirme_remet_l_ancienne_version()
        {
            Write("", "v2");
            Write(".old", "v1");
            Assert.AreEqual(Updater.StartState.Trial, Updater.BeginStart(exe));
            Assert.AreEqual(Updater.StartState.RolledBack, Updater.BeginStart(exe));
            Assert.AreEqual("v1", Read(""));
            Assert.AreEqual("v2", Read(".bad"));
            Assert.IsFalse(Exists(".old"));
            Assert.IsFalse(Exists(".trial"));
            // La version restaurée démarre ensuite normalement et fait le ménage
            Assert.AreEqual(Updater.StartState.Normal, Updater.BeginStart(exe));
            Assert.IsFalse(Exists(".bad"));
        }

        [TestMethod]
        public void Retour_impossible_garde_la_nouvelle_version()
        {
            Write("", "v2");
            Write(".old", "v1");
            Write(".trial", "");
            using (new FileStream(exe + ".old", FileMode.Open, FileAccess.Read, FileShare.None))   // .old verrouillé
                Assert.AreEqual(Updater.StartState.Trial, Updater.BeginStart(exe));
            Assert.AreEqual("v2", Read(""));
            Assert.AreEqual("v1", Read(".old"));
        }

        [TestMethod]
        public void Remplacement_par_une_mise_a_jour_repart_d_un_essai_neuf()
        {
            Write("", "v1");
            Write(".trial", "reste d'un essai précédent");
            string staged = Path.Combine(dir, "staged.tmp");
            File.WriteAllText(staged, "v2");
            Updater.ReplaceExecutable(staged, exe);
            Assert.AreEqual("v2", Read(""));
            Assert.AreEqual("v1", Read(".old"));
            Assert.IsFalse(Exists(".trial"));
            Assert.AreEqual(Updater.StartState.Trial, Updater.BeginStart(exe));
        }
    }
}
