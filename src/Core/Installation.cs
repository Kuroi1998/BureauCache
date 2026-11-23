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
        Directory.CreateDirectory(Dir);
        if (!RunningFromInstall) FileOps.CopyFile(Application.ExecutablePath, Exe);
        ShellUtil.CreateShortcut(StartMenuLnk, Exe, L.T("Ton bureau, rangé sur le côté de l'écran", "Your desktop, tucked at the edge of the screen", "Tu escritorio, al borde de la pantalla"));
        RegisterVersion();
        if (Startup.Enabled) Startup.Set(true, Exe);
    }

    public static void RegisterVersion()
    {
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
        var left = new List<string>();
        Settings.CancelPendingSave(); // sinon un enregistrement différé recréerait le dossier des réglages
        Native.RestoreDesktopIcons();
        try { Startup.Set(false); } catch { }
        if (Startup.Enabled) left.Add(L.T("le lancement au démarrage de Windows", "starting with Windows", "el inicio con Windows"));
        try { File.Delete(StartMenuLnk); } catch { }
        if (File.Exists(StartMenuLnk)) left.Add(L.T("le raccourci du menu Démarrer", "the Start menu shortcut", "el acceso directo del menú Inicio"));
        try { Registry.CurrentUser.DeleteSubKeyTree(UninstKey, false); } catch { }
        try { using (RegistryKey k = Registry.CurrentUser.OpenSubKey(UninstKey)) if (k != null) left.Add(L.T("l'entrée « Applications installées »", "the \"Installed apps\" entry", "la entrada de «Aplicaciones instaladas»")); } catch { }
        PanelForm.RecycleLeftovers();
        try { File.Delete(Settings.FilePath); } catch { }
        if (File.Exists(Settings.FilePath)) left.Add(L.T("le fichier des réglages", "the settings file", "el archivo de configuración") + " (" + Settings.FilePath + ")");
        try { Directory.Delete(PanelForm.StashRoot); } catch { }
        try { Directory.Delete(Settings.Dir); } catch { }
        if (Directory.Exists(PanelForm.StashRoot)) left.Add(L.T("des éléments mis de côté", "set-aside items", "elementos apartados") + " (" + PanelForm.StashRoot + ")");
        // Garde-fou : on ne supprime que le dossier d'installation attendu
        string expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "BureauCache");
        if (Directory.Exists(Dir) && string.Equals(Path.GetFullPath(Dir).TrimEnd('\\'), expected.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
        {
            ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", "/c ping 127.0.0.1 -n 6 > nul & rmdir /s /q \"" + Dir + "\"");
            psi.WindowStyle = ProcessWindowStyle.Hidden;
            psi.CreateNoWindow = true;
            psi.UseShellExecute = false;
            psi.WorkingDirectory = Path.GetTempPath();
            try { Process.Start(psi); }
            catch { left.Add(L.T("le dossier du programme", "the program folder", "la carpeta del programa") + " (" + Dir + ")"); }
        }
        return left;
    }

    // Message de fin de désinstallation, avec ce qui reste à supprimer à la main (R-05)
    public static void ReportUninstall(List<string> left)
    {
        if (left.Count == 0)
            Dialogs.Info(null, L.T("BureauCache a été désinstallé.", "BureauCache has been uninstalled.", "BureauCache se ha desinstalado."));
        else
            Dialogs.Warn(null, L.T("BureauCache est désinstallé, mais ces éléments n'ont pas pu être supprimés :",
                                   "BureauCache is uninstalled, but these items couldn't be removed:",
                                   "BureauCache se ha desinstalado, pero no se pudieron eliminar estos elementos:")
                + Environment.NewLine + Environment.NewLine + "• " + string.Join(Environment.NewLine + "• ", left));
    }
}
