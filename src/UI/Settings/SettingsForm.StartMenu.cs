// BureauCache — SettingsForm : section « Menu Démarrer » de la page Barre des tâches (T-044, D-019)
using System;
using System.IO;
using System.Windows.Forms;

partial class SettingsForm
{
    public const int TaskbarPage = 5;   // page « Barre des tâches », ouverte par « Personnaliser le menu Démarrer… »

    Toggle tStartSearch, tStartAccount, tStartBureauCache, tStartWindows, tStartSession, tStartPower;
    Label lShortcuts, lHiddenTiles;

    void BuildStartMenuSection()
    {
        Section(T("MENU DÉMARRER", "START MENU", "MENÚ INICIO"));
        tStartSearch = NewToggle(v => Settings.StartSearch = v);
        Row(T("Recherche", "Search", "Búsqueda"), null, tStartSearch, null);
        tStartAccount = NewToggle(v => Settings.StartAccount = v);
        Row(T("Compte", "Account", "Cuenta"), T("Photo, nom et PC de la session.", "Picture, name and PC of the session.", "Foto, nombre y PC de la sesión."), tStartAccount, null);
        tStartBureauCache = NewToggle(v => Settings.StartBureauCache = v);
        Row(T("Section BureauCache", "BureauCache section", "Sección BureauCache"), null, tStartBureauCache, null);
        tStartWindows = NewToggle(v => Settings.StartWindows = v);
        Row(T("Section Windows", "Windows section", "Sección Windows"), null, tStartWindows, null);
        tStartSession = NewToggle(v => Settings.StartSession = v);
        Row(T("Session Windows", "Windows session", "Sesión de Windows"), T("Verrouiller, se déconnecter.", "Lock, sign out.", "Bloquear, cerrar sesión."), tStartSession, null);
        tStartPower = NewToggle(v => Settings.StartPower = v);
        Row(T("Alimentation", "Power", "Energía"), T("Veille, redémarrer, arrêter.", "Sleep, restart, shut down.", "Suspender, reiniciar, apagar."), tStartPower, null);

        lHiddenTiles = Row(T("Tuiles masquées", "Hidden tiles", "Mosaicos ocultos"), "-",
            Button(T("Tout réafficher", "Show all", "Mostrar todo"), false, S(150), () => { Settings.StartHidden.Clear(); Settings.Notify(); }), null);

        Section(T("MES RACCOURCIS", "MY SHORTCUTS", "MIS ACCESOS DIRECTOS"));
        lShortcuts = Row(T("Raccourcis du menu Démarrer", "Start menu shortcuts", "Accesos directos del menú Inicio"), "-", null, null);
        Row(T("Ajouter une application", "Add an app", "Añadir una aplicación"), null, Button(T("Choisir…", "Choose…", "Elegir…"), false, S(130), AddAppShortcut), null);
        Row(T("Ajouter un dossier", "Add a folder", "Añadir una carpeta"), null, Button(T("Choisir…", "Choose…", "Elegir…"), false, S(130), AddFolderShortcut), null);
        Row(T("Ajouter un site", "Add a site", "Añadir un sitio"), null, Button(T("Ajouter…", "Add…", "Añadir…"), false, S(130), AddSiteShortcut), null);
        Note(T("Clic droit sur une tuile du menu Démarrer : la masquer, ou retirer un raccourci.",
               "Right-click a Start menu tile to hide it or remove a shortcut.",
               "Clic derecho en un mosaico del menú Inicio para ocultarlo o quitar un acceso directo."));
    }

    void LoadStartMenuValues()
    {
        tStartSearch.Checked = Settings.StartSearch;
        tStartAccount.Checked = Settings.StartAccount;
        tStartBureauCache.Checked = Settings.StartBureauCache;
        tStartWindows.Checked = Settings.StartWindows;
        tStartSession.Checked = Settings.StartSession;
        tStartPower.Checked = Settings.StartPower;
        int hidden = Settings.StartHidden.Count, count = StartShortcuts.All().Count;
        lHiddenTiles.Text = hidden == 0 ? T("Toutes les tuiles sont affichées.", "All tiles are shown.", "Se muestran todos los mosaicos.")
            : T(hidden + " tuile(s) masquée(s) par clic droit.", hidden + " tile(s) hidden by right-click.", hidden + " mosaico(s) oculto(s) con clic derecho.");
        lShortcuts.Text = count == 0 ? T("Aucun pour l'instant (12 au plus).", "None yet (up to 12).", "Ninguno por ahora (12 como máximo).")
            : T(count + " sur 12, dans la section « Mes raccourcis ».", count + " of 12, in the \"My shortcuts\" section.", count + " de 12, en la sección «Mis accesos directos».");
    }

    void AddShortcut(char kind, string target, string name)
    {
        if (StartShortcuts.Add(kind, target, name)) { Settings.Notify(); return; }
        Dialogs.Warn(this, Settings.StartShortcuts.Count >= StartShortcuts.Max
            ? T("Le menu Démarrer a déjà 12 raccourcis. Retires-en un d'abord (clic droit dans le menu).", "The Start menu already has 12 shortcuts. Remove one first (right-click in the menu).",
                "El menú Inicio ya tiene 12 accesos directos. Quita uno primero (clic derecho en el menú).")
            : T("Ce raccourci est déjà dans le menu, ou sa cible n'est pas acceptée.", "This shortcut is already in the menu, or its target isn't accepted.",
                "Este acceso directo ya está en el menú o su destino no se acepta."));
    }

    void AddAppShortcut()
    {
        using (OpenFileDialog d = new OpenFileDialog())
        {
            d.Title = T("Choisir une application", "Choose an app", "Elegir una aplicación");
            d.Filter = T("Applications et raccourcis", "Apps and shortcuts", "Aplicaciones y accesos directos") + "|*.exe;*.lnk;*.appref-ms";
            d.DereferenceLinks = false;
            if (d.ShowDialog(this) != DialogResult.OK) return;
            AddShortcut(StartShortcuts.App, d.FileName, StartShortcuts.NameFor(StartShortcuts.App, d.FileName));
        }
    }

    void AddFolderShortcut()
    {
        using (FolderBrowserDialog d = new FolderBrowserDialog())
        {
            d.Description = T("Choisir un dossier pour le menu Démarrer", "Choose a folder for the Start menu", "Elegir una carpeta para el menú Inicio");
            if (d.ShowDialog(this) != DialogResult.OK || !Directory.Exists(d.SelectedPath)) return;
            AddShortcut(StartShortcuts.Folder, d.SelectedPath, StartShortcuts.NameFor(StartShortcuts.Folder, d.SelectedPath));
        }
    }

    void AddSiteShortcut()
    {
        using (SiteShortcutDialog d = new SiteShortcutDialog())
        {
            if (d.ShowDialog(this) != DialogResult.OK) return;
            AddShortcut(StartShortcuts.Site, d.Address, d.SiteName.Length > 0 ? d.SiteName : StartShortcuts.NameFor(StartShortcuts.Site, d.Address));
        }
    }
}
