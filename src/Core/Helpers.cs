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

static class Paths
{
    public static string Desktop { get { return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory); } }
    public static string CommonDesktop { get { return Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory); } }
    public static string[] Desktops { get { return new[] { Desktop, CommonDesktop }.Where(d => !string.IsNullOrEmpty(d) && Directory.Exists(d)).ToArray(); } }

    public static bool SameFolder(string a, string b)
    {
        return a != null && b != null && string.Equals(a.TrimEnd('\\'), b.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
    }
}

static class FileOps
{
    static readonly Regex Reserved = new Regex(@"^(con|prn|aux|nul|com[0-9]|lpt[0-9])(\..*)?$", RegexOptions.IgnoreCase);

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
        if (folder) Directory.Move(from, to); else File.Move(from, to);
    }

    // Envoie à la corbeille ; confirm = afficher les fenêtres de Windows (confirmation, progression)
    public static void Recycle(string path, bool confirm)
    {
        VbUi ui = confirm ? VbUi.AllDialogs : VbUi.OnlyErrorDialogs;
        if (Directory.Exists(path)) VbFs.DeleteDirectory(path, ui, VbRecycle.SendToRecycleBin);
        else VbFs.DeleteFile(path, ui, VbRecycle.SendToRecycleBin);
    }

    // Copie ou déplace vers dest avec les fenêtres de Windows (conflits de noms, progression)
    public static void CopyOrMove(string src, string dest, bool move)
    {
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
    const string Title = "BureauCache";

    public static bool Ask(IWin32Window owner, string msg)
    {
        return MessageBox.Show(owner, msg, Title, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
    }

    public static void Info(IWin32Window owner, string msg)
    {
        MessageBox.Show(owner, msg, Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    public static void Warn(IWin32Window owner, string msg)
    {
        MessageBox.Show(owner, msg, Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
