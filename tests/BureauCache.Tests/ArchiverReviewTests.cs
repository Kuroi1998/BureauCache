using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BureauCache.Tests
{
    [TestClass]
    public class ArchiverReviewTests
    {
        private string root;
        private string junction;
        private DateTime old;
        private DateTime limit;

        [TestInitialize]
        public void Setup()
        {
            root = Path.Combine(Path.GetTempPath(), "bc-qa-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            DateTime now = DateTime.Now;
            old = now.AddDays(-120);
            limit = now.AddDays(-60);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (junction != null && Directory.Exists(junction)) Directory.Delete(junction, false);
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        // Revue de Claude : remarque écartée. La date de dernier accès n'est pas fiable : NTFS ne la met pas à jour
        // sur beaucoup de volumes, et l'indexation, l'antivirus ou la sauvegarde la rafraîchissent sans que
        // l'utilisateur ait rien ouvert, ce qui empêcherait tout rangement. Seule l'ouverture depuis le panneau
        // (Settings.Usage) compte comme une utilisation sans modification. Le test fixe ce choix (limite R-22).
        [TestMethod]
        public void LectureSeuleNeComptePasCommeActivite()
        {
            string file = Path.Combine(root, "lu.txt");
            WriteOldFile(file);
            Assert.AreEqual("test", File.ReadAllText(file));
            File.SetLastAccessTime(file, DateTime.Now);
            AgeDirectory(root);

            Assert.AreEqual(Archiver.Activity.Old, Archiver.ActivityOf(root, limit, 10));
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void JonctionVersUnDossierExterieurRendLeScanInconnu(bool cibleDuScan)
        {
            string desktop = Directory.CreateDirectory(Path.Combine(root, "bureau")).FullName;
            string outside = Directory.CreateDirectory(Path.Combine(root, "exterieur")).FullName;
            WriteOldFile(Path.Combine(outside, "ancien.txt"));
            AgeDirectory(outside);
            CreateJunction(Path.Combine(desktop, "lien"), outside);
            AgeDirectory(desktop);
            AgeDirectory(root);
            // Le seuil rend la jonction fraîchement créée éligible au parcours.
            DateTime threshold = DateTime.Now.AddDays(1);

            Assert.AreEqual(Archiver.Activity.Unknown,
                Archiver.ActivityOf(cibleDuScan ? junction : desktop, threshold, 10));
        }

        [TestMethod]
        public void JonctionCycliqueNeConfirmePasLAnciennete()
        {
            CreateJunction(Path.Combine(root, "boucle"), root);
            AgeDirectory(root);

            // Budget complet : c'est la détection du lien qui doit arrêter le parcours, pas le plafond
            Assert.AreEqual(Archiver.Activity.Unknown,
                Archiver.ActivityOf(root, DateTime.Now.AddDays(1), Archiver.MaxScan));
        }

        [TestMethod]
        public void AnnulationConserveLeJournalQuandUnParentEstUneJonction()
        {
            string desktop = Directory.CreateDirectory(Path.Combine(root, "bureau")).FullName;
            string outside = Directory.CreateDirectory(Path.Combine(root, "exterieur", "SousDossier")).FullName;
            string file = Path.Combine(outside, "document.txt");
            WriteOldFile(file);
            CreateJunction(Path.Combine(desktop, "Archives"), Path.GetDirectoryName(outside));
            string original = Path.Combine(desktop, "document.txt");
            string archived = Path.Combine(junction, "SousDossier", "document.txt");
            string entry = original + ">" + archived;
            var journal = new List<string> { entry };

            Assert.AreEqual(0, Archiver.RestoreArchived(desktop, journal));
            Assert.IsTrue(File.Exists(file));
            Assert.IsFalse(File.Exists(original));
            CollectionAssert.AreEqual(new[] { entry }, journal);
        }

        [TestMethod]
        public void HeureRepeteeCompareLesInstantsDansLeBonOrdre()
        {
            DateTime writeUtc = new DateTime(2026, 10, 25, 0, 50, 0, DateTimeKind.Utc);
            DateTime limitUtc = new DateTime(2026, 10, 25, 1, 10, 0, DateTimeKind.Utc);
            DateTime localLimit = limitUtc.ToLocalTime();
            if (writeUtc.ToLocalTime() <= localLimit || !TimeZoneInfo.Local.IsAmbiguousTime(localLimit))
                Assert.Inconclusive("Ce test requiert le changement d’heure d’octobre de Bruxelles ou un équivalent.");

            string file = Path.Combine(root, "heure-repetee.txt");
            WriteOldFile(file);
            File.SetCreationTimeUtc(file, writeUtc.AddDays(-2));
            File.SetLastWriteTimeUtc(file, writeUtc);
            AgeDirectory(root);

            Assert.AreEqual(Archiver.Activity.Old, Archiver.ActivityOf(file, localLimit, 10));
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void DateFutureProtegeLeDossier(bool creation)
        {
            string child = Directory.CreateDirectory(Path.Combine(root, "sous-dossier")).FullName;
            string file = Path.Combine(child, "futur.txt");
            WriteOldFile(file);
            DateTime future = DateTime.Now.AddYears(5);
            if (creation) File.SetCreationTime(file, future);
            else File.SetLastWriteTime(file, future);
            AgeDirectory(child);
            AgeDirectory(root);

            Assert.AreEqual(Archiver.Activity.Recent, Archiver.ActivityOf(root, limit, 10));
        }

        private void WriteOldFile(string path)
        {
            File.WriteAllText(path, "test");
            File.SetCreationTime(path, old);
            File.SetLastWriteTime(path, old);
        }

        private void AgeDirectory(string path)
        {
            Directory.SetCreationTime(path, old);
            Directory.SetLastWriteTime(path, old);
        }

        private void CreateJunction(string path, string target)
        {
            junction = path;
            string cmd = Path.Combine(Environment.SystemDirectory, "cmd.exe");
            var start = new ProcessStartInfo(cmd, "/D /V:OFF /C mklink /J \"%BC_QA_LINK%\" \"%BC_QA_TARGET%\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            };
            start.EnvironmentVariables["BC_QA_LINK"] = path;
            start.EnvironmentVariables["BC_QA_TARGET"] = target;
            using (Process process = Process.Start(start))
            {
                if (!process.WaitForExit(5000))
                {
                    process.Kill();
                    process.WaitForExit();
                    Assert.Fail("Délai dépassé pendant la création de la jonction.");
                }

                if (process.ExitCode != 0)
                    Assert.Inconclusive("Jonction indisponible dans le dossier temporaire. " + process.StandardError.ReadToEnd());
            }
            Assert.IsTrue((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0);
        }
    }
}
