// BureauCache — WindowList : fenêtres ouvertes affichées dans la barre (D-013, T-033)
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

class TaskWindow
{
    public IntPtr Handle;
    public string Title = "";
    public bool Flashing;
}

// Suivi par les événements de Windows (SetWinEventHook hors contexte : rien n'est injecté dans les autres
// programmes), regroupés par un court minuteur. L'ordre est stable : une nouvelle fenêtre s'ajoute à la fin.
// Les titres ne sont jamais écrits dans un journal (ils contiennent souvent des noms de fichiers ou de messages).
sealed class WindowList : IDisposable
{
    public event Action Changed;
    public List<TaskWindow> Items = new List<TaskWindow>();
    public IntPtr Active { get; private set; }

    readonly Native.WinEventProc proc;
    readonly IntPtr[] hooks = new IntPtr[Ranges.Length];   // un crochet par plage ; une plage refusée est retentée (BAR-04)
    readonly Timer debounce = new Timer();
    readonly HashSet<IntPtr> flashing = new HashSet<IntPtr>();
    readonly Func<IntPtr, bool> isOwn;

    // isOwn : fenêtres de la barre elle-même, jamais listées
    public WindowList(Func<IntPtr, bool> isOwn)
    {
        this.isOwn = isOwn;
        proc = OnEvent;   // gardé en champ : le délégué ne doit pas être collecté tant que le crochet existe
        debounce.Interval = 80;
        debounce.Tick += (s, e) => { debounce.Stop(); Refresh(); };
    }

    static readonly uint[][] Ranges =
    {
        new[] { Native.EVENT_SYSTEM_FOREGROUND, Native.EVENT_SYSTEM_FOREGROUND },
        new[] { Native.EVENT_SYSTEM_MINIMIZESTART, Native.EVENT_SYSTEM_MINIMIZEEND },
        new[] { Native.EVENT_OBJECT_CREATE, Native.EVENT_OBJECT_HIDE },
        new[] { Native.EVENT_OBJECT_NAMECHANGE, Native.EVENT_OBJECT_NAMECHANGE },
        new[] { Native.EVENT_OBJECT_CLOAKED, Native.EVENT_OBJECT_UNCLOAKED },
    };

    public void Start()
    {
        if (Sandbox.Active) return;
        for (int i = 0; i < Ranges.Length; i++)
            if (hooks[i] == IntPtr.Zero)
                hooks[i] = Native.SetWinEventHook(Ranges[i][0], Ranges[i][1], IntPtr.Zero, proc, 0, 0, Native.WINEVENT_OUTOFCONTEXT);
        Refresh();
    }

    void OnEvent(IntPtr hook, uint ev, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        if (idObject != 0 /*OBJID_WINDOW*/ || idChild != 0 || hwnd == IntPtr.Zero) return;
        if (ev == Native.EVENT_SYSTEM_FOREGROUND) flashing.Remove(hwnd);
        Schedule();
    }

    void Schedule() { if (!debounce.Enabled) debounce.Start(); }

    // Une fenêtre demande l'attention (message HSHELL_FLASH reçu par la barre)
    public void Flash(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || hwnd == Native.GetForegroundWindow()) return;
        flashing.Add(hwnd);
        Schedule();
    }

    public void Refresh()
    {
        List<IntPtr> found = new List<IntPtr>();
        // Énumération en échec : la dernière liste reste affichée plutôt qu'une barre vide (BAR-05)
        if (!Native.EnumWindows((h, l) => { if (!isOwn(h) && IsTaskWindow(h)) found.Add(h); return true; }, IntPtr.Zero)) return;
        found.Reverse();   // EnumWindows suit l'ordre Z : les plus anciennes sont plutôt en dessous
        HashSet<IntPtr> present = new HashSet<IntPtr>(found);
        List<TaskWindow> next = Items.Where(w => present.Contains(w.Handle)).ToList();
        HashSet<IntPtr> known = new HashSet<IntPtr>(next.Select(w => w.Handle));
        foreach (IntPtr h in found) if (!known.Contains(h)) next.Add(new TaskWindow { Handle = h });
        flashing.RemoveWhere(h => !present.Contains(h));
        foreach (TaskWindow w in next)
        {
            w.Title = Native.WindowTitle(w.Handle);
            w.Flashing = flashing.Contains(w.Handle);
        }
        TaskIcons.Forget(present);
        Items = next;
        Active = Native.GetForegroundWindow();
        if (Changed != null) Changed();
    }

    static bool IsTaskWindow(IntPtr h)
    {
        return Qualifies(Native.IsWindowVisible(h), Native.IsCloaked(h), Native.WindowStyle(h, Native.GWL_STYLE),
            Native.WindowStyle(h, Native.GWL_EXSTYLE), Native.GetWindow(h, Native.GW_OWNER) != IntPtr.Zero,
            Native.GetWindowTextLength(h), Native.WindowClass(h));
    }

    static readonly string[] ShellClasses = { "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Windows.UI.Core.CoreWindow" };

    // Règles proches de celles de la barre de Windows, séparées des appels Windows pour être testées
    internal static bool Qualifies(bool visible, bool cloaked, long style, long exStyle, bool owned, int titleLength, string cls)
    {
        if (!visible || cloaked || (style & Native.WS_CHILD) != 0) return false;
        if (ShellClasses.Contains(cls)) return false;
        if ((exStyle & Native.WS_EX_APPWINDOW) != 0) return true;   // la fenêtre demande explicitement un bouton
        if ((exStyle & (Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE)) != 0) return false;
        if (owned) return false;                                     // boîtes de dialogue : leur propriétaire suffit
        return titleLength > 0;
    }

    // Clic sur un bouton : active la fenêtre, la restaure si elle est réduite, la réduit si elle est déjà au premier plan
    public static void Activate(IntPtr h, bool wasActive)
    {
        Sandbox.Guard("activation d'une fenêtre");
        if (!Native.IsWindow(h)) return;
        if (wasActive && !Native.IsIconic(h)) { Native.ShowWindow(h, Native.SW_MINIMIZE); return; }
        if (Native.IsIconic(h)) Native.ShowWindow(h, Native.SW_RESTORE);
        // Le dernier clic vient de notre processus : Windows nous laisse donner le premier plan
        if (!Native.SetForegroundWindow(h) || Native.GetForegroundWindow() != h) Native.SwitchToThisWindow(h, true);
    }

    // « Fermer la fenêtre » : la même demande que le bouton X, l'application peut proposer d'enregistrer
    public static void Close(IntPtr h)
    {
        Sandbox.Guard("fermeture d'une fenêtre");
        if (Native.IsWindow(h)) Native.PostMessage(h, Native.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
    }

    public void Dispose()
    {
        debounce.Stop();
        debounce.Dispose();
        for (int i = 0; i < hooks.Length; i++)
            if (hooks[i] != IntPtr.Zero) { Native.UnhookWinEvent(hooks[i]); hooks[i] = IntPtr.Zero; }
        TaskIcons.Clear();   // images libérées ; une icône absente est recherchée de nouveau à la prochaine barre (BAR-03)
    }
}

// Icônes des fenêtres : celle que la fenêtre déclare, sinon celle de son application du Store, sinon celle de son exe
static class TaskIcons
{
    // Une image par fenêtre ET par taille : la barre et l'aperçu n'ont pas la même (BAR-03)
    static readonly Dictionary<Tuple<IntPtr, int>, Bitmap> cache = new Dictionary<Tuple<IntPtr, int>, Bitmap>();

    public static Bitmap For(IntPtr h, int px)
    {
        Tuple<IntPtr, int> key = Tuple.Create(h, px);
        Bitmap b;
        if (cache.TryGetValue(key, out b)) return b;
        b = Make(h, px);
        cache[key] = b;
        return b;
    }

    static Bitmap Make(IntPtr h, int px)
    {
        try
        {
            if (Native.WindowClass(h) == "ApplicationFrameWindow")
            {
                string id = Native.AppUserModelId(h);
                if (id != null) { Bitmap s = Icons.ShellImage(@"shell:AppsFolder\" + id, px, false); if (s != null) return s; }
            }
            Bitmap own = Icons.FromHIcon(Native.WindowIcon(h, true));
            if (own != null) return own;
            string exe = Native.ProcessPath(h);
            if (exe != null) return Icons.ForPath(exe, px);
        }
        catch { }
        return null;
    }

    // Fenêtres fermées : leurs images sont libérées
    public static void Forget(HashSet<IntPtr> alive)
    {
        foreach (Tuple<IntPtr, int> k in cache.Keys.Where(k => !alive.Contains(k.Item1)).ToList())
        {
            if (cache[k] != null) cache[k].Dispose();
            cache.Remove(k);
        }
    }

    public static void Clear() { Forget(new HashSet<IntPtr>()); }
}
