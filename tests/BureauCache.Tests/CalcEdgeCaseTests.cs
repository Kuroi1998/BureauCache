using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BureauCache.Tests
{
    [TestClass]
    public class CalcEdgeCaseTests
    {
        // Le français groupe par espaces insécables et utilise la virgule décimale.
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

        [TestMethod]
        public void Groupement_anglais_incomplet_refuse()
        {
            using (LanguageScope.English())
                AssertRejected("12,34.5");
        }

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

        // M\u00EAme tol\u00E9rance que \u00AB 1,234.5 \u00BB en fran\u00E7ais : un nombre au format fran\u00E7ais coll\u00E9 alors que
        // l'interface est en anglais reste compris. Avec des espaces de groupement, la virgule est d\u00E9cimale.
        [TestMethod]
        [DataRow("1\u00A0234,5")]
        [DataRow("1\u202F234,5")]
        [DataRow("1 234,5")]
        public void Format_francais_colle_en_anglais_accepte(string expression)
        {
            using (LanguageScope.English())
                AssertValue(expression, 1234.5);
        }

        // T-007 : r\u00E8gles de lecture des nombres
        [TestMethod]
        [DataRow("1,000", false, 1.0)]          // FR : virgule d\u00E9cimale
        [DataRow("1,000", true, 1000.0)]        // EN : groupement
        [DataRow("0,500", true, 0.5)]           // pas de groupe de t\u00EAte nul : d\u00E9cimale
        [DataRow("1,5", true, 1.5)]             // EN tol\u00E8re la virgule d\u00E9cimale
        [DataRow("1,5+1.5", false, 3.0)]        // chaque nombre est lu seul
        [DataRow("12 345 678", false, 12345678.0)]
        [DataRow("2 000*3", false, 6000.0)]
        [DataRow("1 234.5", true, 1234.5)]      // espaces et point d\u00E9cimal
        [DataRow("1,234,567.25", false, 1234567.25)]
        public void Lecture_des_nombres(string expression, bool english, double expected)
        {
            using (english ? LanguageScope.English() : LanguageScope.French())
                AssertValue(expression, expected);
        }

        [TestMethod]
        [DataRow("1234 567")]                   // groupe de t\u00EAte de plus de trois chiffres
        [DataRow("1 23")]                       // groupe de moins de trois chiffres
        [DataRow("1 2345")]                     // groupe de plus de trois chiffres
        [DataRow("1 234,567,8")]                // espaces puis plusieurs virgules
        [DataRow("1 234,5.6")]                  // espaces, virgule et point
        [DataRow("1.2.3")]
        [DataRow("1,23,456")]
        public void Nombres_mal_formes_refuses(string expression)
        {
            using (LanguageScope.French())
                AssertRejected(expression);
            using (LanguageScope.English())
                AssertRejected(expression);
        }

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
        [DataRow("2/(1/0)")]
        [DataRow("(1/0)^0")]
        [DataRow("sqrt(-1)^0")]
        [DataRow("1/(1e200*1e200)")]
        public void Resultat_intermediaire_non_fini_refuse(string expression)
        {
            AssertRejected(expression);
        }

        [TestMethod]
        public void Les_grands_resultats_affiches_restent_calculables()
        {
            foreach (Func<LanguageScope> use in new Func<LanguageScope>[] { LanguageScope.French, LanguageScope.English, LanguageScope.Spanish })
                using (use())
                    AssertValue(Calc.Format(1.5e300), 1.5e300);
        }

        [TestMethod]
        public void Cosinus_d_un_angle_fini_tres_grand()
        {
            AssertValue("cos(1e308)", Math.Cos(1e308 * (Math.PI / 180)));
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

        [TestMethod]
        [DataRow(-9876.125)]
        [DataRow(1000.0)]
        [DataRow(1e100)]
        public void Aller_retour_nombres_groupes_en_francais(double value)
        {
            using (LanguageScope.French())
                AssertRoundTrip(value, "FR");
        }

        [TestMethod]
        public void Aller_retour_entier_a_un_groupement_en_anglais()
        {
            using (LanguageScope.English())
                AssertRoundTrip(1000.0, "EN");
        }

        [TestMethod]
        public void Aller_retour_entier_a_plusieurs_groupements_en_anglais()
        {
            using (LanguageScope.English())
                AssertRoundTrip(1e100, "EN");
        }

        [TestMethod]
        public void Aller_retour_pres_du_seuil_de_formatage_en_francais()
        {
            using (LanguageScope.French())
                AssertRoundTrip(1.04e-9, "FR");
        }

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
