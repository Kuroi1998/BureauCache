// BureauCache — WindowsBar : masque la barre des tâches de Windows et la rend telle quelle (D-013)
using System;
using System.IO;
using System.Text;

// Explorer n'est jamais modifié : on passe sa barre en masquage automatique (réglage officiel, pour rendre la place
// aux fenêtres) et on cache sa fenêtre. L'état d'origine est écrit AVANT tout changement dans « barre.etat » : tant
// que ce fichier existe, la barre est à rendre. La sortie normale, la surveillance (plantage), « --restore » et le
// démarrage suivant appellent Restore, qui ne fait rien sans ce fichier.
static class WindowsBar
{
    const string Header = "BureauCache barre 1";
    public static bool Hidden { get; private set; }

    public static string StatePath { get { return Path.Combine(Settings.Dir, "barre.etat"); } }

    // Contenu du fichier d'état : une ligne d'en-tête, puis « etat=N »
    internal static string FormatState(int state) { return Header + "\r\netat=" + state + "\r\n"; }

    // -1 si le fichier n'est pas le nôtre ou s'il est abîmé ; seuls les bits connus sont gardés
    internal static int ParseState(string text)
    {
        if (string.IsNullOrEmpty(text) || text.Length > 200) return -1;
        string[] lines = text.Replace("\r", "").Split('\n');
        if (lines.Length < 2 || lines[0] != Header || !lines[1].StartsWith("etat=")) return -1;
        int v;
        if (!int.TryParse(lines[1].Substring(5), out v) || v < 0 || v > 3) return -1;
        return v;
    }

    // Faux si la barre n'a pas pu être cachée en toute sécurité (Explorer absent, état non enregistré)
    public static bool Hide()
    {
        Sandbox.Guard("masquage de la barre des tâches de Windows");
        IntPtr bar = Native.WindowsTaskbar();
        if (bar == IntPtr.Zero) return false;
        int original;
        if (File.Exists(StatePath)) original = ReadState();   // plantage précédent : l'état d'origine est déjà là
        else
        {
            original = Native.GetTaskbarState();
            if (!WriteState(original)) return false;          // sans filet, on ne touche à rien
        }
        if (original < 0) original = Native.ABS_ALWAYSONTOP;
        Native.SetTaskbarState(original | Native.ABS_AUTOHIDE);
        Native.ShowWindow(bar, Native.SW_HIDE);
        Hidden = true;
        return true;
    }

    // Explorer réaffiche sa barre après un redémarrage, un changement d'écran ou l'ouverture du menu Démarrer
    public static void Reapply()
    {
        if (!Hidden) return;
        IntPtr bar = Native.WindowsTaskbar();
        if (bar == IntPtr.Zero) return;
        if ((Native.GetTaskbarState() & Native.ABS_AUTOHIDE) == 0) Native.SetTaskbarState(Native.GetTaskbarState() | Native.ABS_AUTOHIDE);
        if (Native.IsWindowVisible(bar)) Native.ShowWindow(bar, Native.SW_HIDE);
    }

    // Rend la barre de Windows dans son état d'origine. Appelable depuis la surveillance, sans réglages chargés.
    public static void Restore()
    {
        if (Sandbox.Active) return;
        try
        {
            IntPtr bar = Native.WindowsTaskbar();
            if (!File.Exists(StatePath))
            {
                if (Hidden && bar != IntPtr.Zero) Native.ShowWindow(bar, Native.SW_SHOWNA);
                Hidden = false;
                return;
            }
            if (bar == IntPtr.Zero) return;   // Explorer absent : le fichier reste, on rendra la barre plus tard
            int original = ReadState();
            Native.SetTaskbarState(original < 0 ? Native.ABS_ALWAYSONTOP : original);
            Native.ShowWindow(bar, Native.SW_SHOWNA);
            File.Delete(StatePath);
            Hidden = false;
        }
        catch { }
    }

    static int ReadState()
    {
        try { return ParseState(File.ReadAllText(StatePath, Encoding.UTF8)); }
        catch { return -1; }
    }

    static bool WriteState(int state)
    {
        string tmp = null;
        try
        {
            Directory.CreateDirectory(Settings.Dir);
            tmp = Path.Combine(Settings.Dir, Guid.NewGuid().ToString("N") + ".tmp");
            File.WriteAllText(tmp, FormatState(state), new UTF8Encoding(false));
            File.Move(tmp, StatePath);
            return ParseState(File.ReadAllText(StatePath, Encoding.UTF8)) == state;
        }
        catch
        {
            try { if (tmp != null && File.Exists(tmp)) File.Delete(tmp); } catch { }
            return false;
        }
    }
}
