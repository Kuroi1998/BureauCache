using System;
using System.IO;
using System.Security.Cryptography;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BureauCache.Tests
{
    [TestClass]
    public class UpdaterSecurityReviewTests
    {
        private string directory;
        private string exe;
        private string staged;

        [TestInitialize]
        public void Setup()
        {
            directory = Path.Combine(Path.GetTempPath(), "bc-update-qa-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            exe = Path.Combine(directory, "BureauCache.exe");
            staged = Path.Combine(directory, "stage.tmp");
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [TestMethod]
        public void DepotOfficielNormaliseDepuisLesReglages()
        {
            string previous = Settings.UpdateRepo;
            try
            {
                Settings.UpdateRepo = "  https://GITHUB.COM/" + Updater.DefaultRepo.ToLowerInvariant() + "/  ";
                Assert.IsTrue(Updater.IsOfficial(Updater.RepoName));
                Settings.UpdateRepo = "  ";
                Assert.IsTrue(Updater.IsOfficial(Updater.RepoName));
                Settings.UpdateRepo = Updater.DefaultRepo + "-fork";
                Assert.IsFalse(Updater.IsOfficial(Updater.RepoName));
            }
            finally
            {
                Settings.UpdateRepo = previous;
            }
        }

        [TestMethod]
        public void SignatureAccepteBomEtRefuseOctetsSurnumeraires()
        {
            byte[] hash = new byte[32];
            using (var rsa = new RSACryptoServiceProvider(3072, new CspParameters(24)))
            {
                rsa.PersistKeyInCsp = false;
                string[] keys = { rsa.ToXmlString(false) };
                byte[] signature = rsa.SignHash(hash, CryptoConfig.MapNameToOID("SHA256"));
                string encoded = Convert.ToBase64String(signature);
                Assert.IsTrue(Updater.VerifySignature(hash, encoded, keys));
                // Revue de Claude : un BOM est ignor\u00E9 (il ne modifie pas la signature et ne doit pas bloquer la mise \u00E0 jour)
                Assert.IsTrue(Updater.VerifySignature(hash, "\uFEFF" + encoded, keys));
                Array.Resize(ref signature, signature.Length + 1);
                Assert.IsFalse(Updater.VerifySignature(hash, Convert.ToBase64String(signature), keys));
            }
        }

        [TestMethod]
        public void EssaiSansMarqueurNePeutPasContinuerSilencieusement()
        {
            File.WriteAllText(exe, "candidate");
            File.WriteAllText(exe + ".old", "stable");
            Directory.CreateDirectory(exe + ".trial");

            try
            {
                Assert.AreNotEqual(Updater.StartState.Trial, Updater.BeginStart(exe));
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }

            Assert.IsTrue(File.ReadAllText(exe) == "stable" ||
                          (File.Exists(exe + ".old") && File.ReadAllText(exe + ".old") == "stable"));
        }

        [TestMethod]
        public void ConfirmationSansEssaiConserveLaSauvegarde()
        {
            File.WriteAllText(exe, "candidate");
            File.WriteAllText(exe + ".old", "stable");

            Updater.ConfirmStart(exe);

            Assert.IsTrue(File.Exists(exe + ".old"));
            Assert.AreEqual("stable", File.ReadAllText(exe + ".old"));
        }

        [TestMethod]
        public void SecondRemplacementConserveLaVersionStable()
        {
            File.WriteAllText(exe, "stable");
            File.WriteAllText(staged, "candidate 1");
            Updater.ReplaceExecutable(staged, exe);
            Assert.AreEqual(Updater.StartState.Trial, Updater.BeginStart(exe));
            File.WriteAllText(staged, "candidate 2");

            try { Updater.ReplaceExecutable(staged, exe); }
            catch (IOException) { }
            catch (InvalidOperationException) { }

            Assert.AreEqual("stable", File.ReadAllText(exe + ".old"));
        }

        [TestMethod]
        public void RetourArriereInterrompuPeutReprendre()
        {
            File.WriteAllText(exe + ".old", "stable");
            File.WriteAllText(exe + ".bad", "candidate");
            File.WriteAllText(exe + ".trial", "essai");

            Updater.RollBack(exe);

            Assert.AreEqual("stable", File.ReadAllText(exe));
            Assert.AreEqual("candidate", File.ReadAllText(exe + ".bad"));
            Assert.IsFalse(File.Exists(exe + ".trial"));
        }
    }
}
