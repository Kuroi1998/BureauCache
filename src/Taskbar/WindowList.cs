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
    public string AppKey;   // application de la fenêtre (D-014) : « aumid:… » ou « exe:… »
    public const int MaxDiscordAlerts = 100;
    public int DiscordAlerts { get; private set; }

    public bool RegisterDiscordAlert()
    {
        if (!IsDiscord(AppKey)) return false;
        DiscordAlerts = Math.Min(MaxDiscordAlerts, DiscordAlerts + 1);
        return true;
    }

    public void ClearDiscordAlerts() { DiscordAlerts = 0; }

    static bool IsDiscord(string key)
    {
        if (string.Equals(key, "aumid:com.squirrel.Discord.Discord", StringComparison.OrdinalIgnoreCase)) return true;
        if (key == null || !key.StartsWith("exe:", StringComparison.OrdinalIgnoreCase)) return false;
        int nameStart = Math.Max(4, key.LastIndexOfAny(new[] { '\\', '/' }) + 1);
        return string.Equals(key.Substring(nameStart), "Discord.exe", StringComparison.OrdinalIgnoreCase);
    }
}

// Suivi par les événements de Windows (SetWinEventHook hors contexte : rien n'est injecté dans les autres
// programmes), regroupés par un court minuteur. L'ordre est stable : une nouvelle fenêtre s'ajoute à la fin.
// Les titres ne sont jamais écrits dans un journal (ils contiennent souvent des noms de fichiers ou de messages).
sealed class WindowList : IDisposable
{
    public event Action Changed;
    public List<TaskWindow> Items = new List<TaskWindow>();
    public IntPtr Active { get; private set; }
    public bool NeedsRefresh { get; private set; }

    readonly Native.WinEventProc proc;
    readonly IntPtr[] hooks = new IntPtr[Ranges.Length];   // un crochet par plage ; une plage refusée est retentée (BAR-04)
    readonly Timer debounce = new Timer();
    readonly HashSet<IntPtr> flashing = new HashSet<IntPtr>();
    readonly Func<IntPtr, bool> isOwn;
    readonly List<IntPtr> sessionOrder = new List<IntPtr>();
    readonly Dictionary<IntPtr, TaskWindow> tracked = new Dictionary<IntPtr, TaskWindow>();

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
        Refresh();
    }

    void OnEvent(IntPtr hook, uint ev, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        if (idObject != 0 /*OBJID_WINDOW*/ || idChild != 0 || hwnd == IntPtr.Zero) return;
        if (ev == Native.EVENT_SYSTEM_FOREGROUND || ev == Native.EVENT_OBJECT_DESTROY)
        {
            flashing.Remove(hwnd);
            ClearDiscordAlerts(hwnd);
        }
        if (NeedsFullRefresh(ev, hwnd)) Schedule();
    }

    // Chaque infobulle, menu ou contrôle qui apparaît, et chaque titre qui change (lecteur de musique, navigateur),
    // relançait l'énumération de toutes les fenêtres du système. Seuls comptent maintenant les événements d'une
    // fenêtre déjà listée ou qui pourrait l'être ; un titre qui change sur une fenêtre listée est mis à jour sur
    // place (QA-69). Une actualisation complète de sûreté a lieu toutes les 5 s (Tick).
    bool NeedsFullRefresh(uint ev, IntPtr hwnd)
    {
        bool known = tracked.ContainsKey(hwnd);
        if (ev == Native.EVENT_SYSTEM_FOREGROUND) return true;
        if (ev == Native.EVENT_OBJECT_DESTROY || ev == Native.EVENT_OBJECT_HIDE || ev == Native.EVENT_OBJECT_CLOAKED) return known;
        if (ev == Native.EVENT_OBJECT_NAMECHANGE && known)
        {
            TaskWindow w = tracked[hwnd];
            string title = Native.WindowTitle(hwnd);
            if (title == w.Title) return false;
            w.Title = title;
            if (Items.Contains(w) && Changed != null) Changed();
            return false;
        }
        // Création, affichage, retour d'un autre bureau, titre d'une fenêtre encore inconnue : seulement si elle
        // a sa place dans la barre (quelques appels pour une fenêtre au lieu de toutes les fenêtres)
        return known || IsTaskWindow(hwnd);
    }

    int ticks;

    // Appelé chaque seconde par la barre : nouvel essai si un crochet ou l'énumération a échoué, et actualisation
    // complète toutes les 5 s, pour un changement qu'aucun événement n'aurait signalé (style d'une fenêtre)
    public void Tick()
    {
        if (NeedsRefresh || ++ticks % 5 == 0) Refresh();
    }

    void Schedule() { if (!debounce.Enabled) debounce.Start(); }

    // Une fenêtre demande l'attention (message HSHELL_FLASH reçu par la barre)
    public void Flash(IntPtr hwnd)
    {
        if (Sandbox.Active || hwnd == IntPtr.Zero || hwnd == Native.GetForegroundWindow()) return;
        flashing.Add(hwnd);
        TaskWindow window;
        if (!tracked.TryGetValue(hwnd, out window) && IsTaskWindow(hwnd))
        {
            window = new TaskWindow { Handle = hwnd, AppKey = AppKeyOf(hwnd) };
            tracked.Add(hwnd, window);
            if (!sessionOrder.Contains(hwnd)) sessionOrder.Add(hwnd);
        }
        // Un cycle produit plusieurs HSHELL_FLASH. La barre garde son propre état et arrête le cycle après le premier.
        if (window != null && window.RegisterDiscordAlert()) Native.StopTaskbarFlash(hwnd);
        Schedule();
    }

    public void ClearDiscordAlerts(IntPtr hwnd)
    {
        TaskWindow window;
        if (tracked.TryGetValue(hwnd, out window)) window.ClearDiscordAlerts();
    }

    public void Refresh()
    {
        if (Sandbox.Active) return;
        NeedsRefresh = false;
        for (int i = 0; i < Ranges.Length; i++)
        {
            if (hooks[i] == IntPtr.Zero)
                hooks[i] = Native.SetWinEventHook(Ranges[i][0], Ranges[i][1], IntPtr.Zero, proc, 0, 0, Native.WINEVENT_OUTOFCONTEXT);
            if (hooks[i] == IntPtr.Zero) NeedsRefresh = true;
        }
        List<IntPtr> found = new List<IntPtr>();
        // Énumération en échec : la dernière liste reste affichée plutôt qu'une barre vide (BAR-05)
        if (!Native.EnumWindows((h, l) => { if (!isOwn(h) && IsTaskWindow(h)) found.Add(h); return true; }, IntPtr.Zero))
        {
            NeedsRefresh = true;
            return;
        }
        found.Reverse();   // EnumWindows suit l'ordre Z : les plus anciennes sont plutôt en dessous
        HashSet<IntPtr> present = new HashSet<IntPtr>(found);
        // Ordre de session gardé pour les fenêtres momentanément absentes (autre bureau virtuel, masquées par DWM) :
        // elles retrouvent leur place au retour au lieu d'être rajoutées à la fin (CDX-08). Une fenêtre détruite sort.
        foreach (IntPtr gone in sessionOrder.Where(x => !present.Contains(x) && !Native.IsWindow(x)).ToList())
        {
            sessionOrder.Remove(gone);
            tracked.Remove(gone);
        }
        foreach (IntPtr h in found) if (!sessionOrder.Contains(h)) sessionOrder.Add(h);
        if (sessionOrder.Count > 500) sessionOrder.RemoveAll(x => !present.Contains(x));
        List<TaskWindow> next = new List<TaskWindow>();
        foreach (IntPtr h in sessionOrder)
        {
            if (!present.Contains(h)) continue;
            TaskWindow w;
            if (!tracked.TryGetValue(h, out w)) tracked[h] = w = new TaskWindow { Handle = h, AppKey = AppKeyOf(h) };
            next.Add(w);
        }
        flashing.RemoveWhere(h => !present.Contains(h));
        Active = Native.GetForegroundWindow();
        flashing.Remove(Active);
        ClearDiscordAlerts(Active);
        foreach (TaskWindow w in next)
        {
            w.Title = Native.WindowTitle(w.Handle);
            w.Flashing = flashing.Contains(w.Handle);
        }
        TaskIcons.Forget(present);
        Items = next;
        if (Changed != null) Changed();
    }

    // Ordre choisi par glisser-déposer (D-014) : les fenêtres citées passent dans cet ordre, les autres restent après
    public void Reorder(IList<IntPtr> order)
    {
        List<TaskWindow> next = new List<TaskWindow>();
        foreach (IntPtr h in order)
        {
            TaskWindow w = Items.FirstOrDefault(x => x.Handle == h);
            if (w != null && !next.Contains(w)) next.Add(w);
        }
        foreach (TaskWindow w in Items) if (!next.Contains(w)) next.Add(w);
        // Les places des fenêtres visibles dans l'ordre de session reçoivent le nouvel ordre ; les absentes gardent la leur
        HashSet<IntPtr> moved = new HashSet<IntPtr>(next.Select(w => w.Handle));
        foreach (TaskWindow w in next) if (!sessionOrder.Contains(w.Handle)) sessionOrder.Add(w.Handle);
        List<int> slots = new List<int>();
        for (int i = 0; i < sessionOrder.Count; i++) if (moved.Contains(sessionOrder[i])) slots.Add(i);
        for (int k = 0; k < slots.Count && k < next.Count; k++) sessionOrder[slots[k]] = next[k].Handle;
        Items = next;
        if (Changed != null) Changed();
    }

    // Clé d'application : AppUserModelID déclaré par la fenêtre, sinon chemin de l'exe (D-014)
    internal static string KeyFor(string aumid, string exe)
    {
        if (!string.IsNullOrEmpty(aumid)) return "aumid:" + aumid;
        if (!string.IsNullOrEmpty(exe)) return "exe:" + exe;
        return null;
    }

    static string AppKeyOf(IntPtr h)
    {
        string aumid = Native.AppUserModelId(h);
        return KeyForWindow(aumid, aumid == null ? Native.ProcessPath(h) : null);
    }

    // Programmes qui hébergent les fenêtres d'autres applications (cadre des applis du Store, boîtes de dialogue COM,
    // panneaux de configuration) : leur exe ne désigne pas l'application. Sans identifiant, la fenêtre n'a pas de clé :
    // elle ne peut être ni épinglée ni relancée, et n'est pas confondue avec les autres fenêtres du même hôte.
    static readonly string[] HostExes = { "ApplicationFrameHost.exe", "dllhost.exe", "rundll32.exe", "consent.exe", "mmc.exe" };

    internal static string KeyForWindow(string aumid, string exe)
    {
        if (string.IsNullOrEmpty(aumid) && !string.IsNullOrEmpty(exe))
        {
            string file = exe.Substring(exe.LastIndexOfAny(new[] { '\\', '/' }) + 1);
            foreach (string host in HostExes)
                if (string.Equals(file, host, StringComparison.OrdinalIgnoreCase)) return null;
        }
        return KeyFor(aumid, exe);
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
        // Demandes postées (ShowWindowAsync) : une application figée ne bloque jamais la barre ni le panneau (CDX-03)
        if (wasActive && !Native.IsIconic(h)) { Native.ShowWindowAsync(h, Native.SW_MINIMIZE); return; }
        // Fenêtre réduite : elle revient dans l'état où elle était, en plein écran si elle l'était. SwitchToThisWindow,
        // qui la remettait en taille normale, n'est plus utilisé pour elle (retour de l'essai, T-038).
        if (Native.IsIconic(h))
        {
            Native.ShowWindowAsync(h, Native.RestoresToMaximized(h) ? Native.SW_SHOWMAXIMIZED : Native.SW_RESTORE);
            Native.SetForegroundWindow(h);
            return;
        }
        // Le dernier clic vient de notre processus : Windows nous laisse donner le premier plan
        if (!Native.SetForegroundWindow(h) || Native.GetForegroundWindow() != h) Native.SwitchToThisWindow(h, true);
    }

    // « Fermer la fenêtre » : la même demande que le bouton X, l'application peut proposer d'enregistrer
    public static void Close(IntPtr h, Action<int> reportError)
    {
        Sandbox.Guard("fermeture d'une fenêtre");
        TryClose(h, Native.IsWindow,
            x => Native.PostMessageChecked(x, Native.WM_CLOSE, IntPtr.Zero, IntPtr.Zero) ? 0 : System.Runtime.InteropServices.Marshal.GetLastWin32Error(),
            reportError);
    }

    // Faux si la demande n'est pas partie. Une fenêtre lancée en administrateur refuse le message d'une application
    // ordinaire (erreur 5) : l'échec est signalé au lieu d'être silencieux (CDX-10). Une demande partie ne garantit pas
    // la fermeture : l'application peut proposer d'enregistrer, ou refuser.
    internal static bool TryClose(IntPtr h, Func<IntPtr, bool> isWindow, Func<IntPtr, int> postClose, Action<int> reportError)
    {
        if (!isWindow(h)) return false;
        int error = postClose(h);
        if (error == 0) return true;
        if (reportError != null) reportError(error);
        return false;
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

    // Icône pas encore prête (fenêtre qui vient de s'ouvrir) : nouvel essai toutes les 2 s, 3 fois au plus. Avant,
    // l'absence était retenue pour toujours et la fenêtre gardait l'icône générique.
    static readonly Dictionary<Tuple<IntPtr, int>, int> missCount = new Dictionary<Tuple<IntPtr, int>, int>();
    static readonly Dictionary<Tuple<IntPtr, int>, DateTime> missAt = new Dictionary<Tuple<IntPtr, int>, DateTime>();
    internal const int MaxMisses = 3;
    internal static TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

    internal static Bitmap CachedWindow(IntPtr window, int px, out bool pending)
    {
        var key = Tuple.Create(window, px);
        Bitmap image;
        cache.TryGetValue(key, out image);
        int misses;
        pending = image == null && (!missCount.TryGetValue(key, out misses) ||
            (misses < MaxMisses && DateTime.UtcNow - missAt[key] >= RetryDelay));
        return image;
    }

    internal static Bitmap CachedKey(string key, int px, out bool pending)
    {
        pending = false;
        if (key == null) return null;
        Bitmap image;
        pending = !byKey.TryGetValue(px + "|" + key, out image);
        return image;
    }

    public static Bitmap For(IntPtr h, int px)
    {
        Tuple<IntPtr, int> key = Tuple.Create(h, px);
        Bitmap b;
        if (cache.TryGetValue(key, out b)) return b;
        int misses;
        if (missCount.TryGetValue(key, out misses) && (misses >= MaxMisses || DateTime.UtcNow - missAt[key] < RetryDelay)) return null;
        b = Make(h, px);
        if (b != null) { cache[key] = b; missCount.Remove(key); missAt.Remove(key); }
        else { missCount[key] = misses + 1; missAt[key] = DateTime.UtcNow; }
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
            // L'icône appartient à la fenêtre : on en convertit une copie, que FromHIcon détruit ensuite (CDX-07)
            IntPtr borrowed = Native.WindowIcon(h, true);
            IntPtr copy = borrowed != IntPtr.Zero ? Native.CopyIcon(borrowed) : IntPtr.Zero;
            Bitmap own = Icons.FromHIcon(copy);
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
        foreach (Tuple<IntPtr, int> k in missCount.Keys.Where(k => !alive.Contains(k.Item1)).ToList()) { missCount.Remove(k); missAt.Remove(k); }
    }

    public static void Clear()
    {
        Forget(new HashSet<IntPtr>());
        foreach (Bitmap b in byKey.Values) if (b != null) b.Dispose();
        byKey.Clear();
    }

    // Icône d'une épingle dont l'application est fermée : celle de l'application du Store, ou celle de l'exe
    static readonly Dictionary<string, Bitmap> byKey = new Dictionary<string, Bitmap>(StringComparer.OrdinalIgnoreCase);

    // Images des épingles et applications qui n'ont plus aucun bouton : libérées à chaque reconstruction (CDX-12)
    public static void PruneKeys(HashSet<string> liveKeys)
    {
        foreach (string k in byKey.Keys.ToList())
        {
            int bar = k.IndexOf('|');
            if (bar >= 0 && liveKeys.Contains(k.Substring(bar + 1))) continue;
            if (byKey[k] != null) byKey[k].Dispose();
            byKey.Remove(k);
        }
    }

    public static Bitmap ForKey(string key, int px)
    {
        if (key == null) return null;
        string cacheKey = px + "|" + key;
        Bitmap b;
        if (byKey.TryGetValue(cacheKey, out b)) return b;
        try
        {
            if (key.StartsWith("aumid:")) b = Icons.ShellImage(@"shell:AppsFolder\" + key.Substring(6), px, false);
            else if (key.StartsWith("exe:")) b = Icons.ForPath(key.Substring(4), px);
        }
        catch { b = null; }
        byKey[cacheKey] = b;
        return b;
    }
}
