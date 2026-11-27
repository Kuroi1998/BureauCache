// BureauCache — TaskbandRoute : fait arriver les badges et progressions des applications à la barre BureauCache (D-016)
using System;
using System.Globalization;
using System.IO;
using System.Text;

// Les appels ITaskbarList3 des applications (badge, progression) sont envoyés à la fenêtre désignée par la propriété
// « TaskbandHWND » de la barre d'Explorer. On la fait pointer vers la barre BureauCache, après avoir noté sa valeur
// d'origine dans « badges.etat » ; Restore la remet (sortie, surveillance après plantage, --restore, fin de session).
static class TaskbandRoute
{
    public const string PropName = "TaskbandHWND";
    // Prototype non concluant (badge de Discord absent à l'essai) : routage désactivé dans les versions publiées tant
    // que le diagnostic n'est pas fait. Restore reste appelé partout et nettoie le reste d'une version d'essai (3.11).
    public static readonly bool Enabled = false;
    const string Header = "BureauCache badges 1";
    static IntPtr installedOn;   // fenêtre d'Explorer modifiée par cette instance

    public static string StatePath { get { return Path.Combine(Settings.Dir, "badges.etat"); } }

    // Fichier d'état : en-tête, fenêtre d'Explorer, valeur d'origine, notre fenêtre (en hexadécimal)
    internal static string Format(long tray, long original, long ours)
    {
        return Header + "\r\n" + tray.ToString("X", CultureInfo.InvariantCulture) + "\r\n" +
               original.ToString("X", CultureInfo.InvariantCulture) + "\r\n" + ours.ToString("X", CultureInfo.InvariantCulture) + "\r\n";
    }

    internal static bool Parse(string text, out long tray, out long original, out long ours)
    {
        tray = original = ours = 0;
        if (string.IsNullOrEmpty(text) || text.Length > 300) return false;
        string[] l = text.Replace("\r", "").Split('\n');
        return l.Length >= 4 && l[0] == Header &&
               long.TryParse(l[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out tray) &&
               long.TryParse(l[2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out original) &&
               long.TryParse(l[3], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ours) && tray != 0 && ours != 0;
    }

    // Faux si rien n'a été changé (Explorer absent, état non enregistré, propriété refusée)
    public static bool Install(IntPtr ours)
    {
        Sandbox.Guard("routage des badges de la barre des tâches");
        // Un reste d'arrêt brutal, ou une installation précédente : on repart de l'état d'Explorer. Si la valeur
        // d'origine n'a pas pu être remise, on ne réécrit pas le journal, qui est le seul à la connaître (CDX-41).
        if (!Restore()) return false;
        IntPtr tray = Native.WindowsTaskbar();
        if (tray == IntPtr.Zero || ours == IntPtr.Zero) return false;
        IntPtr original = Native.GetProp(tray, PropName);
        if (original == ours) return true;
        try
        {
            Directory.CreateDirectory(Settings.Dir);
            File.WriteAllText(StatePath, Format(tray.ToInt64(), original.ToInt64(), ours.ToInt64()), new UTF8Encoding(false));
        }
        catch { return false; }   // sans filet, on ne touche à rien
        if (!Native.SetProp(tray, PropName, ours) || Native.GetProp(tray, PropName) != ours)
        {
            Restore();
            return false;
        }
        installedOn = tray;
        return true;
    }

    // Remet la valeur d'origine si la propriété pointe encore vers notre fenêtre. Explorer redémarré entre-temps a
    // reposé la sienne : on n'y touche pas. Appelable depuis la surveillance, sans réglages chargés.
    public static bool Restore()
    {
        if (Sandbox.Active) return true;
        try
        {
            if (!File.Exists(StatePath)) { installedOn = IntPtr.Zero; return true; }
            long tray, original, ours;
            if (Parse(File.ReadAllText(StatePath, Encoding.UTF8), out tray, out original, out ours))
            {
                IntPtr t = new IntPtr(tray);
                if (Native.IsWindow(t) && Native.GetProp(t, PropName) == new IntPtr(ours))
                {
                    if (original != 0 && Native.IsWindow(new IntPtr(original))) Native.SetProp(t, PropName, new IntPtr(original));
                    else Native.RemoveProp(t, PropName);
                    if (Native.GetProp(t, PropName) == new IntPtr(ours)) return false;   // pas constaté : nouvel essai plus tard
                }
            }
            File.Delete(StatePath);
            installedOn = IntPtr.Zero;
            return true;
        }
        catch { return false; }
    }

    public static bool Installed { get { return installedOn != IntPtr.Zero; } }
}
