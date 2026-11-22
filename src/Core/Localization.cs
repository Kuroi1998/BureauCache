// BureauCache — L
using System.Drawing;
using System.Globalization;

// =====================================================================
//  Langue
// =====================================================================
static class L
{
    public static bool En;
    public static void Refresh()
    {
        En = Settings.Language == 2 || (Settings.Language == 0 && CultureInfo.CurrentUICulture.TwoLetterISOLanguageName != "fr");
    }
    public static string T(string fr, string en) { return En ? en : fr; }
    public static CultureInfo Culture { get { return En ? CultureInfo.GetCultureInfo("en-US") : CultureInfo.GetCultureInfo("fr-FR"); } }

    public static string Cat(string key)
    {
        switch (key)
        {
            case "Dossiers": return T("Dossiers", "Folders");
            case "Jeux": return T("Jeux", "Games");
            case "Applications": return T("Applications", "Apps");
            case "Fichiers": return T("Fichiers", "Files");
        }
        return key;
    }

    public static string Size(long b)
    {
        string[] u = En ? new[] { "B", "KB", "MB", "GB", "TB" } : new[] { "o", "Ko", "Mo", "Go", "To" };
        double v = b;
        int i = 0;
        while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
        return v.ToString(i == 0 ? "0" : v < 10 ? "0.#" : "0", Culture) + " " + u[i];
    }
}
