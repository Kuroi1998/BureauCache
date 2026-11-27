using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class WindowStartReviewTests
{
    // Le modèle compile WindowList.cs inchangé. Aucun P/Invoke dans ses dépendances.
    const string Doubles = @"
using System;
using System.Drawing;
static class Sandbox { public const bool Active = false; public static void Guard(string s) {} }
static class Native {
    public delegate void WinEventProc(IntPtr h,uint ev,IntPtr w,int obj,int child,uint thread,uint time);
    public const uint EVENT_SYSTEM_FOREGROUND=3, EVENT_SYSTEM_MINIMIZESTART=22, EVENT_SYSTEM_MINIMIZEEND=23,
        EVENT_OBJECT_CREATE=32768, EVENT_OBJECT_HIDE=32771, EVENT_OBJECT_NAMECHANGE=32780,
        EVENT_OBJECT_CLOAKED=32791, EVENT_OBJECT_UNCLOAKED=32792, WINEVENT_OUTOFCONTEXT=0;
    public const int GWL_STYLE=-16,GWL_EXSTYLE=-20,GW_OWNER=4,WS_CHILD=0x40000000,
        WS_EX_APPWINDOW=0x40000,WS_EX_TOOLWINDOW=128,WS_EX_NOACTIVATE=0x08000000,SW_MINIMIZE=6,SW_RESTORE=9,WM_CLOSE=16;
    public static int FailedRangeAttempts;
    public static bool EnumerationFails, IconMissing;
    public static IntPtr SetWinEventHook(uint a,uint b,IntPtr m,WinEventProc cb,int pid,int tid,uint flags) {
        if(a==EVENT_OBJECT_NAMECHANGE && ++FailedRangeAttempts==1) return IntPtr.Zero;
        return new IntPtr(a);
    }
    public static bool UnhookWinEvent(IntPtr h) {return true;}
    public static bool TwoWindows, Cloaked, Iconic, Maximized;
    public static bool RestoresToMaximized(IntPtr h){return Maximized;}
    public const int SW_SHOWMAXIMIZED=3;
    public static int SyncShows, AsyncShows, LastShow;
    public static IntPtr BorrowedIcon;
    public static bool EnumWindows(Func<IntPtr,IntPtr,bool> cb, IntPtr p) {
        if (EnumerationFails) return false;
        return cb(new IntPtr(1),p) && (!TwoWindows || cb(new IntPtr(2),p));
    }
    public static IntPtr GetForegroundWindow(){return IntPtr.Zero;}
    public static bool IsWindowVisible(IntPtr h){return true;}
    public static bool IsWindow(IntPtr h){return true;}
    public static bool IsCloaked(IntPtr h){return Cloaked;}
    public static bool IsIconic(IntPtr h){return Iconic;}
    public static bool ShowWindowAsync(IntPtr h,int cmd){AsyncShows++; LastShow=cmd; return true;}
    public static IntPtr CopyIcon(IntPtr h){return h==IntPtr.Zero ? IntPtr.Zero : new IntPtr(17);}
    public static bool PostMessageChecked(IntPtr h,int m,IntPtr w,IntPtr l){return true;}
    public static long WindowStyle(IntPtr h,int i){return 0;}
    public static IntPtr GetWindow(IntPtr h,int cmd){return IntPtr.Zero;}
    public static int GetWindowTextLength(IntPtr h){return 1;}
    public static string WindowTitle(IntPtr h){return ""X"";}
    public static string WindowClass(IntPtr h){return ""Window"";}
    public static string AppUserModelId(IntPtr h){return null;}
    public static IntPtr WindowIcon(IntPtr h,bool big){return BorrowedIcon;}
    public static string ProcessPath(IntPtr h){return IconMissing ? null : ""fake"";}
    public static void ShowWindow(IntPtr h,int cmd){SyncShows++;}
    public static bool SetForegroundWindow(IntPtr h){return true;}
    public static void SwitchToThisWindow(IntPtr h,bool alt){}
    public static bool PostMessage(IntPtr h,int m,IntPtr w,IntPtr l){return true;}
}
static class Icons {
    public static IntPtr ConsumedIcon;
    public static Bitmap FromHIcon(IntPtr h){ConsumedIcon=h; return h==IntPtr.Zero ? null : new Bitmap(16,16);}
    public static Bitmap ForPath(string p,int px){return new Bitmap(px,px);}
    public static Bitmap ShellImage(string p,int px,bool f){return null;}
}";

    Type native, listType, icons;
    IDisposable list;

    [TestInitialize]
    public void CreateModel()
    {
        Assert.IsTrue(Sandbox.Active);
        string root = AppDomain.CurrentDomain.BaseDirectory;
        while (root != null && !File.Exists(Path.Combine(root, "src", "Taskbar", "WindowList.cs")))
            root = Path.GetDirectoryName(root);
        if (root == null) root = Environment.CurrentDirectory;
        string source = File.ReadAllText(Path.Combine(root, "src", "Taskbar", "WindowList.cs"));
        using (CodeDomProvider provider = CodeDomProvider.CreateProvider("CSharp"))
        {
            CompilerParameters options = new CompilerParameters(new[] {
                "System.dll", "System.Core.dll", "System.Drawing.dll", "System.Windows.Forms.dll" });
            options.GenerateInMemory = true;
            options.TempFiles = new TempFileCollection(Path.GetTempPath(), false);
            CompilerResults result = provider.CompileAssemblyFromSource(options, source, Doubles);
            Assert.IsFalse(result.Errors.HasErrors, string.Join("\n", result.Errors.Cast<CompilerError>()));
            native = result.CompiledAssembly.GetType("Native", true);
            listType = result.CompiledAssembly.GetType("WindowList", true);
            icons = result.CompiledAssembly.GetType("TaskIcons", true);
            list = (IDisposable)Activator.CreateInstance(listType, new Func<IntPtr, bool>(h => false));
        }
    }

    [TestCleanup]
    public void ReleaseModel()
    {
        if (native != null) native.GetField("IconMissing").SetValue(null, false);
        if (list != null) list.Dispose();
        if (icons != null) icons.GetMethod("Forget").Invoke(null, new object[] { new HashSet<IntPtr>() });
    }

    Bitmap Icon(int px)
    {
        return (Bitmap)icons.GetMethod("For").Invoke(null, new object[] { new IntPtr(1), px });
    }

    [TestMethod]
    public void BAR_03_CacheUsesRequestedPixelSize()
    {
        Assert.AreEqual(16, Icon(16).Width);
        Assert.AreEqual(32, Icon(32).Width);
    }

    // QA-66 : une icône pas encore prête n'est plus perdue pour toute la session, mais on ne la redemande pas sans arrêt
    [TestMethod]
    public void QA66_MissingIconIsRetriedLaterThenGivenUp()
    {
        native.GetField("IconMissing").SetValue(null, true);
        Assert.IsNull(Icon(16));
        native.GetField("IconMissing").SetValue(null, false);
        Assert.IsNull(Icon(16), "Pas de nouvel essai avant le délai.");
        icons.GetField("RetryDelay", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, TimeSpan.Zero);
        Assert.IsNotNull(Icon(16), "Après le délai, l'icône devenue disponible est prise.");
    }

    [TestMethod]
    public void QA66_IconRetriesStopAfterAFewMisses()
    {
        icons.GetField("RetryDelay", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, TimeSpan.Zero);
        native.GetField("IconMissing").SetValue(null, true);
        for (int i = 0; i < 5; i++) Assert.IsNull(Icon(16));
        native.GetField("IconMissing").SetValue(null, false);
        Assert.IsNull(Icon(16), "Après 3 échecs, on ne redemande plus (fenêtre sans icône).");
    }

    [TestMethod]
    public void BAR_03_FreshSessionRetriesMissingIcon()
    {
        native.GetField("IconMissing").SetValue(null, true);
        Assert.IsNull(Icon(16));
        list.Dispose();
        list = null;
        native.GetField("IconMissing").SetValue(null, false);
        list = (IDisposable)Activator.CreateInstance(listType, new Func<IntPtr, bool>(h => false));
        Assert.IsNotNull(Icon(16));
    }

    [TestMethod]
    public void BAR_03_StoppingBarReleasesCachedBitmap()
    {
        Bitmap bitmap = Icon(16);
        list.Dispose();
        list = null;
        Assert.ThrowsException<ArgumentException>(() => bitmap.GetPixel(0, 0));
    }

    [TestMethod]
    public void BAR_04_StartRetriesMissingRange()
    {
        listType.GetMethod("Start").Invoke(list, null);
        listType.GetMethod("Start").Invoke(list, null);
        Assert.AreEqual(2, (int)native.GetField("FailedRangeAttempts").GetValue(null));
    }

    [TestMethod]
    public void BAR_05_EnumerationFailurePreservesLastSnapshot()
    {
        listType.GetMethod("Refresh").Invoke(list, null);
        native.GetField("EnumerationFails").SetValue(null, true);
        listType.GetMethod("Refresh").Invoke(list, null);
        Assert.AreEqual(1, ((System.Collections.IList)listType.GetField("Items").GetValue(list)).Count);
    }

    [TestMethod]
    public void RefreshRetriesARejectedHookAfterStartup()
    {
        listType.GetMethod("Start").Invoke(list, null);
        Assert.AreEqual(1, native.GetField("FailedRangeAttempts").GetValue(null));
        listType.GetMethod("Refresh").Invoke(list, null);
        Assert.AreEqual(2, native.GetField("FailedRangeAttempts").GetValue(null));
    }

    [TestMethod]
    public void DisposingThumbnailPreviewReleasesItsIcon()
    {
        using (WindowPreview preview = new WindowPreview())
        {
            Bitmap owned = new Bitmap(8, 8);
            FieldInfo icon = typeof(WindowPreview).GetField("icon", BindingFlags.Instance | BindingFlags.NonPublic);
            icon.SetValue(preview, owned);
            try
            {
                preview.Dispose();
                Assert.IsNull(icon.GetValue(preview));
            }
            finally { owned.Dispose(); }
        }
    }

    [TestMethod]
    public void BAR_12_InitialsKeepWholeUnicodeCharacters()
    {
        Assert.AreEqual("\U00010400M", SessionActions.Initials("\U00010400 Martin"));
    }

    // Revue Codex (QA-67) ------------------------------------------------------------------------------------------

    [TestMethod]
    public void CDX03_MinimizeAndRestoreArePosted()
    {
        IntPtr hwnd = new IntPtr(1);
        MethodInfo activate = listType.GetMethod("Activate");

        activate.Invoke(null, new object[] { hwnd, true });
        Assert.AreEqual(6, native.GetField("LastShow").GetValue(null));

        native.GetField("Iconic").SetValue(null, true);
        activate.Invoke(null, new object[] { hwnd, false });
        Assert.AreEqual(9, native.GetField("LastShow").GetValue(null));

        Assert.AreEqual(0, native.GetField("SyncShows").GetValue(null), "Aucun ShowWindow synchrone vers une autre application.");
        Assert.AreEqual(2, native.GetField("AsyncShows").GetValue(null));
    }

    // T-038 : une fenêtre réduite depuis le plein écran revient en plein écran, sans repasser par SwitchToThisWindow
    [TestMethod]
    public void T038_MinimizedMaximizedWindowComesBackMaximized()
    {
        IntPtr hwnd = new IntPtr(1);
        MethodInfo activate = listType.GetMethod("Activate");
        native.GetField("Iconic").SetValue(null, true);
        native.GetField("Maximized").SetValue(null, true);
        activate.Invoke(null, new object[] { hwnd, false });
        Assert.AreEqual(3, native.GetField("LastShow").GetValue(null), "SW_SHOWMAXIMIZED, pas SW_RESTORE.");
        Assert.AreEqual(1, native.GetField("AsyncShows").GetValue(null));
    }

    [TestMethod]
    public void CDX07_OnlyTheIconCopyIsConsumed()
    {
        IntPtr borrowed = new IntPtr(9);
        native.GetField("BorrowedIcon").SetValue(null, borrowed);

        Assert.IsNotNull(Icon(16));

        Type fakeIcons = native.Assembly.GetType("Icons", true);
        Assert.AreEqual(new IntPtr(17), fakeIcons.GetField("ConsumedIcon").GetValue(null), "FromHIcon reçoit la copie, jamais l'icône de la fenêtre.");
    }

    [TestMethod]
    public void CDX08_WindowOrderSurvivesVirtualDesktopRoundTrip()
    {
        native.GetField("TwoWindows").SetValue(null, true);
        MethodInfo refresh = listType.GetMethod("Refresh");
        FieldInfo items = listType.GetField("Items");
        Func<IntPtr[]> order = delegate
        {
            System.Collections.IList windows = (System.Collections.IList)items.GetValue(list);
            return windows.Cast<object>().Select(w => (IntPtr)w.GetType().GetField("Handle").GetValue(w)).ToArray();
        };
        IntPtr[] chosen = { new IntPtr(1), new IntPtr(2) };

        refresh.Invoke(list, null);
        listType.GetMethod("Reorder").Invoke(list, new object[] { chosen });
        CollectionAssert.AreEqual(chosen, order());

        native.GetField("Cloaked").SetValue(null, true);   // autre bureau virtuel
        refresh.Invoke(list, null);
        Assert.AreEqual(0, order().Length);

        native.GetField("Cloaked").SetValue(null, false);  // retour
        refresh.Invoke(list, null);
        CollectionAssert.AreEqual(chosen, order(), "L'ordre choisi est retrouvé au retour sur le bureau.");
    }

    [TestMethod]
    public void CDX12_PruneDisposesAnIconWithNoRemainingButton()
    {
        const string gone = @"exe:C:\Fictif\avant.exe";
        const string kept = @"exe:C:\Fictif\maintenant.exe";
        MethodInfo get = icons.GetMethod("ForKey");
        Bitmap removed = (Bitmap)get.Invoke(null, new object[] { gone, 16 });
        Bitmap retained = (Bitmap)get.Invoke(null, new object[] { kept, 16 });

        icons.GetMethod("PruneKeys").Invoke(null, new object[] { new HashSet<string>(StringComparer.OrdinalIgnoreCase) { kept } });

        Assert.ThrowsException<ArgumentException>(() => removed.GetPixel(0, 0), "Image libérée.");
        Assert.AreEqual(16, retained.Width, "Image encore utilisée : gardée.");
    }
}
