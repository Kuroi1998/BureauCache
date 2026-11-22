// BureauCache — SystemCatalog
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

// Paramètres Windows et commandes système (construits sur le fil de l'interface)
static class SystemCatalog
{
    static readonly string[][] WinSettings = {
        new[] { "Wi-Fi", "Wi-Fi", "ms-settings:network-wifi", null, "wifi reseau internet sans fil" },
        new[] { "Bluetooth et appareils", "Bluetooth & devices", "ms-settings:bluetooth", null, "bluetooth casque manette" },
        new[] { "Affichage", "Display", "ms-settings:display", null, "ecran resolution luminosite hdr screen" },
        new[] { "Son", "Sound", "ms-settings:sound", null, "audio volume micro haut parleur" },
        new[] { "Notifications", "Notifications", "ms-settings:notifications", null, "" },
        new[] { "Alimentation et batterie", "Power & battery", "ms-settings:powersleep", null, "batterie veille energie" },
        new[] { "Stockage", "Storage", "ms-settings:storagesense", null, "disque espace nettoyage" },
        new[] { "Fond d'écran", "Background", "ms-settings:personalization-background", null, "wallpaper image" },
        new[] { "Couleurs", "Colors", "ms-settings:colors", null, "theme sombre clair accent" },
        new[] { "Thèmes", "Themes", "ms-settings:themes", null, "" },
        new[] { "Écran de verrouillage", "Lock screen", "ms-settings:lockscreen", null, "" },
        new[] { "Barre des tâches", "Taskbar", "ms-settings:taskbar", null, "" },
        new[] { "Applications installées", "Installed apps", "ms-settings:appsfeatures", null, "desinstaller programmes uninstall" },
        new[] { "Applications par défaut", "Default apps", "ms-settings:defaultapps", null, "navigateur browser" },
        new[] { "Applications au démarrage", "Startup apps", "ms-settings:startupapps", null, "demarrage" },
        new[] { "Comptes", "Accounts", "ms-settings:yourinfo", null, "utilisateur profil" },
        new[] { "Options de connexion", "Sign-in options", "ms-settings:signinoptions", null, "mot de passe pin password" },
        new[] { "Date et heure", "Date & time", "ms-settings:dateandtime", null, "horloge clock" },
        new[] { "Langue et région", "Language & region", "ms-settings:regionlanguage", null, "langue clavier" },
        new[] { "Saisie", "Typing", "ms-settings:typing", null, "clavier keyboard" },
        new[] { "Souris", "Mouse", "ms-settings:mousetouchpad", null, "pointeur" },
        new[] { "Imprimantes et scanners", "Printers & scanners", "ms-settings:printers", null, "imprimante printer" },
        new[] { "Réseau et Internet", "Network & internet", "ms-settings:network", null, "ethernet" },
        new[] { "VPN", "VPN", "ms-settings:network-vpn", null, "" },
        new[] { "Mode avion", "Airplane mode", "ms-settings:network-airplanemode", null, "" },
        new[] { "Windows Update", "Windows Update", "ms-settings:windowsupdate", null, "mise a jour update" },
        new[] { "Sécurité Windows", "Windows Security", "windowsdefender:", null, "antivirus defender" },
        new[] { "Confidentialité", "Privacy", "ms-settings:privacy", null, "" },
        new[] { "Accessibilité", "Accessibility", "ms-settings:easeofaccess", null, "" },
        new[] { "Jeux", "Gaming", "ms-settings:gaming-gamebar", null, "game bar xbox" },
        new[] { "Mode jeu", "Game Mode", "ms-settings:gaming-gamemode", null, "" },
        new[] { "Graphiques", "Graphics", "ms-settings:display-advancedgraphics", null, "gpu carte graphique" },
        new[] { "Multitâche", "Multitasking", "ms-settings:multitasking", null, "ancrage snap" },
        new[] { "Presse-papiers", "Clipboard", "ms-settings:clipboard", null, "historique" },
        new[] { "Paramètres", "Settings", "ms-settings:", null, "windows" },
        new[] { "Panneau de configuration", "Control Panel", "control.exe", null, "" },
        new[] { "Gestionnaire de périphériques", "Device Manager", "devmgmt.msc", null, "pilotes drivers" },
        new[] { "Gestion des disques", "Disk Management", "diskmgmt.msc", null, "partition" },
        new[] { "Programmes et fonctionnalités", "Programs and Features", "appwiz.cpl", null, "desinstaller" },
        new[] { "Options d'alimentation", "Power Options", "powercfg.cpl", null, "" },
        new[] { "Connexions réseau", "Network Connections", "ncpa.cpl", null, "carte reseau adapter" },
        new[] { "Variables d'environnement", "Environment Variables", "rundll32.exe", "sysdm.cpl,EditEnvironmentVariables", "path" },
    };

    public static List<DesktopItem> WindowsSettings(int px)
    {
        Bitmap icon = IconCache.Tile(Glyphs.Settings, Ui.GlyphFont, Color.FromArgb(84, 96, 116), px);
        return WinSettings.Select(s => new DesktopItem
        {
            Kind = ItemKind.Setting, Key = "set:" + s[2] + s[3], Name = L.T(s[0], s[1]), Target = s[2], Args = s[3],
            Keywords = s[4] + " " + s[0] + " " + s[1], Detail = L.T("Paramètres Windows", "Windows settings"), Icon = icon
        }).ToList();
    }

    static bool Confirm(string msg)
    {
        return Dialogs.Ask(null, msg);
    }

    static void Start(string exe, string args)
    {
        ProcessStartInfo psi = new ProcessStartInfo(exe, args ?? "");
        psi.UseShellExecute = true;
        Process.Start(psi);
    }

    static void Shutdown(string args)
    {
        ProcessStartInfo psi = new ProcessStartInfo("shutdown.exe", args) { CreateNoWindow = true, UseShellExecute = false };
        Process.Start(psi);
    }

    public static List<DesktopItem> Commands(int px, Action openSettings)
    {
        Color slate = Color.FromArgb(100, 96, 120), red = Color.FromArgb(200, 70, 70);
        List<DesktopItem> c = new List<DesktopItem>();
        Action<string, string, string, Color, string, Action> add = (fr, en, glyph, col, kw, run) =>
            c.Add(new DesktopItem
            {
                Kind = ItemKind.Command, Key = "cmd:" + en, Name = L.T(fr, en), Detail = L.T("Commande", "Command"),
                Keywords = kw + " " + fr + " " + en, Run = run, Icon = IconCache.Tile(glyph, Ui.GlyphFont, col, px)
            });
        add("Verrouiller", "Lock", Glyphs.Lock, slate, "lock verrou", () => Native.LockWorkStation());
        add("Mettre en veille", "Sleep", Glyphs.Sleep, slate, "veille sleep suspend", () => Application.SetSuspendState(PowerState.Suspend, false, false));
        add("Redémarrer", "Restart", Glyphs.Restart, red, "reboot redemarrer", () =>
        { if (Confirm(L.T("Redémarrer l'ordinateur maintenant ?", "Restart the computer now?"))) Shutdown("/r /t 0"); });
        add("Éteindre", "Shut down", Glyphs.Power, red, "eteindre arreter shutdown", () =>
        { if (Confirm(L.T("Éteindre l'ordinateur maintenant ?", "Shut down the computer now?"))) Shutdown("/s /t 0"); });
        add("Se déconnecter", "Sign out", Glyphs.User, red, "deconnexion logoff", () =>
        { if (Confirm(L.T("Fermer ta session maintenant ?", "Sign out now?"))) Native.ExitWindowsEx(0, 0); });
        add("Vider la corbeille", "Empty Recycle Bin", Glyphs.Delete, slate, "corbeille trash", () => Native.SHEmptyRecycleBin(IntPtr.Zero, null, 0));
        add("Gestionnaire des tâches", "Task Manager", Glyphs.TaskManager, slate, "taskmgr processus", () => Start("taskmgr.exe", null));
        add("Invite de commandes", "Command Prompt", Glyphs.Terminal, slate, "cmd terminal console", () => Start("cmd.exe", null));
        add("PowerShell", "PowerShell", Glyphs.Terminal, slate, "terminal console", () => Start("powershell.exe", null));
        add("Explorateur de fichiers", "File Explorer", Glyphs.Folder, slate, "explorer dossiers", () => Start("explorer.exe", null));
        add(Settings.HideIcons ? "Afficher les icônes du bureau" : "Cacher les icônes du bureau",
            Settings.HideIcons ? "Show desktop icons" : "Hide desktop icons", Glyphs.Eye, Settings.Accent, "icones bureau desktop", () =>
            { Settings.HideIcons = !Settings.HideIcons; Settings.Notify(); });
        add("Paramètres de BureauCache", "BureauCache settings", Glyphs.Settings, Settings.Accent, "bureaucache reglages options", openSettings);
        return c;
    }
}
