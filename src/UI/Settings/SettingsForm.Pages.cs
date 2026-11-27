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
        NewPage(T("Général", "General", "General"), T("Bureau, démarrage et langue.", "Desktop, startup and language.", "Escritorio, inicio e idioma."));
        Section(T("BUREAU", "DESKTOP", "ESCRITORIO"));
        tHide = NewToggle(v => Settings.HideIcons = v);
        Row(T("Cacher les icônes du bureau", "Hide desktop icons", "Ocultar los iconos del escritorio"), T("Elles restent accessibles depuis le panneau.", "They stay available in the panel.", "Siguen accesibles desde el panel."), tHide, null);
        tTaskbar = new Toggle();
        tTaskbar.Changed += (s, e) =>
        {
            if (loading) return;
            // Confirmation : la barre de Windows disparaît, on dit tout de suite comment la retrouver (D-013)
            if (tTaskbar.Checked && !Ask(TaskbarConsent.Question))
            {
                loading = true; tTaskbar.Checked = false; loading = false;
                return;
            }
            Settings.TaskbarOn = tTaskbar.Checked;
            Settings.Notify();
        };
        Row(TaskbarConsent.Label, TaskbarConsent.Detail, tTaskbar, null);

        Section("WINDOWS");
        tStartup = new Toggle();
        tStartup.Changed += (s, e) =>
        {
            if (loading) return;
            try { Startup.Set(tStartup.Checked, Installer.IsInstalled ? Installer.Exe : null); }
            catch (Exception ex) { Dialogs.Info(this, ex.Message); }
            loading = true; tStartup.Checked = Startup.Enabled; loading = false;
        };
        Row(T("Lancer au démarrage de Windows", "Start with Windows", "Iniciar con Windows"), null, tStartup, null);
        bInstall = new FlatButton(T("Installer", "Install", "Instalar"), true, S(150));
        bInstall.Click += (s, e) =>
        {
            if (Installer.IsInstalled && Installer.RunningFromInstall) { if (act.Uninstall != null) act.Uninstall(); }
            else if (act.Install != null) act.Install();
        };
        lInstall = Row(T("Installation", "Installation", "Instalación"), "-", bInstall, null);

        Section(T("LANGUE", "LANGUAGE", "IDIOMA"));
        sLang = NewSeg(v => Settings.Language = v, T("Système", "System", "Sistema"), "Français", "English", "Español");
        Row(T("Langue", "Language", "Idioma"), null, sLang, null);

        Section(T("ICÔNE PRÈS DE L'HORLOGE", "ICON BY THE CLOCK", "ICONO JUNTO AL RELOJ"));
        lTray = Row(T("Toujours visible", "Always visible", "Siempre visible"), "-",
            Button(T("Ouvrir le réglage", "Open the setting", "Abrir el ajuste"), false, S(150), () => { try { ShellUtil.Open("ms-settings:taskbar", null); } catch { } }), null);
        tQuiet = NewToggle(v => Settings.QuietStart = v);
        Row(T("Démarrage discret", "Quiet start", "Inicio discreto"), T("Pas de bulle d'information au lancement.", "No info bubble at startup.", "Sin aviso al iniciar."), tQuiet, null);
        Note(T("Clic sur l'icône : ouvrir la liste · double-clic : ces paramètres · clic droit : le menu. Réduire ou fermer cette fenêtre la range près de l'horloge, BureauCache continue de tourner.",
               "Click the icon: open the list · double-click: these settings · right-click: the menu. Minimizing or closing this window tucks it by the clock; BureauCache keeps running.", "Un clic en el icono abre la lista, un doble clic abre la configuración y un clic derecho abre el menú. Al minimizar o cerrar esta ventana, BureauCache sigue activo junto al reloj."));

        Section(T("AIDE", "HELP", "AYUDA"));
        Row(T("Visite guidée", "Guided tour", "Visita guiada"), T("Les 4 écrans de bienvenue, pour redécouvrir l'essentiel.", "The 4 welcome screens, to go over the basics again.", "Vuelve a ver las cuatro pantallas de bienvenida."),
            Button(T("Revoir", "Show again", "Volver a ver"), false, S(130), () => { if (act.Welcome != null) act.Welcome(); }), null);
        Row(T("Raccourcis clavier", "Keyboard shortcuts", "Atajos de teclado"), T("Appuie sur F1 dans le panneau pour tous les voir.", "Press F1 in the panel to see them all.", "Pulsa F1 en el panel para verlos todos."), null, null);
    }

    void BuildOpening()
    {
        NewPage(T("Ouverture", "Opening", "Apertura"), T("Bord de l’écran et raccourci clavier.", "Screen edge and keyboard shortcut.", "Borde de la pantalla y atajo de teclado."));
        Section(T("SOURIS", "MOUSE", "RATÓN"));
        sSide = NewSeg(v => Settings.RightSide = v == 1, T("Gauche", "Left", "Izquierda"), T("Droite", "Right", "Derecha"));
        Row(T("Bord de l'écran", "Screen edge", "Borde de la pantalla"), null, sSide, null);
        sZone = NewSeg(v => Settings.EdgeZone = v, T("Tout", "All", "Todo"), T("Haut", "Top", "Arriba"), T("Milieu", "Middle", "Centro"), T("Bas", "Bottom", "Abajo"));
        Row(T("Partie du bord", "Part of the edge", "Parte del borde"), T("Zone du bord qui ouvre la liste.", "Part of the edge that opens the list.", "Zona del borde que abre la lista."), sZone, null);
        Screen[] all = Screen.AllScreens;
        int n = all.Length;
        string[] scr = new string[n + 1];
        scr[0] = T("Tous", "All", "Todas");
        for (int i = 0; i < n; i++) scr[i + 1] = (i + 1).ToString();
        sScreen = NewSeg(v => { Settings.ScreenIdx = v - 1; Settings.ScreenName = v > 0 && v <= all.Length ? all[v - 1].DeviceName : ""; }, scr);
        Row(T("Écran", "Screen", "Pantalla"), n > 1 ? T("Écran sur lequel le bord est actif.", "Screen where the edge is active.", "Pantalla en la que está activo el borde.") : T("Un seul écran détecté.", "Only one screen detected.", "Se ha detectado una sola pantalla."), sScreen, null);
        slDelay = new Slider(0, 1000, 20);
        lDelay = ValueLabel();
        slDelay.Changed += (s, e) => { if (loading) return; Settings.Delay = slDelay.Value; Settings.Notify(); };
        Row(T("Délai d'ouverture", "Opening delay", "Retraso de apertura"), T("Évite les ouvertures accidentelles.", "Avoids accidental opening.", "Evita aperturas accidentales."), slDelay, lDelay);
        tAuto = NewToggle(v => Settings.AutoClose = v);
        Row(T("Fermer quand la souris s'éloigne", "Close when the mouse leaves", "Cerrar al alejar el ratón"), null, tAuto, null);
        tFull = NewToggle(v => Settings.NoFullscreen = v);
        Row(T("Désactiver en plein écran", "Disable in full screen", "Desactivar en pantalla completa"), T("Jeux, vidéos et présentations ne sont pas dérangés.", "Games, videos and slideshows are left alone.", "Evita interrupciones en juegos, vídeos y presentaciones."), tFull, null);

        Section(T("CLAVIER", "KEYBOARD", "TECLADO"));
        tHot = NewToggle(v => Settings.HotkeyOn = v);
        Row(T("Raccourci clavier", "Keyboard shortcut", "Atajo de teclado"), T("Ouvre ou ferme la liste depuis n'importe où.", "Opens or closes the list from anywhere.", "Abre o cierra la lista desde cualquier lugar."), tHot, null);
        hk = new HotkeyBox();
        hk.Changed += (s, e) => { Settings.HotMods = hk.Mods; Settings.HotKey = hk.Key; Settings.Notify(); };
        lHotStatus = Row(T("Combinaison", "Combination", "Combinación"), "-", hk, null);
    }

    void BuildLook()
    {
        NewPage(T("Apparence", "Appearance", "Apariencia"), T("Thème, disposition et widgets du panneau.", "Theme, layout and panel widgets.", "Tema, diseño y widgets del panel."));
        Section(T("THÈMES PRÊTS", "READY-MADE THEMES", "TEMAS LISTOS"));
        themes = new ThemePicker();
        themes.Changed += (s, e) => { if (loading || themes.Selected < 0) return; Palettes.ApplyPreset(themes.Selected); Settings.Notify(); };
        Row(T("En un clic", "In one click", "Con un clic"), T("Couleurs des fonds, accent, mode et fond de la barre. Chaque réglage reste modifiable ci-dessous.",
            "Background colors, accent, mode and taskbar background. Each setting can still be changed below.",
            "Colores de fondo, acento, modo y fondo de la barra. Cada ajuste se puede cambiar debajo."), themes, null);

        Section(T("THÈME", "THEME", "TEMA"));
        sTheme = NewSeg(v => Settings.ThemeMode = v, T("Système", "System", "Sistema"), T("Sombre", "Dark", "Oscuro"), T("Clair", "Light", "Claro"));
        Row(T("Thème", "Theme", "Tema"), T("« Système » suit Windows.", "\"System\" follows Windows.", "«Sistema» sigue la configuración de Windows."), sTheme, null);
        swatch = new Swatches();
        swatch.Changed += (s, e) => { if (loading) return; Settings.Accent = swatch.Selected; Settings.AccentFromWindows = false; Settings.Notify(); };
        Row(T("Couleur d'accent", "Accent color", "Color de énfasis"), null, swatch, null);
        tAccentWindows = NewToggle(v => { Settings.AccentFromWindows = v; Settings.RefreshWindowsAccent(); });
        Row(T("Suivre l'accent de Windows", "Follow the Windows accent", "Seguir el acento de Windows"),
            T("La couleur choisie dans Paramètres Windows > Personnalisation > Couleurs.", "The color chosen in Windows Settings > Personalization > Colors.",
              "El color elegido en Configuración de Windows > Personalización > Colores."), tAccentWindows, null);
        string[] paletteNames = new string[Palettes.All.Length];
        for (int i = 0; i < paletteNames.Length; i++) paletteNames[i] = Palettes.All[i].Name;
        sPalette = NewSeg(v => Settings.Palette = v, paletteNames);
        Row(T("Teinte des fonds", "Background tint", "Tono de los fondos"), T("Une nuance discrète sur les fonds ; le texte garde son contraste.", "A subtle shade on backgrounds; text keeps its contrast.",
            "Un matiz discreto en los fondos; el texto conserva su contraste."), sPalette, null);
        sZoom = NewSeg(v => Settings.InterfaceZoom = Settings.Zooms[v], "90 %", "100 %", "115 %", "130 %");
        lZoom = Row(T("Taille de l'interface", "Interface size", "Tamaño de la interfaz"),
            T("Textes et éléments de BureauCache. S'applique au prochain démarrage.", "BureauCache text and elements. Applies at next start.", "Textos y elementos de BureauCache. Se aplica en el próximo inicio."), sZoom, null,
            null, Button(T("Redémarrer", "Restart", "Reiniciar"), false, S(120), () => { if (act.Restart != null) act.Restart(); }));
        tGlass = NewToggle(v => Settings.Glass = v);
        Row(T("Effet verre", "Glass effect", "Efecto de cristal"), T("Le panneau laisse deviner ce qu'il y a derrière.", "The panel shows a blur of what's behind it.", "Difumina el fondo del panel."), tGlass, null);
        tLive = NewToggle(v => Settings.LiveBlur = v);
        Row(T("Flou en direct", "Live blur", "Desenfoque en directo"), T(
            "Actualise le fond quand l'effet verre et le masquage en capture sont activés et que le panneau est détaché.",
            "Updates the background when glass and capture hiding are enabled and the panel is unpinned.",
            "Actualiza el fondo si el efecto de cristal y la ocultación en capturas están activados y el panel está desfijado."), tLive, null);
        tCapture = NewToggle(v =>
        {
            Settings.HidePanelInCapture = v;
            // Avant Windows 10 2004, Windows ne sait pas retirer une fenêtre des captures : il la remplace par du noir
            if (v && !loading && !Native.CaptureExclusionSupported)
                Dialogs.Info(this, T("Sur cette version de Windows, le panneau apparaîtra en noir dans les captures au lieu d'en être absent (Windows 10 version 2004 ou plus récente requise).",
                                     "On this version of Windows, the panel will show as black in captures instead of being hidden (Windows 10 version 2004 or later is required).",
                                     "En esta versión de Windows, el panel aparecerá en negro en las capturas en lugar de ocultarse (se requiere Windows 10 versión 2004 o posterior)."));
        });
        Row(T("Masquer le panneau dans les captures", "Hide panel from capture", "Ocultar el panel en capturas"), T(
            "Désactive ce réglage pour montrer le panneau en partageant l'écran entier. Le flou prend alors une image du fond à l'ouverture.",
            "Turn this off to show the panel when sharing your entire screen. The blur then uses a snapshot taken when the panel opens.",
            "Desactiva este ajuste para mostrar el panel al compartir toda la pantalla. El desenfoque usa una imagen tomada al abrir el panel."), tCapture, null);
        tAnim = NewToggle(v => Settings.Animations = v);
        Row(T("Animations", "Animations", "Animaciones"), null, tAnim, null);

        Section(T("PANNEAU", "PANEL", "PANEL"));
        sView = NewSeg(v => Settings.GridView = v == 1, T("Liste", "List", "Lista"), T("Grille", "Grid", "Cuadrícula"));
        Row(T("Affichage", "Layout", "Vista"), null, sView, null);
        slWidth = new Slider(260, 560, 10);
        lWidth = ValueLabel();
        slWidth.Changed += (s, e) => { if (loading) return; Settings.PanelWidth = slWidth.Value; Settings.Notify(); };
        Row(T("Largeur du panneau", "Panel width", "Ancho del panel"), null, slWidth, lWidth);
        tWidgets = NewToggle(v => Settings.Widgets = v);
        Row(T("Widgets", "Widgets", "Widgets"), T("Heure, espace disque et corbeille en haut du panneau. Clic droit sur le disque : nettoyage rapide.", "Clock, disk space and Recycle Bin at the top. Right-click the disk: quick clean-up.", "Hora, espacio en disco y papelera en la parte superior. Haz clic derecho en el disco para abrir la limpieza rápida."), tWidgets, null);
        tWeather = NewToggle(v =>
        {
            // Sans lieu, la météo n'est pas activée tout de suite : c'est le choix d'un lieu qui l'active (Q30-08).
            // Le lieu se cherche juste en dessous, sans fenêtre à part (Gemini A8).
            if (v && !Weather.Configured) { AskForPlace(); return; }
            Settings.WeatherOn = v;
            if (!v) Weather.Reset();
        });
        Row(T("Météo", "Weather", "Tiempo"), T("Temps et prévisions du lieu choisi (données Open-Meteo.com, CC BY 4.0).", "Weather and forecast for the chosen place (Open-Meteo.com data, CC BY 4.0).", "Tiempo y pronóstico del lugar elegido (datos de Open-Meteo.com, CC BY 4.0)."), tWeather, null);
        BuildPlaceRow();
        tPreview = NewToggle(v => Settings.HoverPreview = v);
        Row(T("Aperçu au survol", "Preview on hover", "Vista previa al pasar el ratón"), T("Miniature des images, PDF et vidéos.", "Thumbnail of images, PDFs and videos.", "Miniaturas de imágenes, PDF y vídeos."), tPreview, null);
    }

    void BuildSearch()
    {
        NewPage(T("Recherche", "Search", "Búsqueda"), T("Sources de recherche et moteur web.", "Search sources and web engine.", "Fuentes de búsqueda y motor web."));
        Section(T("CHERCHER AUSSI DANS", "ALSO SEARCH IN", "BUSCAR TAMBIÉN EN"));
        tSApps = NewToggle(v => Settings.SearchApps = v);
        Row(T("Applications", "Apps", "Aplicaciones"), T("Menu Démarrer et Microsoft Store.", "Start menu and Microsoft Store.", "Menú Inicio y Microsoft Store."), tSApps, null);
        tSRecent = NewToggle(v => Settings.SearchRecent = v);
        Row(T("Fichiers récents", "Recent files", "Archivos recientes"), T("Les fichiers ouverts récemment dans Windows.", "Files recently opened in Windows.", "Archivos abiertos recientemente en Windows."), tSRecent, null);
        tSSettings = NewToggle(v => Settings.SearchSettings = v);
        Row(T("Paramètres Windows", "Windows settings", "Configuración de Windows"), null, tSSettings, null);
        tSCmd = NewToggle(v => Settings.SearchCommands = v);
        Row(T("Commandes système", "System commands", "Comandos del sistema"), T("Verrouiller, veille, redémarrer…", "Lock, sleep, restart…", "Bloquear, suspender, reiniciar…"), tSCmd, null);
        tSContent = NewToggle(v => Settings.SearchContent = v);
        Row(T("Contenu des fichiers du bureau", "Desktop file contents", "Contenido de los archivos del escritorio"), T("Utilise l'index de Windows Search : trouve un document par un mot qu'il contient.", "Uses the Windows Search index: finds a document by a word it contains.", "Usa el índice de Windows Search: encuentra un documento por una palabra que contiene."), tSContent, null);

        Section(T("PRESSE-PAPIERS", "CLIPBOARD", "PORTAPAPELES"));
        tClip = NewToggle(v => Settings.ClipboardOn = v);
        Row(T("Historique des textes copiés", "History of copied text", "Historial de textos copiados"),
            T("Les 20 derniers textes, dans le panneau. Gardés en mémoire seulement : rien n'est écrit sur le disque, tout disparaît à la fermeture. Les copies privées (gestionnaires de mots de passe) sont ignorées.",
              "The last 20 texts, in the panel. Kept in memory only: nothing is written to disk, everything is gone when the app closes. Private copies (password managers) are ignored.",
              "Los últimos 20 textos, en el panel. Solo en memoria: no se escribe nada en el disco y todo desaparece al cerrar. Se ignoran las copias privadas (gestores de contraseñas)."), tClip, null);

        Section("WEB");
        sEngine = NewSeg(v => Settings.WebEngine = v, "Google", "Bing", "DuckDuckGo", T("Aucun", "None", "Ninguno"));
        Row(T("Moteur de recherche", "Search engine", "Motor de búsqueda"), null, sEngine, null);

        Section(T("ASTUCES", "TIPS", "CONSEJOS"));
        Note(T("Tape directement dans le panneau. Les fautes de frappe sont tolérées (« asasin » trouve « Assassin's Creed »).\n" +
               "=  12*7  calcule (Entrée copie le résultat). Un calcul simple fonctionne aussi sans « = ».\n" +
               "?  météo  cherche sur le web.\n" +
               ">  affiche les commandes système.",
               "Type straight into the panel. Typos are tolerated (\"asasin\" finds \"Assassin's Creed\").\n" +
               "=  12*7  calculates (Enter copies the result). Simple math works without \"=\" too.\n" +
               "?  weather  searches the web.\n" +
               ">  shows system commands.",
               "Escribe directamente en el panel. Se admiten erratas («asasin» encuentra «Assassin's Creed»).\n" +
               "=  12*7  calcula (Intro copia el resultado). Los cálculos sencillos también funcionan sin «=».\n" +
               "?  tiempo  busca en la web.\n" +
               ">  muestra los comandos del sistema."));
    }

    void BuildOrganize()
    {
        NewPage(T("Organisation", "Organization", "Organización"), T("Catégories, favoris et rangement du bureau.", "Categories, favorites and desktop tidy-up.", "Categorías, favoritos y orden del escritorio."));
        Section(T("AFFICHAGE", "DISPLAY", "VISUALIZACIÓN"));
        tGroups = NewToggle(v => Settings.GroupCats = v);
        Row(T("Grouper par catégories", "Group by category", "Agrupar por categoría"), null, tGroups, null);
        tRecents = NewToggle(v => Settings.ShowRecents = v);
        Row(T("Afficher les récents", "Show recent", "Mostrar recientes"), T("Les 5 derniers éléments ouverts.", "The last 5 items opened.", "Los últimos 5 elementos abiertos."), tRecents, null);
        tSystem = NewToggle(v => Settings.ShowSystem = v);
        Row(T("Afficher « Ce PC » et la Corbeille", "Show \"This PC\" and Recycle Bin", "Mostrar «Este equipo» y la papelera"), null, tSystem, null);
        Note(T("Glisse un élément pour le réordonner, l'ajouter aux favoris ou changer sa catégorie. Glisse-le hors du panneau pour le déposer ailleurs. " +
               "Dépose un fichier sur le panneau pour l'ajouter au bureau (Maj : déplacer, Alt : raccourci).",
               "Drag an item to reorder it, add it to favorites or change its category. Drag it out of the panel to drop it elsewhere. " +
               "Drop a file on the panel to add it to the desktop (Shift: move, Alt: shortcut).",
               "Arrastra un elemento para ordenarlo, añadirlo a favoritos o cambiar su categoría. Arrástralo fuera del panel para soltarlo en otro lugar. " +
               "Suelta un archivo sobre el panel para añadirlo al escritorio (Mayús: mover, Alt: crear acceso directo)."));

        Section(T("NOUVEAUTÉS", "NEW ITEMS", "ELEMENTOS NUEVOS"));
        tNew = NewToggle(v => Settings.NewBadge = v);
        Row(T("Badge « Nouveau »", "\"New\" badge", "Etiqueta «Nuevo»"), T("Sur les fichiers arrivés récemment et jamais ouverts.", "On recently added files you haven't opened.", "En archivos añadidos recientemente que aún no has abierto."), tNew, null);
        sNewDays = NewSeg(v => Settings.NewDays = NewDaysOpts[v], T("1 jour", "1 day", "1 día"), T("3 jours", "3 days", "3 días"), T("7 jours", "7 days", "7 días"));
        Row(T("Pendant", "For", "Durante"), null, sNewDays, null);

        Section(T("RANGEMENT AUTOMATIQUE", "AUTOMATIC TIDY-UP", "ORGANIZACIÓN AUTOMÁTICA"));
        tArchive = new Toggle();
        tArchive.Changed += (s, e) =>
        {
            if (loading) return;
            if (tArchive.Checked && !Ask(T("Les fichiers et dossiers du bureau inutilisés depuis " + Settings.ArchiveDays + " jours seront déplacés dans le dossier « " +
                    Settings.ArchiveName + " » sur le bureau.\nLes raccourcis et les favoris ne sont jamais déplacés.\n\nActiver le rangement automatique ?",
                    "Desktop files and folders unused for " + Settings.ArchiveDays + " days will be moved into the \"" + Settings.ArchiveName +
                    "\" folder on the desktop.\nShortcuts and favorites are never moved.\n\nTurn on automatic tidy-up?",
                    "Los archivos y carpetas del escritorio sin usar durante " + Settings.ArchiveDays + " días se moverán a «" + Settings.ArchiveName +
                    "» en el escritorio.\nSe conservarán los accesos directos y los favoritos en su lugar.\n\n¿Activar la organización automática?")))
            {
                loading = true; tArchive.Checked = false; loading = false;
                return;
            }
            Settings.ArchiveOn = tArchive.Checked;
            Settings.Notify();
        };
        Row(T("Ranger les fichiers inutilisés", "Tidy up unused files", "Archivar archivos sin usar"), T("Vérifié au démarrage puis toutes les 6 heures.", "Checked at startup, then every 6 hours.", "Se comprueba al iniciar y cada 6 horas."), tArchive, null);
        sArchDays = NewSeg(v => Settings.ArchiveDays = ArchiveOpts[v], T("30 j", "30 d", "30 d"), T("60 j", "60 d", "60 d"), T("90 j", "90 d", "90 d"), T("180 j", "180 d", "180 d"));
        Row(T("Inutilisés depuis", "Unused for", "Sin usar durante"), null, sArchDays, null);
        bArchiveNow = Button(T("Ranger maintenant", "Tidy up now", "Archivar ahora"), false, S(170), TidyNow);
        lArchive = Row(T("Ranger maintenant", "Tidy up now", "Archivar ahora"), "-", bArchiveNow, null);
        bArchiveUndo = Button(T("Annuler", "Undo", "Deshacer"), false, S(170), () =>
        {
            Begin(lArchiveUndo);
            int n = Archiver.Undo();
            Done(lArchiveUndo, T(n + " élément(s) remis sur le bureau.", n + " item(s) put back on the desktop.", n + " elemento(s) devuelto(s) al escritorio."));
            Settings.Fire();
        });
        lArchiveUndo = Row(T("Annuler le dernier rangement", "Undo the last tidy-up", "Deshacer el último archivado"), T("Remet les éléments rangés à leur place sur le bureau.", "Puts the moved items back on the desktop.", "Devuelve al escritorio los elementos movidos."), bArchiveUndo, null);

        BuildClutterSection();
        BuildRulesSection();

        Section(T("RÉINITIALISER", "RESET", "RESTABLECER"));
        Row(T("Favoris", "Favorites", "Favoritos"), null, ResetButton(T("Tout retirer", "Remove all", "Quitar todo"), () => Settings.Favorites.Clear()), null);
        Row(T("Ordre et catégories", "Order and categories", "Orden y categorías"), null, ResetButton(T("Réinitialiser", "Reset", "Restablecer"), () => { Settings.Order.Clear(); Settings.CatOverride.Clear(); }), null);
        Row(T("Historique des récents", "Recent history", "Historial reciente"), null, ResetButton(T("Effacer", "Clear", "Borrar"), Settings.ClearRecents), null);

        Section(T("CATÉGORIES PERSONNALISÉES", "CUSTOM CATEGORIES", "CATEGORÍAS PERSONALIZADAS"));
        customCard = cur;
        RebuildCustoms();
    }
}
