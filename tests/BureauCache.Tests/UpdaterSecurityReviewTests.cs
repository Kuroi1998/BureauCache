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
        [DataRow("owner/repo", true)]
        [DataRow("owner/repo.v2", true)]
        [DataRow("../repo", false)]
        [DataRow("owner/.", false)]
        [DataRow("owner/..", false)]
        [DataRow("owner/repo\n", false)]
        public void RepositoryMustContainTwoOrdinaryPathSegments(string name, bool valid)
        {
            Assert.AreEqual(valid, Updater.ValidRepository(name));
        }

        [TestMethod]
        [DataRow("https://github.com/owner/repo", true)]
        [DataRow("https://release-assets.githubusercontent.com/example?x=1&y=2", true)]
        [DataRow("http://github.com/owner/repo", false)]
        [DataRow("https://github.com:444/owner/repo", false)]
        [DataRow("https://user@github.com/owner/repo", false)]
        [DataRow("https://github.com.example/owner/repo", false)]
        [DataRow("https://github.com/owner/repo\n", false)]
        public void DownloadAcceptsOnlyTheExpectedHttpsEndpoints(string url, bool valid)
        {
            Assert.AreEqual(valid, Updater.TrustedUrl(url));
        }

        [TestMethod]
        public void AutomaticUpdateRecoversFromAClockRollback()
        {
            var now = new DateTime(2026, 11, 27, 18, 0, 0);
            Assert.IsTrue(Updater.AutomaticCheckDue(now.AddHours(2).Ticks, now));
            Assert.IsTrue(Updater.AutomaticCheckDue(long.MaxValue, now));
            Assert.IsTrue(Updater.AutomaticCheckDue(0, now));
            Assert.IsFalse(Updater.AutomaticCheckDue(now.AddHours(-24).AddTicks(1).Ticks, now));
            Assert.IsTrue(Updater.AutomaticCheckDue(now.AddHours(-24).Ticks, now));
        }

        [TestMethod]
        [DataRow("v")]
        [DataRow("v999.1-preview")]
        [DataRow("v2147483648.1")]
        [DataRow("v999. 1")]
        public void InvalidReleaseTagIsReportedInsteadOfBeingTreatedAsUpToDate(string tag)
        {
            string json = "{\"tag_name\":\"" + tag + "\",\"assets\":[]}";
            Assert.ThrowsException<IOException>(() => Updater.ParseRelease(json, Updater.DefaultRepo));
        }

        [TestMethod]
        public void ReleaseUsesOnlyTopLevelTagAndNamedAssets()
        {
            const string prefix = "https://github.com/sample/project/releases/download/v999.1/";
            string json = "{\"example\":{\"tag_name\":\"v1.0\",\"browser_download_url\":\"" + prefix + "other/BureauCache.exe\"}," +
                "\"tag_name\":\"v999.1\",\"body\":\"Changes\\nversion\",\"assets\":[" +
                "{\"name\":\"BureauCache.exe\",\"browser_download_url\":\"" + prefix + "BureauCache.exe\"}," +
                "{\"name\":\"BureauCache.exe.sha256\",\"browser_download_url\":\"" + prefix + "BureauCache.exe.sha256\"}," +
                "{\"name\":\"motif.txt\",\"browser_download_url\":\"" + prefix + "BureauCache.exe.sig\"}]}";

            Updater.Release official = Updater.ParseRelease(json, Updater.DefaultRepo);
            Assert.AreEqual("999.1", official.Version);
            Assert.AreEqual("Changes\nversion", official.Notes);
            Assert.AreEqual(prefix + "BureauCache.exe", official.Url);
            Assert.IsNull(official.SigUrl);
            Assert.IsFalse(official.Available);
            Assert.IsTrue(official.Incomplete);
            Assert.IsTrue(Updater.ParseRelease(json, "sample/project").Available);

            string signedJson = json.Replace("motif.txt", "BureauCache.exe.sig");
            Assert.IsTrue(Updater.ParseRelease(signedJson, Updater.DefaultRepo).Available);
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
