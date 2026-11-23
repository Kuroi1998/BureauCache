using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BureauCache.Tests
{
    [TestClass]
    public class CleanerWeatherReviewTests
    {
        private const string ForecastStart = "{\"current\":{\"temperature_2m\":-4,\"weather_code\":3}";
        private string sandbox;
        private string allowed;
        private string[] roots;

        [TestInitialize]
        public void CreateSandbox()
        {
            sandbox = Path.Combine(Path.GetTempPath(), "BureauCache.Review." + Guid.NewGuid().ToString("N"));
            allowed = Directory.CreateDirectory(Path.Combine(sandbox, "allowed")).FullName;
            roots = new[] { allowed + Path.DirectorySeparatorChar };
        }

        [TestCleanup]
        public void RemoveSandbox()
        {
            if (Directory.Exists(sandbox))
                Directory.Delete(sandbox, true);
        }

        [TestMethod]
        public void IsAllowed_RejectsSiblingWithTheSamePrefix()
        {
            string sibling = Path.Combine(sandbox, "allowed-sibling", "fixture.dat");
            Assert.IsFalse(Cleaner.IsAllowed(sibling, roots, new string[0]));
        }

        [TestMethod]
        public void IsAllowed_RejectsSiblingEvenWithoutTrailingSeparatorInRoot()
        {
            string sibling = Path.Combine(sandbox, "allowed-sibling", "fixture.dat");
            Assert.IsFalse(Cleaner.IsAllowed(sibling, new[] { allowed }, new string[0]));
        }

        [TestMethod]
        public void Files_OmitsForbiddenDescendants()
        {
            string blocked = Directory.CreateDirectory(Path.Combine(allowed, "blocked")).FullName;
            string visible = WriteFixture(Path.Combine(allowed, "cache.bin"));
            WriteFixture(Path.Combine(blocked, "keep.bin"));
            var files = Cleaner.Files(new CleanTarget { Dir = allowed }, roots,
                new[] { blocked + Path.DirectorySeparatorChar }, DateTime.UtcNow);

            CollectionAssert.AreEquivalent(new[] { visible }, files.Select(f => f.FullName).ToArray());
        }

        [TestMethod]
        public void Clean_PreservesForbiddenEmptyDirectories()
        {
            string blocked = Directory.CreateDirectory(Path.Combine(allowed, "blocked")).FullName;
            string empty = Directory.CreateDirectory(Path.Combine(blocked, "keep-empty")).FullName;
            CleanResult result = RunClean(new CleanTarget { Dir = allowed },
                new[] { blocked + Path.DirectorySeparatorChar });

            Assert.AreEqual(0, result.Deleted);
            Assert.AreEqual(0L, result.Freed);
            Assert.IsTrue(Directory.Exists(blocked));
            Assert.IsTrue(Directory.Exists(empty));
        }

        [TestMethod]
        public void Files_RequiresBothTimestampsToBeOldEnough()
        {
            DateTime now = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);
            string old = WriteFixture(Path.Combine(allowed, "old.bin"));
            SetTimes(old, now.AddHours(-24), now.AddHours(-24));
            SetTimes(WriteFixture(Path.Combine(allowed, "new-write.bin")), now.AddDays(-2), now.AddHours(-23));
            SetTimes(WriteFixture(Path.Combine(allowed, "new-creation.bin")), now.AddHours(-23), now.AddDays(-2));
            SetTimes(WriteFixture(Path.Combine(allowed, "future.bin")), now.AddHours(1), now.AddDays(-2));

            var target = new CleanTarget { Dir = allowed, MinAge = TimeSpan.FromHours(24) };
            CollectionAssert.AreEqual(new[] { old }, Cleaner.Files(target, roots, new string[0], now)
                .Select(f => f.FullName).ToArray());
        }

        [TestMethod]
        public void Clean_KeepsCacheWhileConfiguredProcessIsRunning()
        {
            string path = WriteFixture(Path.Combine(allowed, "cache.bin"));
            using (Process process = Process.GetCurrentProcess())
            {
                var target = new CleanTarget { Dir = allowed, Processes = new[] { process.ProcessName } };
                CleanResult result = RunClean(target, new string[0]);
                Assert.AreEqual(0, result.Deleted);
                Assert.AreEqual(0L, result.Freed);
                Assert.IsTrue(File.Exists(path));
            }
        }

        [TestMethod]
        public void Clean_ReportsLockedFileAndRemovesUnicodeFixture()
        {
            string locked = WriteFixture(Path.Combine(allowed, "locked.bin"));
            string unicode = WriteFixture(Path.Combine(allowed, "caf\u00e9-\u6f22\u5b57-\U0001F986.bin"));
            CleanResult result;
            using (File.Open(locked, FileMode.Open, FileAccess.Read, FileShare.None))
                result = RunClean(new CleanTarget { Dir = allowed }, new string[0]);

            Assert.IsTrue(File.Exists(locked));
            Assert.IsFalse(File.Exists(unicode));
            Assert.IsTrue(Directory.Exists(allowed));
            Assert.AreEqual(1, result.Deleted);
            Assert.AreEqual(1, result.Skipped);
            Assert.AreEqual(3L, result.Freed);
        }

        [TestMethod]
        public void Clean_LeavesFileOpenForWritingEvenIfItsHandleSharesDeletion()
        {
            string path = WriteFixture(Path.Combine(allowed, "being-written.bin"));
            CleanResult result;
            using (File.Open(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
                result = RunClean(new CleanTarget { Dir = allowed }, new string[0]);

            Assert.IsTrue(File.Exists(path));
            Assert.AreEqual(0, result.Deleted);
            Assert.AreEqual(0L, result.Freed);
        }

        [TestMethod]
        [DataRow("ab", "ab*ab", false)]
        [DataRow("abab", "ab*ab", true)]
        [DataRow("CAF\u00c9.bin", "caf\u00e9*.BIN", true)]
        [DataRow("caf\u00e9.txt", "caf\u00e9*.BIN", false)]
        public void Matches_ChecksWholeNameWithoutOverlappingPrefixAndSuffix(string name, string pattern, bool expected)
        {
            Assert.AreEqual(expected, Cleaner.Matches(name, pattern));
        }

        [TestMethod]
        public void ParseForecast_AcceptsNegativeValuesPositiveExponentsAndBracesInStrings()
        {
            string json = "{\"current\":{\"note\":\"} ] \\\" {\",\"temperature_2m\":-1.25e+1,"
                + "\"weather_code\":7.1E1,\"apparent_temperature\":-2.55e1,\"wind_speed_10m\":3e+0,\"is_day\":0}}";
            WeatherReport report = Weather.ParseForecast(json);

            Assert.IsNotNull(report);
            Assert.AreEqual(-12.5, report.Temp, 1e-9);
            Assert.AreEqual(-25.5, report.Feels, 1e-9);
            Assert.AreEqual(3.0, report.Wind, 1e-9);
            Assert.AreEqual(71, report.Code);
            Assert.IsFalse(report.IsDay);
        }

        [TestMethod]
        public void ParseForecast_AcceptsNegativeExponents()
        {
            string json = "{\"current\":{\"temperature_2m\":-4,\"weather_code\":3,\"wind_speed_10m\":3e-1}}";
            WeatherReport report = Weather.ParseForecast(json);

            Assert.IsNotNull(report);
            Assert.AreEqual(0.3, report.Wind, 1e-9);
        }

        [TestMethod]
        public void ParseForecast_DoesNotTakeCurrentFromAnUnrelatedNestedObject()
        {
            string json = "{\"metadata\":{\"current\":{\"temperature_2m\":99,\"weather_code\":0}},"
                + "\"current\":{\"temperature_2m\":-4,\"weather_code\":3}}";
            WeatherReport report = Weather.ParseForecast(json);

            Assert.IsNotNull(report);
            Assert.AreEqual(-4.0, report.Temp, 1e-9);
            Assert.AreEqual(3, report.Code);
        }

        [TestMethod]
        public void ParseForecast_KeepsMissingOptionalMeasurementsUnavailable()
        {
            WeatherReport report = Weather.ParseForecast(ForecastStart + "}");
            Assert.IsNotNull(report);
            Assert.IsTrue(double.IsNaN(report.Feels), "Un ressenti absent ne vaut pas zéro.");
            Assert.IsTrue(double.IsNaN(report.Wind), "Un vent absent ne vaut pas zéro.");
        }

        [TestMethod]
        public void ParseForecast_RejectsNonFiniteDailyTemperatures()
        {
            string json = ForecastStart + ",\"daily\":{\"time\":[\"2026-01-15\",\"2026-01-16\"],"
                + "\"weather_code\":[3,1],\"temperature_2m_max\":[\"NaN\",5],\"temperature_2m_min\":[-2,1]}}";
            WeatherReport report = Weather.ParseForecast(json);

            Assert.IsNotNull(report);
            Assert.AreEqual(1, report.Days.Count);
            Assert.AreEqual(new DateTime(2026, 1, 16), report.Days[0].Date);
        }

        [TestMethod]
        public void ParseForecast_DoesNotTurnAnInvalidNumericTokenIntoAValue()
        {
            Assert.IsNull(Weather.ParseForecast("{\"current\":{\"temperature_2m\":-4x,\"weather_code\":3}}"));
        }

        [TestMethod]
        public void ParseSearch_HandlesBracesAndEscapedQuotesInNamesAndRejectsInvalidCoordinates()
        {
            string json = "{\"results\":[{\"name\":\"A {B} \\\"C\\\"\",\"latitude\":-1.2e1,\"longitude\":2e1},"
                + "{\"name\":\"NaN\",\"latitude\":\"NaN\",\"longitude\":20},"
                + "{\"name\":\"Outside\",\"latitude\":91,\"longitude\":20}]}";
            var places = Weather.ParseSearch(json);
            Assert.AreEqual(1, places.Count);
            Assert.AreEqual("A {B} \"C\"", places[0].Name);
            Assert.AreEqual(-12.0, places[0].Lat, 1e-9);
            Assert.AreEqual(20.0, places[0].Lon, 1e-9);
        }

        [TestMethod]
        public void ParseSearch_PreservesAResultWithNestedMetadata()
        {
            string json = "{\"results\":[{\"name\":\"Mons\",\"latitude\":50.45,\"longitude\":3.95,"
                + "\"metadata\":{\"name\":\"Other\",\"latitude\":1,\"longitude\":2}}]}";
            var places = Weather.ParseSearch(json);

            Assert.AreEqual(1, places.Count);
            Assert.AreEqual("Mons", places[0].Name);
            Assert.AreEqual(50.45, places[0].Lat, 1e-9);
        }

        [TestMethod]
        public void ParseReverse_IgnoresNestedCityWhenSelectingTopLevelLocality()
        {
            string json = "{\"meta\":{\"city\":\"Other\"},\"city\":\"\",\"locality\":\"Mons\"}";
            WeatherPlace place = Weather.ParseReverse(json, 50.45, 3.95);
            Assert.IsNotNull(place);
            Assert.AreEqual("Mons", place.Name);
        }

        [TestMethod]
        public void ParseReverse_RejectsNonFiniteOrOutOfRangeCoordinates()
        {
            const string json = "{\"city\":\"Mons\"}";
            Assert.IsNull(Weather.ParseReverse(json, double.NaN, 3.95));
            Assert.IsNull(Weather.ParseReverse(json, 50.45, double.PositiveInfinity));
            Assert.IsNull(Weather.ParseReverse(json, 50.45, -181));
        }

        [TestMethod]
        public void ForecastUrl_UsesExpectedHttpsEndpointAndLocalDailyDates()
        {
            var uri = new Uri(Weather.ForecastUrl(90, -180, false));
            Assert.AreEqual(Uri.UriSchemeHttps, uri.Scheme);
            Assert.AreEqual("api.open-meteo.com", uri.Host);
            Assert.AreEqual("/v1/forecast", uri.AbsolutePath);
            StringAssert.Contains(uri.Query, "latitude=90&longitude=-180");
            StringAssert.Contains(uri.Query, "&timezone=auto&forecast_days=3");
        }

        [TestMethod]
        public void ForecastUrl_RejectsNonFiniteOrOutOfRangeCoordinates()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => Weather.ForecastUrl(double.NaN, 3.95, false));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => Weather.ForecastUrl(50.45, double.PositiveInfinity, false));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => Weather.ForecastUrl(-91, 3.95, false));
        }

        [TestMethod]
        public void Unescape_DecodesSupplementaryUnicodeAndNormalizesLineWhitespace()
        {
            Assert.AreEqual("A \U0001F986 B / \\", Weather.Unescape("A\\t\\uD83E\\uDD86\\rB \\/ \\\\"));
        }

        [TestMethod]
        public void Unescape_NormalizesEscapedBackspaceAndFormFeedForDisplay()
        {
            Assert.AreEqual("a b c", Weather.Unescape("a\\bb\\fc"));
        }

        private static string WriteFixture(string path)
        {
            File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
            return path;
        }

        private static void SetTimes(string path, DateTime created, DateTime written)
        {
            File.SetCreationTimeUtc(path, created);
            File.SetLastWriteTimeUtc(path, written);
        }

        private CleanResult RunClean(CleanTarget target, string[] forbidden)
        {
            var category = new CleanCategory { Id = "qa-fixtures", Checked = true };
            category.Targets.Add(target);
            return Cleaner.Clean(new[] { category }, roots, forbidden);
        }
    }
}
