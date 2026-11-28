// BureauCache — petits utilitaires partagés (chemins, fichiers, boîtes de message, tâches de fond)
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using VbFs = Microsoft.VisualBasic.FileIO.FileSystem;
using VbUi = Microsoft.VisualBasic.FileIO.UIOption;
using VbRecycle = Microsoft.VisualBasic.FileIO.RecycleOption;

// Bac à sable des tests (QA-34) : pendant les tests, aucun effet de bord réel. Le bureau et les réglages pointent
// vers des dossiers temporaires, et ce qui toucherait au registre, au menu Démarrer ou à l'installation lève une
// exception qui fait échouer le test au lieu de modifier le PC.
static class Sandbox
{
    internal static bool Active;

    public static void Guard(string what)
    {
        if (Active) throw new InvalidOperationException("Effet de bord bloqué pendant les tests : " + what);
    }

    // En mode test, on n'écrit que sous %TEMP% : les tests y créent leurs fichiers, et aucun fichier de
    // l'utilisateur ne s'y trouve (QA34-01 à 03)
    public static void GuardPath(string path, string what)
    {
        if (!Active) return;
        string full = System.IO.Path.GetFullPath(path), temp = System.IO.Path.GetFullPath(System.IO.Path.GetTempPath());
        if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Effet de bord bloqué pendant les tests : " + what + " hors de %TEMP% (" + full + ")");
    }
}

static class Paths
{
    internal static string DesktopOverride;   // posé par les tests (Sandbox)
    public static string Desktop { get { return DesktopOverride ?? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory); } }
    public static string CommonDesktop { get { return Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory); } }
    // En mode test, le bureau commun (réel) n'est jamais parcouru (QA34-02)
    public static string[] Desktops { get { if (DesktopOverride != null) return new[] { DesktopOverride }; return new[] { Desktop, CommonDesktop }.Where(d => !string.IsNullOrEmpty(d) && Directory.Exists(d)).ToArray(); } }

    public static bool SameFolder(string a, string b)
    {
        return a != null && b != null && string.Equals(a.TrimEnd('\\'), b.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
    }
}

static class FileOps
{
    static readonly Regex Reserved = new Regex(@"^(con|prn|aux|nul|com[0-9\u00B9\u00B2\u00B3]|lpt[0-9\u00B9\u00B2\u00B3])(\..*)?$", RegexOptions.IgnoreCase);

    // Nom de fichier sûr : pas de caractère interdit, pas de « .. », pas de nom réservé par Windows (CON, NUL…)
    public static bool IsValidName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 240) return false;
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return false;
        if (name.Trim('.').Length == 0 || name.EndsWith(".") || name.EndsWith(" ")) return false;
        return !Reserved.IsMatch(name);
    }

    public static void Move(string from, string to, bool folder)
    {
        Sandbox.GuardPath(from, "déplacement"); Sandbox.GuardPath(to, "déplacement");
        // Changement de casse seule (« projet » → « Projet ») : Directory.Move le refuse sous .NET Framework (même
        // chemin, sans tenir compte de la casse). Passage par un nom temporaire, remis en place en cas d'échec (QA-68).
        if (folder && !string.Equals(from, to, StringComparison.Ordinal) && string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
        {
            string temp = from.TrimEnd('\\') + ".bc-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            Directory.Move(from, temp);
            try { Directory.Move(temp, to); }
            catch { Directory.Move(temp, from); throw; }
            return;
        }
        if (folder) Directory.Move(from, to); else File.Move(from, to);
    }

    // Envoie à la corbeille ; confirm = afficher les fenêtres de Windows (confirmation, progression)
    public static void Recycle(string path, bool confirm)
    {
        Sandbox.Guard("corbeille");
        VbUi ui = confirm ? VbUi.AllDialogs : VbUi.OnlyErrorDialogs;
        if (Directory.Exists(path)) VbFs.DeleteDirectory(path, ui, VbRecycle.SendToRecycleBin);
        else VbFs.DeleteFile(path, ui, VbRecycle.SendToRecycleBin);
    }

    // Copie silencieuse d'un fichier de l'application (export des réglages, installation), remplace la cible
    public static void CopyFile(string src, string dest)
    {
        Sandbox.GuardPath(dest, "copie");
        File.Copy(src, dest, true);
    }

    internal static void AppendBoundedLog(string path, string header, string lines)
    {
        Sandbox.GuardPath(path, "journal de démarrage");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var file = new FileInfo(path);
        // La taille de la ligne ajoutée compte : sans cela, un fichier juste sous 64 Ko dépassait la limite (T-048)
        long added = System.Text.Encoding.UTF8.GetByteCount(lines);
        if (!file.Exists || file.Length == 0 || file.Length + added >= 64 * 1024) File.WriteAllText(path, header + lines);
        else File.AppendAllText(path, lines);
    }

    // Copie ou déplace vers dest avec les fenêtres de Windows (conflits de noms, progression)
    public static void CopyOrMove(string src, string dest, bool move)
    {
        Sandbox.GuardPath(src, "copie ou déplacement"); Sandbox.GuardPath(dest, "copie ou déplacement");
        if (Directory.Exists(src))
        {
            if (move) VbFs.MoveDirectory(src, dest, VbUi.AllDialogs); else VbFs.CopyDirectory(src, dest, VbUi.AllDialogs);
        }
        else
        {
            if (move) VbFs.MoveFile(src, dest, VbUi.AllDialogs); else VbFs.CopyFile(src, dest, VbUi.AllDialogs);
        }
    }
}

static class Dialogs
{
    // Boîtes aux couleurs de l'application (MessageDialog, T-022). safeDefault : « Non » sélectionné par défaut,
    // pour une action lourde qu'un Entrée réflexe ne doit pas déclencher.
    public static bool Ask(IWin32Window owner, string msg, bool safeDefault = false)
    {
        Sandbox.Guard("boîte de dialogue");   // un test ne doit jamais attendre une réponse
        return MessageDialog.Show(owner, msg, MessageDialog.Kind.Question, safeDefault);
    }

    public static void Info(IWin32Window owner, string msg)
    {
        Sandbox.Guard("boîte de dialogue");
        MessageDialog.Show(owner, msg, MessageDialog.Kind.Info);
    }

    public static void Warn(IWin32Window owner, string msg)
    {
        Sandbox.Guard("boîte de dialogue");
        MessageDialog.Show(owner, msg, MessageDialog.Kind.Warning);
    }
}

static class Background
{
    // Lance un travail sur un fil d'arrière-plan ; sta = nécessaire pour les objets COM du shell Windows
    public static void Run(Action work, bool sta = false)
    {
        Thread t = new Thread(() => { try { work(); } catch { } });
        if (sta) t.SetApartmentState(ApartmentState.STA);
        t.IsBackground = true;
        t.Start();
    }
}
