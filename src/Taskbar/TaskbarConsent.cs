// BureauCache — TaskbarConsent : textes de l'option « Remplacer la barre des tâches » (D-013), partagés par les
// Paramètres et la visite guidée pour qu'ils disent toujours la même chose
static class TaskbarConsent
{
    // Confirmation avant d'activer l'option : la barre de Windows disparaît, on dit tout de suite comment la retrouver
    public static string Question
    {
        get
        {
            return L.T("La barre des tâches de Windows sera cachée tant que BureauCache est ouvert, et remplacée par la sienne : menu Démarrer, fenêtres ouvertes avec aperçu, applications épinglées, Paramètres rapides et horloge.\n\nPour la retrouver : clic droit sur la barre BureauCache, puis « Rétablir la barre des tâches de Windows ». Quitter BureauCache la rend aussi.\n\nRemplacer la barre des tâches ?",
                       "The Windows taskbar will be hidden while BureauCache is running, and replaced by its own: Start menu, open windows with previews, pinned apps, Quick Settings and clock.\n\nTo get it back: right-click the BureauCache bar, then \"Bring back the Windows taskbar\". Quitting BureauCache also brings it back.\n\nReplace the taskbar?",
                       "La barra de tareas de Windows se ocultará mientras BureauCache esté abierto y se reemplazará por la suya: menú Inicio, ventanas abiertas con vista previa, aplicaciones ancladas, Configuración rápida y reloj.\n\nPara recuperarla: clic derecho en la barra de BureauCache y luego «Restablecer la barra de tareas de Windows». Al salir de BureauCache también vuelve.\n\n¿Reemplazar la barra de tareas?");
        }
    }

    public static string Label { get { return L.T("Remplacer la barre des tâches", "Replace the taskbar", "Reemplazar la barra de tareas"); } }

    public static string Detail
    {
        get
        {
            return L.T("Barre BureauCache en bas de l'écran principal : menu Démarrer, fenêtres ouvertes, épingles et aperçus. Les icônes près de l'horloge arriveront ensuite.",
                       "BureauCache bar at the bottom of the main screen: Start menu, open windows, pins and previews. Tray icons are coming later.",
                       "Barra de BureauCache en la parte inferior de la pantalla principal: menú Inicio, ventanas abiertas, anclajes y vistas previas. Los iconos junto al reloj llegarán más adelante.");
        }
    }
}
