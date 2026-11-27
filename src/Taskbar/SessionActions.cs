// BureauCache — SessionActions : compte et alimentation du menu Démarrer (D-013, T-034)
using System;
using System.Drawing;
using System.IO;
using System.Security.Principal;
using System.Windows.Forms;
using Microsoft.Win32;

static class SessionActions
{
    public static void Lock()
    {
        Sandbox.Guard("verrouillage de la session");
        Native.LockWorkStation();
    }

    public static void SignOut()
    {
        Sandbox.Guard("déconnexion");
        Native.ExitWindowsEx(0 /*EWX_LOGOFF*/, 0);
    }

    public static void Sleep()
    {
        Sandbox.Guard("mise en veille");
        Application.SetSuspendState(PowerState.Suspend, false, false);
    }

    public static bool Restart() { return Shutdown(true); }
    public static bool PowerOff() { return Shutdown(false); }

    // Comme Windows : mises à jour en attente installées, et arrêt « rapide » si le démarrage rapide est activé
    static bool Shutdown(bool restart)
    {
        Sandbox.Guard(restart ? "redémarrage" : "arrêt du PC");
        uint flags = ShutdownFlags(restart, UpdatesPending, FastStartup);
        uint r = Native.Shutdown(flags);
        // Démarrage rapide impossible (veille prolongée désactivée entre-temps) : arrêt normal
        if (r != 0 && (flags & Native.SHUTDOWN_HYBRID) != 0) r = Native.Shutdown(flags & ~Native.SHUTDOWN_HYBRID);
        return r == 0;
    }

    internal static uint ShutdownFlags(bool restart, bool updates, bool fastStartup)
    {
        uint flags = restart ? Native.SHUTDOWN_RESTART : Native.SHUTDOWN_POWEROFF;
        if (updates) flags |= Native.SHUTDOWN_INSTALL_UPDATES;
        if (!restart && fastStartup) flags |= Native.SHUTDOWN_HYBRID;
        return flags;
    }

    // Mises à jour Windows qui attendent un redémarrage (lecture seule du registre)
    public static bool UpdatesPending
    {
        get
        {
            return KeyExists(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired") ||
                   KeyExists(@"SOFTWARE\Microsoft\WindowsUpdate\Orchestrator\RebootRequired");
        }
    }

    static bool FastStartup
    {
        get
        {
            try
            {
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Power"))
                    return k != null && Convert.ToInt32(k.GetValue("HiberbootEnabled", 0)) == 1;
            }
            catch { return false; }
        }
    }

    static bool KeyExists(string path)
    {
        try { using (RegistryKey k = Registry.LocalMachine.OpenSubKey(path)) return k != null; }
        catch { return false; }
    }

    // Photo du compte telle que Windows la range (Paramètres → Comptes), sinon null : le menu dessine les initiales
    public static Bitmap Picture()
    {
        try
        {
            string sid;
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent()) sid = identity.User.Value;
            using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\AccountPicture\Users\" + sid))
            {
                if (k == null) return null;
                foreach (string name in new[] { "Image192", "Image96", "Image448", "Image64" })
                {
                    string path = k.GetValue(name) as string;
                    if (string.IsNullOrEmpty(path) || !File.Exists(path)) continue;
                    // Copie en mémoire : le fichier n'est pas verrouillé
                    using (MemoryStream ms = new MemoryStream(File.ReadAllBytes(path)))
                    using (Image img = Image.FromStream(ms))
                        return new Bitmap(img);
                }
            }
        }
        catch { }
        return null;
    }

    internal static string Initials(string name)
    {
        string result = "";
        foreach (string part in (name ?? "").Split(new[] { ' ', '.', '-', '_' }, StringSplitOptions.RemoveEmptyEntries))
        {
            // Premier caractère entier, même hors du plan de base d'Unicode (deux unités UTF-16) (BAR-12)
            result += System.Globalization.StringInfo.GetNextTextElement(part).ToUpperInvariant();
            if (new System.Globalization.StringInfo(result).LengthInTextElements == 2) break;
        }
        return result.Length > 0 ? result : "?";
    }
}
