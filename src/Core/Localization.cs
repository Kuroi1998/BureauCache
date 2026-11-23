// BureauCache — L
using System.Globalization;

// =====================================================================
//  Langue
// =====================================================================
static class L
{
    public const int SystemLanguage = 0, French = 1, English = 2, Spanish = 3;
    static readonly CultureInfo FrenchCulture = CultureInfo.GetCultureInfo("fr-FR");
    static readonly CultureInfo EnglishCulture = CultureInfo.GetCultureInfo("en-US");
    static readonly CultureInfo SpanishCulture = CultureInfo.GetCultureInfo("es-ES");

    internal static volatile int CurrentLanguage = French;
    public static bool En { get { return CurrentLanguage == English; } }
    public static CultureInfo Culture
    {
        get { return CurrentLanguage == Spanish ? SpanishCulture : En ? EnglishCulture : FrenchCulture; }
    }

    internal static int ResolveLanguage(int setting, CultureInfo systemCulture)
    {
        if (setting >= French && setting <= Spanish) return setting;
        switch (systemCulture.TwoLetterISOLanguageName)
        {
            case "fr": return French;
            case "es": return Spanish;
            default: return English;
        }
    }

    public static void Refresh()
    {
        CurrentLanguage = ResolveLanguage(Settings.Language, CultureInfo.CurrentUICulture);
    }

    public static string T(string fr, string en, string es = null)
    {
        if (CurrentLanguage == Spanish) return string.IsNullOrEmpty(es) ? en : es;
        return En ? en : fr;
    }

    public static string Cat(string key)
    {
        switch (key)
        {
            case "Dossiers": return T("Dossiers", "Folders", "Carpetas");
            case "Jeux": return T("Jeux", "Games", "Juegos");
            case "Applications": return T("Applications", "Apps", "Aplicaciones");
            case "Fichiers": return T("Fichiers", "Files", "Archivos");
        }
        return key;
    }

    public static string Size(long b)
    {
        string[] u = CurrentLanguage == French ? new[] { "o", "Ko", "Mo", "Go", "To" } : new[] { "B", "KB", "MB", "GB", "TB" };
        double v = b;
        int i = 0;
        while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
        return v.ToString(i == 0 ? "0" : v < 10 ? "0.#" : "0", Culture) + " " + u[i];
    }
}
