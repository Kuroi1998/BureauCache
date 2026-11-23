using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BureauCache.Tests
{
    [TestClass]
    public class CalcEdgeCaseTests
    {
        // Le français groupe par espaces insécables et utilise la virgule décimale.
        [Ignore("Défaut confirmé R-19 : les espaces insécables de groupement sont refusés.")]
        [TestMethod]
        [DataRow("1\u00A0234,5")]
        [DataRow("1\u202F234,5")]
        public void Separateurs_francais_acceptes(string expression)
        {
            using (LanguageScope.French())
                AssertValue(expression, 1234.5);
        }

        // Choix de conception (Claude, revue T-002) : un nombre au format anglais collé alors que
        // l'interface est en français reste compris. La tolérance est voulue, ce n'est pas un défaut.
        [TestMethod]
        public void Format_anglais_colle_en_francais_accepte()
        {
            using (LanguageScope.French())
                AssertValue("1,234.5", 1234.5);
        }

        [Ignore("Décision en attente (R-16) : faut-il refuser un groupe de chiffres mal placé ?")]
        [TestMethod]
        public void Groupement_francais_incomplet_refuse()
        {
            using (LanguageScope.French())
                AssertRejected("12,34.5");
        }

        // En anglais américain, seule une virgule de groupement bien placée
        // peut précéder le point décimal dans un nombre groupé.
        [TestMethod]
        public void Separateurs_anglais_valides()
        {
            using (LanguageScope.English())
                AssertValue("1,234.5", 1234.5);
        }

        [Ignore("Décision en attente (R-16) : faut-il refuser un groupe de chiffres mal placé ?")]
        [TestMethod]
        public void Groupement_anglais_incomplet_refuse()
        {
            using (LanguageScope.English())
                AssertRejected("12,34.5");
        }

        [Ignore("Défaut confirmé R-16 : 1.234,5 est évalué à 1,2345 sans message.")]
        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void Virgule_apres_le_point_refusee(bool english)
        {
            if (english)
            {
                using (LanguageScope.English())
                    AssertRejected("1.234,5");
            }
            else
            {
                using (LanguageScope.French())
                    AssertRejected("1.234,5");
            }
        }

        [TestMethod]
        public void Virgules_multiples_mal_placees_refusees()
        {
            using (LanguageScope.French())
                AssertRejected("1,2,3");
            using (LanguageScope.English())
                AssertRejected("1,2,3");
        }

        [TestMethod]
        [DataRow("1\u00A0234,5")]
        [DataRow("1\u202F234,5")]
        public void Espaces_de_groupement_francais_refuses_en_anglais(string expression)
        {
            using (LanguageScope.English())
                AssertRejected(expression);
        }

        [Ignore("Décision en attente (R-20) : -2^2 vaut 4 aujourd'hui (convention d'Excel), -4 en notation mathématique.")]
        [TestMethod]
        public void Puissance_prioritaire_sur_le_moins_unaire()
        {
            AssertValue("-2^2", -4.0);
        }

        // « 50%% » n'a pas de sens usuel : le refuser est acceptable (test de ChatGPT écarté).
        [TestMethod]
        public void Pourcentage_double_refuse()
        {
            AssertRejected("50%%");
        }

        [TestMethod]
        [DataRow("2^-1", 0.5)]
        [DataRow("--3", 3.0)]
        [DataRow("10%*10%", 0.01)]
        [DataRow("1e5", 100000.0)]
        [DataRow("1E-5", 0.00001)]
        [DataRow("1e+5", 100000.0)]
        [DataRow("1.e5", 100000.0)]
        [DataRow(".5", 0.5)]
        [DataRow("5.", 5.0)]
        [DataRow("SQRT(4)", 2.0)]
        [DataRow("sqrt (4)", 2.0)]
        [DataRow("1e150*1e150", 1e300)]
        [DataRow("1e-150*1e-150", 1e-300)]
        public void Expressions_valides(string expression, double expected)
        {
            using (LanguageScope.English())
                AssertValue(expression, expected);
        }

        [TestMethod]
        [DataRow("1e5e5")]
        [DataRow("e5")]
        [DataRow("sinus(90)")]
        [DataRow("sqrt 4")]
        [DataRow("2pi")]
        [DataRow("2e")]
        public void Syntaxes_invalides_refusees(string expression)
        {
            AssertRejected(expression);
        }

        [TestMethod]
        public void X_detecte_comme_multiplication_entre_nombres()
        {
            Assert.IsTrue(Calc.LooksLikeMath("2x3"));
            AssertValue("2x3", 6.0);
        }

        [TestMethod]
        [DataRow("xbox360")]
        [DataRow("1+")]
        public void Texte_non_calculable_refuse_par_les_deux_entrees(string expression)
        {
            Assert.IsFalse(Calc.LooksLikeMath(expression), expression);
            AssertRejected(expression);
        }

        [TestMethod]
        public void Expression_de_200_caracteres_acceptee()
        {
            string expression = "10" + string.Concat(Enumerable.Repeat("+1", 99));
            Assert.AreEqual(200, expression.Length);
            AssertValue(expression, 109.0);
        }

        [TestMethod]
        public void Expression_de_201_caracteres_refusee()
        {
            string expression = "10" + string.Concat(Enumerable.Repeat("+1", 98)) + "+10";
            Assert.AreEqual(201, expression.Length);
            AssertRejected(expression);
        }

        [TestMethod]
        public void A_20_paires_de_parentheses_expression_acceptee()
        {
            AssertValue(ParenthesizedOne(20), 1.0);
        }

        // MaxDepth = 64 compte les appels récursifs (3 par paire), pas les paires : la limite réelle
        // est d'environ 21 paires. C'est une protection contre le débordement de pile, largement
        // suffisante pour un lanceur. Le contrat « 64 paires » proposé par ChatGPT est écarté.
        [TestMethod]
        public void A_65_paires_de_parentheses_expression_refusee()
        {
            AssertRejected(ParenthesizedOne(65));
        }

        [TestMethod]
        public void Zero_negatif_conserve_en_calcul_et_affiche_comme_zero()
        {
            using (LanguageScope.French())
            {
                double value;
                Assert.IsTrue(Calc.TryEval("-0", out value));
                Assert.AreEqual(long.MinValue, BitConverter.DoubleToInt64Bits(value));
                Assert.AreEqual("0", Calc.Format(value));
            }
        }

        [TestMethod]
        [DataRow(0.0)]
        [DataRow(1.0)]
        [DataRow(-1.0)]
        [DataRow(0.001)]
        [DataRow(-0.0625)]
        [DataRow(0.3333333333)]
        [DataRow(12.3456789012)]
        [DataRow(999.9999999999)]
        [DataRow(1e-9)]
        [DataRow(-5e-10)]
        [DataRow(1e-300)]
        public void Aller_retour_sans_groupement_dans_les_deux_langues(double value)
        {
            using (LanguageScope.French())
                AssertRoundTrip(value, "FR");
            using (LanguageScope.English())
                AssertRoundTrip(value, "EN");
        }

        [TestMethod]
        public void Aller_retour_fraction_groupee_en_anglais()
        {
            using (LanguageScope.English())
                AssertRoundTrip(-9876.125, "EN");
        }

        [Ignore("Défaut confirmé R-19 : TryEval refuse l'espace de groupement produit par Format.")]
        [TestMethod]
        [DataRow(-9876.125)]
        [DataRow(1000.0)]
        [DataRow(1e100)]
        public void Aller_retour_nombres_groupes_en_francais(double value)
        {
            using (LanguageScope.French())
                AssertRoundTrip(value, "FR");
        }

        [Ignore("Défaut confirmé R-19 : en anglais, 1,000 est relu comme 1 sans message.")]
        [TestMethod]
        public void Aller_retour_entier_a_un_groupement_en_anglais()
        {
            using (LanguageScope.English())
                AssertRoundTrip(1000.0, "EN");
        }

        [Ignore("Défaut confirmé R-19 : plusieurs virgules de groupement sans point sont refusées.")]
        [TestMethod]
        public void Aller_retour_entier_a_plusieurs_groupements_en_anglais()
        {
            using (LanguageScope.English())
                AssertRoundTrip(1e100, "EN");
        }

        [Ignore("Défaut confirmé R-21 : Format garde 10 décimales, 1.04e-9 devient 0,000000001.")]
        [TestMethod]
        public void Aller_retour_pres_du_seuil_de_formatage_en_francais()
        {
            using (LanguageScope.French())
                AssertRoundTrip(1.04e-9, "FR");
        }

        [Ignore("Défaut confirmé R-21 : Format garde 10 décimales, 1.04e-9 devient 0,000000001.")]
        [TestMethod]
        public void Aller_retour_pres_du_seuil_de_formatage_en_anglais()
        {
            using (LanguageScope.English())
                AssertRoundTrip(1.04e-9, "EN");
        }

        private static string ParenthesizedOne(int pairs)
        {
            return new string('(', pairs) + "1" + new string(')', pairs);
        }

        private static void AssertValue(string expression, double expected)
        {
            double actual;
            Assert.IsTrue(Calc.TryEval(expression, out actual), "Expression refusée : " + expression);
            Assert.AreEqual(expected, actual, Math.Abs(expected) * 1e-12, expression);
        }

        private static void AssertRejected(string expression)
        {
            double actual;
            Assert.IsFalse(Calc.TryEval(expression, out actual), "Expression acceptée : " + expression + " -> " + actual);
        }

        private static void AssertRoundTrip(double expected, string language)
        {
            string formatted = Calc.Format(expected);
            string context = language + " : « " + formatted + " »";
            double actual;
            Assert.IsTrue(Calc.TryEval(formatted, out actual), context + " refusé");
            Assert.AreEqual(expected, actual, Math.Abs(expected) * 1e-9, context);
        }
    }
}
