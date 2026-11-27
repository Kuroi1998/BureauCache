using System;
using System.Globalization;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BureauCache.Tests
{
    /// <summary>Météo (D-005) : lecture des réponses d'Open-Meteo et de BigDataCloud, codes WMO, URL.</summary>
    [TestClass]
    public class WeatherTests
    {
        // Réponse réelle d'Open-Meteo pour Lima (23/11/2026), abrégée
        private const string Forecast = "{\"latitude\":-12.0,\"longitude\":-77.0,\"timezone\":\"America/Lima\"," +
            "\"current_units\":{\"time\":\"iso8601\",\"temperature_2m\":\"°C\",\"weather_code\":\"wmo code\",\"is_day\":\"\"}," +
            "\"current\":{\"time\":\"2026-11-23T14:30\",\"interval\":900,\"temperature_2m\":27.0,\"apparent_temperature\":29.8,\"weather_code\":1,\"wind_speed_10m\":18.6,\"is_day\":1}," +
            "\"daily_units\":{\"time\":\"iso8601\",\"weather_code\":\"wmo code\"}," +
            "\"daily\":{\"time\":[\"2026-11-23\",\"2026-11-24\",\"2026-11-25\"],\"weather_code\":[51,51,3],\"temperature_2m_max\":[27.7,26.9,26.8],\"temperature_2m_min\":[23.3,23.8,23.6]}}";

        [TestMethod]
        public void Lecture_de_la_prevision()
        {
            WeatherReport r = Weather.ParseForecast(Forecast);
            Assert.IsNotNull(r);
            Assert.AreEqual(27.0, r.Temp, 1e-9);
            Assert.AreEqual(29.8, r.Feels, 1e-9);
            Assert.AreEqual(18.6, r.Wind, 1e-9);
            Assert.AreEqual(1, r.Code);
            Assert.IsTrue(r.IsDay);
            Assert.AreEqual(3, r.Days.Count);
            Assert.AreEqual(new DateTime(2026, 11, 24), r.Days[1].Date);
            Assert.AreEqual(51, r.Days[1].Code);
            Assert.AreEqual(26.9, r.Days[1].Max, 1e-9);
            Assert.AreEqual(23.8, r.Days[1].Min, 1e-9);
        }

        [TestMethod]
        public void Prevision_nuit_et_temperatures_negatives()
        {
            string json = "{\"current\":{\"temperature_2m\":-3.5,\"apparent_temperature\":-8,\"weather_code\":71,\"wind_speed_10m\":20,\"is_day\":0}}";
            WeatherReport r = Weather.ParseForecast(json);
            Assert.AreEqual(-3.5, r.Temp, 1e-9);
            Assert.IsFalse(r.IsDay);
            Assert.AreEqual(0, r.Days.Count);
        }

        [TestMethod]
        [DataRow("")]
        [DataRow("{}")]
        [DataRow("{\"error\":true,\"reason\":\"Latitude must be in range\"}")]
        [DataRow("{\"current\":{\"temperature_2m\":\"abc\"}}")]
        [DataRow("<html>erreur</html>")]
        [DataRow("{\"current\":{\"temperature_2m\":18,\"weather_code\":1.5}}")]
        [DataRow("{\"current\":{\"temperature_2m\":18,\"weather_code\":1e100}}")]
        public void Reponse_invalide_ignoree(string json)
        {
            Assert.IsNull(Weather.ParseForecast(json));
        }

        [TestMethod]
        public void DayNameHandlesTheLastRepresentableDate()
        {
            var report = new WeatherReport { LocalToday = DateTime.MaxValue.Date };
            DateTime previous = report.LocalToday.AddDays(-1);
            Assert.AreEqual(previous.ToString("dddd", L.Culture), Weather.DayName(report, previous));
        }

        [TestMethod]
        public void Lecture_de_la_recherche_de_ville()
        {
            string json = "{\"results\":[{\"id\":3936456,\"name\":\"Lima\",\"latitude\":-12.04318,\"longitude\":-77.02824,\"country_code\":\"PE\",\"admin1\":\"Lima\",\"country\":\"P\\u00e9rou\",\"postcodes\":[\"15001\"]}," +
                          "{\"id\":4517009,\"name\":\"Lima\",\"latitude\":40.74255,\"longitude\":-84.10523,\"admin1\":\"Ohio\",\"country\":\"États-Unis\"}],\"generationtime_ms\":0.5}";
            var places = Weather.ParseSearch(json);
            Assert.AreEqual(2, places.Count);
            Assert.AreEqual("Lima, Pérou", places[0].Label);             // région identique au nom : omise
            Assert.AreEqual("Lima, Ohio, États-Unis", places[1].Label);
            Assert.AreEqual(-12.04318, places[0].Lat, 1e-9);
        }

        [TestMethod]
        public void Recherche_sans_resultat()
        {
            Assert.AreEqual(0, Weather.ParseSearch("{\"generationtime_ms\":0.2}").Count);
        }

        [TestMethod]
        public void Lecture_du_nom_d_une_position()
        {
            // Format réel de BigDataCloud (espaces après les deux-points)
            string json = "{\n  \"latitude\": -12.05,\n  \"countryName\": \"Perou\",\n  \"principalSubdivision\": \"Lima\",\n  \"city\": \"Lima\",\n  \"locality\": \"district de Lima\"\n}";
            WeatherPlace p = Weather.ParseReverse(json, -12.05, -77.04);
            Assert.AreEqual("Lima, Perou", p.Label);
            Assert.AreEqual(-77.04, p.Lon, 1e-9);
            string sansVille = "{ \"city\": \"\", \"locality\": \"Baudour\", \"countryName\": \"Belgique\" }";
            Assert.AreEqual("Baudour, Belgique", Weather.ParseReverse(sansVille, 50.48, 3.83).Label);
            Assert.IsNull(Weather.ParseReverse("{ \"city\": \"\" }", 0, 0));
        }

        // Une virgule décimale (culture française) casserait l'URL
        [TestMethod]
        public void Url_independante_de_la_culture()
        {
            CultureInfo previous = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("fr-BE");
                string url = Weather.ForecastUrl(-12.04318, -77.02824, false);
                StringAssert.Contains(url, "latitude=-12.04&longitude=-77.03");
                Assert.IsFalse(url.Contains("fahrenheit"));
                StringAssert.Contains(Weather.ForecastUrl(40.7, -84.1, true), "temperature_unit=fahrenheit&wind_speed_unit=mph");
            }
            finally { Thread.CurrentThread.CurrentCulture = previous; }
        }

        [TestMethod]
        [DataRow(0, (int)Sky.Clear)]
        [DataRow(1, (int)Sky.MainlyClear)]
        [DataRow(2, (int)Sky.PartlyCloudy)]
        [DataRow(3, (int)Sky.Overcast)]
        [DataRow(45, (int)Sky.Fog)]
        [DataRow(48, (int)Sky.Fog)]
        [DataRow(53, (int)Sky.Drizzle)]
        [DataRow(57, (int)Sky.FreezingRain)]
        [DataRow(63, (int)Sky.Rain)]
        [DataRow(67, (int)Sky.FreezingRain)]
        [DataRow(75, (int)Sky.Snow)]
        [DataRow(77, (int)Sky.Snow)]
        [DataRow(81, (int)Sky.Showers)]
        [DataRow(86, (int)Sky.SnowShowers)]
        [DataRow(95, (int)Sky.Thunder)]
        [DataRow(99, (int)Sky.Thunder)]
        [DataRow(42, (int)Sky.Overcast)]      // code inconnu : couvert, sans planter
        public void Codes_WMO(int code, int expected)
        {
            Assert.AreEqual((Sky)expected, Weather.SkyOf(code));
            Assert.IsFalse(string.IsNullOrEmpty(Weather.Describe(code)));
        }

        [TestMethod]
        public void Dessin_de_toutes_les_icones_sans_erreur()
        {
            using (var bmp = new System.Drawing.Bitmap(64, 64))
            using (var g = System.Drawing.Graphics.FromImage(bmp))
                foreach (int code in new[] { 0, 1, 2, 3, 45, 51, 56, 61, 71, 80, 85, 95 })
                {
                    WeatherIcons.Draw(g, code, true, new System.Drawing.RectangleF(0, 0, 64, 64));
                    WeatherIcons.Draw(g, code, false, new System.Drawing.RectangleF(0, 0, 64, 64));
                }
        }

        [TestMethod]
        public void Echappements_JSON()
        {
            Assert.AreEqual("Pérou", Weather.Unescape("P\\u00e9rou"));
            Assert.AreEqual("a \"b\" c/d", Weather.Unescape("a \\\"b\\\" c\\/d"));
            Assert.AreEqual("ligne une ligne deux", Weather.Unescape("ligne une\\nligne deux"));
        }
    }
}
