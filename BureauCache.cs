// BureauCache 3.0 - cache les icônes du bureau et les affiche dans un panneau-lanceur
// qui s'ouvre au bord de l'écran ou avec un raccourci clavier.
// Compilation : build.cmd
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("BureauCache")]
[assembly: AssemblyProduct("BureauCache")]
[assembly: AssemblyDescription("Ton bureau, rangé sur le côté de l'écran")]
[assembly: AssemblyVersion("3.0.0.0")]
[assembly: AssemblyFileVersion("3.0.0.0")]
[assembly: AssemblyInformationalVersion("3.0")]

// =====================================================================
//  Langue
// =====================================================================
static class L
{
    public static bool En;
    public static void Refresh()
    {
        En = Settings.Language == 2 || (Settings.Language == 0 && CultureInfo.CurrentUICulture.TwoLetterISOLanguageName != "fr");
    }
    public static string T(string fr, string en) { return En ? en : fr; }
    public static CultureInfo Culture { get { return En ? CultureInfo.GetCultureInfo("en-US") : CultureInfo.GetCultureInfo("fr-FR"); } }

    public static string Cat(string key)
    {
        switch (key)
        {
            case "Dossiers": return T("Dossiers", "Folders");
            case "Jeux": return T("Jeux", "Games");
            case "Applications": return T("Applications", "Apps");
            case "Fichiers": return T("Fichiers", "Files");
        }
        return key;
    }

    public static string Size(long b)
    {
        string[] u = En ? new[] { "B", "KB", "MB", "GB", "TB" } : new[] { "o", "Ko", "Mo", "Go", "To" };
        double v = b;
        int i = 0;
        while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
        return v.ToString(i == 0 ? "0" : v < 10 ? "0.#" : "0", Culture) + " " + u[i];
    }
}

// =====================================================================
//  Win32
// =====================================================================
static class Native
{
    [DllImport("user32.dll")] public static extern IntPtr FindWindow(string cls, string name);
    [DllImport("user32.dll")] public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string name);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int cmd);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr hIcon);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr hWnd, StringBuilder sb, int max);
    [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mods, uint vk);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")] public static extern short GetKeyState(int vk);
    [DllImport("user32.dll")] public static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO info);
    [DllImport("user32.dll")] public static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint affinity);
    [DllImport("user32.dll")] public static extern bool LockWorkStation();
    [DllImport("user32.dll")] public static extern bool ExitWindowsEx(uint flags, uint reason);
    [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] public static extern int SetWindowTheme(IntPtr hWnd, string app, string idList);
    [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
    [DllImport("shell32.dll")] public static extern int SHQueryUserNotificationState(out int state);
    [DllImport("shell32.dll")] public static extern int SHGetImageList(int list, ref Guid riid, out IntPtr ppv);
    [DllImport("comctl32.dll")] public static extern IntPtr ImageList_GetIcon(IntPtr himl, int i, int flags);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] public static extern bool ShellExecuteEx(ref SHELLEXECUTEINFO info);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] public static extern int SHEmptyRecycleBin(IntPtr hwnd, string root, uint flags);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] public static extern int SHQueryRecycleBin(string root, ref SHQUERYRBINFO info);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    public static extern void SHCreateItemFromParsingName(string path, IntPtr pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IShellItemImageFactory ppv);

    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct ICONINFO { public bool fIcon; public int x, y; public IntPtr hbmMask, hbmColor; }
    [StructLayout(LayoutKind.Sequential)] public struct SIZE { public int cx, cy; }
    [StructLayout(LayoutKind.Sequential)] public struct SHQUERYRBINFO { public int cbSize; public long i64Size; public long i64NumItems; }

    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IShellItemImageFactory { [PreserveSig] int GetImage(SIZE size, int flags, out IntPtr phbm); }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct SHFILEINFO
    {
        public IntPtr hIcon; public int iIcon; public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr SHGetFileInfo(string path, uint attr, ref SHFILEINFO sfi, uint size, uint flags);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct SHSTOCKICONINFO
    {
        public uint cbSize; public IntPtr hIcon; public int iSysIconIndex; public int iIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szPath;
    }
    [DllImport("shell32.dll")] public static extern int SHGetStockIconInfo(uint siid, uint flags, ref SHSTOCKICONINFO info);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct SHELLEXECUTEINFO
    {
        public int cbSize; public uint fMask; public IntPtr hwnd;
        public string lpVerb, lpFile, lpParameters, lpDirectory;
        public int nShow; public IntPtr hInstApp, lpIDList; public string lpClass;
        public IntPtr hkeyClass; public uint dwHotKey; public IntPtr hIcon, hProcess;
    }

    public const int SW_HIDE = 0, SW_SHOW = 5;

    public static IntPtr FindDesktopListView()
    {
        IntPtr progman = FindWindow("Progman", null);
        IntPtr defView = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
        if (defView == IntPtr.Zero)
        {
            IntPtr w = IntPtr.Zero;
            while ((w = FindWindowEx(IntPtr.Zero, w, "WorkerW", null)) != IntPtr.Zero)
            {
                defView = FindWindowEx(w, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (defView != IntPtr.Zero) break;
            }
        }
        if (defView == IntPtr.Zero) return IntPtr.Zero;
        return FindWindowEx(defView, IntPtr.Zero, "SysListView32", null);
    }

    public static void RestoreDesktopIcons()
    {
        IntPtr lv = FindDesktopListView();
        if (lv != IntPtr.Zero) ShowWindow(lv, SW_SHOW);
    }

    public static bool ForegroundIsFullscreen()
    {
        int st;
        if (SHQueryUserNotificationState(out st) == 0 && (st == 2 || st == 3 || st == 4)) return true;
        IntPtr h = GetForegroundWindow();
        if (h == IntPtr.Zero) return false;
        StringBuilder sb = new StringBuilder(64);
        GetClassName(h, sb, 64);
        string cls = sb.ToString();
        if (cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd") return false;
        RECT r;
        if (!GetWindowRect(h, out r)) return false;
        Rectangle b = Screen.FromHandle(h).Bounds;
        return r.Left <= b.Left && r.Top <= b.Top && r.Right >= b.Right && r.Bottom >= b.Bottom;
    }

    public static void SetDarkTitleBar(IntPtr hwnd, bool dark)
    {
        int v = dark ? 1 : 0;
        try { DwmSetWindowAttribute(hwnd, 20, ref v, 4); } catch { }
    }

    public static void SetRoundCorners(IntPtr hwnd)
    {
        int v = 2;
        try { DwmSetWindowAttribute(hwnd, 33, ref v, 4); } catch { }
    }

    public static void SetDarkScrollbars(IntPtr hwnd, bool dark)
    {
        try { SetWindowTheme(hwnd, dark ? "DarkMode_Explorer" : "Explorer", null); } catch { }
    }

    public static void RecycleBinInfo(out long count, out long size)
    {
        count = size = 0;
        try
        {
            SHQUERYRBINFO i = new SHQUERYRBINFO();
            i.cbSize = Marshal.SizeOf(i);
            if (SHQueryRecycleBin(null, ref i) == 0) { count = i.i64NumItems; size = i.i64Size; }
        }
        catch { }
    }
}

// =====================================================================
//  Icônes, miniatures et cache
// =====================================================================
static class Icons
{
    static Guid IID_IImageList = new Guid("46EB5926-582E-4017-9FDF-E8998DAA0950");

    public static Bitmap ForPath(string path, int px)
    {
        Native.SHFILEINFO fi = new Native.SHFILEINFO();
        IntPtr r = Native.SHGetFileInfo(path, 0, ref fi, (uint)Marshal.SizeOf(fi), 0x4000);
        if (r == IntPtr.Zero) return null;
        return FromSysIndex(fi.iIcon, px);
    }

    public static Bitmap Stock(uint id, int px)
    {
        Native.SHSTOCKICONINFO si = new Native.SHSTOCKICONINFO();
        si.cbSize = (uint)Marshal.SizeOf(si);
        if (Native.SHGetStockIconInfo(id, 0x4000, ref si) != 0) return null;
        return FromSysIndex(si.iSysIconIndex, px);
    }

    static Bitmap FromSysIndex(int idx, int px)
    {
        int list = px <= 32 ? 0 : px <= 48 ? 2 : 4;
        Bitmap b = FromList(list, idx);
        if (list == 4 && b != null && MostlyEmpty(b)) { b.Dispose(); b = FromList(2, idx); }
        return b;
    }

    static Bitmap FromList(int list, int idx)
    {
        IntPtr himl;
        Guid g = IID_IImageList;
        if (Native.SHGetImageList(list, ref g, out himl) != 0 || himl == IntPtr.Zero) return null;
        return FromHIcon(Native.ImageList_GetIcon(himl, idx, 1));
    }

    // Image (miniature ou icône) d'un élément du shell, avec transparence
    public static Bitmap ShellImage(string parsingName, int px, bool thumbnailOnly)
    {
        try
        {
            Native.IShellItemImageFactory f;
            Native.SHCreateItemFromParsingName(parsingName, IntPtr.Zero, typeof(Native.IShellItemImageFactory).GUID, out f);
            IntPtr hb;
            Native.SIZE sz = new Native.SIZE { cx = px, cy = px };
            int hr = f.GetImage(sz, thumbnailOnly ? 0x8 : 0x0, out hb);
            Marshal.ReleaseComObject(f);
            if (hr != 0 || hb == IntPtr.Zero) return null;
            return FromHBitmap(hb, PixelFormat.Format32bppPArgb);
        }
        catch { return null; }
    }

    static Bitmap FromHBitmap(IntPtr hb, PixelFormat alphaFormat)
    {
        try
        {
            using (Bitmap src = Image.FromHbitmap(hb))
            {
                if (Image.GetPixelFormatSize(src.PixelFormat) == 32)
                {
                    Rectangle rc = new Rectangle(0, 0, src.Width, src.Height);
                    BitmapData d = src.LockBits(rc, ImageLockMode.ReadOnly, src.PixelFormat);
                    try
                    {
                        using (Bitmap wrap = new Bitmap(d.Width, d.Height, d.Stride, alphaFormat, d.Scan0))
                            if (HasAlpha(wrap)) return new Bitmap(wrap);
                    }
                    finally { src.UnlockBits(d); }
                }
                return new Bitmap(src);
            }
        }
        finally { Native.DeleteObject(hb); }
    }

    public static Bitmap FromHIcon(IntPtr h)
    {
        if (h == IntPtr.Zero) return null;
        try
        {
            Native.ICONINFO ii;
            if (Native.GetIconInfo(h, out ii))
            {
                if (ii.hbmMask != IntPtr.Zero) Native.DeleteObject(ii.hbmMask);
                if (ii.hbmColor != IntPtr.Zero)
                {
                    Bitmap b = FromHBitmap(ii.hbmColor, PixelFormat.Format32bppArgb);
                    if (b != null && b.PixelFormat == PixelFormat.Format32bppArgb) return b;
                    if (b != null) b.Dispose();
                }
            }
            using (Icon ic = Icon.FromHandle(h)) return ic.ToBitmap();
        }
        catch { return null; }
        finally { Native.DestroyIcon(h); }
    }

    static bool HasAlpha(Bitmap b)
    {
        for (int y = 0; y < b.Height; y += 2)
            for (int x = 0; x < b.Width; x += 2)
                if (b.GetPixel(x, y).A > 0) return true;
        return false;
    }

    static bool MostlyEmpty(Bitmap b)
    {
        int maxX = 0, maxY = 0;
        for (int y = 0; y < b.Height; y += 4)
            for (int x = 0; x < b.Width; x += 4)
                if (b.GetPixel(x, y).A > 16) { if (x > maxX) maxX = x; if (y > maxY) maxY = y; }
        return maxX < b.Width / 2 && maxY < b.Height / 2;
    }

    // Petite tuile colorée avec un glyphe (paramètres, commandes, calcul, web)
    public static Bitmap Tile(string glyph, string font, Color c, int px)
    {
        Bitmap b = new Bitmap(px, px);
        using (Graphics g = Graphics.FromImage(b))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            Ui.Fill(g, c, new RectangleF(px * 0.06f, px * 0.06f, px * 0.88f, px * 0.88f), px * 0.22f);
            bool glyphFont = font == Ui.GlyphFont;
            using (Font f = new Font(font, px * (glyphFont ? 0.40f : 0.5f), glyphFont ? FontStyle.Regular : FontStyle.Bold, GraphicsUnit.Pixel))
            using (SolidBrush br = new SolidBrush(Color.White))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString(glyph, f, br, new RectangleF(0, glyphFont ? 0 : -px * 0.04f, px, px), sf);
        }
        return b;
    }
}

static class IconCache
{
    static readonly Dictionary<string, Bitmap> map = new Dictionary<string, Bitmap>(StringComparer.OrdinalIgnoreCase);
    static readonly object lk = new object();

    public static Bitmap Get(string key, Func<Bitmap> make)
    {
        lock (lk) { Bitmap b; if (map.TryGetValue(key, out b)) return b; }
        Bitmap nb = null;
        try { nb = make(); } catch { }
        lock (lk)
        {
            Bitmap b;
            if (map.TryGetValue(key, out b)) { if (nb != null) nb.Dispose(); return b; }
            map[key] = nb;
            return nb;
        }
    }

    public static Bitmap ForFile(string path, int px)
    {
        long stamp = 0;
        try { stamp = File.GetLastWriteTimeUtc(path).Ticks; } catch { }
        return Get(path + "|" + px + "|" + stamp, () => Icons.ForPath(path, px));
    }

    public static Bitmap Tile(string glyph, string font, Color c, int px)
    {
        return Get("tile|" + glyph + "|" + font + "|" + c.ToArgb() + "|" + px, () => Icons.Tile(glyph, font, c, px));
    }
}

// =====================================================================
//  Raccourcis, liens, verbes du shell
// =====================================================================
static class ShellUtil
{
    [ThreadStatic] static object wsh;
    static dynamic Wsh
    {
        get
        {
            if (wsh == null) wsh = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
            return wsh;
        }
    }

    public static string ResolveLnk(string p)
    {
        try { dynamic s = Wsh.CreateShortcut(p); return (string)s.TargetPath; }
        catch { return null; }
    }

    public static void CreateShortcut(string lnk, string target, string desc)
    {
        dynamic s = Wsh.CreateShortcut(lnk);
        s.TargetPath = target;
        string wd = Path.GetDirectoryName(target);
        if (!string.IsNullOrEmpty(wd)) s.WorkingDirectory = wd;
        if (desc != null) s.Description = desc;
        s.Save();
    }

    public static string ReadUrl(string p)
    {
        try
        {
            foreach (string l in File.ReadAllLines(p))
                if (l.StartsWith("URL=", StringComparison.OrdinalIgnoreCase)) return l.Substring(4);
        }
        catch { }
        return null;
    }

    public static List<KeyValuePair<string, object>> Verbs(string path)
    {
        List<KeyValuePair<string, object>> res = new List<KeyValuePair<string, object>>();
        try
        {
            dynamic app = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application"));
            dynamic folder = app.NameSpace(Path.GetDirectoryName(path));
            if (folder == null) return res;
            dynamic item = folder.ParseName(Path.GetFileName(path));
            if (item == null) return res;
            dynamic verbs = item.Verbs();
            int n = verbs.Count;
            for (int i = 0; i < n; i++)
            {
                dynamic v = verbs.Item(i);
                string name = v.Name;
                if (!string.IsNullOrEmpty(name) && name.Replace("&", "").Trim().Length > 0)
                    res.Add(new KeyValuePair<string, object>(name, (object)v));
            }
        }
        catch { }
        return res;
    }

    public static void InvokeVerb(object verb)
    {
        try { ((dynamic)verb).DoIt(); } catch { }
    }

    public static void Properties(string path)
    {
        Native.SHELLEXECUTEINFO i = new Native.SHELLEXECUTEINFO();
        i.cbSize = Marshal.SizeOf(i);
        i.lpVerb = "properties";
        i.lpFile = path;
        i.nShow = 1;
        i.fMask = 0x0000000C;
        Native.ShellExecuteEx(ref i);
    }

    public static void Open(string target, string args)
    {
        ProcessStartInfo psi = new ProcessStartInfo(target);
        if (args != null) psi.Arguments = args;
        psi.UseShellExecute = true;
        Process.Start(psi);
    }
}

// =====================================================================
//  Réglages (%APPDATA%\BureauCache\settings.ini)
// =====================================================================
class CustomCat
{
    public string Name, Glyph;
    public Color Color;
}

static class Settings
{
    public static bool HideIcons = true, RightSide = false, AutoClose = true, ShowSystem = true, ShowRecents = true,
        GroupCats = true, GridView = false, Glass = true, LiveBlur = true, Animations = true, NoFullscreen = true, HotkeyOn = true,
        SearchApps = true, SearchRecent = true, SearchSettings = true, SearchCommands = true,
        NewBadge = true, ArchiveOn = false, HoverPreview = true, Widgets = true, AutoUpdate = true;
    public static int ThemeMode = 0, Language = 0, Delay = 120, PanelWidth = 340, EdgeZone = 0, ScreenIdx = -1,
        HotMods = 3, HotKey = (int)Keys.B, WebEngine = 0, NewDays = 3, ArchiveDays = 60;
    public static long LastUpdateCheck, LastArchive;
    public static string ArchiveName = "Archive du bureau", UpdateRepo = "";
    public static Color Accent = Color.FromArgb(59, 130, 246);
    public static List<string> Favorites = new List<string>(), Order = new List<string>();
    public static HashSet<string> Collapsed = new HashSet<string>();
    public static Dictionary<string, string> CatOverride = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public static Dictionary<string, long[]> Usage = new Dictionary<string, long[]>(StringComparer.OrdinalIgnoreCase);
    public static List<CustomCat> CustomCats = new List<CustomCat>();
    public static bool FirstRun;
    public static event Action Changed;

    static bool sysDark = true;
    public static bool Dark { get { return ThemeMode == 1 || (ThemeMode == 0 && sysDark); } }

    public static string Dir { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BureauCache"); } }
    public static string FilePath { get { return Path.Combine(Dir, "settings.ini"); } }

    public static bool RefreshSystemTheme()
    {
        bool old = sysDark;
        try
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
            {
                object v = k == null ? null : k.GetValue("AppsUseLightTheme");
                sysDark = !(v is int && (int)v == 1);
            }
        }
        catch { }
        return old != sysDark;
    }

    static List<string> SplitList(string v) { return v.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries).ToList(); }
    static Color ParseColor(string v, Color def)
    {
        int n;
        if (int.TryParse(v, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out n))
            return Color.FromArgb(255, (n >> 16) & 255, (n >> 8) & 255, n & 255);
        return def;
    }
    static string Hex(Color c) { return (c.ToArgb() & 0xFFFFFF).ToString("X6"); }

    public static void Load() { Load(FilePath); }

    public static bool Load(string path)
    {
        RefreshSystemTheme();
        if (!File.Exists(path)) { FirstRun = true; return false; }
        bool hasThemeMode = false;
        string oldDark = null;
        try
        {
            foreach (string line in File.ReadAllLines(path))
            {
                int i = line.IndexOf('=');
                if (i <= 0) continue;
                string k = line.Substring(0, i).Trim(), v = line.Substring(i + 1).Trim();
                int n;
                long ln;
                bool b = v == "1";
                switch (k)
                {
                    case "HideIcons": HideIcons = b; break;
                    case "RightSide": RightSide = b; break;
                    case "AutoClose": AutoClose = b; break;
                    case "ShowSystem": ShowSystem = b; break;
                    case "ShowRecents": ShowRecents = b; break;
                    case "GroupCats": GroupCats = b; break;
                    case "GridView": GridView = b; break;
                    case "Glass": Glass = b; break;
                    case "LiveBlur": LiveBlur = b; break;
                    case "Animations": Animations = b; break;
                    case "NoFullscreen": NoFullscreen = b; break;
                    case "HotkeyOn": HotkeyOn = b; break;
                    case "SearchApps": SearchApps = b; break;
                    case "SearchRecent": SearchRecent = b; break;
                    case "SearchSettings": SearchSettings = b; break;
                    case "SearchCommands": SearchCommands = b; break;
                    case "NewBadge": NewBadge = b; break;
                    case "ArchiveOn": ArchiveOn = b; break;
                    case "HoverPreview": HoverPreview = b; break;
                    case "Widgets": Widgets = b; break;
                    case "AutoUpdate": AutoUpdate = b; break;
                    case "Dark": oldDark = v; break;
                    case "ThemeMode": if (int.TryParse(v, out n)) { ThemeMode = n; hasThemeMode = true; } break;
                    case "Language": if (int.TryParse(v, out n)) Language = n; break;
                    case "Delay": if (int.TryParse(v, out n)) Delay = n; break;
                    case "PanelWidth": if (int.TryParse(v, out n)) PanelWidth = n; break;
                    case "EdgeZone": if (int.TryParse(v, out n)) EdgeZone = n; break;
                    case "ScreenIdx": if (int.TryParse(v, out n)) ScreenIdx = n; break;
                    case "HotMods": if (int.TryParse(v, out n)) HotMods = n; break;
                    case "HotKey": if (int.TryParse(v, out n)) HotKey = n; break;
                    case "WebEngine": if (int.TryParse(v, out n)) WebEngine = n; break;
                    case "NewDays": if (int.TryParse(v, out n)) NewDays = n; break;
                    case "ArchiveDays": if (int.TryParse(v, out n)) ArchiveDays = n; break;
                    case "LastUpdateCheck": if (long.TryParse(v, out ln)) LastUpdateCheck = ln; break;
                    case "LastArchive": if (long.TryParse(v, out ln)) LastArchive = ln; break;
                    case "ArchiveName": if (v.Length > 0 && v.IndexOfAny(Path.GetInvalidFileNameChars()) < 0) ArchiveName = v; break;
                    case "UpdateRepo": UpdateRepo = v; break;
                    case "Accent": Accent = ParseColor(v, Accent); break;
                    case "Favorites": Favorites = SplitList(v); break;
                    case "Order": Order = SplitList(v); break;
                    case "Collapsed": Collapsed = new HashSet<string>(SplitList(v)); break;
                    case "CatOverride":
                        CatOverride.Clear();
                        foreach (string e in SplitList(v))
                        {
                            int j = e.IndexOf('>');
                            if (j > 0) CatOverride[e.Substring(0, j)] = e.Substring(j + 1);
                        }
                        break;
                    case "Usage":
                        Usage.Clear();
                        foreach (string e in SplitList(v))
                        {
                            int j = e.LastIndexOf('>');
                            if (j <= 0) continue;
                            string[] p = e.Substring(j + 1).Split(',');
                            long c, t;
                            if (p.Length == 2 && long.TryParse(p[0], out c) && long.TryParse(p[1], out t))
                                Usage[e.Substring(0, j)] = new long[] { c, t };
                        }
                        break;
                    case "CustomCats":
                        CustomCats.Clear();
                        foreach (string e in SplitList(v))
                        {
                            string[] p = e.Split('>');
                            int g;
                            if (p.Length == 3 && p[0].Length > 0 && int.TryParse(p[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out g))
                                CustomCats.Add(new CustomCat { Name = p[0], Glyph = ((char)g).ToString(), Color = ParseColor(p[2], Color.Gray) });
                        }
                        break;
                }
            }
        }
        catch { return false; }
        if (!hasThemeMode && oldDark != null) ThemeMode = oldDark == "1" ? 1 : 2;
        Delay = Math.Max(0, Math.Min(1000, Delay));
        PanelWidth = Math.Max(260, Math.Min(560, PanelWidth));
        EdgeZone = Math.Max(0, Math.Min(3, EdgeZone));
        ThemeMode = Math.Max(0, Math.Min(2, ThemeMode));
        Language = Math.Max(0, Math.Min(2, Language));
        WebEngine = Math.Max(0, Math.Min(3, WebEngine));
        if (NewDays < 1) NewDays = 3;
        if (ArchiveDays < 7) ArchiveDays = 60;
        return true;
    }

    static string B(bool b) { return b ? "1" : "0"; }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllLines(FilePath, new string[] {
                "HideIcons=" + B(HideIcons), "RightSide=" + B(RightSide), "AutoClose=" + B(AutoClose),
                "ShowSystem=" + B(ShowSystem), "ShowRecents=" + B(ShowRecents), "GroupCats=" + B(GroupCats),
                "GridView=" + B(GridView), "Glass=" + B(Glass), "LiveBlur=" + B(LiveBlur), "Animations=" + B(Animations),
                "NoFullscreen=" + B(NoFullscreen), "HotkeyOn=" + B(HotkeyOn),
                "SearchApps=" + B(SearchApps), "SearchRecent=" + B(SearchRecent), "SearchSettings=" + B(SearchSettings),
                "SearchCommands=" + B(SearchCommands), "NewBadge=" + B(NewBadge), "ArchiveOn=" + B(ArchiveOn),
                "HoverPreview=" + B(HoverPreview), "Widgets=" + B(Widgets), "AutoUpdate=" + B(AutoUpdate),
                "ThemeMode=" + ThemeMode, "Language=" + Language, "Delay=" + Delay, "PanelWidth=" + PanelWidth,
                "EdgeZone=" + EdgeZone, "ScreenIdx=" + ScreenIdx, "HotMods=" + HotMods, "HotKey=" + HotKey,
                "WebEngine=" + WebEngine, "NewDays=" + NewDays, "ArchiveDays=" + ArchiveDays,
                "LastUpdateCheck=" + LastUpdateCheck, "LastArchive=" + LastArchive,
                "ArchiveName=" + ArchiveName, "UpdateRepo=" + UpdateRepo,
                "Accent=" + Hex(Accent),
                "Favorites=" + string.Join("|", Favorites),
                "Order=" + string.Join("|", Order),
                "Collapsed=" + string.Join("|", Collapsed),
                "CatOverride=" + string.Join("|", CatOverride.Select(kv => kv.Key + ">" + kv.Value)),
                "Usage=" + string.Join("|", Usage.Select(kv => kv.Key + ">" + kv.Value[0] + "," + kv.Value[1])),
                "CustomCats=" + string.Join("|", CustomCats.Select(c => c.Name + ">" + ((int)c.Glyph[0]).ToString("X4") + ">" + Hex(c.Color)))
            });
        }
        catch { }
    }

    public static void Notify() { Save(); Fire(); }
    public static void SaveQuiet() { Save(); }
    public static void Fire() { if (Changed != null) Changed(); }

    public static int FavIndex(string key)
    {
        return Favorites.FindIndex(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
    }

    public static CustomCat FindCat(string name)
    {
        return CustomCats.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    public static void RecordUse(string key)
    {
        long[] u;
        if (!Usage.TryGetValue(key, out u)) u = new long[] { 0, 0 };
        Usage[key] = new long[] { u[0] + 1, DateTime.Now.Ticks };
        SaveQuiet();
    }

    public static void RenameKey(string o, string n)
    {
        for (int i = 0; i < Favorites.Count; i++) if (string.Equals(Favorites[i], o, StringComparison.OrdinalIgnoreCase)) Favorites[i] = n;
        for (int i = 0; i < Order.Count; i++) if (string.Equals(Order[i], o, StringComparison.OrdinalIgnoreCase)) Order[i] = n;
        string c;
        if (CatOverride.TryGetValue(o, out c)) { CatOverride.Remove(o); CatOverride[n] = c; }
        long[] u;
        if (Usage.TryGetValue(o, out u)) { Usage.Remove(o); Usage[n] = u; }
        SaveQuiet();
    }

    public static void RemoveKey(string k)
    {
        Favorites.RemoveAll(x => string.Equals(x, k, StringComparison.OrdinalIgnoreCase));
        Order.RemoveAll(x => string.Equals(x, k, StringComparison.OrdinalIgnoreCase));
        CatOverride.Remove(k);
        Usage.Remove(k);
        SaveQuiet();
    }

    public static void RenameCategory(CustomCat c, string newName)
    {
        foreach (string k in CatOverride.Keys.ToList())
            if (string.Equals(CatOverride[k], c.Name, StringComparison.OrdinalIgnoreCase)) CatOverride[k] = newName;
        if (Collapsed.Remove("cat:" + c.Name)) Collapsed.Add("cat:" + newName);
        c.Name = newName;
    }

    public static void DeleteCategory(CustomCat c)
    {
        foreach (string k in CatOverride.Keys.ToList())
            if (string.Equals(CatOverride[k], c.Name, StringComparison.OrdinalIgnoreCase)) CatOverride.Remove(k);
        Collapsed.Remove("cat:" + c.Name);
        CustomCats.Remove(c);
    }
}

static class Startup
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string Name = "BureauCache";

    public static bool Enabled
    {
        get
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey))
                    return k != null && k.GetValue(Name) != null;
            }
            catch { return false; }
        }
    }

    public static void Set(bool on, string exe = null)
    {
        using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RunKey))
        {
            if (on) k.SetValue(Name, "\"" + (exe ?? Application.ExecutablePath) + "\"");
            else k.DeleteValue(Name, false);
        }
    }
}

// =====================================================================
//  Installation (par utilisateur, sans droits administrateur)
// =====================================================================
static class Installer
{
    public const string Version = "3.0";
    const string UninstKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\BureauCache";

    public static string Dir { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "BureauCache"); } }
    public static string Exe { get { return Path.Combine(Dir, "BureauCache.exe"); } }
    static string StartMenuLnk { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "BureauCache.lnk"); } }

    public static bool IsInstalled
    {
        get
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(UninstKey))
                    return k != null && File.Exists(Exe);
            }
            catch { return false; }
        }
    }

    public static bool RunningFromInstall
    {
        get
        {
            try { return string.Equals(Path.GetFullPath(Application.ExecutablePath), Path.GetFullPath(Exe), StringComparison.OrdinalIgnoreCase); }
            catch { return false; }
        }
    }

    public static void Install()
    {
        Directory.CreateDirectory(Dir);
        if (!RunningFromInstall) File.Copy(Application.ExecutablePath, Exe, true);
        ShellUtil.CreateShortcut(StartMenuLnk, Exe, L.T("Ton bureau, rangé sur le côté de l'écran", "Your desktop, tucked at the edge of the screen"));
        RegisterVersion();
        if (Startup.Enabled) Startup.Set(true, Exe);
    }

    public static void RegisterVersion()
    {
        using (RegistryKey k = Registry.CurrentUser.CreateSubKey(UninstKey))
        {
            k.SetValue("DisplayName", "BureauCache");
            k.SetValue("DisplayVersion", Version);
            k.SetValue("Publisher", "BureauCache");
            k.SetValue("DisplayIcon", Exe + ",0");
            k.SetValue("InstallLocation", Dir);
            k.SetValue("UninstallString", "\"" + Exe + "\" --uninstall");
            k.SetValue("NoModify", 1, RegistryValueKind.DWord);
            k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            try { k.SetValue("EstimatedSize", (int)(new FileInfo(Exe).Length / 1024) + 1, RegistryValueKind.DWord); } catch { }
        }
    }

    public static void Uninstall()
    {
        Native.RestoreDesktopIcons();
        try { Startup.Set(false); } catch { }
        try { File.Delete(StartMenuLnk); } catch { }
        try { Registry.CurrentUser.DeleteSubKeyTree(UninstKey, false); } catch { }
        try { if (Directory.Exists(Settings.Dir)) Directory.Delete(Settings.Dir, true); } catch { }
        if (Directory.Exists(Dir))
        {
            ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", "/c ping 127.0.0.1 -n 6 > nul & rmdir /s /q \"" + Dir + "\"");
            psi.WindowStyle = ProcessWindowStyle.Hidden;
            psi.CreateNoWindow = true;
            psi.UseShellExecute = false;
            psi.WorkingDirectory = Path.GetTempPath();
            try { Process.Start(psi); } catch { }
        }
    }
}

// =====================================================================
//  Mises à jour via GitHub Releases
// =====================================================================
static class Updater
{
    public const string DefaultRepo = "Kuroi1998/BureauCache";
    public static string Latest, Url, ShaUrl, Notes, PageUrl, Error;

    public static string RepoName
    {
        get
        {
            string r = (Settings.UpdateRepo ?? "").Trim().TrimEnd('/');
            if (r.Length == 0) r = DefaultRepo;
            if (r.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase)) r = r.Substring(19);
            return r;
        }
    }

    public static bool RepoValid { get { return Regex.IsMatch(RepoName, @"^[\w.-]+/[\w.-]+$"); } }

    static WebClient Client()
    {
        ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; // TLS 1.2
        WebClient wc = new WebClient();
        wc.Headers[HttpRequestHeader.UserAgent] = "BureauCache-Updater";
        wc.Encoding = Encoding.UTF8;
        return wc;
    }

    // Vrai si une version plus récente avec un BureauCache.exe est disponible
    public static bool Check()
    {
        Latest = Url = ShaUrl = Notes = PageUrl = Error = null;
        if (!RepoValid) { Error = L.T("Dépôt invalide (format : utilisateur/projet).", "Invalid repository (format: owner/name)."); return false; }
        try
        {
            string json;
            using (WebClient wc = Client()) json = wc.DownloadString("https://api.github.com/repos/" + RepoName + "/releases/latest");
            Match t = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"([^\"]+)\"");
            Match a = Regex.Match(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]+/BureauCache\\.exe)\"");
            Match h = Regex.Match(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]+/BureauCache\\.exe\\.sha256)\"");
            Match b = Regex.Match(json, "\"body\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
            Match p = Regex.Match(json, "\"html_url\"\\s*:\\s*\"([^\"]+/releases/tag/[^\"]+)\"");
            if (!t.Success) { Error = L.T("Aucune version publiée.", "No release published."); return false; }
            Latest = t.Groups[1].Value.TrimStart('v', 'V');
            Url = a.Success ? a.Groups[1].Value : null;
            ShaUrl = h.Success ? h.Groups[1].Value : null;
            Notes = b.Success ? Unescape(b.Groups[1].Value) : null;
            PageUrl = p.Success ? p.Groups[1].Value : "https://github.com/" + RepoName + "/releases";
            Settings.LastUpdateCheck = DateTime.Now.Ticks;
            Settings.SaveQuiet();
            return IsNewer(Latest, Installer.Version) && Url != null && ShaUrl != null;
        }
        catch (Exception ex) { Error = ex.Message; return false; }
    }

    static string Unescape(string s)
    {
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] != '\\' || i + 1 >= s.Length) { sb.Append(s[i]); continue; }
            char c = s[++i];
            switch (c)
            {
                case 'n': sb.Append('\n'); break;
                case 'r': break;
                case 't': sb.Append('\t'); break;
                case 'u':
                    if (i + 4 < s.Length) { sb.Append((char)Convert.ToInt32(s.Substring(i + 1, 4), 16)); i += 4; }
                    break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString().Trim();
    }

    // Notes de version raccourcies pour une boîte de dialogue
    public static string ShortNotes
    {
        get
        {
            if (string.IsNullOrEmpty(Notes)) return "";
            string n = Regex.Replace(Notes, @"[#*`]", "").Trim();
            return n.Length > 700 ? n.Substring(0, 700) + "…" : n;
        }
    }

    static bool IsNewer(string a, string b)
    {
        Version va, vb;
        if (a.IndexOf('.') < 0) a += ".0";
        if (b.IndexOf('.') < 0) b += ".0";
        return Version.TryParse(a, out va) && Version.TryParse(b, out vb) && va > vb;
    }

    // Télécharge la nouvelle version et la met à la place de l'exe actuel (appliquée au redémarrage)
    public static void Download() { Download(Application.ExecutablePath); App.RelaunchPath = Application.ExecutablePath; }

    public static string Sha256(string file)
    {
        using (SHA256 sha = SHA256.Create())
        using (FileStream fs = File.OpenRead(file))
            return BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "").ToLowerInvariant();
    }

    // Télécharge, vérifie l'empreinte SHA-256, puis remplace l'exe (l'ancien est gardé en .old)
    public static void Download(string exe)
    {
        string tmp = Path.Combine(Path.GetTempPath(), "BureauCache-update.exe");
        string expected;
        using (WebClient wc = Client())
        {
            expected = wc.DownloadString(ShaUrl).Trim().Split(' ', '\t', '\r', '\n')[0].ToLowerInvariant();
            wc.DownloadFile(Url, tmp);
        }
        byte[] head = new byte[2];
        using (FileStream fs = File.OpenRead(tmp)) fs.Read(head, 0, 2);
        if (head[0] != 'M' || head[1] != 'Z' || new FileInfo(tmp).Length < 50000)
            throw new Exception(L.T("Le fichier téléchargé n'est pas un programme valide.", "The downloaded file is not a valid program."));
        if (!Regex.IsMatch(expected, "^[0-9a-f]{64}$") || Sha256(tmp) != expected)
        {
            File.Delete(tmp);
            throw new Exception(L.T("L'empreinte du fichier téléchargé ne correspond pas : mise à jour annulée.", "The downloaded file's checksum doesn't match: update cancelled."));
        }
        string old = exe + ".old";
        if (File.Exists(old)) File.Delete(old);
        File.Move(exe, old);
        File.Copy(tmp, exe, true);
        File.Delete(tmp);
    }

    public static void CleanupOld()
    {
        try { string old = Application.ExecutablePath + ".old"; if (File.Exists(old)) File.Delete(old); } catch { }
    }
}

// =====================================================================
//  Rangement automatique du bureau
// =====================================================================
static class Archiver
{
    public static string Desk { get { return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory); } }
    public static string Folder { get { return Path.Combine(Desk, Settings.ArchiveName); } }

    static DateTime Max(DateTime a, DateTime b) { return a > b ? a : b; }

    public static List<string> Candidates()
    {
        List<string> res = new List<string>();
        DateTime limit = DateTime.Now.AddDays(-Settings.ArchiveDays);
        string[] entries;
        try { entries = Directory.GetFileSystemEntries(Desk); } catch { return res; }
        foreach (string p in entries)
        {
            try
            {
                string name = Path.GetFileName(p);
                if (string.Equals(p, Folder, StringComparison.OrdinalIgnoreCase)) continue;
                FileAttributes a = File.GetAttributes(p);
                if ((a & (FileAttributes.Hidden | FileAttributes.System)) != 0) continue;
                string ext = Path.GetExtension(p).ToLowerInvariant();
                if (ext == ".lnk" || ext == ".url" || name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                if (Settings.FavIndex(p) >= 0) continue;
                DateTime last = Max(File.GetLastWriteTime(p), File.GetCreationTime(p));
                long[] u;
                if (Settings.Usage.TryGetValue(p, out u)) last = Max(last, new DateTime(u[1]));
                if ((a & FileAttributes.Directory) != 0)
                    try { foreach (string c in Directory.EnumerateFileSystemEntries(p).Take(300)) last = Max(last, File.GetLastWriteTime(c)); } catch { }
                if (last < limit) res.Add(p);
            }
            catch { }
        }
        return res;
    }

    public static int Run(List<string> items)
    {
        if (items.Count == 0) return 0;
        Directory.CreateDirectory(Folder);
        int n = 0;
        foreach (string p in items)
        {
            try
            {
                string dest = Unique(Path.Combine(Folder, Path.GetFileName(p)));
                if (Directory.Exists(p)) Directory.Move(p, dest); else File.Move(p, dest);
                Settings.RenameKey(p, dest);
                n++;
            }
            catch { }
        }
        Settings.LastArchive = DateTime.Now.Ticks;
        Settings.SaveQuiet();
        return n;
    }

    static string Unique(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path)) return path;
        string dir = Path.GetDirectoryName(path), name = Path.GetFileNameWithoutExtension(path), ext = Path.GetExtension(path);
        for (int i = 2; ; i++)
        {
            string p = Path.Combine(dir, name + " (" + i + ")" + ext);
            if (!File.Exists(p) && !Directory.Exists(p)) return p;
        }
    }
}

// =====================================================================
//  Recherche floue et calculatrice
// =====================================================================
static class Search
{
    public static string Norm(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        string d = s.Normalize(NormalizationForm.FormD);
        StringBuilder sb = new StringBuilder(d.Length);
        bool space = false;
        foreach (char c in d)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c)) { sb.Append(char.ToLowerInvariant(c)); space = false; }
            else if (c == '\'' || c == '’') continue;
            else if (!space && sb.Length > 0) { sb.Append(' '); space = true; }
        }
        return sb.ToString().Trim();
    }

    static int Osa(string a, string b)
    {
        int[,] d = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) d[0, j] = j;
        for (int i = 1; i <= a.Length; i++)
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1]) d[i, j] = Math.Min(d[i, j], d[i - 2, j - 2] + 1);
            }
        return d[a.Length, b.Length];
    }

    static bool Subsequence(string q, string s)
    {
        int j = 0;
        for (int i = 0; i < s.Length && j < q.Length; i++) if (s[i] == q[j]) j++;
        return j == q.Length;
    }

    // Score de correspondance (q déjà normalisé). -1 = pas de correspondance.
    public static int Score(string q, string name)
    {
        if (q.Length == 0) return 0;
        string n = Norm(name);
        if (n.Length == 0) return -1;
        if (n == q) return 1000;
        if (n.StartsWith(q)) return 900 - Math.Min(50, n.Length - q.Length);
        string[] words = n.Split(' ');
        foreach (string w in words) if (w.StartsWith(q)) return 820;
        if (q.Length >= 2)
        {
            string ini = new string(words.Where(w => w.Length > 0).Select(w => w[0]).ToArray());
            if (ini.StartsWith(q)) return 760;
        }
        int idx = n.IndexOf(q, StringComparison.Ordinal);
        if (idx >= 0) return 700 - Math.Min(50, idx);
        string nc = n.Replace(" ", ""), qc = q.Replace(" ", "");
        if (nc.Contains(qc)) return 650;
        string[] qw = q.Split(' ');
        if (qw.Length > 1 && qw.All(x => words.Any(w => w.StartsWith(x)))) return 620;
        if (qc.Length >= 3)
        {
            // lettres manquantes : "asasin" -> "assassins", "cyberpnk" -> "cyberpunk"
            foreach (string w in words)
                if (w.Length > 0 && w[0] == qc[0] && w.Length <= qc.Length * 2 && Subsequence(qc, w)) return 480;
            if (Subsequence(qc, nc) && nc.Length <= qc.Length * 3) return 450;
        }
        int tol = qc.Length >= 7 ? 2 : qc.Length >= 4 ? 1 : 0;
        if (tol > 0)
        {
            int best = int.MaxValue;
            foreach (string w in words.Concat(new[] { nc }))
                for (int len = qc.Length - 1; len <= qc.Length + 1; len++)
                {
                    if (len <= 0) continue;
                    int d = Osa(qc, w.Substring(0, Math.Min(len, w.Length)));
                    if (d < best) best = d;
                }
            if (best <= tol) return 350 - best * 30;
        }
        return -1;
    }
}

static class Calc
{
    public static bool LooksLikeMath(string s)
    {
        s = s.Trim();
        if (s.Length < 2 || !s.Any(char.IsDigit)) return false;
        if (!Regex.IsMatch(s, @"^[\d\s.,+\-*/^()×÷x%]+$")) return false;
        return Regex.IsMatch(s, @"[\d)%]\s*[+\-*/^×÷x]\s*[\d(.]") || s.EndsWith("%");
    }

    public static bool TryEval(string s, out double v)
    {
        v = 0;
        try
        {
            s = s.Trim().ToLowerInvariant();
            if (s.Contains(",") && !s.Contains(".")) s = s.Replace(',', '.'); else s = s.Replace(",", "");
            s = s.Replace("×", "*").Replace("÷", "/").Replace("**", "^");
            Parser p = new Parser { S = s };
            v = p.Expr();
            p.Skip();
            return p.I == s.Length && !double.IsNaN(v) && !double.IsInfinity(v);
        }
        catch { return false; }
    }

    public static string Format(double v)
    {
        if (Math.Abs(v) < 1e15 && Math.Abs(v - Math.Round(v)) < 1e-10) return Math.Round(v).ToString("#,0", L.Culture);
        return v.ToString("#,0.##########", L.Culture);
    }

    class Parser
    {
        public string S;
        public int I;
        public void Skip() { while (I < S.Length && S[I] == ' ') I++; }
        bool Eat(char c) { Skip(); if (I < S.Length && S[I] == c) { I++; return true; } return false; }

        public double Expr()
        {
            double v = Term();
            while (true)
            {
                if (Eat('+')) v += Term();
                else if (Eat('-')) v -= Term();
                else return v;
            }
        }

        double Term()
        {
            double v = Factor();
            while (true)
            {
                if (Eat('*') || Eat('x')) v *= Factor();
                else if (Eat('/')) v /= Factor();
                else return v;
            }
        }

        double Factor()
        {
            double b = Unary();
            if (Eat('^')) return Math.Pow(b, Factor());
            return b;
        }

        double Unary()
        {
            if (Eat('-')) return -Unary();
            if (Eat('+')) return Unary();
            double v = Primary();
            if (Eat('%')) v /= 100;
            return v;
        }

        double Primary()
        {
            Skip();
            if (Eat('('))
            {
                double v = Expr();
                if (!Eat(')')) throw new FormatException();
                return v;
            }
            int st = I;
            while (I < S.Length && (char.IsDigit(S[I]) || S[I] == '.')) I++;
            if (I > st) return double.Parse(S.Substring(st, I - st), CultureInfo.InvariantCulture);
            while (I < S.Length && char.IsLetter(S[I]) && S[I] != 'x') I++;
            string f = S.Substring(st, I - st);
            if (f == "pi") return Math.PI;
            if (f == "e") return Math.E;
            if (f.Length == 0) throw new FormatException();
            if (!Eat('(')) throw new FormatException();
            double a = Expr();
            if (!Eat(')')) throw new FormatException();
            switch (f)
            {
                case "sqrt": case "racine": return Math.Sqrt(a);
                case "sin": return Math.Sin(a * Math.PI / 180);
                case "cos": return Math.Cos(a * Math.PI / 180);
                case "tan": return Math.Tan(a * Math.PI / 180);
                case "log": return Math.Log10(a);
                case "ln": return Math.Log(a);
                case "abs": return Math.Abs(a);
                case "round": case "arrondi": return Math.Round(a);
            }
            throw new FormatException();
        }
    }
}

// =====================================================================
//  Thème et dessin
// =====================================================================
static class Theme
{
    static Color D(int r, int g, int b, int lr, int lg, int lb)
    { return Settings.Dark ? Color.FromArgb(r, g, b) : Color.FromArgb(lr, lg, lb); }
    public static Color Bg { get { return D(24, 24, 28, 243, 243, 247); } }
    public static Color Side { get { return D(31, 31, 37, 233, 233, 239); } }
    public static Color Card { get { return D(36, 36, 42, 255, 255, 255); } }
    public static Color Surface { get { return D(46, 46, 54, 232, 232, 238); } }
    public static Color Hover { get { return D(54, 54, 63, 222, 222, 230); } }
    public static Color Line { get { return D(50, 50, 58, 228, 228, 235); } }
    public static Color Track { get { return D(72, 72, 84, 200, 200, 210); } }
    public static Color Fg { get { return D(236, 236, 241, 24, 24, 28); } }
    public static Color FgDim { get { return D(150, 150, 162, 105, 105, 115); } }
    public static Color Danger { get { return Color.FromArgb(239, 68, 68); } }
    public static Color Accent { get { return Settings.Accent; } }
    public static Color HoverOverlay { get { return Settings.Dark ? Color.FromArgb(26, 255, 255, 255) : Color.FromArgb(16, 0, 0, 0); } }
    public static Color SelOverlay { get { return Settings.Dark ? Color.FromArgb(44, 255, 255, 255) : Color.FromArgb(28, 0, 0, 0); } }

    public static Color Mix(Color a, Color b, float t)
    {
        return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
    }
}

static class Ui
{
    public static float Scale = 1f;
    public static int S(int v) { return (int)Math.Round(v * Scale); }
    public const string GlyphFont = "Segoe MDL2 Assets";

    public static GraphicsPath Round(RectangleF r, float rad)
    {
        GraphicsPath p = new GraphicsPath();
        float d = Math.Min(rad * 2, Math.Min(r.Width, r.Height));
        if (d <= 0) { p.AddRectangle(r); return p; }
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public static void Fill(Graphics g, Color c, RectangleF r, float rad)
    {
        SmoothingMode old = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (GraphicsPath p = Round(r, rad))
        using (SolidBrush b = new SolidBrush(c)) g.FillPath(b, p);
        g.SmoothingMode = old;
    }

    public static void DrawLogo(Graphics g, Rectangle r, Color accent)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Fill(g, accent, r, r.Width * 0.24f);
        float u = r.Width / 32f;
        for (int i = 0; i < 3; i++)
        {
            float y = r.Y + (8 + i * 6.5f) * u;
            Fill(g, Color.White, new RectangleF(r.X + 7 * u, y, 4.5f * u, 4.5f * u), 1.2f * u);
            Fill(g, Color.FromArgb(210, 255, 255, 255), new RectangleF(r.X + 14 * u, y + 1.2f * u, 11 * u, 2.2f * u), 1.1f * u);
        }
    }

    public static Icon MakeIcon(Color accent)
    {
        using (Bitmap b = new Bitmap(32, 32))
        {
            using (Graphics g = Graphics.FromImage(b)) DrawLogo(g, new Rectangle(1, 1, 30, 30), accent);
            IntPtr h = b.GetHicon();
            Icon tmp = Icon.FromHandle(h);
            Icon copy = (Icon)tmp.Clone();
            tmp.Dispose();
            Native.DestroyIcon(h);
            return copy;
        }
    }

    public static void Glyph(Graphics g, string glyph, float pt, Color c, Rectangle r)
    {
        using (Font f = new Font(GlyphFont, pt))
            TextRenderer.DrawText(g, glyph, f, r, c, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    public static Bitmap GlyphImage(string glyph, Color c)
    {
        int s = S(16);
        Bitmap b = new Bitmap(s, s);
        using (Graphics g = Graphics.FromImage(b))
        using (Font f = new Font(GlyphFont, 10f))
        using (SolidBrush br = new SolidBrush(c))
        using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
        {
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            g.DrawString(glyph, f, br, new RectangleF(0, 0, s, s), sf);
        }
        return b;
    }

    public static void WriteIco(string path)
    {
        int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 128, 256 };
        List<byte[]> pngs = new List<byte[]>();
        foreach (int s in sizes)
            using (Bitmap b = new Bitmap(s, s))
            {
                using (Graphics g = Graphics.FromImage(b))
                {
                    int pad = s >= 32 ? Math.Max(1, s / 32) : 0;
                    DrawLogo(g, new Rectangle(pad, pad, s - pad * 2, s - pad * 2), Color.FromArgb(59, 130, 246));
                }
                using (MemoryStream ms = new MemoryStream()) { b.Save(ms, ImageFormat.Png); pngs.Add(ms.ToArray()); }
            }
        using (BinaryWriter w = new BinaryWriter(File.Create(path)))
        {
            w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                w.Write((byte)0); w.Write((byte)0);
                w.Write((short)1); w.Write((short)32);
                w.Write(pngs[i].Length); w.Write(offset);
                offset += pngs[i].Length;
            }
            foreach (byte[] p in pngs) w.Write(p);
        }
    }
}

class MenuColors : ProfessionalColorTable
{
    public override Color MenuItemSelected { get { return Theme.Hover; } }
    public override Color MenuItemBorder { get { return Theme.Hover; } }
    public override Color MenuBorder { get { return Theme.Line; } }
    public override Color ToolStripDropDownBackground { get { return Theme.Card; } }
    public override Color ImageMarginGradientBegin { get { return Theme.Card; } }
    public override Color ImageMarginGradientMiddle { get { return Theme.Card; } }
    public override Color ImageMarginGradientEnd { get { return Theme.Card; } }
    public override Color SeparatorDark { get { return Theme.Line; } }
    public override Color SeparatorLight { get { return Theme.Card; } }
    public override Color MenuItemSelectedGradientBegin { get { return Theme.Hover; } }
    public override Color MenuItemSelectedGradientEnd { get { return Theme.Hover; } }
    public override Color MenuItemPressedGradientBegin { get { return Theme.Hover; } }
    public override Color MenuItemPressedGradientEnd { get { return Theme.Hover; } }
    public override Color CheckBackground { get { return Theme.Hover; } }
    public override Color CheckSelectedBackground { get { return Theme.Hover; } }
    public override Color CheckPressedBackground { get { return Theme.Hover; } }
}

class ThemedRenderer : ToolStripProfessionalRenderer
{
    public ThemedRenderer() : base(new MenuColors()) { RoundedEdges = false; }
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? Theme.Fg : Theme.FgDim;
        base.OnRenderItemText(e);
    }
    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = Theme.Fg;
        base.OnRenderArrow(e);
    }
}

static class Menus
{
    public static ContextMenuStrip Create()
    {
        ContextMenuStrip m = new ContextMenuStrip();
        m.Renderer = new ThemedRenderer();
        m.Font = new Font("Segoe UI", 9.75f);
        m.ShowImageMargin = true;
        return m;
    }

    public static ToolStripMenuItem Item(string text, string glyph, Action click)
    {
        ToolStripMenuItem it = new ToolStripMenuItem(text);
        if (glyph != null) it.Image = Ui.GlyphImage(glyph, Theme.Fg);
        if (click != null) it.Click += (s, e) => click();
        it.Padding = new Padding(0, Ui.S(2), 0, Ui.S(2));
        return it;
    }

    public static ToolStripMenuItem Sub(ContextMenuStrip owner, string text, string glyph)
    {
        ToolStripMenuItem it = Item(text, glyph, null);
        ((ToolStripDropDownMenu)it.DropDown).Renderer = owner.Renderer;
        return it;
    }
}

// =====================================================================
//  Contrôles dessinés à la main
// =====================================================================
abstract class FlatControl : Control
{
    protected FlatControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Cursor = Cursors.Hand;
    }
    protected Graphics Prep(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        return g;
    }
}

class Toggle : FlatControl
{
    bool on;
    float pos;
    readonly System.Windows.Forms.Timer anim = new System.Windows.Forms.Timer();
    public event EventHandler Changed;

    public Toggle()
    {
        Size = new Size(Ui.S(44), Ui.S(24));
        TabStop = true;
        anim.Interval = 12;
        anim.Tick += (s, e) =>
        {
            float target = on ? 1f : 0f;
            pos += (target - pos) * 0.45f;
            if (Math.Abs(target - pos) < 0.02f) { pos = target; anim.Stop(); }
            Invalidate();
        };
    }

    public bool Checked
    {
        get { return on; }
        set
        {
            bool animate = on != value && Visible && IsHandleCreated && Settings.Animations;
            on = value;
            if (animate) anim.Start();
            else { anim.Stop(); pos = on ? 1 : 0; Invalidate(); }
        }
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        Checked = !Checked;
        if (Changed != null) Changed(this, EventArgs.Empty);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space) OnClick(EventArgs.Empty);
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = Prep(e);
        RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        Ui.Fill(g, Theme.Mix(Theme.Track, Theme.Accent, pos), r, r.Height / 2);
        float pad = Ui.S(3);
        float d = r.Height - pad * 2;
        float x = r.X + pad + pos * (r.Width - d - pad * 2);
        using (SolidBrush b = new SolidBrush(Color.White)) g.FillEllipse(b, x, r.Y + pad, d, d);
    }
}

class Segmented : FlatControl
{
    readonly string[] opts;
    int sel, hover = -1;
    public event EventHandler Changed;

    public Segmented(params string[] options)
    {
        opts = options;
        Font = new Font("Segoe UI", 9.5f);
        int w = 0;
        foreach (string o in opts) w = Math.Max(w, TextRenderer.MeasureText(o, Font).Width);
        Size = new Size((w + Ui.S(22)) * opts.Length + Ui.S(6), Ui.S(32));
    }

    public int Selected
    {
        get { return sel; }
        set { sel = Math.Max(0, Math.Min(opts.Length - 1, value)); Invalidate(); }
    }

    float SegW { get { return (Width - Ui.S(6)) / (float)opts.Length; } }
    int IndexAt(int x) { return Math.Max(0, Math.Min(opts.Length - 1, (int)((x - Ui.S(3)) / SegW))); }

    protected override void OnMouseMove(MouseEventArgs e) { int h = IndexAt(e.X); if (h != hover) { hover = h; Invalidate(); } base.OnMouseMove(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = -1; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        int i = IndexAt(e.X);
        if (i != sel) { sel = i; Invalidate(); if (Changed != null) Changed(this, EventArgs.Empty); }
        base.OnMouseDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = Prep(e);
        Ui.Fill(g, Theme.Surface, new RectangleF(0, 0, Width - 1, Height - 1), Ui.S(8));
        float sw = SegW, pad = Ui.S(3);
        for (int i = 0; i < opts.Length; i++)
        {
            RectangleF r = new RectangleF(pad + i * sw, pad, sw, Height - pad * 2 - 1);
            if (i == sel) Ui.Fill(g, Theme.Accent, r, Ui.S(6));
            else if (i == hover) Ui.Fill(g, Theme.Hover, r, Ui.S(6));
            TextRenderer.DrawText(g, opts[i], Font, Rectangle.Round(r), i == sel ? Color.White : Theme.Fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }
}

class Slider : FlatControl
{
    public int Min, Max, Step = 1;
    int val;
    bool drag;
    public event EventHandler Changed;

    public Slider(int min, int max, int step)
    {
        Min = min; Max = max; Step = step;
        Size = new Size(Ui.S(170), Ui.S(26));
    }

    public int Value
    {
        get { return val; }
        set { val = Math.Max(Min, Math.Min(Max, value)); Invalidate(); }
    }

    float Pad { get { return Ui.S(9); } }

    void SetFromX(int x)
    {
        float t = (x - Pad) / (Width - Pad * 2);
        t = Math.Max(0, Math.Min(1, t));
        int v = Min + (int)Math.Round(t * (Max - Min) / Step) * Step;
        v = Math.Max(Min, Math.Min(Max, v));
        if (v != val) { val = v; Invalidate(); if (Changed != null) Changed(this, EventArgs.Empty); }
    }

    protected override void OnMouseDown(MouseEventArgs e) { drag = true; SetFromX(e.X); base.OnMouseDown(e); }
    protected override void OnMouseMove(MouseEventArgs e) { if (drag) SetFromX(e.X); base.OnMouseMove(e); }
    protected override void OnMouseUp(MouseEventArgs e) { drag = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        int v = Math.Max(Min, Math.Min(Max, val + (e.Delta > 0 ? Step : -Step)));
        if (v != val) { val = v; Invalidate(); if (Changed != null) Changed(this, EventArgs.Empty); }
        base.OnMouseWheel(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = Prep(e);
        float cy = Height / 2f, th = Ui.S(4);
        float t = (val - Min) / (float)(Max - Min);
        float kx = Pad + t * (Width - Pad * 2);
        Ui.Fill(g, Theme.Track, new RectangleF(Pad, cy - th / 2, Width - Pad * 2, th), th / 2);
        Ui.Fill(g, Theme.Accent, new RectangleF(Pad, cy - th / 2, Math.Max(th, kx - Pad), th), th / 2);
        float kr = Ui.S(8);
        using (SolidBrush b = new SolidBrush(Theme.Accent)) g.FillEllipse(b, kx - kr, cy - kr, kr * 2, kr * 2);
        float ir = drag ? Ui.S(3) : Ui.S(4);
        using (SolidBrush b = new SolidBrush(Color.White)) g.FillEllipse(b, kx - ir, cy - ir, ir * 2, ir * 2);
    }
}

class Swatches : FlatControl
{
    public static readonly Color[] Presets = {
        Color.FromArgb(59, 130, 246), Color.FromArgb(139, 92, 246), Color.FromArgb(236, 72, 153),
        Color.FromArgb(239, 68, 68), Color.FromArgb(249, 115, 22), Color.FromArgb(22, 163, 74),
        Color.FromArgb(13, 148, 136)
    };
    public Color Selected;
    public event EventHandler Changed;
    int D { get { return Ui.S(22); } }
    int Gap { get { return Ui.S(8); } }

    public Swatches()
    {
        Size = new Size(Presets.Length * (D + Gap) - Gap + Ui.S(6), D + Ui.S(6));
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        int i = (e.X - Ui.S(3)) / (D + Gap);
        if (i >= 0 && i < Presets.Length)
        {
            Selected = Presets[i];
            Invalidate();
            if (Changed != null) Changed(this, EventArgs.Empty);
        }
        base.OnMouseDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = Prep(e);
        for (int i = 0; i < Presets.Length; i++)
        {
            float x = Ui.S(3) + i * (D + Gap), y = Ui.S(3);
            using (SolidBrush b = new SolidBrush(Presets[i])) g.FillEllipse(b, x, y, D, D);
            if (Presets[i].ToArgb() == Selected.ToArgb())
            {
                using (Pen p = new Pen(Theme.Fg, Ui.S(2))) g.DrawEllipse(p, x - Ui.S(2), y - Ui.S(2), D + Ui.S(4), D + Ui.S(4));
                using (SolidBrush w = new SolidBrush(Color.White)) g.FillEllipse(w, x + D / 2f - Ui.S(3), y + D / 2f - Ui.S(3), Ui.S(6), Ui.S(6));
            }
        }
    }
}

class GlyphPicker : FlatControl
{
    readonly string[] glyphs;
    public string Selected;
    const int Cols = 10;
    int Cell { get { return Ui.S(34); } }

    public GlyphPicker(string[] glyphs)
    {
        this.glyphs = glyphs;
        Selected = glyphs[0];
        int rows = (glyphs.Length + Cols - 1) / Cols;
        Size = new Size(Cols * Cell, rows * Cell);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        int i = (e.Y / Cell) * Cols + e.X / Cell;
        if (i >= 0 && i < glyphs.Length) { Selected = glyphs[i]; Invalidate(); }
        base.OnMouseDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = Prep(e);
        for (int i = 0; i < glyphs.Length; i++)
        {
            Rectangle r = new Rectangle((i % Cols) * Cell + Ui.S(2), (i / Cols) * Cell + Ui.S(2), Cell - Ui.S(4), Cell - Ui.S(4));
            bool sel = glyphs[i] == Selected;
            Ui.Fill(g, sel ? Theme.Accent : Theme.Surface, r, Ui.S(6));
            Ui.Glyph(g, glyphs[i], 11f, sel ? Color.White : Theme.Fg, r);
        }
    }
}

class GlyphTileControl : FlatControl
{
    public string Glyph;
    public Color TileColor;
    public GlyphTileControl(string glyph, Color c) { Glyph = glyph; TileColor = c; Size = new Size(Ui.S(30), Ui.S(30)); Cursor = Cursors.Default; }
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = Prep(e);
        Ui.Fill(g, TileColor, new RectangleF(0, 0, Width - 1, Height - 1), Ui.S(7));
        Ui.Glyph(g, Glyph, 10f, Color.White, ClientRectangle);
    }
}

class FlatButton : FlatControl
{
    public bool Primary, Danger;
    bool hover;

    public FlatButton(string text, bool primary, int width = 0)
    {
        Text = text;
        Primary = primary;
        Font = new Font("Segoe UI Semibold", 9.75f);
        Size = new Size(width > 0 ? width : TextRenderer.MeasureText(text, Font).Width + Ui.S(32), Ui.S(36));
    }

    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = Prep(e);
        RectangleF r = new RectangleF(0, 0, Width - 1, Height - 1);
        Color fill = Primary ? Theme.Accent : Danger ? Theme.Danger : Theme.Surface;
        Color bg = Primary || Danger ? (hover ? Theme.Mix(fill, Color.Black, 0.12f) : fill) : (hover ? Theme.Hover : Theme.Surface);
        if (!Enabled) bg = Theme.Surface;
        Ui.Fill(g, bg, r, Ui.S(8));
        TextRenderer.DrawText(g, Text, Font, ClientRectangle, !Enabled ? Theme.FgDim : Primary || Danger ? Color.White : Theme.Fg,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
    }
}

class InputBox : Panel
{
    public readonly TextBox Box;

    public InputBox(int width)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Size = new Size(width, Ui.S(34));
        Padding = new Padding(Ui.S(10), Ui.S(8), Ui.S(10), 0);
        Box = new TextBox { BorderStyle = BorderStyle.None, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 10f) };
        Controls.Add(Box);
        Cursor = Cursors.IBeam;
        Click += (s, e) => Box.Focus();
        ApplyTheme();
    }

    public void ApplyTheme()
    {
        Box.BackColor = Theme.Surface;
        Box.ForeColor = Theme.Fg;
        Invalidate();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        Ui.Fill(e.Graphics, Theme.Surface, new RectangleF(0, 0, Width - 1, Height - 1), Ui.S(8));
    }
}

class HotkeyBox : FlatControl
{
    public int Mods, Key;
    bool capturing;
    string live;
    public static bool Capturing;
    public event EventHandler Changed;

    public HotkeyBox()
    {
        SetStyle(ControlStyles.Selectable, true);
        TabStop = true;
        Font = new Font("Segoe UI Semibold", 9.75f);
        Size = new Size(Ui.S(210), Ui.S(34));
    }

    public static string Format(int mods, int key)
    {
        if (key == 0) return L.T("Aucun", "None");
        List<string> p = new List<string>();
        if ((mods & 2) != 0) p.Add("Ctrl");
        if ((mods & 1) != 0) p.Add("Alt");
        if ((mods & 4) != 0) p.Add(L.T("Maj", "Shift"));
        if ((mods & 8) != 0) p.Add("Win");
        p.Add(KeyName((Keys)key));
        return string.Join(" + ", p);
    }

    static string ModsOnly(int mods)
    {
        List<string> p = new List<string>();
        if ((mods & 2) != 0) p.Add("Ctrl");
        if ((mods & 1) != 0) p.Add("Alt");
        if ((mods & 4) != 0) p.Add(L.T("Maj", "Shift"));
        if ((mods & 8) != 0) p.Add("Win");
        return string.Join(" + ", p);
    }

    static string KeyName(Keys k)
    {
        if (k >= Keys.D0 && k <= Keys.D9) return ((int)(k - Keys.D0)).ToString();
        if (k >= Keys.NumPad0 && k <= Keys.NumPad9) return L.T("Pavé ", "Num ") + (int)(k - Keys.NumPad0);
        switch (k)
        {
            case Keys.Space: return L.T("Espace", "Space");
            case Keys.Return: return L.T("Entrée", "Enter");
            case Keys.Tab: return "Tab";
            case Keys.Up: return L.T("Haut", "Up");
            case Keys.Down: return L.T("Bas", "Down");
            case Keys.Left: return L.T("Gauche", "Left");
            case Keys.Right: return L.T("Droite", "Right");
            case Keys.Oemtilde: return "²";
        }
        return k.ToString();
    }

    protected override bool IsInputKey(Keys keyData) { return true; }
    protected override void OnGotFocus(EventArgs e) { capturing = true; Capturing = true; live = null; Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { capturing = false; Capturing = false; Invalidate(); base.OnLostFocus(e); }
    protected override void OnMouseDown(MouseEventArgs e) { Focus(); base.OnMouseDown(e); }

    int CurrentMods(KeyEventArgs e)
    {
        int m = 0;
        if (e.Control) m |= 2;
        if (e.Alt) m |= 1;
        if (e.Shift) m |= 4;
        if ((Native.GetKeyState(0x5B) & 0x8000) != 0 || (Native.GetKeyState(0x5C) & 0x8000) != 0) m |= 8;
        return m;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        e.Handled = true;
        e.SuppressKeyPress = true;
        Keys k = e.KeyCode;
        int m = CurrentMods(e);
        if (k == Keys.Escape && m == 0) { Parent.Focus(); return; }
        if (k == Keys.ControlKey || k == Keys.ShiftKey || k == Keys.Menu || k == Keys.LWin || k == Keys.RWin)
        {
            live = m == 0 ? null : ModsOnly(m) + " + …";
            Invalidate();
            return;
        }
        bool fkey = k >= Keys.F1 && k <= Keys.F24;
        if (m == 0 && !fkey) { live = L.T("Ajoute Ctrl, Alt ou Maj", "Add Ctrl, Alt or Shift"); Invalidate(); return; }
        Mods = m;
        Key = (int)k;
        live = null;
        if (Changed != null) Changed(this, EventArgs.Empty);
        Parent.Focus();
    }

    protected override void WndProc(ref Message m)
    {
        if (capturing && (m.Msg == 0x105 || m.Msg == 0x106)) return;
        base.WndProc(ref m);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = Prep(e);
        RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        Ui.Fill(g, Theme.Surface, r, Ui.S(8));
        if (capturing)
            using (GraphicsPath p = Ui.Round(r, Ui.S(8)))
            using (Pen pen = new Pen(Theme.Accent, Ui.S(2))) g.DrawPath(pen, p);
        string txt = capturing ? (live ?? L.T("Appuie sur tes touches…", "Press your keys…")) : Format(Mods, Key);
        TextRenderer.DrawText(g, txt, Font, ClientRectangle, capturing && live == null ? Theme.FgDim : Theme.Fg,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }
}

class Card : Panel
{
    public readonly List<int> Separators = new List<int>();

    public Card()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(Theme.Bg);
        Ui.Fill(g, Theme.Card, new RectangleF(0, 0, Width - 1, Height - 1), Ui.S(10));
        using (Pen p = new Pen(Theme.Line))
            foreach (int y in Separators) g.DrawLine(p, Ui.S(16), y, Width - Ui.S(16), y);
    }
}

class BufferedPanel : Panel
{
    public BufferedPanel() { SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); }
}

class SideNav : FlatControl
{
    readonly string[] names, glyphs;
    int sel, hover = -1;
    public event Action<int> SelectedChanged;
    readonly Font fName = new Font("Segoe UI Semibold", 12.5f);
    readonly Font fVer = new Font("Segoe UI", 8.75f);
    readonly Font fItem = new Font("Segoe UI", 10.5f);

    public SideNav(string[] names, string[] glyphs) { this.names = names; this.glyphs = glyphs; }

    Rectangle ItemRect(int i) { return new Rectangle(Ui.S(12), Ui.S(92) + i * Ui.S(42), Width - Ui.S(24), Ui.S(38)); }
    public int Selected { get { return sel; } set { sel = value; Invalidate(); } }

    int HitTest(Point p)
    {
        for (int i = 0; i < names.Length; i++) if (ItemRect(i).Contains(p)) return i;
        return -1;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        int h = HitTest(e.Location);
        if (h != hover) { hover = h; Invalidate(); }
        Cursor = h >= 0 ? Cursors.Hand : Cursors.Default;
        base.OnMouseMove(e);
    }
    protected override void OnMouseLeave(EventArgs e) { hover = -1; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        int h = HitTest(e.Location);
        if (h >= 0 && h != sel) { sel = h; Invalidate(); if (SelectedChanged != null) SelectedChanged(h); }
        base.OnMouseDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = Prep(e);
        Ui.DrawLogo(g, new Rectangle(Ui.S(20), Ui.S(24), Ui.S(38), Ui.S(38)), Settings.Accent);
        TextRenderer.DrawText(g, "BureauCache", fName, new Point(Ui.S(68), Ui.S(22)), Theme.Fg, TextFormatFlags.NoPrefix);
        TextRenderer.DrawText(g, "Version " + Installer.Version, fVer, new Point(Ui.S(70), Ui.S(46)), Theme.FgDim, TextFormatFlags.NoPrefix);
        for (int i = 0; i < names.Length; i++)
        {
            Rectangle r = ItemRect(i);
            if (i == sel)
            {
                Ui.Fill(g, Theme.SelOverlay, r, Ui.S(8));
                Ui.Fill(g, Theme.Accent, new RectangleF(r.X, r.Y + Ui.S(10), Ui.S(3), r.Height - Ui.S(20)), Ui.S(2));
            }
            else if (i == hover) Ui.Fill(g, Theme.HoverOverlay, r, Ui.S(8));
            Ui.Glyph(g, glyphs[i], 11f, i == sel ? Theme.Accent : Theme.Fg, new Rectangle(r.X + Ui.S(8), r.Y, Ui.S(32), r.Height));
            TextRenderer.DrawText(g, names[i], fItem, new Rectangle(r.X + Ui.S(46), r.Y, r.Width - Ui.S(46), r.Height), Theme.Fg,
                TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
        using (Pen p = new Pen(Theme.Line)) g.DrawLine(p, Width - 1, 0, Width - 1, Height);
    }
}

// Base des petites boîtes de dialogue
class ThemedDialog : Form
{
    public ThemedDialog(string title, int w, int h)
    {
        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.None;
        ClientSize = new Size(w, h);
        BackColor = Theme.Bg;
        Icon = Ui.MakeIcon(Settings.Accent);
    }

    protected Label AddLabel(string text, int x, int y)
    {
        Label l = new Label { Text = text, AutoSize = true, Font = new Font("Segoe UI", 9.75f), ForeColor = Theme.FgDim, BackColor = Theme.Bg, Location = new Point(x, y) };
        Controls.Add(l);
        return l;
    }

    protected void AddButtons(Action ok, string okText)
    {
        FlatButton bOk = new FlatButton(okText, true, Ui.S(120));
        FlatButton bCancel = new FlatButton(L.T("Annuler", "Cancel"), false, Ui.S(100));
        bOk.BackColor = bCancel.BackColor = Theme.Bg;
        bCancel.Location = new Point(ClientSize.Width - Ui.S(20) - bCancel.Width, ClientSize.Height - Ui.S(18) - bCancel.Height);
        bOk.Location = new Point(bCancel.Left - Ui.S(8) - bOk.Width, bCancel.Top);
        bOk.Click += (s, e) => ok();
        bCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
        Controls.Add(bOk);
        Controls.Add(bCancel);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Native.SetDarkTitleBar(Handle, Settings.Dark);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }
}

class RenameDialog : ThemedDialog
{
    readonly InputBox input;
    public string Value;

    public RenameDialog(string current, bool selectWithoutExt) : base(L.T("Renommer", "Rename"), Ui.S(420), Ui.S(156))
    {
        AddLabel(L.T("Nouveau nom", "New name"), Ui.S(20), Ui.S(16));
        input = new InputBox(Ui.S(380)) { Location = new Point(Ui.S(20), Ui.S(42)), BackColor = Theme.Bg };
        input.Box.Text = current;
        input.Box.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Accept(); } };
        Controls.Add(input);
        AddButtons(Accept, L.T("Renommer", "Rename"));
        Shown += (s, e) =>
        {
            input.Box.Focus();
            int dot = selectWithoutExt ? current.LastIndexOf('.') : -1;
            if (dot > 0) input.Box.Select(0, dot); else input.Box.SelectAll();
        };
    }

    void Accept()
    {
        string v = input.Box.Text.Trim();
        if (v.Length == 0 || v.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            MessageBox.Show(this, L.T("Ce nom n'est pas valide.", "This name is not valid."), "BureauCache", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        Value = v;
        DialogResult = DialogResult.OK;
        Close();
    }
}

class CategoryDialog : ThemedDialog
{
    readonly InputBox input;
    readonly GlyphPicker picker;
    readonly Swatches colors;
    readonly CustomCat editing;
    public string CatName, Glyph;
    public Color CatColor;

    public CategoryDialog(CustomCat existing)
        : base(existing == null ? L.T("Nouvelle catégorie", "New category") : L.T("Modifier la catégorie", "Edit category"), Ui.S(420), Ui.S(330))
    {
        editing = existing;
        AddLabel(L.T("Nom", "Name"), Ui.S(20), Ui.S(14));
        input = new InputBox(Ui.S(380)) { Location = new Point(Ui.S(20), Ui.S(38)), BackColor = Theme.Bg };
        input.Box.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Accept(); } };
        Controls.Add(input);
        AddLabel(L.T("Icône", "Icon"), Ui.S(20), Ui.S(84));
        picker = new GlyphPicker(Catalog.PickerGlyphs) { Location = new Point(Ui.S(20), Ui.S(108)), BackColor = Theme.Bg };
        Controls.Add(picker);
        AddLabel(L.T("Couleur", "Color"), Ui.S(20), Ui.S(186));
        colors = new Swatches { Location = new Point(Ui.S(17), Ui.S(208)), BackColor = Theme.Bg };
        colors.Selected = Swatches.Presets[CustomCountColor()];
        Controls.Add(colors);
        if (existing != null)
        {
            input.Box.Text = existing.Name;
            picker.Selected = existing.Glyph;
            colors.Selected = existing.Color;
        }
        AddButtons(Accept, existing == null ? L.T("Créer", "Create") : L.T("Enregistrer", "Save"));
        Shown += (s, e) => { input.Box.Focus(); input.Box.SelectAll(); };
    }

    static int CustomCountColor() { return Settings.CustomCats.Count % Swatches.Presets.Length; }

    void Accept()
    {
        string v = input.Box.Text.Trim();
        string err = null;
        if (v.Length == 0) err = L.T("Donne un nom à la catégorie.", "Give the category a name.");
        else if (v.IndexOfAny(new[] { '|', '>', '=' }) >= 0) err = L.T("Le nom ne peut pas contenir | > ou =.", "The name can't contain | > or =.");
        else if (Catalog.BuiltIn.Any(b => string.Equals(b, v, StringComparison.OrdinalIgnoreCase) || string.Equals(L.Cat(b), v, StringComparison.OrdinalIgnoreCase)))
            err = L.T("Ce nom est déjà utilisé par une catégorie automatique.", "This name is used by a built-in category.");
        else
        {
            CustomCat other = Settings.FindCat(v);
            if (other != null && other != editing) err = L.T("Une catégorie porte déjà ce nom.", "A category already has this name.");
        }
        if (err != null) { MessageBox.Show(this, err, "BureauCache", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        CatName = v;
        Glyph = picker.Selected;
        CatColor = colors.Selected;
        DialogResult = DialogResult.OK;
        Close();
    }

    // Crée ou modifie une catégorie ; renvoie son nom ou null
    public static string Run(CustomCat existing)
    {
        using (CategoryDialog d = new CategoryDialog(existing))
        {
            if (d.ShowDialog() != DialogResult.OK) return null;
            if (existing == null)
                Settings.CustomCats.Add(new CustomCat { Name = d.CatName, Glyph = d.Glyph, Color = d.CatColor });
            else
            {
                if (existing.Name != d.CatName) Settings.RenameCategory(existing, d.CatName);
                existing.Glyph = d.Glyph;
                existing.Color = d.CatColor;
            }
            return d.CatName;
        }
    }
}

// =====================================================================
//  Éléments affichés dans le panneau
// =====================================================================
enum ItemKind { Desktop, App, Recent, Setting, Command, Calc, Web }

class DesktopItem
{
    public string Key, Name, Target, Args, AutoCategory, Detail, Keywords;
    public Bitmap Icon;
    public bool IsFolder, IsSystem;
    public ItemKind Kind;
    public Action Run;
    public DateTime Created, Modified;
    public long Size = -1;

    public string Category
    {
        get
        {
            string c;
            if (Settings.CatOverride.TryGetValue(Key, out c) && Catalog.IsValid(c))
            {
                CustomCat cc = Settings.FindCat(c);
                return cc != null ? cc.Name : c;
            }
            return AutoCategory;
        }
    }

    public bool IsDesktopFile { get { return Kind == ItemKind.Desktop && !IsSystem; } }
    public bool HasPath { get { return IsDesktopFile || Kind == ItemKind.Recent || (Kind == ItemKind.App && Args == null); } }

    public bool IsNew
    {
        get
        {
            if (!IsDesktopFile || !Settings.NewBadge) return false;
            if ((DateTime.Now - Created).TotalDays >= Settings.NewDays) return false;
            long[] u;
            if (Settings.Usage.TryGetValue(Key, out u) && u[1] >= Created.Ticks) return false;
            return true;
        }
    }
}

static class Catalog
{
    public static readonly string[] BuiltIn = { "Dossiers", "Jeux", "Applications", "Fichiers" };
    public static readonly string[] BuiltInGlyphs = { "", "", "", "" };
    public static readonly string[] PickerGlyphs = {
        "", "", "", "", "", "", "", "", "", "",
        "", "", "", "", "", "", "", "", "", ""
    };

    static readonly string[] GameHints = {
        "steam://", "com.epicgames.launcher://", "uplay://", "ubisoft://", "battlenet://", "origin://", "origin2://",
        "goggalaxy://", "riotclient", "steamapps", "\\steam\\", "steam.exe", "epic games", "epicgameslauncher",
        "riot games", "ubisoft game launcher", "gog galaxy", "gog games", "battle.net", "ea games", "\\ea app\\",
        "origin games", "rockstar games", "\\games\\", "xboxgames", "minecraft", "lutris", "heroic"
    };
    static readonly string[] AppExts = { ".exe", ".bat", ".cmd", ".msc", ".cpl", ".appref-ms", ".ps1", ".vbs", ".jar", ".msi", ".com", ".scr" };

    public static bool IsValid(string n)
    {
        return BuiltIn.Contains(n) || Settings.FindCat(n) != null;
    }

    public static IEnumerable<string> AllCats()
    {
        foreach (CustomCat c in Settings.CustomCats) yield return c.Name;
        foreach (string b in BuiltIn) yield return b;
    }

    public static string Display(string cat) { return BuiltIn.Contains(cat) ? L.Cat(cat) : cat; }

    public static string GlyphOf(string cat)
    {
        int i = Array.IndexOf(BuiltIn, cat);
        if (i >= 0) return BuiltInGlyphs[i];
        CustomCat c = Settings.FindCat(cat);
        return c != null ? c.Glyph : "";
    }

    public static Color? ColorOf(string cat)
    {
        CustomCat c = Settings.FindCat(cat);
        if (c != null) return c.Color;
        return null;
    }

    public static string Classify(string path, bool isDir)
    {
        if (isDir) return "Dossiers";
        string ext = Path.GetExtension(path).ToLowerInvariant();
        string resolved = null;
        if (ext == ".lnk") resolved = ShellUtil.ResolveLnk(path);
        else if (ext == ".url") resolved = ShellUtil.ReadUrl(path);
        string hay = ((resolved ?? "") + "|" + path).ToLowerInvariant();
        foreach (string h in GameHints) if (hay.Contains(h)) return "Jeux";
        if (ext == ".lnk")
        {
            if (string.IsNullOrEmpty(resolved)) return "Applications";
            if (Directory.Exists(resolved)) return "Dossiers";
            return AppExts.Contains(Path.GetExtension(resolved).ToLowerInvariant()) ? "Applications" : "Fichiers";
        }
        if (ext == ".url") return "Fichiers";
        return AppExts.Contains(ext) ? "Applications" : "Fichiers";
    }

    public static bool IsExecutable(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return ext == ".lnk" || AppExts.Contains(ext);
    }
}

// Chargement en arrière-plan des éléments (bureau, applications, fichiers récents)
static class Loader
{
    static readonly Dictionary<string, string> classCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    static string Classify(string path, DateTime modified)
    {
        string key = path + "|" + modified.Ticks;
        lock (classCache) { string c; if (classCache.TryGetValue(key, out c)) return c; }
        string r = Catalog.Classify(path, false);
        lock (classCache) classCache[key] = r;
        return r;
    }

    static bool IsHidden(string p)
    {
        try { return (File.GetAttributes(p) & (FileAttributes.Hidden | FileAttributes.System)) != 0; }
        catch { return true; }
    }

    public static List<DesktopItem> Desktop(int px, bool showSystem)
    {
        List<DesktopItem> items = new List<DesktopItem>();
        HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string[] dirs = {
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)
        };
        if (showSystem)
        {
            items.Add(new DesktopItem { Key = "::ce-pc", Name = L.T("Ce PC", "This PC"), Target = "explorer.exe", Args = "shell:MyComputerFolder", IsSystem = true, AutoCategory = "Dossiers", Icon = IconCache.Get("stock|15|" + px, () => Icons.Stock(15, px)) });
            items.Add(new DesktopItem { Key = "::corbeille", Name = L.T("Corbeille", "Recycle Bin"), Target = "explorer.exe", Args = "shell:RecycleBinFolder", IsSystem = true, AutoCategory = "Dossiers", Icon = IconCache.Get("stock|31|" + px, () => Icons.Stock(31, px)) });
        }
        foreach (string dir in dirs)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) continue;
            try
            {
                foreach (string d in Directory.GetDirectories(dir))
                {
                    if (IsHidden(d)) continue;
                    string n = Path.GetFileName(d);
                    if (!seen.Add("d:" + n)) continue;
                    DateTime cr = Directory.GetCreationTime(d), mo = Directory.GetLastWriteTime(d);
                    items.Add(new DesktopItem { Key = d, Name = n, Target = d, IsFolder = true, AutoCategory = "Dossiers", Created = cr, Modified = mo, Icon = IconCache.ForFile(d, px) });
                }
                foreach (string f in Directory.GetFiles(dir))
                {
                    if (IsHidden(f)) continue;
                    string fn = Path.GetFileName(f);
                    if (fn.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                    string ext = Path.GetExtension(f).ToLowerInvariant();
                    string n = (ext == ".lnk" || ext == ".url") ? Path.GetFileNameWithoutExtension(f) : fn;
                    if (!seen.Add("f:" + n)) continue;
                    FileInfo fi = new FileInfo(f);
                    items.Add(new DesktopItem
                    {
                        Key = f, Name = n, Target = f, AutoCategory = Classify(f, fi.LastWriteTime),
                        Created = fi.CreationTime, Modified = fi.LastWriteTime, Size = fi.Length, Icon = IconCache.ForFile(f, px)
                    });
                }
            }
            catch { }
        }
        return items;
    }

    static void Walk(string dir, List<string> acc, int depth)
    {
        if (depth > 6) return;
        try { acc.AddRange(Directory.GetFiles(dir, "*.lnk")); } catch { }
        try { foreach (string d in Directory.GetDirectories(dir)) Walk(d, acc, depth + 1); } catch { }
    }

    public static List<DesktopItem> Apps(int px)
    {
        List<DesktopItem> res = new List<DesktopItem>();
        HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        List<string> lnks = new List<string>();
        Walk(Environment.GetFolderPath(Environment.SpecialFolder.Programs), lnks, 0);
        Walk(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), lnks, 0);
        foreach (string l in lnks)
        {
            string n = Path.GetFileNameWithoutExtension(l);
            string nn = Search.Norm(n);
            if (nn.Contains("uninstall") || nn.Contains("desinstall")) continue;
            if (!names.Add(n)) continue;
            res.Add(new DesktopItem { Kind = ItemKind.App, Key = "app:" + l, Name = n, Target = l, Detail = L.T("Application", "App"), Icon = IconCache.ForFile(l, px) });
        }
        try
        {
            dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application"));
            dynamic folder = shell.NameSpace("shell:AppsFolder");
            dynamic items = folder.Items();
            int count = items.Count;
            for (int i = 0; i < count; i++)
            {
                try
                {
                    dynamic it = items.Item(i);
                    string n = it.Name;
                    string p = it.Path;
                    if (string.IsNullOrEmpty(n) || string.IsNullOrEmpty(p) || !p.Contains("!") || names.Contains(n)) continue;
                    names.Add(n);
                    string parsing = "shell:AppsFolder\\" + p;
                    res.Add(new DesktopItem
                    {
                        Kind = ItemKind.App, Key = "app:" + p, Name = n, Target = "explorer.exe", Args = parsing,
                        Detail = L.T("Application du Store", "Store app"),
                        Icon = IconCache.Get("uwp|" + p + "|" + px, () => Icons.ShellImage(parsing, px, false))
                    });
                }
                catch { }
            }
        }
        catch { }
        return res;
    }

    public static List<DesktopItem> Recent(int px)
    {
        List<DesktopItem> res = new List<DesktopItem>();
        try
        {
            string dir = Environment.GetFolderPath(Environment.SpecialFolder.Recent);
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (FileInfo f in new DirectoryInfo(dir).GetFiles("*.lnk").OrderByDescending(f => f.LastWriteTime).Take(120))
            {
                string t = ShellUtil.ResolveLnk(f.FullName);
                if (string.IsNullOrEmpty(t) || !seen.Add(t)) continue;
                bool isDir = Directory.Exists(t);
                if (!isDir && !File.Exists(t)) continue;
                string name = Path.GetFileName(t.TrimEnd('\\'));
                if (string.IsNullOrEmpty(name)) continue;
                res.Add(new DesktopItem
                {
                    Kind = ItemKind.Recent, Key = t, Name = name, Target = t, IsFolder = isDir,
                    Detail = Path.GetDirectoryName(t), Modified = f.LastWriteTime, Icon = IconCache.ForFile(t, px)
                });
                if (res.Count >= 40) break;
            }
        }
        catch { }
        return res;
    }
}

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

    public static List<DesktopItem> Settings_(int px)
    {
        Bitmap icon = IconCache.Tile("", Ui.GlyphFont, Color.FromArgb(84, 96, 116), px);
        return WinSettings.Select(s => new DesktopItem
        {
            Kind = ItemKind.Setting, Key = "set:" + s[2] + s[3], Name = L.T(s[0], s[1]), Target = s[2], Args = s[3],
            Keywords = s[4] + " " + s[0] + " " + s[1], Detail = L.T("Paramètres Windows", "Windows settings"), Icon = icon
        }).ToList();
    }

    static bool Confirm(string msg)
    {
        return MessageBox.Show(msg, "BureauCache", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
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
        add("Verrouiller", "Lock", "", slate, "lock verrou", () => Native.LockWorkStation());
        add("Mettre en veille", "Sleep", "", slate, "veille sleep suspend", () => Application.SetSuspendState(PowerState.Suspend, false, false));
        add("Redémarrer", "Restart", "", red, "reboot redemarrer", () =>
        { if (Confirm(L.T("Redémarrer l'ordinateur maintenant ?", "Restart the computer now?"))) Shutdown("/r /t 0"); });
        add("Éteindre", "Shut down", "", red, "eteindre arreter shutdown", () =>
        { if (Confirm(L.T("Éteindre l'ordinateur maintenant ?", "Shut down the computer now?"))) Shutdown("/s /t 0"); });
        add("Se déconnecter", "Sign out", "", red, "deconnexion logoff", () =>
        { if (Confirm(L.T("Fermer ta session maintenant ?", "Sign out now?"))) Native.ExitWindowsEx(0, 0); });
        add("Vider la corbeille", "Empty Recycle Bin", "", slate, "corbeille trash", () => Native.SHEmptyRecycleBin(IntPtr.Zero, null, 0));
        add("Gestionnaire des tâches", "Task Manager", "", slate, "taskmgr processus", () => Start("taskmgr.exe", null));
        add("Invite de commandes", "Command Prompt", "", slate, "cmd terminal console", () => Start("cmd.exe", null));
        add("PowerShell", "PowerShell", "", slate, "terminal console", () => Start("powershell.exe", null));
        add("Explorateur de fichiers", "File Explorer", "", slate, "explorer dossiers", () => Start("explorer.exe", null));
        add(Settings.HideIcons ? "Afficher les icônes du bureau" : "Cacher les icônes du bureau",
            Settings.HideIcons ? "Show desktop icons" : "Hide desktop icons", "", Settings.Accent, "icones bureau desktop", () =>
            { Settings.HideIcons = !Settings.HideIcons; Settings.Notify(); });
        add("Paramètres de BureauCache", "BureauCache settings", "", Settings.Accent, "bureaucache reglages options", openSettings);
        return c;
    }
}

class Group
{
    public string Key, Title, Glyph;
    public Color? GlyphColor;
    public List<DesktopItem> Items = new List<DesktopItem>();
    public bool IsSearch { get { return Key.StartsWith("s:"); } }
}

class VRow
{
    public Group G;
    public DesktopItem Item;
    public Rectangle R;
    public int IndexInGroup;
    public bool IsHeader { get { return Item == null; } }
}

// =====================================================================
//  Vue des éléments (liste ou grille)
// =====================================================================
class ItemsView : Control
{
    readonly PanelForm owner;
    List<Group> groups = new List<Group>();
    readonly List<VRow> rows = new List<VRow>();
    int contentH;
    float scroll, scrollTarget;
    readonly System.Windows.Forms.Timer scrollAnim = new System.Windows.Forms.Timer();
    readonly System.Windows.Forms.Timer hoverTimer = new System.Windows.Forms.Timer();
    int hover = -1, sel = -1, downRow = -1;
    Point downPt;
    bool dragging, scrollDragging, dropActive;
    VRow dragRow;
    int scrollDragY;
    float scrollDragStart;
    Group dropGroup;
    int dropIndex;
    Rectangle dropMark;
    bool selfDragOut;
    public string EmptyText = "";
    public bool ForceList;

    readonly Font fItem = new Font("Segoe UI", 10.5f);
    readonly Font fDetail = new Font("Segoe UI", 8.5f);
    readonly Font fGrid = new Font("Segoe UI", 9f);
    readonly Font fHeader = new Font("Segoe UI Semibold", 8.75f);
    readonly Font fBadge = new Font("Segoe UI Semibold", 7.5f);
    readonly Font fGlyph = new Font(Ui.GlyphFont, 9f);

    public event Action<DesktopItem> ItemActivated;
    public event Action<DesktopItem, Point> ItemContext;
    public event Action<DesktopItem, Group, Group, int> ItemDropped;
    public event Action<string[], DragDropEffects> FilesDropped;
    public event Action<DesktopItem, Rectangle> HoverItem;
    public event Action HoverEnd;

    public ItemsView(PanelForm owner)
    {
        this.owner = owner;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        SetStyle(ControlStyles.Selectable, false);
        AllowDrop = true;
        scrollAnim.Interval = 10;
        scrollAnim.Tick += (s, e) =>
        {
            scroll += (scrollTarget - scroll) * 0.35f;
            if (Math.Abs(scrollTarget - scroll) < 0.5f) { scroll = scrollTarget; scrollAnim.Stop(); }
            Invalidate();
        };
        hoverTimer.Interval = 450;
        hoverTimer.Tick += (s, e) =>
        {
            hoverTimer.Stop();
            if (dragging || hover < 0 || hover >= rows.Count || rows[hover].IsHeader || HoverItem == null) return;
            HoverItem(rows[hover].Item, RectangleToScreen(Screenify(rows[hover].R)));
        };
    }

    static int S(int v) { return Ui.S(v); }
    bool Grid { get { return Settings.GridView && !ForceList; } }
    int ItemH { get { return S(44); } }
    int HeaderH { get { return S(32); } }
    int CellH { get { return S(100); } }
    int MinCellW { get { return S(88); } }
    float MaxScroll { get { return Math.Max(0, contentH - Height); } }

    public DesktopItem Selected { get { return sel >= 0 && sel < rows.Count ? rows[sel].Item : null; } }

    void EndHover()
    {
        hoverTimer.Stop();
        if (HoverEnd != null) HoverEnd();
    }

    public void SetGroups(List<Group> g, bool keepScroll)
    {
        string selKey = Selected != null ? Selected.Key : null;
        groups = g;
        LayoutRows();
        if (!keepScroll) { scroll = scrollTarget = 0; }
        Clamp();
        sel = -1;
        hover = -1;
        EndHover();
        if (keepScroll && selKey != null)
            for (int i = 0; i < rows.Count; i++) if (!rows[i].IsHeader && rows[i].Item.Key == selKey) { sel = i; break; }
        Invalidate();
    }

    void LayoutRows()
    {
        rows.Clear();
        int y = S(2);
        int w = Width - S(4);
        foreach (Group g in groups)
        {
            if (g.Title != null)
            {
                rows.Add(new VRow { G = g, R = new Rectangle(S(6), y, w - S(8), HeaderH) });
                y += HeaderH;
                if (!g.IsSearch && Settings.Collapsed.Contains(g.Key)) { y += S(2); continue; }
            }
            if (Grid)
            {
                int avail = w - S(8);
                int cols = Math.Max(1, avail / MinCellW);
                int cw = avail / cols;
                for (int i = 0; i < g.Items.Count; i++)
                    rows.Add(new VRow { G = g, Item = g.Items[i], IndexInGroup = i, R = new Rectangle(S(6) + (i % cols) * cw, y + (i / cols) * CellH, cw, CellH) });
                y += ((g.Items.Count + cols - 1) / cols) * CellH;
            }
            else
            {
                for (int i = 0; i < g.Items.Count; i++)
                {
                    rows.Add(new VRow { G = g, Item = g.Items[i], IndexInGroup = i, R = new Rectangle(S(6), y, w - S(8), ItemH) });
                    y += ItemH;
                }
            }
            y += S(8);
        }
        contentH = y;
    }

    void Clamp()
    {
        scrollTarget = Math.Max(0, Math.Min(MaxScroll, scrollTarget));
        scroll = Math.Max(0, Math.Min(MaxScroll, scroll));
    }

    void ScrollTo(float target, bool animate)
    {
        scrollTarget = Math.Max(0, Math.Min(MaxScroll, target));
        EndHover();
        if (animate && Settings.Animations) scrollAnim.Start();
        else { scroll = scrollTarget; Invalidate(); }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        LayoutRows();
        Clamp();
        Invalidate();
    }

    int RowAt(Point p)
    {
        Point c = new Point(p.X, p.Y + (int)scroll);
        for (int i = 0; i < rows.Count; i++) if (rows[i].R.Contains(c)) return i;
        return -1;
    }

    Rectangle Screenify(Rectangle r) { r.Offset(0, -(int)scroll); return r; }

    // ---------- Dessin ----------
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        owner.PaintBg(g, ClientRectangle, Location);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        int top = (int)scroll, bottom = top + Height;
        for (int i = 0; i < rows.Count; i++)
        {
            VRow row = rows[i];
            if (row.R.Bottom < top || row.R.Top > bottom) continue;
            Rectangle r = Screenify(row.R);
            if (row.IsHeader) DrawHeader(g, row, r, i == hover);
            else if (Grid) DrawCell(g, row.Item, r, i == sel, i == hover, dragging && row == dragRow);
            else DrawListItem(g, row.Item, r, i == sel, i == hover, dragging && row == dragRow);
        }
        if (dragging && dropGroup != null && !dropMark.IsEmpty)
            Ui.Fill(g, Theme.Accent, Screenify(dropMark), S(1));

        if (rows.Count == 0)
            TextRenderer.DrawText(g, EmptyText, fItem, new Rectangle(S(10), S(30), Width - S(20), S(60)), Theme.FgDim,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);

        if (contentH > Height)
        {
            Rectangle t = Thumb();
            Ui.Fill(g, Color.FromArgb(scrollDragging ? 150 : 90, Theme.FgDim), t, t.Width / 2f);
        }

        if (dropActive)
        {
            Rectangle r = new Rectangle(S(6), S(4), Width - S(12), Height - S(8));
            using (GraphicsPath p = Ui.Round(r, S(10)))
            using (Pen pen = new Pen(Theme.Accent, S(2)) { DashStyle = DashStyle.Dash })
            using (SolidBrush b = new SolidBrush(Color.FromArgb(30, Theme.Accent)))
            {
                g.FillPath(b, p);
                g.DrawPath(pen, p);
            }
            string msg = L.T("Dépose ici pour ajouter au bureau", "Drop here to add to the desktop");
            Size ts = TextRenderer.MeasureText(msg, fItem);
            Rectangle pill = new Rectangle((Width - ts.Width) / 2 - S(16), Height / 2 - S(20), ts.Width + S(32), S(40));
            Ui.Fill(g, Theme.Accent, pill, S(20));
            TextRenderer.DrawText(g, msg, fItem, pill, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }

    Rectangle Thumb()
    {
        int h = Math.Max(S(36), (int)(Height * (float)Height / contentH));
        int y = MaxScroll <= 0 ? 0 : (int)((Height - h) * scroll / MaxScroll);
        int w = scrollDragging ? S(6) : S(4);
        return new Rectangle(Width - w - S(3), y, w, h);
    }

    void DrawHeader(Graphics g, VRow row, Rectangle r, bool hot)
    {
        if (hot && !dragging && !row.G.IsSearch) Ui.Fill(g, Theme.HoverOverlay, r, S(6));
        bool collapsed = !row.G.IsSearch && Settings.Collapsed.Contains(row.G.Key);
        Ui.Glyph(g, row.G.Glyph ?? "", 8.5f, row.G.GlyphColor ?? Theme.FgDim, new Rectangle(r.X + S(6), r.Y, S(20), r.Height));
        string t = row.G.Title.ToUpper(L.Culture);
        TextRenderer.DrawText(g, t, fHeader, new Rectangle(r.X + S(30), r.Y, r.Width, r.Height), Theme.FgDim,
            TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        int tw = TextRenderer.MeasureText(t, fHeader).Width;
        TextRenderer.DrawText(g, row.G.Items.Count.ToString(), fHeader, new Rectangle(r.X + S(30) + tw + S(2), r.Y, S(40), r.Height),
            Color.FromArgb(150, Theme.FgDim), TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        if (!row.G.IsSearch)
            TextRenderer.DrawText(g, collapsed ? "" : "", fGlyph, new Rectangle(r.Right - S(30), r.Y, S(24), r.Height), Theme.FgDim,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    void DrawIconOrPlaceholder(Graphics g, Bitmap b, Rectangle r, bool ghost)
    {
        if (b == null) { Ui.Fill(g, Theme.HoverOverlay, r, S(6)); return; }
        if (!ghost) { g.DrawImage(b, r); return; }
        using (ImageAttributes ia = new ImageAttributes())
        {
            ia.SetColorMatrix(new ColorMatrix { Matrix33 = 0.35f });
            g.DrawImage(b, r, 0, 0, b.Width, b.Height, GraphicsUnit.Pixel, ia);
        }
    }

    void DrawListItem(Graphics g, DesktopItem it, Rectangle r, bool selected, bool hot, bool ghost)
    {
        Rectangle bg = new Rectangle(r.X, r.Y + S(1), r.Width, r.Height - S(2));
        if (selected) Ui.Fill(g, Theme.SelOverlay, bg, S(8));
        else if (hot && !dragging) Ui.Fill(g, Theme.HoverOverlay, bg, S(8));
        if (selected) Ui.Fill(g, Theme.Accent, new RectangleF(bg.X, bg.Y + S(11), S(3), bg.Height - S(22)), S(2));
        int isz = S(32);
        int ix = r.X + S(12), iy = r.Y + (r.Height - isz) / 2;
        DrawIconOrPlaceholder(g, it.Icon, new Rectangle(ix, iy, isz, isz), ghost);
        int tx = ix + isz + S(12);
        int right = r.Right - S(8);
        if (it.IsNew)
        {
            string nb = L.T("Nouveau", "New");
            Size bs = TextRenderer.MeasureText(nb, fBadge);
            Rectangle pill = new Rectangle(right - bs.Width - S(8), r.Y + (r.Height - S(18)) / 2, bs.Width + S(8), S(18));
            Ui.Fill(g, Theme.Accent, pill, S(9));
            TextRenderer.DrawText(g, nb, fBadge, pill, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            right = pill.Left - S(6);
        }
        Color fg = ghost ? Theme.FgDim : Theme.Fg;
        bool detail = ForceList && !string.IsNullOrEmpty(it.Detail);
        if (detail)
        {
            TextRenderer.DrawText(g, it.Name, fItem, new Rectangle(tx, r.Y + S(3), right - tx, r.Height / 2), fg,
                TextFormatFlags.Bottom | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(g, it.Detail, fDetail, new Rectangle(tx, r.Y + r.Height / 2 + S(1), right - tx, r.Height / 2 - S(4)), Theme.FgDim,
                TextFormatFlags.Top | TextFormatFlags.Left | TextFormatFlags.PathEllipsis | TextFormatFlags.NoPrefix);
        }
        else
            TextRenderer.DrawText(g, it.Name, fItem, new Rectangle(tx, r.Y, right - tx, r.Height), fg,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }

    void DrawCell(Graphics g, DesktopItem it, Rectangle r, bool selected, bool hot, bool ghost)
    {
        Rectangle bg = new Rectangle(r.X + S(3), r.Y + S(3), r.Width - S(6), r.Height - S(6));
        if (selected) Ui.Fill(g, Theme.SelOverlay, bg, S(10));
        else if (hot && !dragging) Ui.Fill(g, Theme.HoverOverlay, bg, S(10));
        int isz = S(48);
        Rectangle ir = new Rectangle(r.X + (r.Width - isz) / 2, r.Y + S(10), isz, isz);
        DrawIconOrPlaceholder(g, it.Icon, ir, ghost);
        if (it.IsNew)
        {
            int d = S(12);
            Rectangle dot = new Rectangle(ir.Right - d + S(3), ir.Top - S(3), d, d);
            using (SolidBrush b = new SolidBrush(Theme.Accent)) g.FillEllipse(b, dot);
            using (Pen p = new Pen(Theme.Bg, S(2))) g.DrawEllipse(p, dot);
        }
        Rectangle tr = new Rectangle(r.X + S(6), ir.Bottom + S(4), r.Width - S(12), r.Bottom - ir.Bottom - S(8));
        TextRenderer.DrawText(g, it.Name, fGrid, tr, ghost ? Theme.FgDim : Theme.Fg,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis |
            TextFormatFlags.TextBoxControl | TextFormatFlags.NoPrefix);
    }

    // ---------- Souris ----------
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        EndHover();
        if (e.Button == MouseButtons.Left && contentH > Height && e.X >= Width - S(14))
        {
            Rectangle t = Thumb();
            if (e.Y >= t.Top && e.Y <= t.Bottom) { scrollDragging = true; scrollDragY = e.Y; scrollDragStart = scroll; }
            else ScrollTo(scrollTarget + (e.Y < t.Top ? -Height : Height) * 0.8f, true);
            Invalidate();
            return;
        }
        downPt = e.Location;
        downRow = RowAt(e.Location);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (scrollDragging)
        {
            Rectangle t = Thumb();
            float ratio = MaxScroll / Math.Max(1f, Height - t.Height);
            scroll = scrollTarget = Math.Max(0, Math.Min(MaxScroll, scrollDragStart + (e.Y - scrollDragY) * ratio));
            Invalidate();
            return;
        }
        if (e.Button == MouseButtons.Left && downRow >= 0 && downRow < rows.Count && !dragging && !rows[downRow].IsHeader &&
            (Math.Abs(e.X - downPt.X) > SystemInformation.DragSize.Width || Math.Abs(e.Y - downPt.Y) > SystemInformation.DragSize.Height))
        {
            dragging = true;
            dragRow = rows[downRow];
            owner.Busy = true;
            EndHover();
        }
        if (dragging)
        {
            bool outside = e.X < -S(4) || e.X > Width + S(4) || e.Y < -S(160) || e.Y > Height + S(4);
            if (outside && dragRow.Item.HasPath) { StartExternalDrag(); return; }
            if (e.Y < S(28)) ScrollTo(scrollTarget - S(14), false);
            else if (e.Y > Height - S(28)) ScrollTo(scrollTarget + S(14), false);
            ComputeDrop(e.Location);
            Invalidate();
            return;
        }
        int h = RowAt(e.Location);
        if (h != hover)
        {
            hover = h;
            EndHover();
            if (h >= 0 && !rows[h].IsHeader) hoverTimer.Start();
            Invalidate();
        }
        Cursor = h >= 0 ? Cursors.Hand : Cursors.Default;
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        EndHover();
        if (hover != -1 && !dragging) { hover = -1; Invalidate(); }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (scrollDragging) { scrollDragging = false; Invalidate(); return; }
        if (dragging)
        {
            dragging = false;
            owner.Busy = false;
            Group g = dropGroup;
            dropGroup = null;
            Invalidate();
            if (g != null && ItemDropped != null) ItemDropped(dragRow.Item, dragRow.G, g, dropIndex);
            downRow = -1;
            return;
        }
        int r = RowAt(e.Location);
        if (r < 0 || r != downRow) { downRow = -1; return; }
        downRow = -1;
        VRow row = rows[r];
        if (row.IsHeader)
        {
            if (e.Button != MouseButtons.Left || row.G.IsSearch) return;
            if (!Settings.Collapsed.Remove(row.G.Key)) Settings.Collapsed.Add(row.G.Key);
            Settings.SaveQuiet();
            LayoutRows();
            Clamp();
            hover = -1;
            Invalidate();
            return;
        }
        sel = r;
        Invalidate();
        if (e.Button == MouseButtons.Left && ItemActivated != null) ItemActivated(row.Item);
        else if (e.Button == MouseButtons.Right && ItemContext != null) ItemContext(row.Item, e.Location);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        ScrollTo(scrollTarget - e.Delta / 120f * S(44) * 2.5f, true);
    }

    public void WheelFromParent(int delta) { ScrollTo(scrollTarget - delta / 120f * S(44) * 2.5f, true); }

    void ComputeDrop(Point p)
    {
        Point c = new Point(p.X, p.Y + (int)scroll);
        VRow row = null;
        int best = int.MaxValue;
        foreach (VRow r in rows)
        {
            if (r.R.Contains(c)) { row = r; break; }
            int d = c.Y < r.R.Top ? r.R.Top - c.Y : c.Y > r.R.Bottom ? c.Y - r.R.Bottom : 0;
            if (d == 0) d = c.X < r.R.Left ? r.R.Left - c.X : c.X - r.R.Right;
            if (d < best) { best = d; row = r; }
        }
        dropGroup = null;
        dropMark = Rectangle.Empty;
        if (row == null || row.G.Key == "recent" || row.G.IsSearch || !dragRow.Item.IsDesktopFile && !dragRow.Item.IsSystem) return;
        dropGroup = row.G;
        if (row.IsHeader)
        {
            dropIndex = 0;
            dropMark = new Rectangle(row.R.X + S(8), row.R.Bottom - S(1), row.R.Width - S(16), S(2));
            return;
        }
        bool before = Grid ? c.X < row.R.X + row.R.Width / 2 : c.Y < row.R.Y + row.R.Height / 2;
        dropIndex = row.IndexInGroup + (before ? 0 : 1);
        if (Grid)
            dropMark = new Rectangle((before ? row.R.Left : row.R.Right) - S(1), row.R.Y + S(8), S(3), row.R.Height - S(16));
        else
            dropMark = new Rectangle(row.R.X + S(8), (before ? row.R.Top : row.R.Bottom) - S(1), row.R.Width - S(16), S(3));
    }

    void StartExternalDrag()
    {
        DesktopItem it = dragRow.Item;
        dragging = false;
        dropGroup = null;
        Invalidate();
        selfDragOut = true;
        owner.Busy = true;
        try { DoDragDrop(new DataObject(DataFormats.FileDrop, new string[] { it.Target }), DragDropEffects.Copy | DragDropEffects.Link); }
        catch { }
        selfDragOut = false;
        owner.Busy = false;
        owner.HidePanel();
    }

    static DragDropEffects EffectFor(DragEventArgs e)
    {
        if ((e.KeyState & 32) != 0 && (e.AllowedEffect & DragDropEffects.Link) != 0) return DragDropEffects.Link;
        if ((e.KeyState & 4) != 0 && (e.AllowedEffect & DragDropEffects.Move) != 0) return DragDropEffects.Move;
        return (e.AllowedEffect & DragDropEffects.Copy) != 0 ? DragDropEffects.Copy : DragDropEffects.Link;
    }

    protected override void OnDragEnter(DragEventArgs e)
    {
        base.OnDragEnter(e);
        if (!selfDragOut && e.Data.GetDataPresent(DataFormats.FileDrop)) { e.Effect = EffectFor(e); dropActive = true; Invalidate(); }
        else e.Effect = DragDropEffects.None;
    }

    protected override void OnDragOver(DragEventArgs e)
    {
        base.OnDragOver(e);
        e.Effect = !selfDragOut && e.Data.GetDataPresent(DataFormats.FileDrop) ? EffectFor(e) : DragDropEffects.None;
    }

    protected override void OnDragLeave(EventArgs e) { base.OnDragLeave(e); dropActive = false; Invalidate(); }

    protected override void OnDragDrop(DragEventArgs e)
    {
        base.OnDragDrop(e);
        dropActive = false;
        Invalidate();
        string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
        DragDropEffects eff = EffectFor(e);
        if (files != null && files.Length > 0 && FilesDropped != null)
            BeginInvoke(new Action(() => FilesDropped(files, eff)));
    }

    // ---------- Clavier ----------
    public void MoveSelection(Keys k)
    {
        List<int> items = new List<int>();
        for (int i = 0; i < rows.Count; i++) if (!rows[i].IsHeader) items.Add(i);
        if (items.Count == 0) return;
        if (sel < 0 || sel >= rows.Count || rows[sel].IsHeader) { sel = items[0]; EnsureVisible(); return; }
        Rectangle cur = rows[sel].R;
        int next = -1;
        if (k == Keys.Left || k == Keys.Right)
        {
            int pos = items.IndexOf(sel) + (k == Keys.Right ? 1 : -1);
            if (pos >= 0 && pos < items.Count) next = items[pos];
        }
        else
        {
            bool down = k == Keys.Down;
            int bestY = down ? int.MaxValue : int.MinValue, bestDx = int.MaxValue;
            foreach (int i in items)
            {
                Rectangle r = rows[i].R;
                if (down ? r.Top <= cur.Top : r.Top >= cur.Top) continue;
                int dx = Math.Abs(r.X - cur.X);
                if ((down ? r.Top < bestY : r.Top > bestY) || (r.Top == bestY && dx < bestDx))
                { bestY = r.Top; bestDx = dx; next = i; }
            }
        }
        if (next >= 0) { sel = next; EnsureVisible(); }
    }

    public void SelectFirst()
    {
        sel = -1;
        for (int i = 0; i < rows.Count; i++) if (!rows[i].IsHeader) { sel = i; break; }
        Invalidate();
    }

    void EnsureVisible()
    {
        Rectangle r = rows[sel].R;
        if (r.Top < scrollTarget) ScrollTo(r.Top - S(36), true);
        else if (r.Bottom > scrollTarget + Height) ScrollTo(r.Bottom - Height + S(8), true);
        Invalidate();
    }
}

// =====================================================================
//  Aperçu au survol
// =====================================================================
class PreviewForm : Form
{
    Bitmap img, placeholder;
    string title, line1, line2;
    int token;
    readonly Font fTitle = new Font("Segoe UI Semibold", 10.5f);
    readonly Font fLine = new Font("Segoe UI", 9f);

    public PreviewForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        AutoScaleMode = AutoScaleMode.None;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        Size = new Size(Ui.S(280), Ui.S(290));
    }

    protected override bool ShowWithoutActivation { get { return true; } }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= 0x80 | 0x08000000 | 0x20 | 0x8; // TOOLWINDOW | NOACTIVATE | TRANSPARENT | TOPMOST
            cp.ClassStyle |= 0x20000;
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Native.SetRoundCorners(Handle); }

    static string Describe(DesktopItem it, out string second)
    {
        second = null;
        try
        {
            if (it.Kind == ItemKind.App && it.Args != null) return L.T("Application du Microsoft Store", "Microsoft Store app");
            string p = it.Target;
            string ext = Path.GetExtension(p).ToLowerInvariant();
            if (Directory.Exists(p))
            {
                int n = 0;
                try { n = Directory.EnumerateFileSystemEntries(p).Take(1000).Count(); } catch { }
                second = L.T("Modifié le ", "Modified ") + Directory.GetLastWriteTime(p).ToString("g", L.Culture);
                return L.T("Dossier · ", "Folder · ") + (n >= 1000 ? "999+" : n.ToString()) + (n > 1 ? L.T(" éléments", " items") : L.T(" élément", " item"));
            }
            FileInfo fi = new FileInfo(p);
            second = L.T("Modifié le ", "Modified ") + fi.LastWriteTime.ToString("g", L.Culture);
            if (ext == ".lnk")
            {
                string t = ShellUtil.ResolveLnk(p);
                return string.IsNullOrEmpty(t) ? L.T("Raccourci", "Shortcut") : L.T("Raccourci vers ", "Shortcut to ") + t;
            }
            if (ext == ".url")
            {
                string u = ShellUtil.ReadUrl(p);
                return u ?? L.T("Lien", "Link");
            }
            return L.Size(fi.Length) + " · " + (ext.Length > 1 ? ext.Substring(1).ToUpperInvariant() : L.T("Fichier", "File"));
        }
        catch { return ""; }
    }

    public void ShowFor(DesktopItem it, Rectangle anchor, bool rightSide)
    {
        int my = ++token;
        title = it.Name;
        line1 = Describe(it, out line2);
        placeholder = it.Icon;
        if (img != null) { img.Dispose(); img = null; }
        Rectangle wa = Screen.FromRectangle(anchor).WorkingArea;
        int x = rightSide ? anchor.Left - Width - Ui.S(14) : anchor.Right + Ui.S(14);
        int y = Math.Max(wa.Top + Ui.S(8), Math.Min(wa.Bottom - Height - Ui.S(8), anchor.Top + anchor.Height / 2 - Height / 2));
        Location = new Point(x, y);
        if (!Visible) Show();
        Invalidate();
        if (!it.HasPath && !(it.Kind == ItemKind.App && it.Args != null)) return;
        string parsing = it.HasPath ? it.Target : it.Args;
        bool thumbs = it.HasPath;
        int px = Ui.S(256);
        Thread t = new Thread(() =>
        {
            Bitmap b = thumbs ? Icons.ShellImage(parsing, px, true) : null;
            if (b == null) b = Icons.ShellImage(parsing, px, false);
            try
            {
                BeginInvoke(new Action(() =>
                {
                    if (my == token && Visible) { img = b; Invalidate(); }
                    else if (b != null) b.Dispose();
                }));
            }
            catch { if (b != null) b.Dispose(); }
        });
        t.SetApartmentState(ApartmentState.STA);
        t.IsBackground = true;
        t.Start();
    }

    public void HidePreview()
    {
        token++;
        if (Visible) Hide();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(Theme.Card);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        int pad = Ui.S(12);
        Rectangle area = new Rectangle(pad, pad, Width - pad * 2, Ui.S(180));
        Ui.Fill(g, Theme.Surface, area, Ui.S(8));
        Bitmap b = img ?? placeholder;
        if (b != null)
        {
            float maxW = area.Width - Ui.S(8), maxH = area.Height - Ui.S(8);
            float sc = Math.Min(maxW / b.Width, maxH / b.Height);
            if (img == null) sc = Math.Min(sc, Ui.S(64) / (float)b.Width);
            int w = (int)(b.Width * sc), h = (int)(b.Height * sc);
            g.DrawImage(b, new Rectangle(area.X + (area.Width - w) / 2, area.Y + (area.Height - h) / 2, w, h));
        }
        int y = area.Bottom + Ui.S(10);
        TextRenderer.DrawText(g, title, fTitle, new Rectangle(pad, y, Width - pad * 2, Ui.S(22)), Theme.Fg,
            TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        y += Ui.S(24);
        if (!string.IsNullOrEmpty(line1))
            TextRenderer.DrawText(g, line1, fLine, new Rectangle(pad, y, Width - pad * 2, Ui.S(18)), Theme.FgDim,
                TextFormatFlags.PathEllipsis | TextFormatFlags.NoPrefix);
        y += Ui.S(20);
        if (!string.IsNullOrEmpty(line2))
            TextRenderer.DrawText(g, line2, fLine, new Rectangle(pad, y, Width - pad * 2, Ui.S(18)), Theme.FgDim,
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        using (Pen p = new Pen(Theme.Line)) g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
    }
}

// =====================================================================
//  Panneau latéral
// =====================================================================
class PanelForm : Form
{
    public readonly ItemsView View;
    readonly TextBox search;
    readonly PreviewForm preview = new PreviewForm();
    readonly Font fTitle = new Font("Segoe UI Semibold", 13f);
    readonly Font fCount = new Font("Segoe UI", 10f);
    readonly Font fClock = new Font("Segoe UI Semibold", 12f);
    readonly Font fSmall = new Font("Segoe UI", 8.25f);
    public List<DesktopItem> All = new List<DesktopItem>(), Apps = new List<DesktopItem>(), Recent = new List<DesktopItem>();
    List<DesktopItem> winSettings = new List<DesktopItem>(), commands = new List<DesktopItem>();
    public volatile bool Dirty = true;
    public bool Busy;
    public event EventHandler SettingsRequested;
    Bitmap glass;
    Rectangle rcGear, rcViewBtn, rcSearch, rcClock, rcDisk, rcBin;
    int hoverBtn = -1;
    readonly ContextMenuStrip menu;
    int loadedPx;
    bool loading, loadPending, loadedOnce;

    // widgets
    readonly System.Windows.Forms.Timer widgetTimer = new System.Windows.Forms.Timer();
    readonly System.Windows.Forms.Timer blurTimer = new System.Windows.Forms.Timer();
    long diskFree, diskTotal, binCount;
    int widgetTicks;

    // animation
    readonly System.Windows.Forms.Timer anim = new System.Windows.Forms.Timer();
    readonly Stopwatch sw = new Stopwatch();
    bool opening;
    int fromLeft, toLeft, openLeft, closedLeft;
    double fromOp, toOp;

    static int S(int v) { return Ui.S(v); }
    public int IconPx { get { return S(Settings.GridView ? 48 : 32); } }
    public bool IsOpen { get { return Visible && opening; } }
    int ListPx { get { return S(32); } }

    public PanelForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        KeyPreview = true;
        AutoScaleMode = AutoScaleMode.None;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

        search = new TextBox { BorderStyle = BorderStyle.None, Font = new Font("Segoe UI", 10.5f) };
        search.TextChanged += (s, e) => { Rebuild(false); if (search.Text.Trim().Length > 0) View.SelectFirst(); };
        search.KeyDown += SearchKeyDown;
        search.HandleCreated += (s, e) => SetCue();

        View = new ItemsView(this);
        View.ItemActivated += it => Launch(it, null);
        View.ItemContext += ShowItemMenu;
        View.ItemDropped += HandleDrop;
        View.FilesDropped += AddFilesToDesktop;
        View.HoverItem += (it, r) =>
        {
            if (!Settings.HoverPreview || Busy || menu.Visible) return;
            if (it.Kind == ItemKind.Desktop && it.IsSystem) return;
            if (it.Kind != ItemKind.Desktop && it.Kind != ItemKind.Recent && it.Kind != ItemKind.App) return;
            preview.ShowFor(it, r, Settings.RightSide);
        };
        View.HoverEnd += () => preview.HidePreview();

        Controls.Add(View);
        Controls.Add(search);

        menu = Menus.Create();
        menu.Opening += (s, e) => preview.HidePreview();

        anim.Interval = 10;
        anim.Tick += AnimTick;
        widgetTimer.Interval = 1000;
        widgetTimer.Tick += (s, e) => { if (++widgetTicks % 15 == 0) RefreshWidgetData(); Invalidate(rcClock); };
        blurTimer.Interval = 120;
        blurTimer.Tick += (s, e) => RefreshGlass();
        RefreshCatalog();
        ApplyTheme();
    }

    void SetCue()
    {
        Native.SendMessage(search.Handle, 0x1501, (IntPtr)1, L.T("Rechercher partout…", "Search everything…"));
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= 0x80;
            cp.ClassStyle |= 0x20000;
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Native.SetRoundCorners(Handle);
    }

    public void RefreshCatalog()
    {
        winSettings = SystemCatalog.Settings_(ListPx);
        commands = SystemCatalog.Commands(ListPx, () => { if (SettingsRequested != null) SettingsRequested(this, EventArgs.Empty); });
        if (search.IsHandleCreated) SetCue();
    }

    public void ApplyTheme()
    {
        search.BackColor = Theme.Surface;
        search.ForeColor = Theme.Fg;
        BackColor = Theme.Bg;
        LayoutChrome();
        Invalidate(true);
    }

    int X0 { get { return Settings.RightSide ? S(2) : 0; } }
    int CW { get { return Width - S(2); } }

    void LayoutChrome()
    {
        int x0 = X0, cw = CW;
        rcGear = new Rectangle(x0 + cw - S(12) - S(34), S(12), S(34), S(34));
        rcViewBtn = new Rectangle(rcGear.X - S(38), S(12), S(34), S(34));
        rcSearch = new Rectangle(x0 + S(12), S(58), cw - S(24), S(38));
        int th = search.PreferredHeight;
        search.Bounds = new Rectangle(rcSearch.X + S(36), rcSearch.Y + (rcSearch.Height - th) / 2 + S(1), rcSearch.Width - S(46), th);
        int top = S(106);
        if (Settings.Widgets)
        {
            int gap = S(8), w = (cw - S(24) - gap * 2) / 3, y = S(104);
            rcClock = new Rectangle(x0 + S(12), y, w, S(46));
            rcDisk = new Rectangle(rcClock.Right + gap, y, w, S(46));
            rcBin = new Rectangle(rcDisk.Right + gap, y, cw - S(12) - (rcDisk.Right + gap - x0), S(46));
            top = S(158);
        }
        else rcClock = rcDisk = rcBin = Rectangle.Empty;
        View.Bounds = new Rectangle(x0, top, cw, Math.Max(0, Height - top));
    }

    protected override void OnResize(EventArgs e) { base.OnResize(e); LayoutChrome(); }

    public void PaintBg(Graphics g, Rectangle area, Point offset)
    {
        if (glass != null)
            g.DrawImage(glass, area, new Rectangle(offset.X + area.X, offset.Y + area.Y, area.Width, area.Height), GraphicsUnit.Pixel);
        else
            using (SolidBrush b = new SolidBrush(Theme.Bg)) g.FillRectangle(b, area);
    }

    protected override void OnPaintBackground(PaintEventArgs e) { PaintBg(e.Graphics, ClientRectangle, Point.Empty); }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        int x0 = X0;
        string title = L.T("Bureau", "Desktop");
        TextRenderer.DrawText(g, title, fTitle, new Point(x0 + S(16), S(16)), Theme.Fg, TextFormatFlags.NoPrefix);
        int tw = TextRenderer.MeasureText(title, fTitle).Width;
        string count = loading && !loadedOnce ? L.T("Chargement…", "Loading…") : All.Count + L.T(" éléments", " items");
        TextRenderer.DrawText(g, count, fCount, new Point(x0 + S(16) + tw, S(20)), Theme.FgDim, TextFormatFlags.NoPrefix);

        if (hoverBtn == 0) Ui.Fill(g, Theme.HoverOverlay, rcViewBtn, S(8));
        if (hoverBtn == 1) Ui.Fill(g, Theme.HoverOverlay, rcGear, S(8));
        Ui.Glyph(g, Settings.GridView ? "" : "", 11f, hoverBtn == 0 ? Theme.Accent : Theme.FgDim, rcViewBtn);
        Ui.Glyph(g, "", 11f, hoverBtn == 1 ? Theme.Accent : Theme.FgDim, rcGear);

        Ui.Fill(g, Theme.Surface, rcSearch, S(8));
        Ui.Glyph(g, "", 10f, Theme.FgDim, new Rectangle(rcSearch.X + S(6), rcSearch.Y, S(28), rcSearch.Height));

        if (Settings.Widgets) DrawWidgets(g);

        using (SolidBrush b = new SolidBrush(Theme.Accent))
            g.FillRectangle(b, Settings.RightSide ? 0 : Width - S(2), 0, S(2), Height);
    }

    void DrawWidgets(Graphics g)
    {
        Rectangle[] rs = { rcClock, rcDisk, rcBin };
        for (int i = 0; i < 3; i++) Ui.Fill(g, hoverBtn == i + 2 && i > 0 ? Theme.SelOverlay : Theme.HoverOverlay, rs[i], S(8));
        DateTime now = DateTime.Now;
        TextRenderer.DrawText(g, now.ToString("HH:mm", L.Culture), fClock, new Rectangle(rcClock.X, rcClock.Y + S(3), rcClock.Width, S(24)), Theme.Fg,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        TextRenderer.DrawText(g, now.ToString("ddd d MMM", L.Culture), fSmall, new Rectangle(rcClock.X, rcClock.Y + S(25), rcClock.Width, S(18)), Theme.FgDim,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);

        Ui.Glyph(g, "", 9f, Theme.FgDim, new Rectangle(rcDisk.X + S(6), rcDisk.Y + S(4), S(18), S(20)));
        TextRenderer.DrawText(g, diskTotal > 0 ? L.Size(diskFree) : "—", fSmall, new Rectangle(rcDisk.X + S(24), rcDisk.Y + S(4), rcDisk.Width - S(28), S(20)), Theme.Fg,
            TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        Rectangle bar = new Rectangle(rcDisk.X + S(8), rcDisk.Y + S(28), rcDisk.Width - S(16), S(5));
        Ui.Fill(g, Theme.Track, bar, S(2));
        if (diskTotal > 0)
        {
            float used = 1f - diskFree / (float)diskTotal;
            Color c = used > 0.9f ? Theme.Danger : Theme.Accent;
            Ui.Fill(g, c, new RectangleF(bar.X, bar.Y, Math.Max(S(4), bar.Width * used), bar.Height), S(2));
        }
        TextRenderer.DrawText(g, L.T("libres", "free"), fSmall, new Rectangle(rcDisk.X, rcDisk.Y + S(32), rcDisk.Width, S(14)), Theme.FgDim,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix);

        Ui.Glyph(g, "", 9f, Theme.FgDim, new Rectangle(rcBin.X + S(6), rcBin.Y + S(4), S(18), S(20)));
        TextRenderer.DrawText(g, L.T("Corbeille", "Bin"), fSmall, new Rectangle(rcBin.X + S(24), rcBin.Y + S(4), rcBin.Width - S(28), S(20)), Theme.Fg,
            TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        string bt = binCount == 0 ? L.T("vide", "empty") : binCount + L.T(" élém.", " items");
        TextRenderer.DrawText(g, bt, fSmall, new Rectangle(rcBin.X, rcBin.Y + S(24), rcBin.Width, S(18)), Theme.FgDim,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }

    void RefreshWidgetData()
    {
        try
        {
            DriveInfo d = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory));
            diskFree = d.AvailableFreeSpace;
            diskTotal = d.TotalSize;
        }
        catch { }
        long size;
        Native.RecycleBinInfo(out binCount, out size);
        if (Visible) Invalidate(Rectangle.Union(rcDisk, rcBin));
    }

    int HitButton(Point p)
    {
        if (rcViewBtn.Contains(p)) return 0;
        if (rcGear.Contains(p)) return 1;
        if (Settings.Widgets)
        {
            if (rcClock.Contains(p)) return 2;
            if (rcDisk.Contains(p)) return 3;
            if (rcBin.Contains(p)) return 4;
        }
        return -1;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int h = HitButton(e.Location);
        if (h != hoverBtn) { hoverBtn = h; Invalidate(); }
        Cursor = h >= 0 && h != 2 ? Cursors.Hand : rcSearch.Contains(e.Location) ? Cursors.IBeam : Cursors.Default;
    }

    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); if (hoverBtn != -1) { hoverBtn = -1; Invalidate(); } }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        int h = HitButton(e.Location);
        if (h == 0 && e.Button == MouseButtons.Left) { Settings.GridView = !Settings.GridView; Settings.Notify(); }
        else if (h == 1 && e.Button == MouseButtons.Left) { HidePanel(); if (SettingsRequested != null) SettingsRequested(this, EventArgs.Empty); }
        else if (h == 3) { HidePanel(); try { ShellUtil.Open("explorer.exe", "shell:MyComputerFolder"); } catch { } }
        else if (h == 4 && e.Button == MouseButtons.Left) { HidePanel(); try { ShellUtil.Open("explorer.exe", "shell:RecycleBinFolder"); } catch { } }
        else if (h == 4 && e.Button == MouseButtons.Right)
        {
            menu.Items.Clear();
            menu.Items.Add(Menus.Item(L.T("Ouvrir la corbeille", "Open Recycle Bin"), "", () => { HidePanel(); ShellUtil.Open("explorer.exe", "shell:RecycleBinFolder"); }));
            ToolStripMenuItem empty = Menus.Item(L.T("Vider la corbeille", "Empty Recycle Bin"), "", () =>
            {
                Busy = true;
                Native.SHEmptyRecycleBin(Handle, null, 0);
                Busy = false;
                RefreshWidgetData();
            });
            empty.Enabled = binCount > 0;
            menu.Items.Add(empty);
            menu.Show(this, e.Location);
        }
        else if (rcSearch.Contains(e.Location)) search.Focus();
    }

    protected override void OnMouseWheel(MouseEventArgs e) { base.OnMouseWheel(e); View.WheelFromParent(e.Delta); }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            if (search.Text.Length > 0) search.Text = ""; else HidePanel();
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        if (!Busy && !menu.Visible) HidePanel();
    }

    // ---------- Chargement en arrière-plan ----------
    public void RequestLoad()
    {
        if (loading) { loadPending = true; return; }
        loading = true;
        Dirty = false;
        int px = IconPx, lpx = ListPx;
        bool sys = Settings.ShowSystem, apps = Settings.SearchApps, rec = Settings.SearchRecent;
        Thread t = new Thread(() =>
        {
            List<DesktopItem> desk = Loader.Desktop(px, sys);
            Post(() => { All = desk; loadedPx = px; loadedOnce = true; Rebuild(true); });
            List<DesktopItem> a = apps ? Loader.Apps(lpx) : new List<DesktopItem>();
            List<DesktopItem> r = rec ? Loader.Recent(lpx) : new List<DesktopItem>();
            Post(() =>
            {
                Apps = a;
                Recent = r;
                loading = false;
                if (search.Text.Trim().Length > 0) Rebuild(true);
                if (loadPending) { loadPending = false; RequestLoad(); }
            });
        });
        t.SetApartmentState(ApartmentState.STA);
        t.IsBackground = true;
        t.Start();
    }

    void Post(Action a)
    {
        try { if (IsHandleCreated) BeginInvoke(a); } catch { }
    }

    public void OnSettingsChanged()
    {
        if (IconPx != loadedPx) Dirty = true;
        RefreshCatalog();
        ApplyTheme();
        if (Dirty) RequestLoad();
        Rebuild(true);
    }

    public void ReloadNow() { Dirty = true; RequestLoad(); }

    public void Rebuild(bool keepScroll)
    {
        string q = search.Text.Trim();
        View.ForceList = q.Length > 0;
        View.EmptyText = q.Length > 0 ? L.T("Aucun résultat pour « ", "No results for \"") + q + L.T(" »", "\"")
                                      : loading && !loadedOnce ? L.T("Chargement…", "Loading…") : L.T("Ton bureau est vide", "Your desktop is empty");
        View.SetGroups(q.Length > 0 ? SearchGroups(q) : BuildGroups(), keepScroll);
        Invalidate();
    }

    List<Group> BuildGroups()
    {
        List<Group> res = new List<Group>();
        Dictionary<string, DesktopItem> byKey = new Dictionary<string, DesktopItem>(StringComparer.OrdinalIgnoreCase);
        foreach (DesktopItem it in All) byKey[it.Key] = it;

        Group fav = new Group { Key = "fav", Title = L.T("Favoris", "Favorites"), Glyph = "", GlyphColor = Theme.Accent };
        HashSet<string> favKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string k in Settings.Favorites)
        {
            DesktopItem it;
            if (byKey.TryGetValue(k, out it) && favKeys.Add(k)) fav.Items.Add(it);
        }
        if (fav.Items.Count > 0) res.Add(fav);

        if (Settings.ShowRecents)
        {
            Group rec = new Group { Key = "recent", Title = L.T("Récents", "Recent"), Glyph = "" };
            rec.Items = All.Where(i => Settings.Usage.ContainsKey(i.Key) && !favKeys.Contains(i.Key))
                           .OrderByDescending(i => Settings.Usage[i.Key][1]).Take(5).ToList();
            if (rec.Items.Count > 0) res.Add(rec);
        }

        Dictionary<string, int> order = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < Settings.Order.Count; i++) order[Settings.Order[i]] = i;
        Comparison<DesktopItem> cmp = (a, b) =>
        {
            int ia, ib;
            bool ha = order.TryGetValue(a.Key, out ia), hb = order.TryGetValue(b.Key, out ib);
            if (ha && hb) return ia.CompareTo(ib);
            if (ha) return -1;
            if (hb) return 1;
            if (a.IsSystem != b.IsSystem) return a.IsSystem ? -1 : 1;
            if (a.IsFolder != b.IsFolder) return a.IsFolder ? -1 : 1;
            return string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
        };
        List<DesktopItem> rest = All.Where(i => !favKeys.Contains(i.Key)).ToList();
        if (Settings.GroupCats)
        {
            foreach (string c in Catalog.AllCats())
            {
                Group g = new Group { Key = "cat:" + c, Title = Catalog.Display(c), Glyph = Catalog.GlyphOf(c), GlyphColor = Catalog.ColorOf(c) };
                g.Items = rest.Where(i => string.Equals(i.Category, c, StringComparison.OrdinalIgnoreCase)).ToList();
                g.Items.Sort(cmp);
                if (g.Items.Count > 0) res.Add(g);
            }
        }
        else
        {
            Group g = new Group { Key = "all", Title = res.Count > 0 ? L.T("Tous les éléments", "All items") : null, Glyph = "" };
            g.Items = rest;
            g.Items.Sort(cmp);
            res.Add(g);
        }
        return res;
    }

    // ---------- Recherche universelle ----------
    static List<KeyValuePair<DesktopItem, int>> Rank(IEnumerable<DesktopItem> list, string nq, int max)
    {
        List<KeyValuePair<DesktopItem, int>> res = new List<KeyValuePair<DesktopItem, int>>();
        foreach (DesktopItem it in list)
        {
            int s = Search.Score(nq, it.Name);
            if (it.Keywords != null)
                foreach (string kw in it.Keywords.Split(' '))
                    if (kw.Length > 0) s = Math.Max(s, Search.Score(nq, kw) - 150);
            if (s < 0) continue;
            long[] u;
            if (Settings.Usage.TryGetValue(it.Key, out u)) s += (int)Math.Min(u[0] * 15, 120);
            res.Add(new KeyValuePair<DesktopItem, int>(it, s));
        }
        return res.OrderByDescending(k => k.Value).ThenBy(k => k.Key.Name, StringComparer.CurrentCultureIgnoreCase).Take(max).ToList();
    }

    List<Group> SearchGroups(string q)
    {
        List<Group> res = new List<Group>();
        int px = ListPx;
        if (q.StartsWith("?"))
        {
            string w = q.Substring(1).Trim();
            if (w.Length > 0) res.Add(WebGroup(w, px, true));
            return res;
        }
        if (q.StartsWith(">"))
        {
            string c = Search.Norm(q.Substring(1));
            Group g = new Group { Key = "s:cmd", Title = L.T("Commandes", "Commands"), Glyph = "" };
            g.Items = c.Length == 0 ? commands.ToList() : Rank(commands, c, 20).Select(k => k.Key).ToList();
            if (g.Items.Count > 0) res.Add(g);
            return res;
        }
        string expr = q.StartsWith("=") ? q.Substring(1) : q;
        double v;
        if ((q.StartsWith("=") || Calc.LooksLikeMath(q)) && Calc.TryEval(expr, out v))
        {
            string r = Calc.Format(v);
            res.Add(new Group
            {
                Key = "s:calc", Title = L.T("Calcul", "Calculator"), Glyph = "",
                Items = { new DesktopItem { Kind = ItemKind.Calc, Key = "calc", Name = "= " + r, Args = r,
                    Detail = L.T("Entrée pour copier le résultat", "Press Enter to copy the result"),
                    Icon = IconCache.Tile("=", "Segoe UI", Color.FromArgb(22, 163, 74), px) } }
            });
        }
        string nq = Search.Norm(q);
        if (nq.Length > 0)
        {
            List<KeyValuePair<Group, int>> found = new List<KeyValuePair<Group, int>>();
            Action<string, string, string, IEnumerable<DesktopItem>, int> addGroup = (key, title, glyph, src, max) =>
            {
                List<KeyValuePair<DesktopItem, int>> r = Rank(src, nq, max);
                if (r.Count == 0) return;
                Group g = new Group { Key = key, Title = title, Glyph = glyph, Items = r.Select(k => k.Key).ToList() };
                found.Add(new KeyValuePair<Group, int>(g, r[0].Value));
            };
            addGroup("s:desk", L.T("Bureau", "Desktop"), "", All, 12);
            HashSet<string> deskNames = new HashSet<string>(All.Select(i => Search.Norm(i.Name)));
            if (Settings.SearchApps) addGroup("s:apps", L.T("Applications", "Apps"), "", Apps.Where(a => !deskNames.Contains(Search.Norm(a.Name))), 8);
            if (Settings.SearchRecent) addGroup("s:recent", L.T("Fichiers récents", "Recent files"), "", Recent, 6);
            if (Settings.SearchSettings) addGroup("s:set", L.T("Paramètres Windows", "Windows settings"), "", winSettings, 5);
            if (Settings.SearchCommands) addGroup("s:cmd", L.T("Commandes", "Commands"), "", commands, 4);
            res.AddRange(found.OrderByDescending(k => k.Value).Select(k => k.Key));
        }
        if (Settings.WebEngine != 3 && !res.Any(g => g.Key == "s:calc")) res.Add(WebGroup(q, px, false));
        return res;
    }

    static string EngineName { get { return new[] { "Google", "Bing", "DuckDuckGo", "" }[Settings.WebEngine]; } }

    static string EngineUrl(string q)
    {
        string e = Uri.EscapeDataString(q);
        switch (Settings.WebEngine)
        {
            case 1: return "https://www.bing.com/search?q=" + e;
            case 2: return "https://duckduckgo.com/?q=" + e;
            default: return "https://www.google.com/search?q=" + e;
        }
    }

    Group WebGroup(string q, int px, bool forced)
    {
        string engine = Settings.WebEngine == 3 ? "Google" : EngineName;
        string url = forced && Settings.WebEngine == 3 ? "https://www.google.com/search?q=" + Uri.EscapeDataString(q) : EngineUrl(q);
        return new Group
        {
            Key = "s:web", Title = "Web", Glyph = "",
            Items = { new DesktopItem {
                Kind = ItemKind.Web, Key = "web", Name = L.T("Rechercher « " + q + " » sur " + engine, "Search " + engine + " for \"" + q + "\""),
                Detail = url, Icon = IconCache.Tile("", Ui.GlyphFont, Settings.Accent, px), Run = () => ShellUtil.Open(url, null) } }
        };
    }

    // ---------- Organisation par glisser-déposer ----------
    void HandleDrop(DesktopItem it, Group from, Group to, int index)
    {
        if (to.Key == "recent" || to.IsSearch) return;
        if (to.Key == "fav")
        {
            int cur = Settings.FavIndex(it.Key);
            if (cur >= 0) { Settings.Favorites.RemoveAt(cur); if (cur < index) index--; }
            index = Math.Max(0, Math.Min(Settings.Favorites.Count, index));
            Settings.Favorites.Insert(index, it.Key);
        }
        else
        {
            int fi = Settings.FavIndex(it.Key);
            if (fi >= 0) Settings.Favorites.RemoveAt(fi);
            if (to.Key.StartsWith("cat:")) SetCategory(it, to.Key.Substring(4), false);
            List<string> keys = to.Items.Select(i => i.Key).ToList();
            int cur = keys.FindIndex(k => string.Equals(k, it.Key, StringComparison.OrdinalIgnoreCase));
            if (cur >= 0) { keys.RemoveAt(cur); if (cur < index) index--; }
            index = Math.Max(0, Math.Min(keys.Count, index));
            keys.Insert(index, it.Key);
            HashSet<string> ks = new HashSet<string>(keys, StringComparer.OrdinalIgnoreCase);
            Settings.Order.RemoveAll(k => ks.Contains(k));
            Settings.Order.AddRange(keys);
        }
        Settings.SaveQuiet();
        Rebuild(true);
    }

    static void SetCategory(DesktopItem it, string cat, bool save)
    {
        if (string.Equals(cat, it.AutoCategory, StringComparison.OrdinalIgnoreCase)) Settings.CatOverride.Remove(it.Key);
        else Settings.CatOverride[it.Key] = cat;
        if (save) Settings.SaveQuiet();
    }

    // ---------- Actions ----------
    void Launch(DesktopItem it, string verb)
    {
        if (it.Kind == ItemKind.Calc)
        {
            try { Clipboard.SetText(it.Args); } catch { }
            HidePanel();
            return;
        }
        if (it.Run != null)
        {
            HidePanel();
            try { it.Run(); } catch (Exception ex) { ShowError(ex.Message); }
            return;
        }
        Settings.RecordUse(it.Key);
        HidePanel();
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo(it.Target);
            if (it.Args != null) psi.Arguments = it.Args;
            psi.UseShellExecute = true;
            if (verb != null) psi.Verb = verb;
            if (it.HasPath)
            {
                string wd = Path.GetDirectoryName(it.Target);
                if (!string.IsNullOrEmpty(wd)) psi.WorkingDirectory = wd;
            }
            Process.Start(psi);
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            if (ex.NativeErrorCode != 1223) ShowError(L.T("Impossible d'ouvrir « ", "Can't open \"") + it.Name + L.T(" » :\n", "\":\n") + ex.Message);
        }
        catch (Exception ex) { ShowError(L.T("Impossible d'ouvrir « ", "Can't open \"") + it.Name + L.T(" » :\n", "\":\n") + ex.Message); }
        Rebuild(true);
    }

    static void ShowError(string msg)
    {
        MessageBox.Show(msg, "BureauCache", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    void ShowItemMenu(DesktopItem it, Point pt)
    {
        menu.Items.Clear();
        ToolStripMenuItem open = Menus.Item(it.Kind == ItemKind.Calc ? L.T("Copier le résultat", "Copy result") : L.T("Ouvrir", "Open"), "", () => Launch(it, null));
        open.Font = new Font(menu.Font, FontStyle.Bold);
        menu.Items.Add(open);
        if (it.HasPath && Catalog.IsExecutable(it.Target))
            menu.Items.Add(Menus.Item(L.T("Exécuter en tant qu'administrateur", "Run as administrator"), "", () => Launch(it, "runas")));
        if (it.HasPath)
            menu.Items.Add(Menus.Item(L.T("Ouvrir l'emplacement du fichier", "Open file location"), "", () =>
            {
                HidePanel();
                try { Process.Start("explorer.exe", "/select,\"" + it.Target + "\""); } catch { }
            }));
        if (it.Kind == ItemKind.Desktop)
        {
            menu.Items.Add(new ToolStripSeparator());
            bool fav = Settings.FavIndex(it.Key) >= 0;
            menu.Items.Add(Menus.Item(fav ? L.T("Retirer des favoris", "Remove from favorites") : L.T("Ajouter aux favoris", "Add to favorites"), fav ? "" : "", () =>
            {
                if (fav) Settings.Favorites.RemoveAt(Settings.FavIndex(it.Key)); else Settings.Favorites.Add(it.Key);
                Settings.SaveQuiet();
                Rebuild(true);
            }));
        }
        if (it.IsDesktopFile)
        {
            ToolStripMenuItem mv = Menus.Sub(menu, L.T("Déplacer vers", "Move to"), "");
            foreach (string cat in Catalog.AllCats())
            {
                string c = cat;
                ToolStripMenuItem ci = Menus.Item(Catalog.Display(c), Catalog.GlyphOf(c), () => { SetCategory(it, c, true); Rebuild(true); });
                ci.Checked = string.Equals(it.Category, c, StringComparison.OrdinalIgnoreCase);
                mv.DropDownItems.Add(ci);
            }
            mv.DropDownItems.Add(new ToolStripSeparator());
            mv.DropDownItems.Add(Menus.Item(L.T("Nouvelle catégorie…", "New category…"), "", () =>
            {
                HidePanel();
                string name = CategoryDialog.Run(null);
                if (name == null) return;
                SetCategory(it, name, false);
                Settings.Notify();
            }));
            mv.DropDownItems.Add(Menus.Item(L.T("Automatique (", "Automatic (") + Catalog.Display(it.AutoCategory) + ")", null, () =>
            {
                Settings.CatOverride.Remove(it.Key);
                Settings.SaveQuiet();
                Rebuild(true);
            }));
            menu.Items.Add(mv);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Menus.Item(L.T("Renommer…", "Rename…"), "", () => Rename(it)));
            menu.Items.Add(Menus.Item(L.T("Supprimer", "Delete"), "", () => Delete(it)));
        }
        if (it.HasPath)
        {
            menu.Items.Add(Menus.Item(L.T("Propriétés", "Properties"), "", () => { HidePanel(); ShellUtil.Properties(it.Target); }));
            ToolStripMenuItem more = Menus.Sub(menu, L.T("Plus d'options", "More options"), "");
            more.DropDownItems.Add(new ToolStripMenuItem(L.T("Chargement…", "Loading…")) { Enabled = false });
            more.DropDownOpening += (s, e) =>
            {
                more.DropDownItems.Clear();
                foreach (KeyValuePair<string, object> v in ShellUtil.Verbs(it.Target))
                {
                    object verb = v.Value;
                    more.DropDownItems.Add(new ToolStripMenuItem(v.Key, null, (s2, e2) => { HidePanel(); ShellUtil.InvokeVerb(verb); Dirty = true; }));
                }
                if (more.DropDownItems.Count == 0) more.DropDownItems.Add(new ToolStripMenuItem(L.T("Aucune option", "No options")) { Enabled = false });
            };
            menu.Items.Add(more);
        }
        menu.Show(View, pt);
    }

    void Rename(DesktopItem it)
    {
        HidePanel();
        string ext = Path.GetExtension(it.Target);
        bool hiddenExt = !it.IsFolder && (ext.Equals(".lnk", StringComparison.OrdinalIgnoreCase) || ext.Equals(".url", StringComparison.OrdinalIgnoreCase));
        using (RenameDialog d = new RenameDialog(it.Name, !it.IsFolder && !hiddenExt))
        {
            if (d.ShowDialog() != DialogResult.OK || d.Value == it.Name) return;
            string newPath = Path.Combine(Path.GetDirectoryName(it.Target), d.Value + (hiddenExt ? ext : ""));
            try
            {
                if (it.IsFolder) Directory.Move(it.Target, newPath); else File.Move(it.Target, newPath);
                Settings.RenameKey(it.Key, newPath);
                ReloadNow();
            }
            catch (Exception ex) { ShowError(L.T("Impossible de renommer :\n", "Can't rename:\n") + ex.Message); }
        }
    }

    void Delete(DesktopItem it)
    {
        HidePanel();
        try
        {
            if (it.IsFolder)
                Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(it.Target, Microsoft.VisualBasic.FileIO.UIOption.AllDialogs,
                    Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
            else
                Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(it.Target, Microsoft.VisualBasic.FileIO.UIOption.AllDialogs,
                    Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
            Settings.RemoveKey(it.Key);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { ShowError(L.T("Impossible de supprimer :\n", "Can't delete:\n") + ex.Message); }
        ReloadNow();
    }

    void AddFilesToDesktop(string[] files, DragDropEffects eff)
    {
        string desk = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        Busy = true;
        foreach (string f0 in files)
        {
            string f = f0.TrimEnd('\\');
            string dir = Path.GetDirectoryName(f);
            if (dir != null && string.Equals(dir.TrimEnd('\\'), desk.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) continue;
            string name = Path.GetFileName(f);
            if (string.IsNullOrEmpty(name)) name = f.Replace(":", "");
            string dest = Path.Combine(desk, name);
            try
            {
                if (eff == DragDropEffects.Link)
                {
                    string lnk = Path.Combine(desk, name + ".lnk");
                    for (int i = 2; File.Exists(lnk); i++) lnk = Path.Combine(desk, name + " (" + i + ").lnk");
                    ShellUtil.CreateShortcut(lnk, f, null);
                }
                else if (Directory.Exists(f))
                {
                    if (eff == DragDropEffects.Move) Microsoft.VisualBasic.FileIO.FileSystem.MoveDirectory(f, dest, Microsoft.VisualBasic.FileIO.UIOption.AllDialogs);
                    else Microsoft.VisualBasic.FileIO.FileSystem.CopyDirectory(f, dest, Microsoft.VisualBasic.FileIO.UIOption.AllDialogs);
                }
                else
                {
                    if (eff == DragDropEffects.Move) Microsoft.VisualBasic.FileIO.FileSystem.MoveFile(f, dest, Microsoft.VisualBasic.FileIO.UIOption.AllDialogs);
                    else Microsoft.VisualBasic.FileIO.FileSystem.CopyFile(f, dest, Microsoft.VisualBasic.FileIO.UIOption.AllDialogs);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { ShowError(L.T("Impossible d'ajouter « ", "Can't add \"") + name + L.T(" » :\n", "\":\n") + ex.Message); }
        }
        Busy = false;
        ReloadNow();
    }

    void SearchKeyDown(object sender, KeyEventArgs e)
    {
        bool emptyText = search.Text.Length == 0;
        if (e.KeyCode == Keys.Down || e.KeyCode == Keys.Up || (Settings.GridView && emptyText && (e.KeyCode == Keys.Left || e.KeyCode == Keys.Right)))
        {
            View.MoveSelection(e.KeyCode);
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == Keys.Enter)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            if (View.Selected == null) View.SelectFirst();
            if (View.Selected != null) Launch(View.Selected, e.Control && e.Shift ? "runas" : null);
        }
    }

    // ---------- Effet verre ----------
    Bitmap MakeGlass(Rectangle area)
    {
        try
        {
            Bitmap cap = new Bitmap(area.Width, area.Height, PixelFormat.Format32bppRgb);
            using (Graphics g = Graphics.FromImage(cap)) g.CopyFromScreen(area.Location, Point.Empty, area.Size);
            int sw2 = Math.Max(2, area.Width / 22), sh2 = Math.Max(2, area.Height / 22);
            Bitmap small = new Bitmap(sw2, sh2);
            using (Graphics g = Graphics.FromImage(small))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                g.DrawImage(cap, new Rectangle(0, 0, sw2, sh2));
            }
            cap.Dispose();
            Bitmap res = new Bitmap(area.Width, area.Height, PixelFormat.Format32bppRgb);
            using (Graphics g = Graphics.FromImage(res))
            using (ImageAttributes ia = new ImageAttributes())
            {
                ia.SetWrapMode(WrapMode.TileFlipXY);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(small, new Rectangle(0, 0, area.Width, area.Height), 0, 0, sw2, sh2, GraphicsUnit.Pixel, ia);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(Settings.Dark ? 196 : 200, Theme.Bg))) g.FillRectangle(b, 0, 0, area.Width, area.Height);
            }
            small.Dispose();
            return res;
        }
        catch { return null; }
    }

    bool liveBlurActive;

    void RefreshGlass()
    {
        if (!Visible || !liveBlurActive) { blurTimer.Stop(); return; }
        Bitmap g = MakeGlass(new Rectangle(openLeft, Top, Width, Height));
        if (g == null) return;
        Bitmap old = glass;
        glass = g;
        if (old != null) old.Dispose();
        Invalidate(true);
    }

    // ---------- Affichage / animations ----------
    public void ShowPanel(Screen scr)
    {
        if (IsOpen) { Activate(); return; }
        if (Visible) { StartAnim(true); Activate(); search.Focus(); return; }
        if (Dirty) RequestLoad();
        Rectangle wa = scr.WorkingArea;
        Width = S(Settings.PanelWidth);
        Height = wa.Height;
        Top = wa.Top;
        openLeft = Settings.RightSide ? wa.Right - Width : wa.Left;
        closedLeft = Settings.RightSide ? wa.Right - Width + S(40) : wa.Left - S(40);
        if (glass != null) { glass.Dispose(); glass = null; }
        liveBlurActive = Settings.Glass && Settings.LiveBlur;
        // Exclut le panneau des captures : permet de capturer ce qu'il y a derrière en continu
        try { Native.SetWindowDisplayAffinity(Handle, liveBlurActive ? 0x11u : 0u); } catch { liveBlurActive = false; }
        if (Settings.Glass) glass = MakeGlass(new Rectangle(openLeft, Top, Width, Height));
        if (Settings.Widgets) { RefreshWidgetData(); widgetTimer.Start(); }
        ApplyTheme();
        if (search.Text.Length > 0) search.Text = "";
        else Rebuild(true);
        Left = Settings.Animations ? closedLeft : openLeft;
        Opacity = Settings.Animations ? 0 : 1;
        Show();
        Activate();
        Native.SetForegroundWindow(Handle);
        search.Focus();
        StartAnim(true);
        if (liveBlurActive) blurTimer.Start();
    }

    public void HidePanel()
    {
        preview.HidePreview();
        if (!Visible || !opening) return;
        StartAnim(false);
    }

    void StartAnim(bool open)
    {
        opening = open;
        fromLeft = Left;
        toLeft = open ? openLeft : closedLeft;
        fromOp = Opacity;
        toOp = open ? 1 : 0;
        if (!Settings.Animations) { Left = open ? openLeft : closedLeft; Opacity = 1; if (!open) Finish(); anim.Stop(); return; }
        sw.Restart();
        anim.Start();
    }

    void Finish()
    {
        Hide();
        widgetTimer.Stop();
        blurTimer.Stop();
    }

    void AnimTick(object sender, EventArgs e)
    {
        double dur = opening ? 180.0 : 120.0;
        double t = Math.Min(1, sw.ElapsedMilliseconds / dur);
        double k = 1 - Math.Pow(1 - t, 3);
        Left = (int)Math.Round(fromLeft + (toLeft - fromLeft) * k);
        Opacity = fromOp + (toOp - fromOp) * k;
        if (t >= 1)
        {
            anim.Stop();
            if (!opening) { Finish(); Opacity = 1; }
        }
    }

    public bool MenuOpen { get { return menu.Visible; } }
}

// =====================================================================
//  Fenêtre de paramètres
// =====================================================================
class SettingsActions
{
    public Action Preview, Quit, Install, Uninstall, InstallUpdate;
    public Func<bool> HotkeyOk;
}

class SettingsForm : Form
{
    readonly SettingsActions act;
    readonly Font fPage = new Font("Segoe UI Semibold", 17f);
    readonly Font fSection = new Font("Segoe UI Semibold", 8.75f);
    readonly Font fRow = new Font("Segoe UI", 10.5f);
    readonly Font fDesc = new Font("Segoe UI", 8.75f);
    readonly HashSet<Label> dim = new HashSet<Label>();
    readonly List<Panel> pages = new List<Panel>();
    readonly SideNav nav;
    readonly int W, H, NavW;

    Toggle tHide, tStartup, tAuto, tFull, tHot, tGlass, tLive, tAnim, tWidgets, tPreview, tSystem, tRecents, tGroups,
        tSApps, tSRecent, tSSettings, tSCmd, tNew, tArchive, tAutoUpd;
    Segmented sSide, sZone, sScreen, sTheme, sView, sLang, sEngine, sNewDays, sArchDays;
    Slider slDelay, slWidth;
    Swatches swatch;
    HotkeyBox hk;
    InputBox repo;
    Label lDelay, lWidth, lHotStatus, lInstall, lArchive, lUpdate;
    FlatButton bInstall, bArchiveNow, bCheck;
    Card customCard;
    string customSig;

    Panel page;
    Card cur;
    int y, rowY;
    bool loading;

    static int S(int v) { return Ui.S(v); }
    static string T(string fr, string en) { return L.T(fr, en); }
    static readonly int[] NewDaysOpts = { 1, 3, 7 };
    static readonly int[] ArchiveOpts = { 30, 60, 90, 180 };

    public int CurrentPage { get { return nav.Selected; } }

    public SettingsForm(SettingsActions act)
    {
        this.act = act;
        W = S(820); H = S(700); NavW = S(220);
        Text = "BureauCache – " + T("Paramètres", "Settings");
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.None;
        ClientSize = new Size(W, H);
        Icon = Ui.MakeIcon(Settings.Accent);
        KeyPreview = true;

        nav = new SideNav(new[] { T("Général", "General"), T("Ouverture", "Opening"), T("Apparence", "Appearance"),
                                  T("Recherche", "Search"), T("Organisation", "Organization"), T("Avancé", "Advanced") },
                          new[] { "", "", "", "", "", "" });
        nav.Bounds = new Rectangle(0, 0, NavW, H);
        nav.Cursor = Cursors.Default;
        nav.SelectedChanged += ShowPage;
        Controls.Add(nav);

        FlatButton bQuit = new FlatButton(T("Quitter BureauCache", "Quit BureauCache"), false, NavW - S(32));
        bQuit.Location = new Point(S(16), H - S(16) - bQuit.Height);
        bQuit.Click += (s, e) => { if (act.Quit != null) act.Quit(); };
        FlatButton bPrev = new FlatButton(T("Aperçu du panneau", "Preview panel"), true, NavW - S(32));
        bPrev.Location = new Point(S(16), bQuit.Top - S(8) - bPrev.Height);
        bPrev.Click += (s, e) => { if (act.Preview != null) act.Preview(); };
        nav.Controls.Add(bQuit);
        nav.Controls.Add(bPrev);

        BuildGeneral();
        BuildOpening();
        BuildLook();
        BuildSearch();
        BuildOrganize();
        BuildAdvanced();

        LoadValues();
        ShowPage(0);
        Restyle();
        Settings.Changed += OnSettingsChanged;
    }

    protected override void Dispose(bool disposing)
    {
        Settings.Changed -= OnSettingsChanged;
        base.Dispose(disposing);
    }

    // ---------- Construction ----------
    void NewPage(string title)
    {
        page = new BufferedPanel { Bounds = new Rectangle(NavW, 0, W - NavW, H), AutoScroll = true, Visible = false };
        page.AutoScrollMargin = new Size(0, S(24));
        page.Controls.Add(new Label { Text = title, Font = fPage, AutoSize = true, Location = new Point(S(28), S(24)) });
        pages.Add(page);
        Controls.Add(page);
        y = S(80);
    }

    void Section(string title)
    {
        Label l = new Label { Text = title, Font = fSection, AutoSize = true, Location = new Point(S(32), y) };
        dim.Add(l);
        page.Controls.Add(l);
        y += S(24);
        cur = new Card { Location = new Point(S(24), y), Width = page.Width - S(48) - S(14) };
        page.Controls.Add(cur);
        rowY = 0;
    }

    Label Row(string title, string desc, Control ctl, Label value, Control lead = null)
    {
        if (rowY > 0) cur.Separators.Add(rowY);
        int h = desc != null ? S(62) : S(52);
        int th = TextRenderer.MeasureText("Ag", fRow).Height;
        int tx = S(16);
        if (lead != null)
        {
            lead.Location = new Point(S(16), rowY + (h - lead.Height) / 2);
            cur.Controls.Add(lead);
            tx = lead.Right + S(12);
        }
        Label t = new Label { Text = title, Font = fRow, AutoSize = true };
        t.Location = new Point(tx, desc != null ? rowY + S(11) : rowY + (h - th) / 2);
        cur.Controls.Add(t);
        Label d = null;
        if (desc != null)
        {
            d = new Label { Text = desc, Font = fDesc, AutoSize = true, Location = new Point(tx, rowY + S(12) + th) };
            dim.Add(d);
            cur.Controls.Add(d);
        }
        int right = cur.Width - S(16);
        if (ctl != null)
        {
            ctl.Location = new Point(right - ctl.Width, rowY + (h - ctl.Height) / 2);
            cur.Controls.Add(ctl);
            right = ctl.Left - S(8);
        }
        if (value != null)
        {
            value.Location = new Point(right - value.Width, rowY + (h - value.Height) / 2);
            cur.Controls.Add(value);
        }
        rowY += h;
        cur.Height = rowY;
        return d;
    }

    void Note(string text)
    {
        if (rowY > 0) cur.Separators.Add(rowY);
        Label l = new Label { Text = text, Font = fDesc, AutoSize = true, MaximumSize = new Size(cur.Width - S(32), 0) };
        l.Location = new Point(S(16), rowY + S(14));
        dim.Add(l);
        cur.Controls.Add(l);
        rowY += l.Height + S(28);
        cur.Height = rowY;
    }

    void EndSection() { y += cur.Height + S(22); }

    Label ValueLabel()
    {
        Label l = new Label { AutoSize = false, Size = new Size(S(64), S(24)), TextAlign = ContentAlignment.MiddleRight, Font = fDesc };
        dim.Add(l);
        return l;
    }

    Toggle NewToggle(Action<bool> set)
    {
        Toggle t = new Toggle();
        t.Changed += (s, e) => { if (!loading) { set(t.Checked); Settings.Notify(); } };
        return t;
    }

    Segmented NewSeg(Action<int> set, params string[] opts)
    {
        Segmented sg = new Segmented(opts);
        sg.Changed += (s, e) => { if (!loading) { set(sg.Selected); Settings.Notify(); } };
        return sg;
    }

    FlatButton Button(string text, bool primary, int width, Action click)
    {
        FlatButton b = new FlatButton(text, primary, width);
        b.Height = S(32);
        b.Click += (s, e) => click();
        return b;
    }

    bool Ask(string msg)
    {
        return MessageBox.Show(this, msg, "BureauCache", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
    }

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
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "BureauCache"); }
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
            if (c.Count == 0) { MessageBox.Show(this, T("Rien à ranger pour le moment.", "Nothing to tidy up right now."), "BureauCache"); return; }
            string list = string.Join("\n", c.Take(12).Select(p => "• " + Path.GetFileName(p))) + (c.Count > 12 ? "\n…" : "");
            if (!Ask(T(c.Count + " élément(s) seront déplacés dans « " + Settings.ArchiveName + " » :\n\n", c.Count + " item(s) will be moved into \"" + Settings.ArchiveName + "\":\n\n") + list)) return;
            int n = Archiver.Run(c);
            MessageBox.Show(this, T(n + " élément(s) rangé(s).", n + " item(s) moved."), "BureauCache");
            Settings.Fire();
        });
        lArchive = Row(T("Ranger maintenant", "Tidy up now"), "-", bArchiveNow, null);
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

    void RebuildCustoms()
    {
        string sig = string.Join("|", Settings.CustomCats.Select(c => c.Name + c.Glyph + c.Color.ToArgb()));
        if (sig == customSig) return;
        customSig = sig;
        Card saved = cur;
        int savedRow = rowY;
        cur = customCard;
        foreach (Control c in customCard.Controls.Cast<Control>().ToList()) c.Dispose();
        customCard.Controls.Clear();
        customCard.Separators.Clear();
        rowY = 0;
        foreach (CustomCat cc in Settings.CustomCats.ToList())
        {
            CustomCat c = cc;
            int count = Settings.CatOverride.Values.Count(v => string.Equals(v, c.Name, StringComparison.OrdinalIgnoreCase));
            FlatButton del = Button(T("Supprimer", "Delete"), false, S(100), () =>
            {
                if (!Ask(T("Supprimer la catégorie « " + c.Name + " » ?\nSes éléments retournent dans leur catégorie automatique.",
                           "Delete the \"" + c.Name + "\" category?\nIts items go back to their automatic category."))) return;
                Settings.DeleteCategory(c);
                Settings.Notify();
            });
            Row(c.Name, count + T(" élément(s)", " item(s)"), del, null, new GlyphTileControl(c.Glyph, c.Color));
            FlatButton edit = Button(T("Modifier", "Edit"), false, S(100), () => { if (CategoryDialog.Run(c) != null) Settings.Notify(); });
            edit.Location = new Point(del.Left - S(8) - edit.Width, del.Top);
            customCard.Controls.Add(edit);
        }
        FlatButton add = Button(T("Nouvelle catégorie", "New category"), true, S(170), () => { if (CategoryDialog.Run(null) != null) Settings.Notify(); });
        Row(Settings.CustomCats.Count == 0 ? T("Aucune catégorie personnalisée", "No custom category") : T("Ajouter une catégorie", "Add a category"),
            T("Ex. : Travail, Cours, Mods… avec ton icône et ta couleur.", "E.g. Work, School, Mods… with your own icon and color."), add, null);
        Style(customCard);
        cur = saved;
        rowY = savedRow;
    }

    void BuildAdvanced()
    {
        NewPage(T("Avancé", "Advanced"));
        Section(T("SAUVEGARDE", "BACKUP"));
        Row(T("Exporter mes réglages", "Export my settings"), T("Réglages, favoris, catégories et ordre.", "Settings, favorites, categories and order."),
            Button(T("Exporter…", "Export…"), false, S(130), Export), null);
        Row(T("Importer des réglages", "Import settings"), T("Remplace les réglages actuels.", "Replaces the current settings."),
            Button(T("Importer…", "Import…"), false, S(130), Import), null);
        EndSection();

        Section(T("MISES À JOUR", "UPDATES"));
        repo = new InputBox(S(230));
        repo.Box.Leave += (s, e) => { if (repo.Box.Text.Trim() != Settings.UpdateRepo) { Settings.UpdateRepo = repo.Box.Text.Trim(); Settings.Notify(); } };
        repo.Box.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; ActiveControl = null; } };
        repo.Box.HandleCreated += (s, e) => Native.SendMessage(repo.Box.Handle, 0x1501, (IntPtr)1, Updater.DefaultRepo);
        Row(T("Dépôt GitHub", "GitHub repository"), T("Laisse vide pour le dépôt officiel.", "Leave empty for the official repository."), repo, null);
        tAutoUpd = NewToggle(v => Settings.AutoUpdate = v);
        Row(T("Vérifier chaque jour", "Check daily"), null, tAutoUpd, null);
        bCheck = Button(T("Vérifier", "Check"), true, S(170), CheckUpdate);
        lUpdate = Row(T("Version ", "Version ") + Installer.Version, "-", bCheck, null);
        EndSection();

        Section(T("SÉCURITÉ", "SAFETY"));
        Note(T("Si BureauCache se ferme de façon inattendue, un petit processus de surveillance réaffiche automatiquement tes icônes du bureau.",
               "If BureauCache closes unexpectedly, a small watchdog process automatically brings your desktop icons back."));
        EndSection();
    }

    void Export()
    {
        Settings.Save();
        using (SaveFileDialog d = new SaveFileDialog())
        {
            d.FileName = T("BureauCache-reglages", "BureauCache-settings") + ".ini";
            d.Filter = T("Réglages BureauCache", "BureauCache settings") + " (*.ini)|*.ini";
            if (d.ShowDialog(this) != DialogResult.OK) return;
            try { File.Copy(Settings.FilePath, d.FileName, true); MessageBox.Show(this, T("Réglages exportés.", "Settings exported."), "BureauCache"); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "BureauCache"); }
        }
    }

    void Import()
    {
        using (OpenFileDialog d = new OpenFileDialog())
        {
            d.Filter = T("Réglages BureauCache", "BureauCache settings") + " (*.ini)|*.ini";
            if (d.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                string txt = File.ReadAllText(d.FileName);
                if (!txt.Contains("HideIcons=")) throw new Exception(T("Ce fichier n'est pas un fichier de réglages BureauCache.", "This is not a BureauCache settings file."));
                if (!Ask(T("Remplacer tes réglages actuels par ceux de ce fichier ?", "Replace your current settings with this file?"))) return;
                Settings.Load(d.FileName);
                L.Refresh();
                Settings.Notify();
                MessageBox.Show(this, T("Réglages importés.", "Settings imported."), "BureauCache");
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "BureauCache"); }
        }
    }

    bool updateReady;

    void CheckUpdate()
    {
        if (updateReady) { if (act.InstallUpdate != null) act.InstallUpdate(); return; }
        Settings.UpdateRepo = repo.Box.Text.Trim();
        Settings.SaveQuiet();
        if (!Updater.RepoValid)
        {
            lUpdate.Text = T("Dépôt invalide (format : utilisateur/projet).", "Invalid repository (format: owner/name).");
            return;
        }
        bCheck.Enabled = false;
        lUpdate.Text = T("Vérification…", "Checking…");
        Thread t = new Thread(() =>
        {
            bool newer = Updater.Check();
            try
            {
                BeginInvoke(new Action(() =>
                {
                    bCheck.Enabled = true;
                    if (newer)
                    {
                        updateReady = true;
                        lUpdate.Text = T("Version ", "Version ") + Updater.Latest + T(" disponible.", " is available.");
                        bCheck.Text = T("Installer la ", "Install ") + Updater.Latest;
                    }
                    else if (Updater.Error != null) lUpdate.Text = Updater.Error;
                    else lUpdate.Text = T("Tu as la dernière version.", "You're up to date.");
                }));
            }
            catch { }
        });
        t.IsBackground = true;
        t.Start();
    }

    FlatButton ResetButton(string text, Action a)
    {
        return Button(text, false, S(130), () =>
        {
            if (!Ask(T("Confirmer : ", "Confirm: ") + text.ToLower(L.Culture) + " ?")) return;
            a();
            Settings.Notify();
        });
    }

    // ---------- Valeurs ----------
    void LoadValues()
    {
        loading = true;
        tHide.Checked = Settings.HideIcons;
        tStartup.Checked = Startup.Enabled;
        sLang.Selected = Settings.Language;
        sSide.Selected = Settings.RightSide ? 1 : 0;
        sZone.Selected = Settings.EdgeZone;
        sScreen.Selected = Settings.ScreenIdx + 1;
        slDelay.Value = Settings.Delay;
        tAuto.Checked = Settings.AutoClose;
        tFull.Checked = Settings.NoFullscreen;
        tHot.Checked = Settings.HotkeyOn;
        hk.Mods = Settings.HotMods;
        hk.Key = Settings.HotKey;
        hk.Invalidate();
        sTheme.Selected = Settings.ThemeMode;
        swatch.Selected = Settings.Accent;
        swatch.Invalidate();
        tGlass.Checked = Settings.Glass;
        tLive.Checked = Settings.LiveBlur;
        tAnim.Checked = Settings.Animations;
        sView.Selected = Settings.GridView ? 1 : 0;
        slWidth.Value = Settings.PanelWidth;
        tWidgets.Checked = Settings.Widgets;
        tPreview.Checked = Settings.HoverPreview;
        tSApps.Checked = Settings.SearchApps;
        tSRecent.Checked = Settings.SearchRecent;
        tSSettings.Checked = Settings.SearchSettings;
        tSCmd.Checked = Settings.SearchCommands;
        sEngine.Selected = Settings.WebEngine;
        tGroups.Checked = Settings.GroupCats;
        tRecents.Checked = Settings.ShowRecents;
        tSystem.Checked = Settings.ShowSystem;
        tNew.Checked = Settings.NewBadge;
        sNewDays.Selected = Math.Max(0, Array.IndexOf(NewDaysOpts, Settings.NewDays));
        tArchive.Checked = Settings.ArchiveOn;
        sArchDays.Selected = Math.Max(0, Array.IndexOf(ArchiveOpts, Settings.ArchiveDays));
        if (!repo.Box.Focused) repo.Box.Text = Settings.UpdateRepo;
        tAutoUpd.Checked = Settings.AutoUpdate;
        RebuildCustoms();
        UpdateTexts();
        loading = false;
    }

    void UpdateTexts()
    {
        lDelay.Text = Settings.Delay + " ms";
        lWidth.Text = Settings.PanelWidth + " px";
        bool ok = act.HotkeyOk == null || act.HotkeyOk();
        if (!Settings.HotkeyOn) { lHotStatus.Text = T("Raccourci désactivé.", "Shortcut turned off."); lHotStatus.Tag = null; }
        else if (ok) { lHotStatus.Text = T("Clique dans le champ puis appuie sur tes touches.", "Click the box, then press your keys."); lHotStatus.Tag = null; }
        else { lHotStatus.Text = T("Déjà utilisé par une autre application : choisis-en un autre.", "Already used by another app: pick another one."); lHotStatus.Tag = "err"; }
        lHotStatus.ForeColor = lHotStatus.Tag != null ? Theme.Danger : Theme.FgDim;
        if (Installer.IsInstalled && Installer.RunningFromInstall)
        {
            lInstall.Text = T("Installé, présent dans le menu Démarrer et les applications.", "Installed, in the Start menu and installed apps.");
            bInstall.Text = T("Désinstaller", "Uninstall"); bInstall.Primary = false; bInstall.Danger = true;
        }
        else if (Installer.IsInstalled)
        {
            lInstall.Text = T("Une version installée existe : la mettre à jour avec celle-ci.", "An installed version exists: update it with this one.");
            bInstall.Text = T("Mettre à jour", "Update"); bInstall.Primary = true; bInstall.Danger = false;
        }
        else
        {
            lInstall.Text = T("Ajoute BureauCache au menu Démarrer et aux applications.", "Adds BureauCache to the Start menu and installed apps.");
            bInstall.Text = T("Installer", "Install"); bInstall.Primary = true; bInstall.Danger = false;
        }
        bInstall.Invalidate();
        if (lUpdate.Text == "-" || lUpdate.Text.Length == 0)
            lUpdate.Text = Updater.RepoValid ? T("Clique pour vérifier.", "Click to check.") : T("Dépôt invalide.", "Invalid repository.");
    }

    void UpdateArchiveText()
    {
        int n = Archiver.Candidates().Count;
        lArchive.Text = n == 0 ? T("Rien à ranger pour le moment.", "Nothing to tidy up right now.")
                               : n + T(" élément(s) inutilisé(s) depuis " + Settings.ArchiveDays + " jours.", " item(s) unused for " + Settings.ArchiveDays + " days.");
    }

    void OnSettingsChanged()
    {
        if (IsDisposed) return;
        LoadValues();
        if (nav.Selected == 4) UpdateArchiveText();
        Restyle();
    }

    void ShowPage(int i)
    {
        for (int k = 0; k < pages.Count; k++) pages[k].Visible = k == i;
        nav.Selected = i;
        if (i == 4) UpdateArchiveText();
    }

    public void SelectPage(int i) { ShowPage(i); }

    void Restyle()
    {
        BackColor = Theme.Bg;
        Style(this);
        Icon old = Icon;
        Icon = Ui.MakeIcon(Settings.Accent);
        if (old != null) old.Dispose();
        if (IsHandleCreated) Native.SetDarkTitleBar(Handle, Settings.Dark);
        foreach (Panel p in pages) if (p.IsHandleCreated) Native.SetDarkScrollbars(p.Handle, Settings.Dark);
        Invalidate(true);
    }

    void Style(Control c)
    {
        foreach (Control ch in c.Controls)
        {
            Color pb = ch.Parent is Card ? Theme.Card : ch.Parent is SideNav ? Theme.Side : Theme.Bg;
            if (ch is Label)
            {
                ch.BackColor = pb;
                ch.ForeColor = (ch.Tag as string) == "err" ? Theme.Danger : dim.Contains((Label)ch) ? Theme.FgDim : Theme.Fg;
            }
            else if (ch is FlatControl) ch.BackColor = ch is SideNav ? Theme.Side : pb;
            else if (ch is InputBox) { ch.BackColor = pb; ((InputBox)ch).ApplyTheme(); continue; }
            else if (ch is Panel && !(ch is Card)) ch.BackColor = Theme.Bg;
            Style(ch);
            ch.Invalidate();
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Native.SetDarkTitleBar(Handle, Settings.Dark);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        foreach (Panel p in pages) Native.SetDarkScrollbars(p.Handle, Settings.Dark);
        UpdateTexts();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape && !HotkeyBox.Capturing) Hide();
        base.OnKeyDown(e);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); }
        base.OnFormClosing(e);
    }
}

// =====================================================================
//  Raccourci clavier global
// =====================================================================
class HotkeyWindow : NativeWindow
{
    public event Action Pressed;
    public HotkeyWindow() { CreateHandle(new CreateParams()); }

    public bool Register(bool on, int mods, int key)
    {
        Native.UnregisterHotKey(Handle, 1);
        if (!on || key == 0) return true;
        return Native.RegisterHotKey(Handle, 1, (uint)(mods | 0x4000), (uint)key);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x0312 && Pressed != null) Pressed();
        base.WndProc(ref m);
    }
}

// =====================================================================
//  Application
// =====================================================================
class App : ApplicationContext
{
    readonly NotifyIcon tray;
    readonly PanelForm panel;
    SettingsForm settingsForm;
    readonly System.Windows.Forms.Timer poll;
    readonly List<FileSystemWatcher> watchers = new List<FileSystemWatcher>();
    readonly HotkeyWindow hotkey = new HotkeyWindow();
    readonly ContextMenuStrip trayMenu;
    IntPtr listView = IntPtr.Zero;
    int edgeTicks, outsideTicks, pollCount;
    bool requireLeave, wasOpen, hotkeyOk = true, autoCloseArmed, lastEn;
    Action balloonAction;
    const int PollMs = 20;
    public static string RelaunchPath;

    public App(EventWaitHandle showSignal, EventWaitHandle quitSignal)
    {
        panel = new PanelForm();
        IntPtr unused = panel.Handle;
        panel.SettingsRequested += (s, e) => ShowSettings();
        lastEn = L.En;

        trayMenu = Menus.Create();
        trayMenu.Opening += (s, e) => BuildTrayMenu();

        tray = new NotifyIcon();
        tray.Icon = Ui.MakeIcon(Settings.Accent);
        tray.Text = "BureauCache";
        tray.ContextMenuStrip = trayMenu;
        tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) ShowSettings(); };
        tray.BalloonTipClicked += (s, e) => { if (balloonAction != null) balloonAction(); };
        tray.Visible = true;

        string[] dirs = {
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)
        };
        foreach (string d in dirs)
        {
            if (string.IsNullOrEmpty(d) || !Directory.Exists(d)) continue;
            try
            {
                FileSystemWatcher w = new FileSystemWatcher(d);
                FileSystemEventHandler h = delegate { panel.Dirty = true; };
                w.Created += h; w.Deleted += h; w.Changed += h;
                w.Renamed += delegate { panel.Dirty = true; };
                w.EnableRaisingEvents = true;
                watchers.Add(w);
            }
            catch { }
        }

        hotkey.Pressed += () =>
        {
            if (HotkeyBox.Capturing) return;
            if (panel.IsOpen) panel.HidePanel();
            else OpenManually(TargetScreen(Cursor.Position));
        };

        Settings.Changed += OnSettingsChanged;
        SystemEvents.UserPreferenceChanged += (s, e) =>
        {
            if (Settings.RefreshSystemTheme() && Settings.ThemeMode == 0)
                panel.BeginInvoke(new Action(Settings.Fire));
        };
        SystemEvents.SessionEnding += delegate { Native.RestoreDesktopIcons(); };

        ApplyIconVisibility();
        panel.RequestLoad();
        RegisterHotkey();

        poll = new System.Windows.Forms.Timer();
        poll.Interval = PollMs;
        poll.Tick += Poll;
        poll.Start();

        Thread t = new Thread(() =>
        {
            while (true)
            {
                int i = WaitHandle.WaitAny(new WaitHandle[] { showSignal, quitSignal });
                try { panel.BeginInvoke(new Action(i == 0 ? (Action)ShowSettings : ExitThread)); } catch { }
                if (i == 1) break;
            }
        });
        t.IsBackground = true;
        t.Start();

        if (Settings.FirstRun) { Settings.Save(); ShowSettings(); }
        else Balloon(L.T("Amène la souris sur le bord " + (Settings.RightSide ? "droit" : "gauche") + " de l'écran", "Move the mouse to the " + (Settings.RightSide ? "right" : "left") + " edge of the screen") +
            (Settings.HotkeyOn ? L.T(" ou appuie sur ", " or press ") + HotkeyBox.Format(Settings.HotMods, Settings.HotKey) : "") + ".", null);
    }

    void Balloon(string text, Action onClick)
    {
        balloonAction = onClick;
        tray.ShowBalloonTip(3000, "BureauCache", text, ToolTipIcon.Info);
    }

    void BuildTrayMenu()
    {
        trayMenu.Items.Clear();
        trayMenu.Items.Add(Menus.Item(L.T("Ouvrir la liste", "Open the list"), "", () => OpenManually(TargetScreen(Cursor.Position))));
        ToolStripMenuItem st = Menus.Item(L.T("Paramètres…", "Settings…"), "", ShowSettings);
        st.Font = new Font(trayMenu.Font, FontStyle.Bold);
        trayMenu.Items.Add(st);
        trayMenu.Items.Add(new ToolStripSeparator());
        ToolStripMenuItem hide = Menus.Item(L.T("Cacher les icônes du bureau", "Hide desktop icons"), null, () => { Settings.HideIcons = !Settings.HideIcons; Settings.Notify(); });
        hide.Checked = Settings.HideIcons;
        trayMenu.Items.Add(hide);
        ToolStripMenuItem start = Menus.Item(L.T("Lancer au démarrage de Windows", "Start with Windows"), null, () =>
        {
            try { Startup.Set(!Startup.Enabled, Installer.IsInstalled ? Installer.Exe : null); } catch { }
            Settings.Fire();
        });
        start.Checked = Startup.Enabled;
        trayMenu.Items.Add(start);
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add(Menus.Item(L.T("Quitter (réaffiche les icônes)", "Quit (shows icons again)"), "", ExitThread));
    }

    void RegisterHotkey() { hotkeyOk = hotkey.Register(Settings.HotkeyOn, Settings.HotMods, Settings.HotKey); }

    void OnSettingsChanged()
    {
        L.Refresh();
        ApplyIconVisibility();
        RegisterHotkey();
        panel.OnSettingsChanged();
        Icon old = tray.Icon;
        tray.Icon = Ui.MakeIcon(Settings.Accent);
        if (old != null) old.Dispose();
        if (L.En != lastEn)
        {
            lastEn = L.En;
            panel.ReloadNow();
            if (settingsForm != null && !settingsForm.IsDisposed)
            {
                SettingsForm old2 = settingsForm;
                int page = old2.CurrentPage;
                Point loc = old2.Location;
                bool vis = old2.Visible;
                panel.BeginInvoke(new Action(() =>
                {
                    old2.Dispose();
                    settingsForm = new SettingsForm(Actions());
                    settingsForm.SelectPage(page);
                    settingsForm.StartPosition = FormStartPosition.Manual;
                    settingsForm.Location = loc;
                    if (vis) settingsForm.Show();
                }));
            }
        }
    }

    SettingsActions Actions()
    {
        return new SettingsActions
        {
            Preview = Preview, Quit = ExitThread, Install = DoInstall, Uninstall = DoUninstall,
            InstallUpdate = InstallUpdate, HotkeyOk = () => hotkeyOk
        };
    }

    void ShowSettings()
    {
        if (settingsForm == null || settingsForm.IsDisposed) settingsForm = new SettingsForm(Actions());
        if (!settingsForm.Visible) settingsForm.Show();
        if (settingsForm.WindowState == FormWindowState.Minimized) settingsForm.WindowState = FormWindowState.Normal;
        settingsForm.Activate();
        Native.SetForegroundWindow(settingsForm.Handle);
    }

    void Preview() { OpenManually(settingsForm != null ? Screen.FromControl(settingsForm) : Screen.PrimaryScreen); }

    void OpenManually(Screen scr)
    {
        autoCloseArmed = false;
        outsideTicks = 0;
        panel.ShowPanel(scr);
    }

    void DoInstall()
    {
        try
        {
            Installer.Install();
            MessageBox.Show(settingsForm, L.T("BureauCache est installé.\nTu le trouveras dans le menu Démarrer et dans Paramètres > Applications.\n\nIl va redémarrer depuis son dossier d'installation.",
                "BureauCache is installed.\nYou'll find it in the Start menu and in Settings > Apps.\n\nIt will now restart from its install folder."),
                "BureauCache", MessageBoxButtons.OK, MessageBoxIcon.Information);
            if (!Installer.RunningFromInstall) { RelaunchPath = Installer.Exe; ExitThread(); }
            else Settings.Fire();
        }
        catch (Exception ex)
        {
            MessageBox.Show(settingsForm, L.T("L'installation a échoué :\n", "Installation failed:\n") + ex.Message, "BureauCache", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    void DoUninstall()
    {
        if (MessageBox.Show(settingsForm, L.T("Désinstaller BureauCache ?\n\nTes icônes du bureau seront réaffichées et tes réglages supprimés.",
            "Uninstall BureauCache?\n\nYour desktop icons will come back and your settings will be deleted."),
            "BureauCache", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        Installer.Uninstall();
        ExitThread();
    }

    void InstallUpdate()
    {
        string notes = Updater.ShortNotes;
        if (MessageBox.Show(L.T("Installer BureauCache " + Updater.Latest + " ?\nLe programme va redémarrer.", "Install BureauCache " + Updater.Latest + "?\nThe program will restart.") +
            (notes.Length > 0 ? "\n\n" + L.T("Nouveautés :", "What's new:") + "\n" + notes : ""),
            "BureauCache", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        try
        {
            Updater.Download();
            ExitThread();
        }
        catch (Exception ex)
        {
            MessageBox.Show(L.T("La mise à jour a échoué :\n", "Update failed:\n") + ex.Message, "BureauCache", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    void CheckUpdateInBackground()
    {
        if (!Settings.AutoUpdate || !Updater.RepoValid) return;
        if (DateTime.Now - new DateTime(Settings.LastUpdateCheck) < TimeSpan.FromHours(24)) return;
        Thread t = new Thread(() =>
        {
            bool newer = Updater.Check();
            if (newer)
                try
                {
                    panel.BeginInvoke(new Action(() => Balloon(L.T("BureauCache " + Updater.Latest + " est disponible. Clique pour l'installer.",
                        "BureauCache " + Updater.Latest + " is available. Click to install."), InstallUpdate)));
                }
                catch { }
        });
        t.IsBackground = true;
        t.Start();
    }

    void RunArchive()
    {
        if (!Settings.ArchiveOn) return;
        int n = Archiver.Run(Archiver.Candidates());
        if (n > 0)
        {
            panel.ReloadNow();
            Balloon(n + L.T(" élément(s) inutilisé(s) rangé(s) dans « " + Settings.ArchiveName + " ».", " unused item(s) moved into \"" + Settings.ArchiveName + "\"."),
                () => { try { ShellUtil.Open(Archiver.Folder, null); } catch { } });
        }
    }

    Screen TargetScreen(Point p)
    {
        Screen[] all = Screen.AllScreens;
        if (Settings.ScreenIdx >= 0 && Settings.ScreenIdx < all.Length) return all[Settings.ScreenIdx];
        return Screen.FromPoint(p);
    }

    void ApplyIconVisibility()
    {
        if (listView == IntPtr.Zero || !Native.IsWindow(listView)) listView = Native.FindDesktopListView();
        if (listView == IntPtr.Zero) return;
        bool vis = Native.IsWindowVisible(listView);
        if (Settings.HideIcons && vis) Native.ShowWindow(listView, Native.SW_HIDE);
        else if (!Settings.HideIcons && !vis) Native.ShowWindow(listView, Native.SW_SHOW);
    }

    static bool ScreenExistsAt(Point p)
    {
        foreach (Screen s in Screen.AllScreens) if (s.Bounds.Contains(p)) return true;
        return false;
    }

    bool AtEdge(Point p, out Screen scr)
    {
        scr = Screen.FromPoint(p);
        Screen[] all = Screen.AllScreens;
        if (Settings.ScreenIdx >= 0 && Settings.ScreenIdx < all.Length && !all[Settings.ScreenIdx].Bounds.Contains(p)) return false;
        Rectangle b = scr.Bounds;
        bool edge = Settings.RightSide
            ? p.X >= b.Right - 1 && !ScreenExistsAt(new Point(p.X + 1, p.Y))
            : p.X <= b.Left && !ScreenExistsAt(new Point(p.X - 1, p.Y));
        if (!edge) return false;
        if (Settings.EdgeZone == 0) return true;
        int third = b.Height / 3, rel = p.Y - b.Top;
        if (Settings.EdgeZone == 1) return rel < third;
        if (Settings.EdgeZone == 2) return rel >= third && rel < 2 * third;
        return rel >= 2 * third;
    }

    void Poll(object sender, EventArgs e)
    {
        pollCount++;
        if (pollCount % 50 == 0) ApplyIconVisibility();
        if (pollCount % 25 == 0 && panel.Dirty && !panel.Busy) panel.RequestLoad();
        if (pollCount == 3000 || pollCount % 1080000 == 0) RunArchive();             // 1 min après le démarrage, puis toutes les 6 h
        if (pollCount == 1500 || pollCount % 180000 == 0) CheckUpdateInBackground();  // 30 s après le démarrage, puis toutes les heures

        Point p = Cursor.Position;
        bool open = panel.IsOpen;
        Screen scr;
        bool atEdge = AtEdge(p, out scr);
        if (wasOpen && !open && atEdge) requireLeave = true;
        wasOpen = open;
        if (requireLeave && !atEdge) requireLeave = false;

        if (!open)
        {
            if (!atEdge || requireLeave) { edgeTicks = 0; return; }
            edgeTicks++;
            int needed = (Settings.Delay + (Control.MouseButtons != MouseButtons.None ? 350 : 0)) / PollMs + 1;
            if (edgeTicks >= needed)
            {
                edgeTicks = 0;
                if (Settings.NoFullscreen && Native.ForegroundIsFullscreen()) { requireLeave = true; return; }
                outsideTicks = 0;
                autoCloseArmed = true;
                panel.ShowPanel(scr);
            }
        }
        else if (Settings.AutoClose)
        {
            Rectangle r = panel.Bounds;
            r.Inflate(20, 0);
            if (r.Contains(p)) autoCloseArmed = true;
            if (autoCloseArmed && !r.Contains(p) && !panel.MenuOpen && !panel.Busy) outsideTicks++;
            else outsideTicks = 0;
            if (outsideTicks >= 25) { outsideTicks = 0; panel.HidePanel(); }
        }
    }

    protected override void ExitThreadCore()
    {
        poll.Stop();
        try { hotkey.Register(false, 0, 0); hotkey.DestroyHandle(); } catch { }
        Native.RestoreDesktopIcons();
        foreach (FileSystemWatcher w in watchers) w.Dispose();
        tray.Visible = false;
        tray.Dispose();
        base.ExitThreadCore();
    }
}

static class Program
{
    const string MutexName = "BureauCache_SingleInstance";

    [STAThread]
    static void Main(string[] args)
    {
        try { Native.SetProcessDPIAware(); } catch { }
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) Ui.Scale = g.DpiX / 96f;

        string cmd = args.Length > 0 ? args[0].ToLowerInvariant() : "";
        if (cmd == "--watchdog" && args.Length > 1) { Watchdog(args[1]); return; }
        if (cmd == "--make-icon" && args.Length > 1) { Ui.WriteIco(args[1]); return; }
        if (cmd == "--restore") { Native.RestoreDesktopIcons(); return; }
        Settings.Load();
        L.Refresh();
        if (cmd == "--uninstall") { UninstallFromCommandLine(); return; }

        bool created;
        using (Mutex m = new Mutex(true, MutexName, out created))
        {
            if (!created)
            {
                try { EventWaitHandle.OpenExisting("BureauCache_ShowSettings").Set(); } catch { }
                return;
            }
            using (EventWaitHandle show = new EventWaitHandle(false, EventResetMode.AutoReset, "BureauCache_ShowSettings"))
            using (EventWaitHandle quit = new EventWaitHandle(false, EventResetMode.AutoReset, "BureauCache_Quit"))
            {
                Updater.CleanupOld();
                if (Installer.RunningFromInstall) try { Installer.RegisterVersion(); } catch { }
                StartWatchdog();
                try { Application.Run(new App(show, quit)); }
                finally { Native.RestoreDesktopIcons(); }
            }
        }
        if (App.RelaunchPath != null)
            try { Process.Start(App.RelaunchPath); } catch { }
    }

    static void StartWatchdog()
    {
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo(Application.ExecutablePath, "--watchdog " + Process.GetCurrentProcess().Id);
            psi.UseShellExecute = false;
            Process.Start(psi);
        }
        catch { }
    }

    static void Watchdog(string pidText)
    {
        int pid;
        if (!int.TryParse(pidText, out pid)) return;
        try { Process.GetProcessById(pid).WaitForExit(); } catch { }
        Thread.Sleep(1500);
        bool running;
        try { using (Mutex.OpenExisting(MutexName)) running = true; }
        catch { running = false; }
        if (!running) Native.RestoreDesktopIcons();
    }

    static void UninstallFromCommandLine()
    {
        if (MessageBox.Show(L.T("Désinstaller BureauCache ?\n\nTes icônes du bureau seront réaffichées et tes réglages supprimés.",
            "Uninstall BureauCache?\n\nYour desktop icons will come back and your settings will be deleted."),
            "BureauCache", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        try { EventWaitHandle.OpenExisting("BureauCache_Quit").Set(); } catch { }
        for (int i = 0; i < 50; i++)
        {
            bool running;
            try { using (Mutex.OpenExisting(MutexName)) running = true; }
            catch { running = false; }
            if (!running) break;
            Thread.Sleep(100);
        }
        Installer.Uninstall();
        MessageBox.Show(L.T("BureauCache a été désinstallé.", "BureauCache has been uninstalled."), "BureauCache", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
