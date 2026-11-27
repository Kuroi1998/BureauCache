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
        RequestSleep(() => Application.SetSuspendState(PowerState.Suspend, false, false), Native.ModernStandby, TurnOffDisplay,
            () => Dialogs.Warn(null, L.T("Windows a refusé la mise en veille.", "Windows refused to go to sleep.", "Windows rechazó la suspensión.")));
    }

    // Veille moderne (beaucoup de portables récents) : Windows refuse la veille classique, et éteindre l'écran, comme un
    // appui sur le bouton d'alimentation, fait entrer le PC en veille. Ailleurs, l'échec est signalé : éteindre l'écran
    // laisserait le PC allumé (CDX-11).
    internal static void RequestSleep(Func<bool> suspend, Func<bool> modernStandby, Action turnOffDisplay, Action reportFailure)
    {
        if (suspend()) return;
        if (modernStandby()) turnOffDisplay();
        else if (reportFailure != null) reportFailure();
    }

    static void TurnOffDisplay()
    {
        NativeWindow w = new NativeWindow();
        try
        {
            w.CreateHandle(new CreateParams());
            Native.SendMessagePtr(w.Handle, 0x0112 /*WM_SYSCOMMAND*/, new IntPtr(0xF170) /*SC_MONITORPOWER*/, new IntPtr(2) /*éteint*/);
        }
        finally { w.DestroyHandle(); }
    }

    public static bool Restart() { return Shutdown(true); }
    public static bool PowerOff() { return Shutdown(false); }

    // Comme Windows : mises à jour en attente installées, et arrêt « rapide » si le démarrage rapide est activé
    static bool Shutdown(bool restart)
    {
        Sandbox.Guard(restart ? "redémarrage" : "arrêt du PC");
        return RequestShutdown(ShutdownFlags(restart, UpdatesPending, FastStartup), Native.Shutdown, ReportShutdownError);
    }

    // Refus que l'arrêt normal ne résoudrait pas mieux que l'arrêt rapide : on les signale sans nouvel essai (CDX-06)
    const uint ErrorAccessDenied = 5, ErrorUsersLoggedOn = 1191, ErrorPrivilegeNotHeld = 1314;

    internal static bool RequestShutdown(uint flags, Func<uint, uint> request, Action<uint> reportError)
    {
        uint r = request(flags);
        // Démarrage rapide impossible (veille prolongée désactivée entre-temps) : arrêt normal, mises à jour comprises
        if (r != 0 && (flags & Native.SHUTDOWN_HYBRID) != 0 &&
            r != ErrorAccessDenied && r != ErrorUsersLoggedOn && r != ErrorPrivilegeNotHeld)
            r = request(flags & ~Native.SHUTDOWN_HYBRID);
        if (r != 0 && reportError != null) reportError(r);
        return r == 0;
    }

    static void ReportShutdownError(uint error)
    {
        Dialogs.Warn(null, error == ErrorUsersLoggedOn
            ? L.T("D'autres utilisateurs sont connectés à ce PC : Windows refuse de l'arrêter ou de le redémarrer d'ici. Utilise l'écran de verrouillage.",
                  "Other users are signed in to this PC, so Windows won't shut it down or restart it from here. Use the lock screen.",
                  "Hay otros usuarios conectados a este PC, así que Windows no permite apagarlo ni reiniciarlo desde aquí. Usa la pantalla de bloqueo.")
            : error == ErrorPrivilegeNotHeld || error == ErrorAccessDenied
            ? L.T("Ton compte n'a pas le droit d'arrêter ce PC (règle de l'administrateur).",
                  "Your account isn't allowed to shut down this PC (administrator policy).",
                  "Tu cuenta no tiene permiso para apagar este PC (directiva del administrador).")
            : L.T("Windows a refusé l'arrêt (erreur " + error + ").", "Windows refused to shut down (error " + error + ").",
                  "Windows rechazó el apagado (error " + error + ")."));
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
