using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BureauCache.Tests
{
    /// <summary>Signature des mises à jour (T-005, D-004) : Updater.VerifySignature et les clés embarquées.</summary>
    [TestClass]
    public class UpdaterSignatureTests
    {
        private static string publicKey, otherPublicKey, smallPublicKey;
        private static RSACryptoServiceProvider signer, smallSigner;
        private static readonly byte[] Hash = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes("BureauCache 3.4"));

        [ClassInitialize]
        public static void Keys(TestContext context)
        {
            signer = NewKey(3072);
            publicKey = signer.ToXmlString(false);
            using (RSACryptoServiceProvider other = NewKey(3072)) otherPublicKey = other.ToXmlString(false);
            smallSigner = NewKey(2048);
            smallPublicKey = smallSigner.ToXmlString(false);
        }

        [ClassCleanup]
        public static void Dispose()
        {
            signer.Dispose();
            smallSigner.Dispose();
        }

        private static RSACryptoServiceProvider NewKey(int bits)
        {
            var rsa = new RSACryptoServiceProvider(bits, new CspParameters(24));
            rsa.PersistKeyInCsp = false;
            return rsa;
        }

        private static string Sign(RSACryptoServiceProvider key, byte[] hash)
        {
            return Convert.ToBase64String(key.SignHash(hash, CryptoConfig.MapNameToOID("SHA256")));
        }

        [TestMethod]
        public void Signature_valide_acceptee()
        {
            Assert.IsTrue(Updater.VerifySignature(Hash, Sign(signer, Hash), new[] { publicKey }));
        }

        [TestMethod]
        public void Signature_valide_avec_espaces_et_fin_de_ligne()
        {
            Assert.IsTrue(Updater.VerifySignature(Hash, "  " + Sign(signer, Hash) + "\r\n", new[] { publicKey }));
        }

        [TestMethod]
        public void Cle_de_secours_acceptee()
        {
            Assert.IsTrue(Updater.VerifySignature(Hash, Sign(signer, Hash), new[] { otherPublicKey, publicKey }));
        }

        [TestMethod]
        public void Exe_modifie_refuse()
        {
            byte[] other = (byte[])Hash.Clone();
            other[0] ^= 1;
            Assert.IsFalse(Updater.VerifySignature(other, Sign(signer, Hash), new[] { publicKey }));
        }

        [TestMethod]
        public void Signature_alteree_refusee()
        {
            byte[] sig = Convert.FromBase64String(Sign(signer, Hash));
            sig[sig.Length / 2] ^= 1;
            Assert.IsFalse(Updater.VerifySignature(Hash, Convert.ToBase64String(sig), new[] { publicKey }));
        }

        [TestMethod]
        public void Autre_cle_refusee()
        {
            Assert.IsFalse(Updater.VerifySignature(Hash, Sign(signer, Hash), new[] { otherPublicKey }));
        }

        [TestMethod]
        public void Cle_trop_courte_refusee()
        {
            Assert.IsFalse(Updater.VerifySignature(Hash, Sign(smallSigner, Hash), new[] { smallPublicKey }));
        }

        [TestMethod]
        [DataRow("")]
        [DataRow("pas du base64 !")]
        [DataRow("AAAA")]
        public void Signature_illisible_refusee(string signature)
        {
            Assert.IsFalse(Updater.VerifySignature(Hash, signature, new[] { publicKey }));
        }

        [TestMethod]
        public void Sans_cle_refusee()
        {
            Assert.IsFalse(Updater.VerifySignature(Hash, Sign(signer, Hash), new string[0]));
        }

        [TestMethod]
        public void Cle_invalide_ignoree()
        {
            Assert.IsTrue(Updater.VerifySignature(Hash, Sign(signer, Hash), new[] { "<RSAKeyValue>cassée</RSAKeyValue>", publicKey }));
        }

        [TestMethod]
        public void Empreinte_invalide_refusee()
        {
            Assert.IsFalse(Updater.VerifySignature(new byte[20], Sign(signer, Hash), new[] { publicKey }));
            Assert.IsFalse(Updater.VerifySignature(null, Sign(signer, Hash), new[] { publicKey }));
        }

        [TestMethod]
        [DataRow("Kuroi1998/BureauCache", true)]
        [DataRow("kuroi1998/bureaucache", true)]
        [DataRow("quelquun/BureauCache", false)]
        [DataRow("", false)]
        public void Depot_officiel(string repository, bool expected)
        {
            Assert.AreEqual(expected, Updater.IsOfficial(repository));
        }

        // Garde-fou : sans clé embarquée, aucune mise à jour du dépôt officiel ne pourrait s'installer.
        // Les clés sont créées par outils\creer-cles.ps1.
        [TestMethod]
        public void Cles_embarquees_valides()
        {
            Assert.IsTrue(UpdateKeys.Trusted.Length >= 2, "UpdateKeys.Trusted doit contenir la clé principale et la clé de secours (outils\\creer-cles.ps1).");
            foreach (string key in UpdateKeys.Trusted)
            {
                StringAssert.DoesNotMatch(key, new System.Text.RegularExpressions.Regex("<D>|<P>|<Q>"), "Une clé PRIVÉE est dans le code !");
                using (var rsa = new RSACryptoServiceProvider(new CspParameters(24)))
                {
                    rsa.PersistKeyInCsp = false;
                    rsa.FromXmlString(key);
                    Assert.IsTrue(rsa.KeySize >= 3072, "Clé trop courte : " + rsa.KeySize + " bits");
                }
            }
            Assert.AreEqual(UpdateKeys.Trusted.Length, new System.Collections.Generic.HashSet<string>(UpdateKeys.Trusted).Count, "Clés en double");
        }
    }
}
