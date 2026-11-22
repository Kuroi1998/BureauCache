// BureauCache — SettingsForm (partie 2)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

partial class SettingsForm
{
    void BuildGeneral()
    {
        NewPage(T("Général", "General"));
        Section(T("BUREAU", "DESKTOP"));
        tHide = NewToggle(v => Settings.HideIcons = v);
        Row(T("Cacher les icônes du bureau", "Hide desktop icons"), T("Elles restent accessibles depuis le panneau.", "They stay available in the panel."), tHide, null);
        EndSection();

        Section("WINDOWS");
        tStartup = new Toggle();
        tStartup.Changed += (s, e) =>
        {
            if (loading) return;
            try { Startup.Set(tStartup.Checked, Installer.IsInstalled ? Installer.Exe : null); }
            catch (Exception ex) { Dialogs.Info(this, ex.Message); }
            loading = true; tStartup.Checked = Startup.Enabled; loading = false;
        };
        Row(T("Lancer au démarrage de Windows", "Start with Windows"), null, tStartup, null);
        bInstall = new FlatButton(T("Installer", "Install"), true, S(150));
        bInstall.Click += (s, e) =>
        {
            if (Installer.IsInstalled && Installer.RunningFromInstall) { if (act.Uninstall != null) act.Uninstall(); }
            else if (act.Install != null) act.Install();
        };
        lInstall = Row(T("Installation", "Installation"), "-", bInstall, null);
        EndSection();

        Section(T("LANGUE", "LANGUAGE"));
        sLang = NewSeg(v => Settings.Language = v, T("Système", "System"), "Français", "English");
        Row(T("Langue", "Language"), null, sLang, null);
        EndSection();

        Section(T("ICÔNE PRÈS DE L'HORLOGE", "ICON BY THE CLOCK"));
        lTray = Row(T("Toujours visible", "Always visible"), "-",
            Button(T("Ouvrir le réglage", "Open the setting"), false, S(150), () => { try { ShellUtil.Open("ms-settings:taskbar", null); } catch { } }), null);
        tQuiet = NewToggle(v => Settings.QuietStart = v);
        Row(T("Démarrage discret", "Quiet start"), T("Pas de bulle d'information au lancement.", "No info bubble at startup."), tQuiet, null);
        Note(T("Clic sur l'icône : ouvrir la liste · double-clic : ces paramètres · clic droit : le menu. Réduire ou fermer cette fenêtre la range près de l'horloge, BureauCache continue de tourner.",
               "Click the icon: open the list · double-click: these settings · right-click: the menu. Minimizing or closing this window tucks it by the clock; BureauCache keeps running."));
        EndSection();

        Section(T("AIDE", "HELP"));
        Row(T("Visite guidée", "Guided tour"), T("Les 4 écrans de bienvenue, pour redécouvrir l'essentiel.", "The 4 welcome screens, to go over the basics again."),
            Button(T("Revoir", "Show again"), false, S(130), () => { if (act.Welcome != null) act.Welcome(); }), null);
        Row(T("Raccourcis clavier", "Keyboard shortcuts"), T("Appuie sur F1 dans le panneau pour tous les voir.", "Press F1 in the panel to see them all."), null, null);
        EndSection();
    }

    void BuildOpening()
    {
        NewPage(T("Ouverture", "Opening"));
        Section(T("SOURIS", "MOUSE"));
        sSide = NewSeg(v => Settings.RightSide = v == 1, T("Gauche", "Left"), T("Droite", "Right"));
        Row(T("Bord de l'écran", "Screen edge"), null, sSide, null);
        sZone = NewSeg(v => Settings.EdgeZone = v, T("Tout", "All"), T("Haut", "Top"), T("Milieu", "Middle"), T("Bas", "Bottom"));
        Row(T("Partie du bord", "Part of the edge"), T("Zone du bord qui ouvre la liste.", "Part of the edge that opens the list."), sZone, null);
        int n = Screen.AllScreens.Length;
        string[] scr = new string[n + 1];
        scr[0] = T("Tous", "All");
        for (int i = 0; i < n; i++) scr[i + 1] = (i + 1).ToString();
        sScreen = NewSeg(v => Settings.ScreenIdx = v - 1, scr);
        Row(T("Écran", "Screen"), n > 1 ? T("Écran sur lequel le bord est actif.", "Screen where the edge is active.") : T("Un seul écran détecté.", "Only one screen detected."), sScreen, null);
        slDelay = new Slider(0, 1000, 20);
        lDelay = ValueLabel();
        slDelay.Changed += (s, e) => { if (loading) return; Settings.Delay = slDelay.Value; UpdateTexts(); Settings.Notify(); };
        Row(T("Délai d'ouverture", "Opening delay"), T("Évite les ouvertures accidentelles.", "Avoids accidental opening."), slDelay, lDelay);
        tAuto = NewToggle(v => Settings.AutoClose = v);
        Row(T("Fermer quand la souris s'éloigne", "Close when the mouse leaves"), null, tAuto, null);
        tFull = NewToggle(v => Settings.NoFullscreen = v);
        Row(T("Désactiver en plein écran", "Disable in full screen"), T("Jeux, vidéos et présentations ne sont pas dérangés.", "Games, videos and slideshows are left alone."), tFull, null);
        EndSection();

        Section(T("CLAVIER", "KEYBOARD"));
        tHot = NewToggle(v => Settings.HotkeyOn = v);
        Row(T("Raccourci clavier", "Keyboard shortcut"), T("Ouvre ou ferme la liste depuis n'importe où.", "Opens or closes the list from anywhere."), tHot, null);
        hk = new HotkeyBox();
        hk.Changed += (s, e) => { Settings.HotMods = hk.Mods; Settings.HotKey = hk.Key; Settings.Notify(); };
        lHotStatus = Row(T("Combinaison", "Combination"), "-", hk, null);
        EndSection();
    }

    void BuildLook()
    {
        NewPage(T("Apparence", "Appearance"));
        Section(T("THÈME", "THEME"));
        sTheme = NewSeg(v => Settings.ThemeMode = v, T("Système", "System"), T("Sombre", "Dark"), T("Clair", "Light"));
        Row(T("Thème", "Theme"), T("« Système » suit Windows.", "\"System\" follows Windows."), sTheme, null);
        swatch = new Swatches();
        swatch.Changed += (s, e) => { if (loading) return; Settings.Accent = swatch.Selected; Settings.Notify(); };
        Row(T("Couleur d'accent", "Accent color"), null, swatch, null);
        tGlass = NewToggle(v => Settings.Glass = v);
        Row(T("Effet verre", "Glass effect"), T("Le panneau laisse deviner ce qu'il y a derrière.", "The panel shows a blur of what's behind it."), tGlass, null);
        tLive = NewToggle(v => Settings.LiveBlur = v);
        Row(T("Flou en direct", "Live blur"), T("Suit ce qui bouge derrière. Le panneau n'apparaît plus sur les captures.", "Follows what moves behind. The panel no longer shows on screenshots."), tLive, null);
        tAnim = NewToggle(v => Settings.Animations = v);
        Row(T("Animations", "Animations"), null, tAnim, null);
        EndSection();

        Section(T("PANNEAU", "PANEL"));
        sView = NewSeg(v => Settings.GridView = v == 1, T("Liste", "List"), T("Grille", "Grid"));
        Row(T("Affichage", "Layout"), null, sView, null);
        slWidth = new Slider(260, 560, 10);
        lWidth = ValueLabel();
        slWidth.Changed += (s, e) => { if (loading) return; Settings.PanelWidth = slWidth.Value; UpdateTexts(); Settings.Notify(); };
        Row(T("Largeur du panneau", "Panel width"), null, slWidth, lWidth);
        tWidgets = NewToggle(v => Settings.Widgets = v);
        Row(T("Widgets", "Widgets"), T("Heure, espace disque et corbeille en haut du panneau.", "Clock, disk space and Recycle Bin at the top."), tWidgets, null);
        tPreview = NewToggle(v => Settings.HoverPreview = v);
        Row(T("Aperçu au survol", "Preview on hover"), T("Miniature des images, PDF et vidéos.", "Thumbnail of images, PDFs and videos."), tPreview, null);
        EndSection();
    }

    void BuildSearch()
    {
        NewPage(T("Recherche", "Search"));
        Section(T("CHERCHER AUSSI DANS", "ALSO SEARCH IN"));
        tSApps = NewToggle(v => Settings.SearchApps = v);
        Row(T("Applications", "Apps"), T("Menu Démarrer et Microsoft Store.", "Start menu and Microsoft Store."), tSApps, null);
        tSRecent = NewToggle(v => Settings.SearchRecent = v);
        Row(T("Fichiers récents", "Recent files"), T("Les fichiers ouverts récemment dans Windows.", "Files recently opened in Windows."), tSRecent, null);
        tSSettings = NewToggle(v => Settings.SearchSettings = v);
        Row(T("Paramètres Windows", "Windows settings"), null, tSSettings, null);
        tSCmd = NewToggle(v => Settings.SearchCommands = v);
        Row(T("Commandes système", "System commands"), T("Verrouiller, veille, redémarrer…", "Lock, sleep, restart…"), tSCmd, null);
        EndSection();

        Section("WEB");
        sEngine = NewSeg(v => Settings.WebEngine = v, "Google", "Bing", "DuckDuckGo", T("Aucun", "None"));
        Row(T("Moteur de recherche", "Search engine"), null, sEngine, null);
        EndSection();

        Section(T("ASTUCES", "TIPS"));
        Note(T("Tape directement dans le panneau. Les fautes de frappe sont tolérées (« asasin » trouve « Assassin's Creed »).\n" +
               "=  12*7  calcule (Entrée copie le résultat). Un calcul simple fonctionne aussi sans « = ».\n" +
               "?  météo  cherche sur le web.\n" +
               ">  affiche les commandes système.",
               "Type straight into the panel. Typos are tolerated (\"asasin\" finds \"Assassin's Creed\").\n" +
               "=  12*7  calculates (Enter copies the result). Simple math works without \"=\" too.\n" +
               "?  weather  searches the web.\n" +
               ">  shows system commands."));
        EndSection();
    }

    void BuildOrganize()
    {
        NewPage(T("Organisation", "Organization"));
        Section(T("AFFICHAGE", "DISPLAY"));
        tGroups = NewToggle(v => Settings.GroupCats = v);
        Row(T("Grouper par catégories", "Group by category"), null, tGroups, null);
        tRecents = NewToggle(v => Settings.ShowRecents = v);
        Row(T("Afficher les récents", "Show recent"), T("Les 5 derniers éléments ouverts.", "The last 5 items opened."), tRecents, null);
        tSystem = NewToggle(v => Settings.ShowSystem = v);
        Row(T("Afficher « Ce PC » et la Corbeille", "Show \"This PC\" and Recycle Bin"), null, tSystem, null);
        Note(T("Glisse un élément pour le réordonner, l'ajouter aux favoris ou changer sa catégorie. Glisse-le hors du panneau pour le déposer ailleurs. " +
               "Dépose un fichier sur le panneau pour l'ajouter au bureau (Maj : déplacer, Alt : raccourci).",
               "Drag an item to reorder it, add it to favorites or change its category. Drag it out of the panel to drop it elsewhere. " +
               "Drop a file on the panel to add it to the desktop (Shift: move, Alt: shortcut)."));
        EndSection();

        Section(T("NOUVEAUTÉS", "NEW ITEMS"));
        tNew = NewToggle(v => Settings.NewBadge = v);
        Row(T("Badge « Nouveau »", "\"New\" badge"), T("Sur les fichiers arrivés récemment et jamais ouverts.", "On recently added files you haven't opened."), tNew, null);
        sNewDays = NewSeg(v => Settings.NewDays = NewDaysOpts[v], T("1 jour", "1 day"), T("3 jours", "3 days"), T("7 jours", "7 days"));
        Row(T("Pendant", "For"), null, sNewDays, null);
        EndSection();

        Section(T("RANGEMENT AUTOMATIQUE", "AUTOMATIC TIDY-UP"));
        tArchive = new Toggle();
        tArchive.Changed += (s, e) =>
        {
            if (loading) return;
            if (tArchive.Checked && !Ask(T("Les fichiers et dossiers du bureau inutilisés depuis " + Settings.ArchiveDays + " jours seront déplacés dans le dossier « " +
                    Settings.ArchiveName + " » sur le bureau.\nLes raccourcis et les favoris ne sont jamais déplacés.\n\nActiver le rangement automatique ?",
                    "Desktop files and folders unused for " + Settings.ArchiveDays + " days will be moved into the \"" + Settings.ArchiveName +
                    "\" folder on the desktop.\nShortcuts and favorites are never moved.\n\nTurn on automatic tidy-up?")))
            {
                loading = true; tArchive.Checked = false; loading = false;
                return;
            }
            Settings.ArchiveOn = tArchive.Checked;
            Settings.Notify();
        };
        Row(T("Ranger les fichiers inutilisés", "Tidy up unused files"), T("Vérifié au démarrage puis toutes les 6 heures.", "Checked at startup, then every 6 hours."), tArchive, null);
        sArchDays = NewSeg(v => Settings.ArchiveDays = ArchiveOpts[v], T("30 j", "30 d"), T("60 j", "60 d"), T("90 j", "90 d"), T("180 j", "180 d"));
        Row(T("Inutilisés depuis", "Unused for"), null, sArchDays, null);
        bArchiveNow = Button(T("Ranger maintenant", "Tidy up now"), false, S(170), () =>
        {
            List<string> c = Archiver.Candidates();
            if (c.Count == 0) { Dialogs.Info(this, T("Rien à ranger pour le moment.", "Nothing to tidy up right now.")); return; }
            string list = string.Join("\n", c.Take(12).Select(p => "• " + Path.GetFileName(p))) + (c.Count > 12 ? "\n…" : "");
            if (!Ask(T(c.Count + " élément(s) seront déplacés dans « " + Settings.ArchiveName + " » :\n\n", c.Count + " item(s) will be moved into \"" + Settings.ArchiveName + "\":\n\n") + list)) return;
            int n = Archiver.Run(c);
            Dialogs.Info(this, T(n + " élément(s) rangé(s).", n + " item(s) moved."));
            Settings.Fire();
        });
        lArchive = Row(T("Ranger maintenant", "Tidy up now"), "-", bArchiveNow, null);
        bArchiveUndo = Button(T("Annuler", "Undo"), false, S(170), () =>
        {
            int n = Archiver.Undo();
            Dialogs.Info(this, T(n + " élément(s) remis sur le bureau.", n + " item(s) put back on the desktop."));
            Settings.Fire();
        });
        Row(T("Annuler le dernier rangement", "Undo the last tidy-up"), T("Remet les éléments rangés à leur place sur le bureau.", "Puts the moved items back on the desktop."), bArchiveUndo, null);
        EndSection();

        Section(T("RÉINITIALISER", "RESET"));
        Row(T("Favoris", "Favorites"), null, ResetButton(T("Tout retirer", "Remove all"), () => Settings.Favorites.Clear()), null);
        Row(T("Ordre et catégories", "Order and categories"), null, ResetButton(T("Réinitialiser", "Reset"), () => { Settings.Order.Clear(); Settings.CatOverride.Clear(); }), null);
        Row(T("Historique des récents", "Recent history"), null, ResetButton(T("Effacer", "Clear"), () => Settings.Usage.Clear()), null);
        EndSection();

        Section(T("CATÉGORIES PERSONNALISÉES", "CUSTOM CATEGORIES"));
        customCard = cur;
        RebuildCustoms();
    }
}
