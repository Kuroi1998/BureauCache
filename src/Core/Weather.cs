// BureauCache — Weather
using System;
using System.Collections.Generic;
using System.Device.Location;
using System.Globalization;
using System.Text;
using System.Threading;

// =====================================================================
//  Météo (décision D-005) : Open-Meteo pour les prévisions et la recherche
//  de ville, BigDataCloud pour nommer une position détectée par Windows.
//  Rien n'est envoyé tant que l'utilisateur n'a pas choisi un lieu.
// =====================================================================
enum Sky { Clear, MainlyClear, PartlyCloudy, Overcast, Fog, Drizzle, Rain, FreezingRain, Snow, Showers, SnowShowers, Thunder }

class WeatherDay
{
    public DateTime Date;
    public int Code;
    public double Max, Min;
}

class WeatherReport
{
    public double Temp, Feels, Wind;
    public int Code;
    public bool IsDay = true;
    public List<WeatherDay> Days = new List<WeatherDay>();
    public DateTime Fetched;
    public DateTime LocalToday;   // date du jour dans le fuseau du lieu (pour « Aujourd'hui », « Demain »)
}

class WeatherPlace
{
    public string Name, Admin1, Country;
    public double Lat, Lon;

    // « Ville, Pays » pour le panneau, construit à partir des champs : une virgule dans le nom ne gêne pas (QA52-05)
    public string Short
    {
        get { return string.IsNullOrEmpty(Country) ? (Name ?? "") : (Name ?? "") + ", " + Country; }
    }

    // « Baudour, Wallonie, Belgique » ; la région est omise si elle répète le nom
    public string Label
    {
        get
        {
            List<string> parts = new List<string> { Name };
            if (!string.IsNullOrEmpty(Admin1) && !string.Equals(Admin1, Name, StringComparison.OrdinalIgnoreCase)) parts.Add(Admin1);
            if (!string.IsNullOrEmpty(Country)) parts.Add(Country);
            return string.Join(", ", parts.FindAll(p => !string.IsNullOrEmpty(p)));
        }
    }
}

static class Weather
{
    public static readonly string[] Hosts = { "api.open-meteo.com", "geocoding-api.open-meteo.com", "api.bigdatacloud.net" };
    public const string Attribution = "Open-Meteo.com (CC BY 4.0)";
    public const string AttributionUrl = "https://open-meteo.com/";
    const long MaxResponse = 512 * 1024;
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static WeatherReport Current;
    public static bool Failed;            // la dernière tentative a échoué (tuile : « indisponible »)
    static DateTime lastAttempt = DateTime.MinValue;
    static int refreshing, generation;    // generation change à chaque changement de lieu (revue QA-23)
    static readonly object sync = new object();

    public static bool Fahrenheit
    {
        get { try { return !RegionInfo.CurrentRegion.IsMetric; } catch { return false; } }
    }
    public static string Unit { get { return Fahrenheit ? "°F" : "°C"; } }
    public static string WindUnit { get { return Fahrenheit ? "mph" : "km/h"; } }

    public static bool Configured
    {
        get { return Settings.WeatherOn && !string.IsNullOrEmpty(Settings.WeatherPlace) && !double.IsNaN(Settings.WeatherLat) && !double.IsNaN(Settings.WeatherLon); }
    }

    // À rafraîchir : rien depuis 30 min (une tentative au plus toutes les 5 min en cas d'échec)
    public static bool Due
    {
        get
        {
            if (!Configured || refreshing != 0) return false;
            DateTime now = DateTime.Now;
            bool stale = Current == null || now - Current.Fetched > TimeSpan.FromMinutes(30);
            return stale && now - lastAttempt > TimeSpan.FromMinutes(5);
        }
    }

    // Lieu changé ou météo masquée : on oublie la météo de l'ancien lieu, et une réponse en cours pour l'ancien lieu
    // sera ignorée à son arrivée
    public static void Reset()
    {
        lock (sync)
        {
            generation++;
            Current = null;
            Failed = false;
            lastAttempt = DateTime.MinValue;
        }
    }

    public static void RefreshAsync(Action done)
    {
        if (Interlocked.CompareExchange(ref refreshing, 1, 0) != 0) return;
        int gen;
        double lat, lon;
        lock (sync) { gen = generation; lastAttempt = DateTime.Now; lat = Settings.WeatherLat; lon = Settings.WeatherLon; }
        bool f = Fahrenheit;
        Background.Run(() =>
        {
            WeatherReport r = null;
            try { r = ParseForecast(Http.FetchText(ForecastUrl(lat, lon, f), Hosts, MaxResponse)); }
            catch { }
            finally
            {
                lock (sync)
                {
                    if (gen == generation && Settings.WeatherOn)
                    {
                        if (r != null) { r.Fetched = DateTime.Now; Current = r; }
                        Failed = r == null;
                    }
                }
                Interlocked.Exchange(ref refreshing, 0);
                if (done != null) done();
            }
        });
    }

    // ---------- URLs ----------
    internal static string ForecastUrl(double lat, double lon, bool fahrenheit)
    {
        if (!ValidCoord(lat, 90)) throw new ArgumentOutOfRangeException("lat");
        if (!ValidCoord(lon, 180)) throw new ArgumentOutOfRangeException("lon");
        return "https://api.open-meteo.com/v1/forecast?latitude=" + lat.ToString("0.##", Inv) + "&longitude=" + lon.ToString("0.##", Inv) +
            "&current=temperature_2m,apparent_temperature,weather_code,wind_speed_10m,is_day" +
            "&daily=weather_code,temperature_2m_max,temperature_2m_min&timezone=auto&forecast_days=3" +
            (fahrenheit ? "&temperature_unit=fahrenheit&wind_speed_unit=mph" : "");
    }

    static string Lang { get { return L.Culture.TwoLetterISOLanguageName; } }

    // ---------- Recherche et détection (appels bloquants : à lancer en arrière-plan) ----------
    public static List<WeatherPlace> Search(string name)
    {
        string url = "https://geocoding-api.open-meteo.com/v1/search?name=" + Uri.EscapeDataString(name.Trim()) +
            "&count=8&language=" + Lang + "&format=json";
        return ParseSearch(Http.FetchText(url, Hosts, MaxResponse));
    }

    // Position Windows (si l'utilisateur l'autorise dans Windows), puis nom de la ville via BigDataCloud
    public static WeatherPlace Locate(out string error)
    {
        error = null;
        double lat, lon;
        using (GeoCoordinateWatcher watcher = new GeoCoordinateWatcher(GeoPositionAccuracy.Default))
        {
            watcher.TryStart(false, TimeSpan.FromSeconds(10));
            if (watcher.Permission == GeoPositionPermission.Denied)
            {
                error = L.T("Windows refuse l'accès à la position (Paramètres > Confidentialité > Emplacement).",
                            "Windows denies access to the location (Settings > Privacy > Location).", "Windows deniega el acceso a la ubicación (Configuración > Privacidad > Ubicación).");
                return null;
            }
            for (int i = 0; i < 20 && watcher.Position.Location.IsUnknown; i++) Thread.Sleep(250);
            GeoCoordinate c = watcher.Position.Location;
            watcher.Stop();
            if (c.IsUnknown)
            {
                error = L.T("Windows ne connaît pas la position de ce PC. Tape plutôt le nom de ta ville.",
                            "Windows doesn't know this PC's location. Type your city instead.", "Windows desconoce la ubicación del equipo. Escribe tu ciudad.");
                return null;
            }
            lat = Math.Round(c.Latitude, 3);
            lon = Math.Round(c.Longitude, 3);
        }
        WeatherPlace p = null;
        try
        {
            // Environ 1 km : assez pour une ville et une prévision, sans révéler l'adresse (AUD-03)
            string url = "https://api.bigdatacloud.net/data/reverse-geocode-client?latitude=" + lat.ToString("0.##", Inv) +
                "&longitude=" + lon.ToString("0.##", Inv) + "&localityLanguage=" + Lang;
            p = ParseReverse(Http.FetchText(url, Hosts, MaxResponse), lat, lon);
        }
        catch { }
        return p ?? new WeatherPlace { Name = L.T("Ma position", "My location", "Mi ubicación"), Lat = lat, Lon = lon };
    }

    // ---------- Lecture des réponses JSON (analyseur strict Json, revue QA-23) ----------
    // Texte destiné à l'affichage : caractères de contrôle (\t, \n, \b, \f…) remplacés par des espaces
    // Lieu enregistré par une version précédente, sans forme courte : premier et dernier morceaux du libellé
    // (« Saint-Ghislain, wallonne, Région, Belgique » → « Saint-Ghislain, Belgique »). Les lieux choisis depuis la
    // 3.10.1 ont leur forme courte enregistrée (WeatherShort), construite à partir des champs (QA52-05).
    public static string ShortPlace(string label)
    {
        if (string.IsNullOrEmpty(label)) return label ?? "";
        string[] parts = label.Split(',');
        if (parts.Length <= 2) return label.Trim();
        return parts[0].Trim() + ", " + parts[parts.Length - 1].Trim();
    }

    internal static string Display(string s)
    {
        if (s == null) return null;
        StringBuilder sb = new StringBuilder(s.Length);
        foreach (char c in s) sb.Append(char.IsControl(c) ? ' ' : c);
        return sb.ToString();
    }

    // Contenu d'une chaîne JSON décodé pour l'affichage
    internal static string Unescape(string s) { return Display(Json.Decode(s)); }

    static object Root(string json)
    {
        try { return Json.Parse(json); }
        catch (FormatException) { return null; }
    }

    static bool Finite(double v) { return !double.IsNaN(v) && !double.IsInfinity(v); }
    static bool ValidCoord(double v, double limit) { return Finite(v) && Math.Abs(v) <= limit; }

    internal static WeatherReport ParseForecast(string json)
    {
        object root = Root(json);
        Dictionary<string, object> cur = Json.Obj(root, "current");
        if (cur == null) return null;
        WeatherReport r = new WeatherReport { Temp = Json.Num(cur, "temperature_2m"), Feels = Json.Num(cur, "apparent_temperature"), Wind = Json.Num(cur, "wind_speed_10m") };
        double code = Json.Num(cur, "weather_code"), isDay = Json.Num(cur, "is_day");
        if (!Finite(r.Temp) || !Finite(code)) return null;
        r.Code = (int)code;
        if (Finite(isDay)) r.IsDay = isDay != 0;
        string now = Json.Str(cur, "time");
        DateTime today;
        if (now != null && now.Length >= 10 && DateTime.TryParseExact(now.Substring(0, 10), "yyyy-MM-dd", Inv, DateTimeStyles.None, out today)) r.LocalToday = today;
        Dictionary<string, object> daily = Json.Obj(root, "daily");
        List<object> dates = Json.List(daily, "time"), codes = Json.List(daily, "weather_code"),
                     max = Json.List(daily, "temperature_2m_max"), min = Json.List(daily, "temperature_2m_min");
        if (dates != null && codes != null && max != null && min != null)
            for (int i = 0; i < dates.Count && i < codes.Count && i < max.Count && i < min.Count; i++)
            {
                WeatherDay d = new WeatherDay();
                string date = dates[i] as string;
                if (date == null || !DateTime.TryParseExact(date, "yyyy-MM-dd", Inv, DateTimeStyles.None, out d.Date)) continue;
                if (!(codes[i] is double) || !(max[i] is double) || !(min[i] is double)) continue;   // « NaN » en texte, null… : jour ignoré
                d.Code = (int)(double)codes[i]; d.Max = (double)max[i]; d.Min = (double)min[i];
                r.Days.Add(d);
            }
        return r;
    }

    internal static List<WeatherPlace> ParseSearch(string json)
    {
        List<WeatherPlace> res = new List<WeatherPlace>();
        List<object> list = Json.List(Root(json), "results");
        if (list == null) return res;
        foreach (object o in list)
        {
            WeatherPlace p = new WeatherPlace { Name = Display(Json.Str(o, "name")), Admin1 = Display(Json.Str(o, "admin1")), Country = Display(Json.Str(o, "country")),
                                                Lat = Json.Num(o, "latitude"), Lon = Json.Num(o, "longitude") };
            if (string.IsNullOrEmpty(p.Name) || !ValidCoord(p.Lat, 90) || !ValidCoord(p.Lon, 180)) continue;
            res.Add(p);
        }
        return res;
    }

    internal static WeatherPlace ParseReverse(string json, double lat, double lon)
    {
        if (!ValidCoord(lat, 90) || !ValidCoord(lon, 180)) return null;
        object root = Root(json);
        string name = Display(Json.Str(root, "city"));
        if (string.IsNullOrEmpty(name)) name = Display(Json.Str(root, "locality"));
        if (string.IsNullOrEmpty(name)) return null;
        return new WeatherPlace { Name = name, Admin1 = Display(Json.Str(root, "principalSubdivision")), Country = Display(Json.Str(root, "countryName")), Lat = lat, Lon = lon };
    }

    // ---------- Codes météo WMO ----------
    public static Sky SkyOf(int code)
    {
        switch (code)
        {
            case 0: return Sky.Clear;
            case 1: return Sky.MainlyClear;
            case 2: return Sky.PartlyCloudy;
            case 3: return Sky.Overcast;
            case 45: case 48: return Sky.Fog;
            case 51: case 53: case 55: return Sky.Drizzle;
            case 56: case 57: case 66: case 67: return Sky.FreezingRain;
            case 61: case 63: case 65: return Sky.Rain;
            case 71: case 73: case 75: case 77: return Sky.Snow;
            case 80: case 81: case 82: return Sky.Showers;
            case 85: case 86: return Sky.SnowShowers;
            case 95: case 96: case 99: return Sky.Thunder;
        }
        return Sky.Overcast;
    }

    public static string Describe(int code)
    {
        switch (SkyOf(code))
        {
            case Sky.Clear: return L.T("Ciel dégagé", "Clear sky", "Cielo despejado");
            case Sky.MainlyClear: return L.T("Plutôt dégagé", "Mainly clear", "Mayormente despejado");
            case Sky.PartlyCloudy: return L.T("Éclaircies", "Partly cloudy", "Parcialmente nublado");
            case Sky.Overcast: return L.T("Couvert", "Overcast", "Cubierto");
            case Sky.Fog: return L.T("Brouillard", "Fog", "Niebla");
            case Sky.Drizzle: return L.T("Bruine", "Drizzle", "Llovizna");
            case Sky.Rain: return L.T("Pluie", "Rain", "Lluvia");
            case Sky.FreezingRain: return L.T("Pluie verglaçante", "Freezing rain", "Lluvia engelante");
            case Sky.Snow: return L.T("Neige", "Snow", "Nieve");
            case Sky.Showers: return L.T("Averses", "Showers", "Chubascos");
            case Sky.SnowShowers: return L.T("Averses de neige", "Snow showers", "Chubascos de nieve");
            default: return L.T("Orage", "Thunderstorm", "Tormenta");
        }
    }

    public static string Temp(double t) { return Finite(t) ? Math.Round(t).ToString("0", L.Culture) + "°" : "—"; }

    // « Aujourd'hui », « Demain » ou le jour de la semaine, d'après la date du lieu (pas celle de ce PC)
    public static string DayName(WeatherReport r, DateTime date)
    {
        DateTime today = r.LocalToday != DateTime.MinValue ? r.LocalToday : DateTime.Today;
        if (date == today) return L.T("Aujourd'hui", "Today", "Hoy");
        if (date == today.AddDays(1)) return L.T("Demain", "Tomorrow", "Mañana");
        return date.ToString("dddd", L.Culture);
    }
}
