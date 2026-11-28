// BureauCache — Startup, Installer
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

static class Startup
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string Name = "BureauCache";

    public static bool Enabled
    {
        get
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey))
                    return k != null && k.GetValue(Name) != null;
            }
            catch { return false; }
        }
    }

    public static void Set(bool on, string exe = null)
    {
        Sandbox.Guard("démarrage avec Windows (HKCU\\" + RunKey + ")");
        using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RunKey))
        {
            if (on) k.SetValue(Name, "\"" + (exe ?? Application.ExecutablePath) + "\"");
            else k.DeleteValue(Name, false);
        }
    }
}

// =====================================================================
//  Installation (par utilisateur, sans droits administrateur)
// =====================================================================
static class Installer
{
    public const string Version = AppInfo.Version;
    const string UninstKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\BureauCache";

    public static string Dir { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "BureauCache"); } }
    public static string Exe { get { return Path.Combine(Dir, "BureauCache.exe"); } }
    static string StartMenuLnk { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "BureauCache.lnk"); } }

    public static bool IsInstalled
    {
        get
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(UninstKey))
                    return k != null && File.Exists(Exe);
            }
            catch { return false; }
        }
    }

    public static bool RunningFromInstall
    {
        get
        {
            try { return string.Equals(Path.GetFullPath(Application.ExecutablePath), Path.GetFullPath(Exe), StringComparison.OrdinalIgnoreCase); }
            catch { return false; }
        }
    }

    public static void Install()
    {
        Sandbox.Guard("installation");
        Directory.CreateDirectory(Dir);
        if (!RunningFromInstall) FileOps.CopyFile(Application.ExecutablePath, Exe);
        ShellUtil.CreateShortcut(StartMenuLnk, Exe, L.T("Ton bureau, rangé sur le côté de l'écran", "Your desktop, tucked at the edge of the screen", "Tu escritorio, al borde de la pantalla"));
        RegisterVersion();
        if (Startup.Enabled) Startup.Set(true, Exe);
    }

    public static void RegisterVersion()
    {
        Sandbox.Guard("entrée « Applications installées »");
        using (RegistryKey k = Registry.CurrentUser.CreateSubKey(UninstKey))
        {
            k.SetValue("DisplayName", "BureauCache");
            k.SetValue("DisplayVersion", Version);
            k.SetValue("Publisher", "BureauCache");
            k.SetValue("DisplayIcon", Exe + ",0");
            k.SetValue("InstallLocation", Dir);
            k.SetValue("UninstallString", "\"" + Exe + "\" --uninstall");
            k.SetValue("NoModify", 1, RegistryValueKind.DWord);
            k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            try { k.SetValue("EstimatedSize", (int)(new FileInfo(Exe).Length / 1024) + 1, RegistryValueKind.DWord); } catch { }
        }
    }

    public static string UninstallQuestion
    {
        get
        {
            return L.T("Désinstaller BureauCache ?\n\nTes icônes du bureau seront réaffichées et tes réglages supprimés.",
                       "Uninstall BureauCache?\n\nYour desktop icons will come back and your settings will be deleted.", "¿Desinstalar BureauCache?\n\nSe restaurarán los iconos del escritorio y se borrará tu configuración.");
        }
    }

    // Renvoie les étapes qui ont laissé quelque chose derrière elles (R-05) : le message de fin les cite au lieu
    // d'annoncer une désinstallation complète. Le dossier du programme est supprimé quelques secondes plus tard.
    public static List<string> Uninstall()
    {
        Sandbox.Guard("désinstallation");
        var left = new List<string>();
        Settings.SaveBlocked = true;     // plus aucune sauvegarde jusqu'à la sortie : elle recréerait le dossier (Q26-03)
        Settings.CancelPendingSave(); // sinon un enregistrement différé recréerait le dossier des réglages
        Native.RestoreDesktopIcons();
        try { Startup.Set(false); } catch { }
        if (Startup.Enabled) left.Add(L.T("le lancement au démarrage de Windows", "starting with Windows", "el inicio con Windows"));
        try { File.Delete(StartMenuLnk); } catch { }
        if (File.Exists(StartMenuLnk)) left.Add(L.T("le raccourci du menu Démarrer", "the Start menu shortcut", "el acceso directo del menú Inicio"));
        try { Registry.CurrentUser.DeleteSubKeyTree(UninstKey, false); } catch { }
        // Une lecture refusée n'est pas une preuve de suppression (Q26-03)
        bool entryLeft;
        try { using (RegistryKey k = Registry.CurrentUser.OpenSubKey(UninstKey)) entryLeft = k != null; } catch { entryLeft = true; }
        if (entryLeft) left.Add(L.T("l'entrée « Applications installées »", "the \"Installed apps\" entry", "la entrada de «Aplicaciones instaladas»"));
        PanelForm.RecycleLeftovers();
        SettingsBackup.DeleteAll();   // sauvegardes automatiques des réglages (D-007)
        try { File.Delete(Settings.FilePath); } catch { }
        if (File.Exists(Settings.FilePath)) left.Add(L.T("le fichier des réglages", "the settings file", "el archivo de configuración") + " (" + Settings.FilePath + ")");
        try { Directory.Delete(PanelForm.StashRoot); } catch { }
        // Journal des avertissements (T-048) : retiré avant le dossier. Si la suppression échoue, le dossier restant est signalé.
        try { File.Delete(Path.Combine(Settings.Dir, Log.FileName)); } catch (Exception) { }
        try { Directory.Delete(Settings.Dir); } catch { }
        if (Directory.Exists(PanelForm.StashRoot)) left.Add(L.T("des éléments mis de côté", "set-aside items", "elementos apartados") + " (" + PanelForm.StashRoot + ")");
        if (Directory.Exists(SettingsBackup.Folder)) left.Add(L.T("les sauvegardes des réglages", "the settings backups", "las copias de seguridad de la configuración") + " (" + SettingsBackup.Folder + ")");
        else if (!Directory.Exists(PanelForm.StashRoot) && Directory.Exists(Settings.Dir)) left.Add(L.T("le dossier des réglages", "the settings folder", "la carpeta de configuración") + " (" + Settings.Dir + ")");
        // Garde-fou : on ne supprime que le dossier d'installation attendu. La suppression part à la toute fin du
        // processus (StartFolderRemoval), après le dernier dialogue : l'exe encore ouvert la bloquerait (Q26-03)
        string expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "BureauCache");
        removeFolderOnExit = Directory.Exists(Dir) && string.Equals(Path.GetFullPath(Dir).TrimEnd('\\'), expected.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
        return left;
    }

    static bool removeFolderOnExit;

    // Appelé juste avant la fin du processus. Le dossier est retenté chaque seconde pendant 30 s, le temps que ce
    // processus et le gardien (--watchdog) se terminent.
    public static void StartFolderRemoval()
    {
        if (!removeFolderOnExit) return;
        Sandbox.Guard("suppression différée du dossier d'installation");
        removeFolderOnExit = false;
        try { using (Process.Start(FolderRemovalCommand(Dir))) { } } catch { }
    }

    internal static ProcessStartInfo FolderRemovalCommand(string directory)
    {
        // Le chemin n'entre dans la commande qu'à l'exécution, par expansion retardée.
        ProcessStartInfo psi = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"),
            "/d /e:on /v:on /c for /l %i in (1,1,30) do (if exist \"!BUREAUCACHE_REMOVE_DIR!\" (\"!BUREAUCACHE_REMOVE_PING!\" 127.0.0.1 -n 2 > nul & rmdir /s /q \"!BUREAUCACHE_REMOVE_DIR!\" 2> nul))");
        psi.WindowStyle = ProcessWindowStyle.Hidden;
        psi.CreateNoWindow = true;
        psi.UseShellExecute = false;
        psi.WorkingDirectory = Path.GetTempPath();
        psi.EnvironmentVariables["BUREAUCACHE_REMOVE_DIR"] = directory;
        psi.EnvironmentVariables["BUREAUCACHE_REMOVE_PING"] = Path.Combine(Environment.SystemDirectory, "ping.exe");
        return psi;
    }

    // Message de fin de désinstallation, avec ce qui reste à supprimer à la main (R-05)
    public static void ReportUninstall(List<string> left)
    {
        if (left.Count == 0)
            Dialogs.Info(null, L.T("BureauCache a été désinstallé. Son dossier sera supprimé quelques secondes après la fermeture de ce message.",
                                   "BureauCache has been uninstalled. Its folder will be removed a few seconds after you close this message.",
                                   "BureauCache se ha desinstalado. Su carpeta se eliminará unos segundos después de cerrar este mensaje."));
        else
            Dialogs.Warn(null, L.T("BureauCache est désinstallé, mais ces éléments n'ont pas pu être supprimés :",
                                   "BureauCache is uninstalled, but these items couldn't be removed:",
                                   "BureauCache se ha desinstalado, pero no se pudieron eliminar estos elementos:")
                + Environment.NewLine + Environment.NewLine + "• " + string.Join(Environment.NewLine + "• ", left));
    }
}
