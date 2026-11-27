// BureauCache — WindowsBar : masque la barre des tâches de Windows et la rend telle quelle (D-013)
using System;
using System.IO;
using System.Text;

// Explorer n'est jamais modifié : on passe sa barre en masquage automatique (réglage officiel, pour rendre la place
// aux fenêtres) et on cache sa fenêtre. L'état d'origine est écrit AVANT tout changement dans « barre.etat » et gardé
// en mémoire tant que la barre est cachée. La sortie normale, la surveillance (plantage), « --restore » et le
// démarrage suivant appellent Restore. Revue QA-55 : la copie en mémoire prime sur le fichier, un fichier illisible
// interdit un nouveau masquage, et le fichier n'est effacé qu'une fois la restauration constatée.
static class WindowsBar
{
    const string Header = "BureauCache barre 1";
    static int original = -1;   // état d'origine, tant que cette instance cache la barre
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

    // Faux si la barre n'a pas pu être cachée en toute sécurité : Explorer absent, état d'origine non enregistré, ou
    // fichier d'un arrêt précédent illisible (on ne masque pas par-dessus un état inconnu ; Restore le soldera).
    // Appelée de nouveau pendant le masquage, elle garde l'état d'origine déjà connu (BAR-05).
    public static bool Hide()
    {
        Sandbox.Guard("masquage de la barre des tâches de Windows");
        IntPtr bar = Native.WindowsTaskbar();
        if (bar == IntPtr.Zero) return false;
        if (!Hidden)
        {
            int start;
            if (File.Exists(StatePath))
            {
                start = ReadState();                          // arrêt brutal précédent : l'état d'origine est là
                if (start < 0) return false;
            }
            else
            {
                start = Native.GetTaskbarState();
                if (!WriteState(start)) return false;         // sans filet, on ne touche à rien
            }
            original = start;
        }
        else if (!KeepJournal() || ReadState() != original) return false;
        Native.SetTaskbarState(original | Native.ABS_AUTOHIDE);
        Native.ShowWindow(bar, Native.SW_HIDE);
        Hidden = true;
        if (!Native.IsWindowVisible(bar) && (Native.GetTaskbarState() & Native.ABS_AUTOHIDE) != 0) return true;
        Restore();
        return false;
    }

    // Explorer réaffiche sa barre après un redémarrage, un changement d'écran ou l'ouverture du menu Démarrer
    public static bool Reapply()
    {
        // Le fichier d'état est vérifié avant tout retour anticipé : un ancien « barre.etat » remis en place pendant que
        // la barre Windows reste cachée ferait sinon restaurer, après un plantage, un état qui n'est pas l'original (CDX-02)
        if (!Hidden || !KeepJournal() || ReadState() != original) return false;
        IntPtr bar = Native.WindowsTaskbar();
        if (bar == IntPtr.Zero) return true;   // Explorer est en train de redémarrer.
        bool autoHide = (Native.GetTaskbarState() & Native.ABS_AUTOHIDE) != 0;
        bool visible = Native.IsWindowVisible(bar);
        if (autoHide && !visible) return true;
        if (!autoHide) Native.SetTaskbarState(original | Native.ABS_AUTOHIDE);
        if (visible || Native.IsWindowVisible(bar)) Native.ShowWindow(bar, Native.SW_HIDE);
        return !Native.IsWindowVisible(bar) && (Native.GetTaskbarState() & Native.ABS_AUTOHIDE) != 0;
    }

    // Fichier effacé pendant le masquage : il est réécrit depuis la copie en mémoire, pour la surveillance (BAR-01)
    static bool KeepJournal()
    {
        return original >= 0 && (File.Exists(StatePath) || WriteState(original));
    }

    // Rend la barre de Windows dans son état d'origine. Appelable depuis la surveillance, sans réglages chargés.
    // Vrai si rien ne reste à rendre ; faux si Explorer est absent ou si la restauration n'est pas constatée : le
    // fichier reste alors en place pour un nouvel essai (BAR-03). La fenêtre est toujours réaffichée : on ne laisse
    // jamais la barre cachée, même si un autre outil l'avait cachée avant nous (BAR-04 écarté).
    public static bool Restore()
    {
        if (Sandbox.Active) return true;
        try
        {
            bool journal = File.Exists(StatePath);
            int target;
            if (Hidden && original >= 0) target = original;   // la copie en mémoire prime sur le fichier
            else if (journal)
            {
                target = ReadState();
                if (target < 0) target = Native.ABS_ALWAYSONTOP;   // fichier abîmé : barre toujours visible, réglage par défaut de Windows
            }
            else return true;
            IntPtr bar = Native.WindowsTaskbar();
            if (bar == IntPtr.Zero) return false;
            Native.SetTaskbarState(target);
            Native.ShowWindow(bar, Native.SW_SHOWNA);
            if (!Native.IsWindowVisible(bar) ||
                (Native.GetTaskbarState() & Native.ABS_AUTOHIDE) != (target & Native.ABS_AUTOHIDE)) return false;
            Hidden = false;
            original = -1;
            if (journal) File.Delete(StatePath);
            return true;
        }
        catch { return false; }
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
