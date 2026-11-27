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
        new[] { "Wi-Fi", "Wi-Fi", "Wi-Fi", "ms-settings:network-wifi", null, "wifi reseau internet sans fil" },
        new[] { "Bluetooth et appareils", "Bluetooth & devices", "Bluetooth y dispositivos", "ms-settings:bluetooth", null, "bluetooth casque manette" },
        new[] { "Affichage", "Display", "Pantalla", "ms-settings:display", null, "ecran resolution luminosite hdr screen" },
        new[] { "Son", "Sound", "Sonido", "ms-settings:sound", null, "audio volume micro haut parleur" },
        new[] { "Notifications", "Notifications", "Notificaciones", "ms-settings:notifications", null, "" },
        new[] { "Alimentation et batterie", "Power & battery", "Energía y batería", "ms-settings:powersleep", null, "batterie veille energie" },
        new[] { "Stockage", "Storage", "Almacenamiento", "ms-settings:storagesense", null, "disque espace nettoyage" },
        new[] { "Fond d'écran", "Background", "Fondo", "ms-settings:personalization-background", null, "wallpaper image" },
        new[] { "Couleurs", "Colors", "Colores", "ms-settings:colors", null, "theme sombre clair accent" },
        new[] { "Thèmes", "Themes", "Temas", "ms-settings:themes", null, "" },
        new[] { "Écran de verrouillage", "Lock screen", "Pantalla de bloqueo", "ms-settings:lockscreen", null, "" },
        new[] { "Barre des tâches", "Taskbar", "Barra de tareas", "ms-settings:taskbar", null, "" },
        new[] { "Applications installées", "Installed apps", "Aplicaciones instaladas", "ms-settings:appsfeatures", null, "desinstaller programmes uninstall" },
        new[] { "Applications par défaut", "Default apps", "Aplicaciones predeterminadas", "ms-settings:defaultapps", null, "navigateur browser" },
        new[] { "Applications au démarrage", "Startup apps", "Aplicaciones de inicio", "ms-settings:startupapps", null, "demarrage" },
        new[] { "Comptes", "Accounts", "Cuentas", "ms-settings:yourinfo", null, "utilisateur profil" },
        new[] { "Options de connexion", "Sign-in options", "Opciones de inicio de sesión", "ms-settings:signinoptions", null, "mot de passe pin password" },
        new[] { "Date et heure", "Date & time", "Fecha y hora", "ms-settings:dateandtime", null, "horloge clock" },
        new[] { "Langue et région", "Language & region", "Idioma y región", "ms-settings:regionlanguage", null, "langue clavier" },
        new[] { "Saisie", "Typing", "Escritura", "ms-settings:typing", null, "clavier keyboard" },
        new[] { "Souris", "Mouse", "Ratón", "ms-settings:mousetouchpad", null, "pointeur" },
        new[] { "Imprimantes et scanners", "Printers & scanners", "Impresoras y escáneres", "ms-settings:printers", null, "imprimante printer" },
        new[] { "Réseau et Internet", "Network & internet", "Red e Internet", "ms-settings:network", null, "ethernet" },
        new[] { "VPN", "VPN", "VPN", "ms-settings:network-vpn", null, "" },
        new[] { "Mode avion", "Airplane mode", "Modo avión", "ms-settings:network-airplanemode", null, "" },
        new[] { "Windows Update", "Windows Update", "Windows Update", "ms-settings:windowsupdate", null, "mise a jour update" },
        new[] { "Sécurité Windows", "Windows Security", "Seguridad de Windows", "windowsdefender:", null, "antivirus defender" },
        new[] { "Confidentialité", "Privacy", "Privacidad", "ms-settings:privacy", null, "" },
        new[] { "Accessibilité", "Accessibility", "Accesibilidad", "ms-settings:easeofaccess", null, "" },
        new[] { "Jeux", "Gaming", "Juegos", "ms-settings:gaming-gamebar", null, "game bar xbox" },
        new[] { "Mode jeu", "Game Mode", "Modo de juego", "ms-settings:gaming-gamemode", null, "" },
        new[] { "Graphiques", "Graphics", "Gráficos", "ms-settings:display-advancedgraphics", null, "gpu carte graphique" },
        new[] { "Multitâche", "Multitasking", "Multitarea", "ms-settings:multitasking", null, "ancrage snap" },
        new[] { "Presse-papiers", "Clipboard", "Portapapeles", "ms-settings:clipboard", null, "historique" },
        new[] { "Paramètres", "Settings", "Configuración", "ms-settings:", null, "windows" },
        new[] { "Panneau de configuration", "Control Panel", "Panel de control", "control.exe", null, "" },
        new[] { "Gestionnaire de périphériques", "Device Manager", "Administrador de dispositivos", "devmgmt.msc", null, "pilotes drivers" },
        new[] { "Gestion des disques", "Disk Management", "Administración de discos", "diskmgmt.msc", null, "partition" },
        new[] { "Programmes et fonctionnalités", "Programs and Features", "Programas y características", "appwiz.cpl", null, "desinstaller" },
        new[] { "Options d'alimentation", "Power Options", "Opciones de energía", "powercfg.cpl", null, "" },
        new[] { "Connexions réseau", "Network Connections", "Conexiones de red", "ncpa.cpl", null, "carte reseau adapter" },
        new[] { "Variables d'environnement", "Environment Variables", "Variables de entorno", "rundll32.exe", "sysdm.cpl,EditEnvironmentVariables", "path" },
    };

    public static List<DesktopItem> WindowsSettings(int px)
    {
        Bitmap icon = IconCache.Tile(Glyphs.Settings, Ui.GlyphFont, Color.FromArgb(84, 96, 116), px);
        return WinSettings.Select(s => new DesktopItem
        {
            Kind = ItemKind.Setting, Key = "set:" + s[3] + s[4], Name = L.T(s[0], s[1], s[2]), Target = s[3], Args = s[4],
            Keywords = s[5] + " " + s[0] + " " + s[1] + " " + s[2], Detail = L.T("Paramètres Windows", "Windows settings", "Configuración de Windows"), Icon = icon
        }).ToList();
    }

    static bool Confirm(string msg)
    {
        return Dialogs.Ask(null, msg, true);   // éteindre, redémarrer… : « Non » par défaut
    }

    static void Start(string exe, string args)
    {
        ProcessStartInfo psi = new ProcessStartInfo(ShellUtil.SystemPath(exe), args ?? "");
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
        Action<string, string, string, string, Color, string, Action> add = (fr, en, es, glyph, col, kw, run) =>
            c.Add(new DesktopItem
            {
                Kind = ItemKind.Command, Key = "cmd:" + en, Name = L.T(fr, en, es), Detail = L.T("Commande", "Command", "Comando"),
                Keywords = kw + " " + fr + " " + en + " " + es, Run = run, Icon = IconCache.Tile(glyph, Ui.GlyphFont, col, px)
            });
        add("Verrouiller", "Lock", "Bloquear", Glyphs.Lock, slate, "lock verrou", () => Native.LockWorkStation());
        add("Mettre en veille", "Sleep", "Suspender", Glyphs.Sleep, slate, "veille sleep suspend", () => Application.SetSuspendState(PowerState.Suspend, false, false));
        add("Redémarrer", "Restart", "Reiniciar", Glyphs.Restart, red, "reboot redemarrer", () =>
        { if (Confirm(L.T("Redémarrer l'ordinateur maintenant ?", "Restart the computer now?", "¿Reiniciar el equipo ahora?"))) Shutdown("/r /t 0"); });
        add("Éteindre", "Shut down", "Apagar", Glyphs.Power, red, "eteindre arreter shutdown", () =>
        { if (Confirm(L.T("Éteindre l'ordinateur maintenant ?", "Shut down the computer now?", "¿Apagar el equipo ahora?"))) Shutdown("/s /t 0"); });
        add("Se déconnecter", "Sign out", "Cerrar sesión", Glyphs.User, red, "deconnexion logoff", () =>
        { if (Confirm(L.T("Fermer ta session maintenant ?", "Sign out now?", "¿Cerrar la sesión ahora?"))) Native.ExitWindowsEx(0, 0); });
        add("Vider la corbeille", "Empty Recycle Bin", "Vaciar la papelera", Glyphs.Delete, slate, "corbeille trash", () => Native.SHEmptyRecycleBin(IntPtr.Zero, null, 0));
        add("Gestionnaire des tâches", "Task Manager", "Administrador de tareas", Glyphs.TaskManager, slate, "taskmgr processus", () => Start("taskmgr.exe", null));
        add("Invite de commandes", "Command Prompt", "Símbolo del sistema", Glyphs.Terminal, slate, "cmd terminal console", () => Start("cmd.exe", null));
        add("PowerShell", "PowerShell", "PowerShell", Glyphs.Terminal, slate, "terminal console", () => Start("powershell.exe", null));
        add("Explorateur de fichiers", "File Explorer", "Explorador de archivos", Glyphs.Folder, slate, "explorer dossiers", () => Start("explorer.exe", null));
        add(Settings.HideIcons ? "Afficher les icônes du bureau" : "Cacher les icônes du bureau",
            Settings.HideIcons ? "Show desktop icons" : "Hide desktop icons",
            Settings.HideIcons ? "Mostrar los iconos del escritorio" : "Ocultar los iconos del escritorio", Glyphs.Eye, Settings.EffectiveAccent, "icones bureau desktop", () =>
            { Settings.HideIcons = !Settings.HideIcons; Settings.Notify(); });
        add("Paramètres de BureauCache", "BureauCache settings", "Configuración de BureauCache", Glyphs.Settings, Settings.EffectiveAccent, "bureaucache reglages options", openSettings);
        return c;
    }
}
