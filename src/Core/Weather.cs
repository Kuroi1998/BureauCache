// BureauCache — Weather
using System;
using System.Collections.Generic;
using System.Device.Location;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
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
}

class WeatherPlace
{
    public string Name, Admin1, Country;
    public double Lat, Lon;

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
    static DateTime lastAttempt = DateTime.MinValue;
    static int refreshing;

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

    // Lieu changé : on oublie la météo de l'ancien lieu
    public static void Reset()
    {
        Current = null;
        lastAttempt = DateTime.MinValue;
    }

    public static void RefreshAsync(Action done)
    {
        if (Interlocked.CompareExchange(ref refreshing, 1, 0) != 0) return;
        lastAttempt = DateTime.Now;
        double lat = Settings.WeatherLat, lon = Settings.WeatherLon;
        bool f = Fahrenheit;
        Background.Run(() =>
        {
            try
            {
                WeatherReport r = ParseForecast(Http.FetchText(ForecastUrl(lat, lon, f), Hosts, MaxResponse));
                if (r != null && lat == Settings.WeatherLat && lon == Settings.WeatherLon) { r.Fetched = DateTime.Now; Current = r; }
            }
            catch { }
            finally
            {
                Interlocked.Exchange(ref refreshing, 0);
                if (done != null) done();
            }
        });
    }

    // ---------- URLs ----------
    internal static string ForecastUrl(double lat, double lon, bool fahrenheit)
    {
        return "https://api.open-meteo.com/v1/forecast?latitude=" + lat.ToString("0.####", Inv) + "&longitude=" + lon.ToString("0.####", Inv) +
            "&current=temperature_2m,apparent_temperature,weather_code,wind_speed_10m,is_day" +
            "&daily=weather_code,temperature_2m_max,temperature_2m_min&timezone=auto&forecast_days=3" +
            (fahrenheit ? "&temperature_unit=fahrenheit&wind_speed_unit=mph" : "");
    }

    static string Lang { get { return L.En ? "en" : "fr"; } }

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
                            "Windows denies access to the location (Settings > Privacy > Location).");
                return null;
            }
            for (int i = 0; i < 20 && watcher.Position.Location.IsUnknown; i++) Thread.Sleep(250);
            GeoCoordinate c = watcher.Position.Location;
            watcher.Stop();
            if (c.IsUnknown)
            {
                error = L.T("Windows ne connaît pas la position de ce PC. Tape plutôt le nom de ta ville.",
                            "Windows doesn't know this PC's location. Type your city instead.");
                return null;
            }
            lat = Math.Round(c.Latitude, 3);
            lon = Math.Round(c.Longitude, 3);
        }
        WeatherPlace p = null;
        try
        {
            string url = "https://api.bigdatacloud.net/data/reverse-geocode-client?latitude=" + lat.ToString("0.###", Inv) +
                "&longitude=" + lon.ToString("0.###", Inv) + "&localityLanguage=" + Lang;
            p = ParseReverse(Http.FetchText(url, Hosts, MaxResponse), lat, lon);
        }
        catch { }
        return p ?? new WeatherPlace { Name = L.T("Ma position", "My location"), Lat = lat, Lon = lon };
    }

    // ---------- Lecture des réponses JSON ----------
    static readonly Regex Number = new Regex(@"^-?\d+(\.\d+)?([eE][+-]?\d+)?$");

    static string Section(string json, string name, char open, char close)
    {
        Match m = Regex.Match(json, "\"" + Regex.Escape(name) + "\"\\s*:\\s*\\" + open);
        if (!m.Success) return null;
        int start = m.Index + m.Length, depth = 1;
        for (int i = start; i < json.Length; i++)
        {
            if (json[i] == '"') { i = SkipString(json, i); continue; }
            if (json[i] == open) depth++;
            else if (json[i] == close && --depth == 0) return json.Substring(start, i - start);
        }
        return null;
    }

    static int SkipString(string s, int i)
    {
        for (i++; i < s.Length; i++)
        {
            if (s[i] == '\\') i++;
            else if (s[i] == '"') return i;
        }
        return s.Length;
    }

    static bool Num(string obj, string key, out double v)
    {
        v = double.NaN;
        Match m = Regex.Match(obj, "\"" + Regex.Escape(key) + "\"\\s*:\\s*(-?[0-9.eE+]+)");
        return m.Success && Number.IsMatch(m.Groups[1].Value) && double.TryParse(m.Groups[1].Value, NumberStyles.Float, Inv, out v);
    }

    static string Str(string obj, string key)
    {
        Match m = Regex.Match(obj, "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
        return m.Success ? Unescape(m.Groups[1].Value) : null;
    }

    static List<string> Values(string obj, string key)
    {
        string a = Section(obj, key, '[', ']');
        List<string> res = new List<string>();
        if (a == null) return res;
        foreach (string part in a.Split(',')) res.Add(part.Trim().Trim('"'));
        return res;
    }

    internal static string Unescape(string s)
    {
        StringBuilder sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c != '\\' || i + 1 >= s.Length) { sb.Append(c); continue; }
            char n = s[++i];
            if (n == 'u' && i + 4 < s.Length)
            {
                int code;
                if (int.TryParse(s.Substring(i + 1, 4), NumberStyles.HexNumber, Inv, out code)) { sb.Append((char)code); i += 4; continue; }
            }
            sb.Append(n == 'n' || n == 'r' || n == 't' ? ' ' : n);
        }
        return sb.ToString().Replace('\r', ' ').Replace('\n', ' ');
    }

    internal static WeatherReport ParseForecast(string json)
    {
        string cur = Section(json, "current", '{', '}');
        if (cur == null) return null;
        WeatherReport r = new WeatherReport();
        double code, isDay;
        if (!Num(cur, "temperature_2m", out r.Temp) || !Num(cur, "weather_code", out code)) return null;
        Num(cur, "apparent_temperature", out r.Feels);
        Num(cur, "wind_speed_10m", out r.Wind);
        r.Code = (int)code;
        if (Num(cur, "is_day", out isDay)) r.IsDay = isDay != 0;
        string daily = Section(json, "daily", '{', '}');
        if (daily != null)
        {
            List<string> dates = Values(daily, "time"), codes = Values(daily, "weather_code"),
                         max = Values(daily, "temperature_2m_max"), min = Values(daily, "temperature_2m_min");
            for (int i = 0; i < dates.Count && i < codes.Count && i < max.Count && i < min.Count; i++)
            {
                WeatherDay d = new WeatherDay();
                double c, hi, lo;
                if (!DateTime.TryParseExact(dates[i], "yyyy-MM-dd", Inv, DateTimeStyles.None, out d.Date) ||
                    !double.TryParse(codes[i], NumberStyles.Float, Inv, out c) ||
                    !double.TryParse(max[i], NumberStyles.Float, Inv, out hi) ||
                    !double.TryParse(min[i], NumberStyles.Float, Inv, out lo)) continue;
                d.Code = (int)c; d.Max = hi; d.Min = lo;
                r.Days.Add(d);
            }
        }
        return r;
    }

    internal static List<WeatherPlace> ParseSearch(string json)
    {
        List<WeatherPlace> res = new List<WeatherPlace>();
        string list = Section(json, "results", '[', ']');
        if (list == null) return res;
        foreach (Match m in Regex.Matches(list, @"\{(?:[^{}""]|""(?:[^""\\]|\\.)*"")*\}"))
        {
            WeatherPlace p = new WeatherPlace { Name = Str(m.Value, "name"), Admin1 = Str(m.Value, "admin1"), Country = Str(m.Value, "country") };
            if (string.IsNullOrEmpty(p.Name) || !Num(m.Value, "latitude", out p.Lat) || !Num(m.Value, "longitude", out p.Lon)) continue;
            if (Math.Abs(p.Lat) > 90 || Math.Abs(p.Lon) > 180) continue;
            res.Add(p);
        }
        return res;
    }

    internal static WeatherPlace ParseReverse(string json, double lat, double lon)
    {
        string name = Str(json, "city");
        if (string.IsNullOrEmpty(name)) name = Str(json, "locality");
        if (string.IsNullOrEmpty(name)) return null;
        return new WeatherPlace { Name = name, Admin1 = Str(json, "principalSubdivision"), Country = Str(json, "countryName"), Lat = lat, Lon = lon };
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
            case Sky.Clear: return L.T("Ciel dégagé", "Clear sky");
            case Sky.MainlyClear: return L.T("Plutôt dégagé", "Mainly clear");
            case Sky.PartlyCloudy: return L.T("Éclaircies", "Partly cloudy");
            case Sky.Overcast: return L.T("Couvert", "Overcast");
            case Sky.Fog: return L.T("Brouillard", "Fog");
            case Sky.Drizzle: return L.T("Bruine", "Drizzle");
            case Sky.Rain: return L.T("Pluie", "Rain");
            case Sky.FreezingRain: return L.T("Pluie verglaçante", "Freezing rain");
            case Sky.Snow: return L.T("Neige", "Snow");
            case Sky.Showers: return L.T("Averses", "Showers");
            case Sky.SnowShowers: return L.T("Averses de neige", "Snow showers");
            default: return L.T("Orage", "Thunderstorm");
        }
    }

    public static string Temp(double t) { return Math.Round(t).ToString("0", L.Culture) + "°"; }
}
