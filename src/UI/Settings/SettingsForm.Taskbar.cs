// BureauCache — SettingsForm : page « Barre des tâches » (T-043, D-019)
using System;

partial class SettingsForm
{
    Segmented sBarSize, sBarAlign, sBarStyle, sBarMiddle;
    Toggle tBarLabels, tBarQuick, tBarBell, tBarDate, tBarSeconds, tBarLine, tBarAutoHide, tBarGroup, tBarWheel;

    void BuildTaskbar()
    {
        NewPage(T("Barre des tâches", "Taskbar", "Barra de tareas"),
            T("Disposition, éléments, apparence et comportement de la barre BureauCache.", "Layout, items, look and behavior of the BureauCache taskbar.", "Diseño, elementos, apariencia y comportamiento de la barra de BureauCache."));
        Section(T("DISPOSITION", "LAYOUT", "DISEÑO"));
        sBarSize = NewSeg(v => Settings.TaskbarSize = v, T("Petite", "Small", "Pequeña"), T("Normale", "Normal", "Normal"), T("Grande", "Large", "Grande"));
        Row(T("Taille", "Size", "Tamaño"), T("Hauteur de la barre et taille des icônes.", "Bar height and icon size.", "Altura de la barra y tamaño de los iconos."), sBarSize, null);
        sBarAlign = NewSeg(v => Settings.TaskbarCenter = v == 1, T("À gauche", "Left", "Izquierda"), T("Centrées", "Centered", "Centrados"));
        Row(T("Icônes", "Icons", "Iconos"), T("Centrées avec le bouton Démarrer, comme Windows 11.", "Centered with the Start button, like Windows 11.", "Centrados con el botón Inicio, como en Windows 11."), sBarAlign, null);
        tBarLabels = NewToggle(v => Settings.TaskbarLabels = v);
        Row(T("Titres des fenêtres", "Window titles", "Títulos de las ventanas"),
            T("Le titre à côté de chaque icône, comme Windows 10.", "The title next to each icon, like Windows 10.", "El título junto a cada icono, como en Windows 10."), tBarLabels, null);

        Section(T("ÉLÉMENTS AFFICHÉS", "SHOWN ITEMS", "ELEMENTOS VISIBLES"));
        tBarQuick = NewToggle(v => Settings.TaskbarQuick = v);
        Row(T("Réseau et son", "Network and sound", "Red y sonido"), T("Ouvre les Paramètres rapides.", "Opens Quick settings.", "Abre la configuración rápida."), tBarQuick, null);
        tBarBell = NewToggle(v => Settings.TaskbarBell = v);
        Row(T("Notifications", "Notifications", "Notificaciones"), T("La cloche et son panneau.", "The bell and its panel.", "La campana y su panel."), tBarBell, null);
        tBarDate = NewToggle(v => Settings.TaskbarDate = v);
        Row(T("Date sous l'heure", "Date under the time", "Fecha bajo la hora"), null, tBarDate, null);
        tBarSeconds = NewToggle(v => Settings.TaskbarSeconds = v);
        Row(T("Secondes", "Seconds", "Segundos"), T("L'horloge de la barre affiche les secondes.", "The taskbar clock shows seconds.", "El reloj de la barra muestra los segundos."), tBarSeconds, null);

        Section(T("APPARENCE", "APPEARANCE", "APARIENCIA"));
        sBarStyle = NewSeg(v => Settings.TaskbarStyle = v, T("Uni", "Solid", "Sólido"), T("Verre", "Glass", "Cristal"), T("Teinté", "Tinted", "Teñido"));
        Row(T("Fond", "Background", "Fondo"), T("Verre : le fond d'écran flouté derrière la barre. Teinté : la couleur d'accent.",
            "Glass: the blurred wallpaper behind the bar. Tinted: the accent color.", "Cristal: el fondo de pantalla desenfocado. Teñido: el color de acento."), sBarStyle, null);
        tBarLine = NewToggle(v => Settings.TaskbarLine = v);
        Row(T("Ligne du haut", "Top line", "Línea superior"), T("Un trait fin sépare la barre des fenêtres.", "A thin line separates the bar from windows.", "Una línea fina separa la barra de las ventanas."), tBarLine, null);

        Section(T("COMPORTEMENT", "BEHAVIOR", "COMPORTAMIENTO"));
        tBarAutoHide = NewToggle(v => Settings.TaskbarAutoHide = v);
        Row(T("Masquer automatiquement", "Automatically hide", "Ocultar automáticamente"),
            T("La barre se replie et revient quand la souris touche le bas de l'écran.", "The bar tucks away and comes back when the mouse touches the bottom of the screen.",
              "La barra se oculta y vuelve cuando el ratón toca el borde inferior de la pantalla."), tBarAutoHide, null);
        tBarGroup = NewToggle(v => Settings.TaskbarGroup = v);
        Row(T("Regrouper les fenêtres", "Group windows", "Agrupar ventanas"),
            T("Un seul bouton par application ; chaque clic passe à sa fenêtre suivante.", "One button per app; each click moves to its next window.", "Un botón por aplicación; cada clic pasa a su siguiente ventana."), tBarGroup, null);
        sBarMiddle = NewSeg(v => Settings.TaskbarMiddleClose = v == 1, T("Nouvelle fenêtre", "New window", "Nueva ventana"), T("Fermer", "Close", "Cerrar"));
        Row(T("Clic du milieu", "Middle click", "Clic central"), T("Sur le bouton d'une application.", "On an app button.", "En el botón de una aplicación."), sBarMiddle, null);
        tBarWheel = NewToggle(v => Settings.TaskbarWheel = v);
        Row(T("Molette sur le son", "Scroll on sound", "Rueda sobre el sonido"), T("Monte ou baisse le volume sans ouvrir de panneau.", "Turns the volume up or down without opening a panel.", "Sube o baja el volumen sin abrir ningún panel."), tBarWheel, null);

        Note(T("Ces réglages s'appliquent quand la barre BureauCache remplace celle de Windows (page Général).",
               "These settings apply when the BureauCache taskbar replaces the Windows one (General page).",
               "Estos ajustes se aplican cuando la barra de BureauCache sustituye a la de Windows (página General)."));
    }

    void LoadTaskbarValues()
    {
        sBarSize.Selected = Math.Max(0, Math.Min(2, Settings.TaskbarSize));
        sBarAlign.Selected = Settings.TaskbarCenter ? 1 : 0;
        tBarLabels.Checked = Settings.TaskbarLabels;
        tBarQuick.Checked = Settings.TaskbarQuick;
        tBarBell.Checked = Settings.TaskbarBell;
        tBarDate.Checked = Settings.TaskbarDate;
        tBarSeconds.Checked = Settings.TaskbarSeconds;
        sBarStyle.Selected = Math.Max(0, Math.Min(2, Settings.TaskbarStyle));
        tBarLine.Checked = Settings.TaskbarLine;
        tBarAutoHide.Checked = Settings.TaskbarAutoHide;
        tBarGroup.Checked = Settings.TaskbarGroup;
        sBarMiddle.Selected = Settings.TaskbarMiddleClose ? 1 : 0;
        tBarWheel.Checked = Settings.TaskbarWheel;
    }
}
