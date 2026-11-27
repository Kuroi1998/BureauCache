// BureauCache — SettingsForm : page « Barre des tâches » (T-043, D-019)
using System;

partial class SettingsForm
{
    Segmented sBarSize, sBarAlign;
    Toggle tBarLabels, tBarQuick, tBarBell, tBarDate, tBarSeconds;

    void BuildTaskbar()
    {
        NewPage(T("Barre des tâches", "Taskbar", "Barra de tareas"),
            T("Disposition, taille et éléments de la barre BureauCache.", "Layout, size and items of the BureauCache taskbar.", "Diseño, tamaño y elementos de la barra de BureauCache."));
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
    }
}
