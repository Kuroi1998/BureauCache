// BureauCache — ArchiveRule, ArchiveRules
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// =====================================================================
//  Règles de rangement (D-009)
// =====================================================================
// « Les captures vont dans Images\Captures après 7 jours ». Une règle range des FICHIERS posés sur le bureau, selon
// leur extension ou un mot de leur nom, vers une destination choisie parmi des dossiers connus (jamais un chemin
// libre). Les protections du rangement s'appliquent : favoris, raccourcis, fichiers cachés ou utilisés récemment
// ne bougent pas, et chaque fichier est revérifié juste avant d'être déplacé (Archiver.Run).
enum RuleFilter { Extensions = 0, NameContains = 1 }

// Destinations permises. L'ordre fixe aussi les valeurs enregistrées dans settings.ini : ne pas le changer.
enum RuleTarget { Archive = 0, Pictures = 1, Documents = 2, Downloads = 3, Videos = 4, Music = 5 }

class ArchiveRule
{
    public string Name = "", Pattern = "", Sub = "";
    public RuleFilter Filter;
    public int Days;
    public RuleTarget Target;

    public static readonly int[] DayChoices = { 0, 1, 7, 30, 90 };

    // Vrai si le nom du fichier correspond au filtre (l'âge et les protections sont vérifiés par l'Archiver)
    public bool Matches(string path)
    {
        string name = Path.GetFileName(path);
        if (Filter == RuleFilter.Extensions)
        {
            string ext = Path.GetExtension(name).TrimStart('.');
            return ext.Length > 0 && Extensions().Contains(ext, StringComparer.OrdinalIgnoreCase);
        }
        string word = Pattern.Trim();
        return word.Length > 0 && Path.GetFileNameWithoutExtension(name).IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public IEnumerable<string> Extensions()
    {
        return Pattern.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(e => e.Trim().TrimStart('.')).Where(e => e.Length > 0);
    }

    // 0 jour = dès que le fichier est là depuis une heure (un fichier qu'on vient d'enregistrer ne part pas)
    public DateTime Limit { get { return Days <= 0 ? DateTime.UtcNow.AddHours(-1) : DateTime.UtcNow.AddDays(-Days); } }

    // Dossier de destination, ou null si le dossier connu est introuvable
    public string Destination(string archiveFolder = null)
    {
        string root = Target == RuleTarget.Archive && archiveFolder != null ? archiveFolder : ArchiveRules.TargetFolder(Target);
        if (string.IsNullOrEmpty(root)) return null;
        return Sub.Trim().Length == 0 ? root : Path.Combine(root, Sub.Trim());
    }

    // Erreur de saisie, ou null. Les caractères | et > servent au format de settings.ini.
    public string Validate()
    {
        if (Name.Trim().Length == 0) return L.T("Donne un nom à la règle.", "Give the rule a name.", "Dale un nombre a la regla.");
        if (Filter == RuleFilter.Extensions ? !Extensions().Any() : Pattern.Trim().Length == 0)
            return Filter == RuleFilter.Extensions ? L.T("Indique au moins une extension (ex. png jpg).", "Enter at least one extension (e.g. png jpg).", "Indica al menos una extensión (p. ej. png jpg).")
                                                   : L.T("Indique un mot à chercher dans le nom.", "Enter a word to look for in the name.", "Indica una palabra que buscar en el nombre.");
        foreach (string s in new[] { Name, Pattern, Sub })
            if (s.IndexOfAny(new[] { '|', '>' }) >= 0) return L.T("Les caractères | et > ne sont pas permis.", "The | and > characters are not allowed.", "No se permiten los caracteres | y >.");
        if (Sub.Trim().Length > 0 && (!FileOps.IsValidName(Sub.Trim()) || Sub.IndexOfAny(new[] { '\\', '/' }) >= 0))
            return L.T("Le sous-dossier doit être un simple nom de dossier.", "The subfolder must be a plain folder name.", "La subcarpeta debe ser un nombre de carpeta simple.");
        return null;
    }

    public string Serialize()
    {
        return Name + ">" + (int)Filter + ">" + Pattern + ">" + Days + ">" + (int)Target + ">" + Sub;
    }

    public static ArchiveRule Parse(string s)
    {
        string[] p = s.Split('>');
        int filter, days, target;
        if (p.Length != 6 || !int.TryParse(p[1], out filter) || !int.TryParse(p[3], out days) || !int.TryParse(p[4], out target)) return null;
        if (filter < 0 || filter > 1 || target < 0 || target > 5 || days < 0 || days > 36500) return null;
        var r = new ArchiveRule { Name = p[0], Filter = (RuleFilter)filter, Pattern = p[2], Days = days, Target = (RuleTarget)target, Sub = p[5] };
        return r.Validate() == null ? r : null;
    }
}

static class ArchiveRules
{
    public const int MaxRules = 20;

    // Dossier réel d'une destination. Pendant les tests, des dossiers isolés à côté du bureau de test.
    public static string TargetFolder(RuleTarget t)
    {
        if (t == RuleTarget.Archive) return Archiver.Folder;
        if (Paths.DesktopOverride != null)
            return Path.Combine(Path.GetDirectoryName(Paths.DesktopOverride.TrimEnd('\\')), "Dossiers", t.ToString());
        switch (t)
        {
            case RuleTarget.Pictures: return Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            case RuleTarget.Documents: return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            case RuleTarget.Videos: return Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
            case RuleTarget.Music: return Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
            case RuleTarget.Downloads: return Native.DownloadsFolder();
        }
        return null;
    }

    public static string TargetName(RuleTarget t)
    {
        switch (t)
        {
            case RuleTarget.Archive: return Settings.ArchiveName;
            case RuleTarget.Pictures: return L.T("Images", "Pictures", "Imágenes");
            case RuleTarget.Documents: return L.T("Documents", "Documents", "Documentos");
            case RuleTarget.Downloads: return L.T("Téléchargements", "Downloads", "Descargas");
            case RuleTarget.Videos: return L.T("Vidéos", "Videos", "Vídeos");
            case RuleTarget.Music: return L.T("Musique", "Music", "Música");
        }
        return "";
    }

    // Première règle dont le filtre correspond au fichier (les dossiers ne sont jamais concernés)
    public static ArchiveRule For(string path, List<ArchiveRule> rules)
    {
        if (Directory.Exists(path)) return null;
        foreach (ArchiveRule r in rules) if (r.Matches(path)) return r;
        return null;
    }

    // Un dossier où le rangement a pu déposer un élément : un dossier posé sur le bureau (archive comprise), un
    // sous-dossier de l'archive, ou une destination de règle (dossier connu ou un de ses sous-dossiers directs).
    // L'annulation ne remet en place que depuis ces dossiers.
    public static bool IsAllowedDestination(string folder, string desktop)
    {
        string parent = Path.GetDirectoryName(folder);
        if (parent == null) return false;
        // Dossier du bureau, ou sous-dossier d'un dossier du bureau : le dossier d'archive et son sous-dossier de règle,
        // même si le nom du dossier d'archive a changé depuis le rangement (QA39-03)
        if (Paths.SameFolder(parent, desktop)) return true;
        string grand = Path.GetDirectoryName(parent);
        if (grand != null && Paths.SameFolder(grand, desktop)) return true;
        foreach (RuleTarget t in Enum.GetValues(typeof(RuleTarget)))
        {
            if (t == RuleTarget.Archive) continue;
            string root = TargetFolder(t);
            if (string.IsNullOrEmpty(root)) continue;
            if (Paths.SameFolder(folder, root) || Paths.SameFolder(parent, root)) return true;
        }
        return false;
    }
}
