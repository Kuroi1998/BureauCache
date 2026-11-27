// BureauCache — raccourcis personnels du menu Démarrer : applications, dossiers et sites (T-044, D-019)
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

// Enregistrés dans Settings.StartShortcuts sous la forme « genre>nom>cible » : a application (.exe, .lnk,
// .appref-ms), f dossier, u site (http ou https). Une entrée abîmée ou douteuse est ignorée, jamais lancée.
static class StartShortcuts
{
    public const int Max = 12;
    public const char App = 'a', Folder = 'f', Site = 'u';

    internal sealed class Entry
    {
        public char Kind;
        public string Name, Target, Raw;
    }

    internal static bool Parse(string raw, out Entry entry)
    {
        entry = null;
        if (string.IsNullOrEmpty(raw) || raw.Length > 2200 || raw.Any(char.IsControl)) return false;
        string[] parts = raw.Split(new[] { '>' }, 3);
        if (parts.Length != 3 || parts[0].Length != 1 || parts[1].Trim().Length == 0) return false;
        char kind = parts[0][0];
        if (!Valid(kind, parts[2])) return false;
        entry = new Entry { Kind = kind, Name = parts[1].Trim(), Target = parts[2], Raw = raw };
        return true;
    }

    internal static string Format(char kind, string name, string target)
    {
        string clean = CleanName(name);
        if (clean.Length == 0) clean = CleanName(NameFor(kind, target));
        if (clean.Length == 0) clean = CleanName(target);
        return kind + ">" + clean + ">" + target;
    }

    static string CleanName(string name)
    {
        string clean = new string((name ?? string.Empty).Where(c => c != '>' && c != '|' && !char.IsControl(c)).ToArray()).Trim();
        if (clean.Length <= 40) return clean;
        int length = char.IsSurrogatePair(clean, 39) ? 39 : 40;
        return clean.Substring(0, length).TrimEnd();
    }

    internal static bool Valid(char kind, string target)
    {
        if (string.IsNullOrEmpty(target) || target.Length > 2000 || target.IndexOf('|') >= 0 || target.IndexOf('>') >= 0 || target.Any(char.IsControl))
            return false;
        if (kind == Site)
        {
            Uri uri;
            return Uri.TryCreate(target, UriKind.Absolute, out uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
        }
        if (kind != App && kind != Folder) return false;
        if (target.IndexOfAny(Path.GetInvalidPathChars()) >= 0 || target.IndexOf('*') >= 0 || target.IndexOf('?') >= 0) return false;
        bool rooted;
        try { rooted = Path.IsPathRooted(target) && Path.GetFullPath(target) == target; }
        catch (Exception) { return false; }
        if (!rooted) return false;
        if (kind == Folder) return true;
        string ext = Path.GetExtension(target).ToLowerInvariant();
        return ext == ".exe" || ext == ".lnk" || ext == ".appref-ms";
    }

    // Nom proposé : description de l'application, nom du dossier, ou adresse du site sans « www. »
    internal static string NameFor(char kind, string target)
    {
        try
        {
            if (kind == Site) { string host = new Uri(target).Host; return host.StartsWith("www.") ? host.Substring(4) : host; }
            if (kind == Folder) { string n = Path.GetFileName(target.TrimEnd('\\', '/')); return n.Length > 0 ? n : target; }
            if (Path.GetExtension(target).Equals(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(target))
            {
                string d = FileVersionInfo.GetVersionInfo(target).FileDescription;
                if (!string.IsNullOrWhiteSpace(d)) return d.Trim();
            }
            return Path.GetFileNameWithoutExtension(target);
        }
        catch (Exception) { return target; }
    }

    // Faux si la liste est pleine, si la cible est refusée ou déjà présente
    internal static bool Add(char kind, string target, string name)
    {
        if (!Valid(kind, target) || Settings.StartShortcuts.Count >= Max) return false;
        Entry existing;
        if (Settings.StartShortcuts.Any(r => Parse(r, out existing) && existing.Kind == kind &&
                                             string.Equals(existing.Target, target, StringComparison.OrdinalIgnoreCase))) return false;
        Settings.StartShortcuts.Add(Format(kind, name, target));
        return true;
    }

    internal static void Remove(string raw)
    {
        Settings.StartShortcuts.Remove(raw);
    }

    internal static System.Collections.Generic.List<Entry> All()
    {
        System.Collections.Generic.List<Entry> list = new System.Collections.Generic.List<Entry>();
        foreach (string raw in Settings.StartShortcuts)
        {
            Entry e;
            if (list.Count < Max && Parse(raw, out e)) list.Add(e);
        }
        return list;
    }

    // Ouvre la cible si elle existe encore ; un site s'ouvre dans le navigateur par défaut
    internal static bool Open(Entry e)
    {
        if (e == null || !Valid(e.Kind, e.Target)) return false;
        if (e.Kind == App && !File.Exists(e.Target)) return false;
        if (e.Kind == Folder && !Directory.Exists(e.Target)) return false;
        ShellUtil.Open(e.Target, null);
        return true;
    }
}
