// BureauCache — TaskbarModel : boutons de la barre, épingles et ordre (D-014)
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

// Un bouton : une épingle, une fenêtre, ou une épingle dont l'application est ouverte (sa première fenêtre)
class BarButton
{
    public string Key, Name;
    public bool Pinned;
    public TaskWindow Window;
}

static class TaskbarModel
{
    const string AumidPrefix = "aumid:", ExePrefix = "exe:";

    // Clé d'une application : son AppUserModelID s'il existe, sinon le chemin de son exe
    public static string KeyFor(string aumid, string exe) { return WindowList.KeyFor(aumid, exe); }

    // Une épingle enregistrée : « clé>nom ». Les entrées abîmées ou douteuses sont ignorées.
    internal static bool ParsePin(string entry, out string key, out string name)
    {
        key = name = null;
        if (string.IsNullOrEmpty(entry) || entry.Length > 1024 || entry.Any(char.IsControl)) return false;
        int sep = entry.IndexOf('>');
        key = sep < 0 ? entry : entry.Substring(0, sep);
        name = sep < 0 ? "" : entry.Substring(sep + 1).Trim();
        if (!ValidKey(key)) return false;
        key = (key.StartsWith(AumidPrefix, StringComparison.OrdinalIgnoreCase) ? AumidPrefix : ExePrefix) +
              key.Substring(key.IndexOf(':') + 1);
        if (name.Length == 0) name = DefaultName(key, null);
        return true;
    }

    internal static bool ValidKey(string key)
    {
        if (string.IsNullOrEmpty(key) || key.Length > 1024 || key.Any(char.IsControl) || key.IndexOf('>') >= 0) return false;
        if (key.StartsWith(AumidPrefix, StringComparison.OrdinalIgnoreCase))
        {
            if (key.Length == AumidPrefix.Length || key.IndexOf(' ') >= 0 || key.IndexOf('|') >= 0) return false;
        }
        else if (key.StartsWith(ExePrefix, StringComparison.OrdinalIgnoreCase))
        {
            string path = key.Substring(ExePrefix.Length);
            // Un exe local désigné par un chemin complet ; jamais un partage réseau
            if (path.Length < 4 || !((path[0] >= 'A' && path[0] <= 'Z') || (path[0] >= 'a' && path[0] <= 'z')) ||
                path[1] != ':' || (path[2] != '\\' && path[2] != '/') || path.IndexOf(':', 2) >= 0 ||
                path.IndexOfAny(Path.GetInvalidPathChars()) >= 0 || path.IndexOf('*') >= 0 || path.IndexOf('?') >= 0 ||
                !path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return false;
        }
        else return false;
        return true;
    }

    public static string FormatPin(string key, string name)
    {
        string clean = new string((name ?? "").Where(c => c != '>' && c != '|' && !char.IsControl(c)).ToArray()).Trim();
        if (clean.Length > 80) clean = clean.Substring(0, 80);
        return key + ">" + clean;
    }

    // Description des exe déjà lus : la liste des boutons est reconstruite à chaque événement de fenêtre
    static readonly Dictionary<string, string> exeNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string, string> appNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);   // null : introuvable

    // Nom d'une application : description de l'exe (« Discord »), sinon la fin du titre (« … - Google Chrome »)
    public static string DefaultName(string key, string title)
    {
        if (key != null && key.StartsWith(ExePrefix))
        {
            string path = key.Substring(ExePrefix.Length), name;
            if (exeNames.TryGetValue(path, out name)) return name;
            name = null;
            try
            {
                string d = File.Exists(path) ? FileVersionInfo.GetVersionInfo(path).FileDescription : null;
                if (!string.IsNullOrEmpty(d) && d.Trim().Length > 0) name = d.Trim();
            }
            catch { }
            if (name == null) name = Path.GetFileNameWithoutExtension(path);
            if (exeNames.Count < 500) exeNames[path] = name;
            return name;
        }
        // Application du Store ou à identifiant : le nom que lui donne Windows (« Calculatrice »), plutôt qu'un morceau
        // du titre, qui change avec le document ouvert (un morceau de chanson pour un lecteur de musique, par exemple)
        if (key != null && key.StartsWith(AumidPrefix))
        {
            string shellName = ShellAppName(key);
            if (shellName != null) return shellName;
        }
        if (!string.IsNullOrEmpty(title))
        {
            int i = Math.Max(title.LastIndexOf(" - "), title.LastIndexOf(" — "));
            return i >= 0 && i + 3 < title.Length ? title.Substring(i + 3).Trim() : title.Trim();
        }
        return key != null && key.StartsWith(AumidPrefix) ? key.Substring(AumidPrefix.Length) : "";
    }

    // Nom de l'application dans shell:AppsFolder, ou null si Windows ne la connaît pas (mis en cache, absences comprises)
    static string ShellAppName(string key)
    {
        string shellName;
        lock (appNames)
        {
            if (!appNames.TryGetValue(key, out shellName))
            {
                shellName = Native.ShellDisplayName(@"shell:AppsFolder\" + key.Substring(AumidPrefix.Length));
                if (appNames.Count < 500) appNames[key] = shellName;
            }
        }
        return shellName;
    }

    // Épingles d'abord, dans l'ordre enregistré, chacune avec la première fenêtre de son application ; puis les autres
    // fenêtres de ces applications, juste après leur épingle ; enfin les fenêtres non épinglées, dans l'ordre de session
    public static List<BarButton> Build(IEnumerable<string> pins, IList<TaskWindow> windows)
    {
        List<BarButton> result = new List<BarButton>();
        HashSet<TaskWindow> placed = new HashSet<TaskWindow>();
        HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string entry in pins)
        {
            string key, name;
            if (!ParsePin(entry, out key, out name) || !seen.Add(key)) continue;
            List<TaskWindow> mine = windows.Where(w => !placed.Contains(w) && string.Equals(w.AppKey, key, StringComparison.OrdinalIgnoreCase)).ToList();
            result.Add(new BarButton { Key = key, Name = name, Pinned = true, Window = mine.FirstOrDefault() });
            for (int i = 0; i < mine.Count; i++)
            {
                placed.Add(mine[i]);
                if (i > 0) result.Add(new BarButton { Key = key, Name = name, Window = mine[i] });
            }
        }
        foreach (TaskWindow w in windows)
            if (!placed.Contains(w)) result.Add(new BarButton { Key = w.AppKey, Name = DefaultName(w.AppKey, w.Title), Window = w });
        return result;
    }

    // Après un glisser-déposer : l'ordre des épingles à enregistrer, et celui des fenêtres pour la session
    public static List<string> PinsInOrder(IEnumerable<BarButton> buttons)
    {
        return buttons.Where(b => b.Pinned).Select(b => FormatPin(b.Key, b.Name)).ToList();
    }

    public static List<IntPtr> WindowsInOrder(IEnumerable<BarButton> buttons)
    {
        return buttons.Where(b => b.Window != null).Select(b => b.Window.Handle).ToList();
    }

    public static bool IsPinned(IEnumerable<string> pins, string key)
    {
        foreach (string entry in pins)
        {
            string k, n;
            if (ParsePin(entry, out k, out n) && string.Equals(k, key, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    public static List<string> WithPin(IEnumerable<string> pins, string key, string name)
    {
        if (!ValidKey(key)) return pins.ToList();
        string entry = FormatPin(key, name);
        if (entry.Length > 1024) return pins.ToList();
        List<string> list = Without(pins, key);
        list.Add(entry);
        return list;
    }

    public static List<string> Without(IEnumerable<string> pins, string key)
    {
        return pins.Where(e => { string k, n; return !(ParsePin(e, out k, out n) && string.Equals(k, key, StringComparison.OrdinalIgnoreCase)); }).ToList();
    }

    // Lance l'application d'une épingle (ou une nouvelle fenêtre de celle d'un bouton)
    public static void Launch(string key)
    {
        Sandbox.Guard("lancement d'une application épinglée");
        if (!CanLaunch(key)) return;
        if (key.StartsWith(AumidPrefix)) ShellUtil.Open(@"shell:AppsFolder\" + key.Substring(AumidPrefix.Length), null);
        else if (key.StartsWith(ExePrefix))
        {
            string path = key.Substring(ExePrefix.Length);
            if (File.Exists(path)) ShellUtil.Open(path, null);
        }
    }

    public static bool CanLaunch(string key) { return CanLaunch(key, id => ShellAppName(AumidPrefix + id) != null); }

    // Un identifiant d'application ne suffit pas : il faut que Windows sache lancer l'application (une entrée dans
    // shell:AppsFolder), sinon l'épingle ne ferait rien au clic (CDX-09)
    internal static bool CanLaunch(string key, Func<string, bool> hasAppsFolderEntry)
    {
        if (key == null) return false;
        if (key.StartsWith(AumidPrefix)) return key.Length > AumidPrefix.Length && hasAppsFolderEntry(key.Substring(AumidPrefix.Length));
        return key.StartsWith(ExePrefix) && File.Exists(key.Substring(ExePrefix.Length));
    }
}
