using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BureauCache.Tests
{
    /// <summary>Analyseur JSON strict (revue QA-23), qui remplace la lecture par expressions régulières de la météo.</summary>
    [TestClass]
    public class JsonTests
    {
        [TestMethod]
        public void Valeurs_de_base()
        {
            var o = (Dictionary<string, object>)Json.Parse("{\"a\":1,\"b\":-2.5e-1,\"c\":\"x\",\"d\":true,\"e\":false,\"f\":null,\"g\":[1,\"2\",{}],\"h\":{\"i\":0}}");
            Assert.AreEqual(1.0, Json.Num(o, "a"));
            Assert.AreEqual(-0.25, Json.Num(o, "b"), 1e-12);
            Assert.AreEqual("x", Json.Str(o, "c"));
            Assert.AreEqual(true, o["d"]);
            Assert.AreEqual(false, o["e"]);
            Assert.IsNull(o["f"]);
            Assert.AreEqual(3, Json.List(o, "g").Count);
            Assert.AreEqual(0.0, Json.Num(Json.Obj(o, "h"), "i"));
        }

        [TestMethod]
        public void Acces_absents_ou_d_un_autre_type()
        {
            var o = Json.Parse("{\"n\":\"12\",\"s\":12}");
            Assert.IsTrue(double.IsNaN(Json.Num(o, "n")));   // un nombre en texte n'est pas un nombre
            Assert.IsNull(Json.Str(o, "s"));
            Assert.IsTrue(double.IsNaN(Json.Num(o, "absent")));
            Assert.IsNull(Json.Obj(null, "x"));
            Assert.IsNull(Json.List(o, "n"));
        }

        [TestMethod]
        [DataRow("")]
        [DataRow("{")]
        [DataRow("{\"a\":}")]
        [DataRow("{\"a\":-4x}")]
        [DataRow("{\"a\":01}")]
        [DataRow("{\"a\":1.}")]
        [DataRow("{\"a\":.5}")]
        [DataRow("{\"a\":1e}")]
        [DataRow("{\"a\":NaN}")]
        [DataRow("{\"a\":Infinity}")]
        [DataRow("{\"a\":1e999}")]
        [DataRow("{\"a\":\"\\x\"}")]
        [DataRow("{\"a\":\"non terminée}")]
        [DataRow("{\"a\":1} en trop")]
        [DataRow("{'a':1}")]
        [DataRow("[1,]")]
        public void Documents_invalides_refuses(string json)
        {
            Assert.ThrowsException<FormatException>(() => Json.Parse(json));
        }

        [TestMethod]
        public void Imbrication_limitee_sans_plantage()
        {
            Assert.ThrowsException<FormatException>(() => Json.Parse(new string('[', 200) + new string(']', 200)));
            Json.Parse(new string('[', 30) + new string(']', 30));
        }

        [TestMethod]
        public void Chaines_et_echappements()
        {
            Assert.AreEqual("A \"B\" \\ / é \U0001F986\n", Json.Parse("\"A \\\"B\\\" \\\\ \\/ \\u00e9 \\uD83E\\uDD86\\n\""));
            Assert.AreEqual("} ] {", Json.Str(Json.Parse("{\"s\":\"} ] {\"}"), "s"));
        }
    }
}
