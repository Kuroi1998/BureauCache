using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class GlobalReviewTests
{
    // C02 écarté (registre QA, revue globale 3.7.1) : un retour arrière impossible donne un nouvel essai à la
    // nouvelle version. « .old » et « .trial » restent en place : un nouveau plantage relancera le retour.
    [TestMethod]
    [TestCategory("Review")]
    public void C02_FailedRollbackRetriesTheTrialAndKeepsTheBackup()
    {
        string folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string exe = Path.Combine(folder, "BureauCache.exe");
        try
        {
            File.WriteAllText(exe, "current");
            File.WriteAllText(exe + ".old", "previous");
            File.WriteAllText(exe + ".trial", "unconfirmed");
            File.WriteAllText(exe + ".bad", "earlier attempt");
            using (File.Open(exe + ".bad", FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Assert.AreEqual(Updater.StartState.Trial, Updater.BeginStart(exe));
                Assert.AreEqual("current", File.ReadAllText(exe));
                Assert.AreEqual("previous", File.ReadAllText(exe + ".old"));
                Assert.IsTrue(File.Exists(exe + ".trial"));
            }
            Assert.AreEqual(Updater.StartState.RolledBack, Updater.BeginStart(exe), "C02 : une fois le verrou levé, le retour aboutit.");
            Assert.AreEqual("previous", File.ReadAllText(exe));
        }
        finally { Directory.Delete(folder, true); }
    }

    // R-26 : un essai confirmé (arrêt propre ou 10 s) ne déclenche plus de retour au démarrage suivant
    [TestMethod]
    [TestCategory("Review")]
    public void C01_ConfirmedTrialIsNotRolledBack()
    {
        string folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string exe = Path.Combine(folder, "BureauCache.exe");
        try
        {
            File.WriteAllText(exe, "new");
            File.WriteAllText(exe + ".old", "previous");
            Assert.AreEqual(Updater.StartState.Trial, Updater.BeginStart(exe));
            Updater.ConfirmStart(exe);
            Assert.AreEqual(Updater.StartState.Normal, Updater.BeginStart(exe));
            Assert.AreEqual("new", File.ReadAllText(exe));
        }
        finally { Directory.Delete(folder, true); }
    }

    // R-14 : l'historique garde les entrées les plus récentes
    [TestMethod]
    [TestCategory("Review")]
    public void R14_UsagePruningKeepsMostRecentEntries()
    {
        Dictionary<string, long[]> previousUsage = Settings.Usage;
        try
        {
            Settings.Usage = new Dictionary<string, long[]>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < Settings.MaxUsage + 10; i++) Settings.Usage.Add("k" + i, new long[] { 1, i + 1 });
            Settings.PruneUsage();
            Assert.AreEqual(Settings.MaxUsage, Settings.Usage.Count);
            Assert.IsFalse(Settings.Usage.ContainsKey("k0"));
            Assert.IsTrue(Settings.Usage.ContainsKey("k" + (Settings.MaxUsage + 9)));
        }
        finally { Settings.Usage = previousUsage; }
    }

    // R-33 : une durée hors des choix est ramenée au premier choix au moins aussi long
    [TestMethod]
    [TestCategory("Review")]
    public void C12_ImportedDurationsSnapToTheNearestChoice()
    {
        Assert.AreEqual(60, Settings.RoundUp(Settings.ArchiveDaysChoices, 45));
        Assert.AreEqual(30, Settings.RoundUp(Settings.ArchiveDaysChoices, 7));
        Assert.AreEqual(180, Settings.RoundUp(Settings.ArchiveDaysChoices, 36500));
        Assert.AreEqual(90, Settings.RoundUp(Settings.ArchiveDaysChoices, 90));
        Assert.AreEqual(3, Settings.RoundUp(Settings.NewDaysChoices, 2));
        Assert.AreEqual(7, Settings.RoundUp(Settings.NewDaysChoices, 30));
    }

    [TestMethod]
    [TestCategory("Review")]
    public void C08_ArithmeticWithALeadingDecimalCommaIsDetected()
    {
        using (BureauCache.Tests.LanguageScope.Spanish())
        {
            const string input = "1 + ,5";
            double value;
            Assert.IsTrue(Calc.TryEval(input, out value));
            Assert.AreEqual(1.5, value, 0.000001);
            Assert.IsTrue(Calc.LooksLikeMath(input),
                "C08: the panel should offer the same result without a leading equals sign.");
        }
    }

    [TestMethod]
    [TestCategory("Review")]
    public void C09_DownloadSizeErrorIsLocalized()
    {
        using (BureauCache.Tests.LanguageScope.Spanish())
        using (var input = new MemoryStream(new byte[] { 1, 2 }))
        using (var output = new MemoryStream())
        {
            var error = Assert.ThrowsException<IOException>(() => Updater.CopyLimited(input, output, 1));
            StringAssert.Contains(error.Message.ToLowerInvariant(), "descarga",
                "C09: the user-facing size-limit message should be in Spanish.");
        }
    }

    [TestMethod]
    [TestCategory("Review")]
    public void R14_SettingsWrittenBySerializerCanBeLoaded()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".ini");
        Dictionary<string, long[]> previousUsage = Settings.Usage;
        bool hideIcons = Settings.HideIcons;
        var serialize = typeof(Settings).GetMethod("Serialize", BindingFlags.Static | BindingFlags.NonPublic);
        try
        {
            Settings.Usage = new Dictionary<string, long[]>(StringComparer.OrdinalIgnoreCase);
            string prefix = @"C:\FakeDesktop\" + new string('x', 210);
            for (int i = 0; i < 10000; i++)
                Settings.Usage.Add(prefix + i + ".txt", new long[] { 1, DateTime.Now.Ticks });
            File.WriteAllLines(path, (string[])serialize.Invoke(null, null));
            Settings.Usage = new Dictionary<string, long[]>(StringComparer.OrdinalIgnoreCase);
            Assert.IsTrue(Settings.Load(path),
                "R-14: a settings file produced by the app must remain readable as its usage history grows.");
            Assert.AreEqual(hideIcons, Settings.HideIcons);
        }
        finally
        {
            Settings.Usage = previousUsage;
            File.Delete(path);
        }
    }

    [TestMethod]
    [TestCategory("Review")]
    [Ignore("C06 : nécessite le bureau interactif pour capturer l'arrière-plan.")]
    public void C06_EnablingStaticGlassUpdatesAnAlreadyOpenPanel()
    {
        RunSta(() =>
        {
            bool glass = Settings.Glass;
            bool capture = Settings.HidePanelInCapture;
            bool live = Settings.LiveBlur;
            bool animations = Settings.Animations;
            try
            {
                Settings.Glass = false;
                Settings.HidePanelInCapture = true;
                Settings.LiveBlur = false;
                Settings.Animations = false;
                using (var panel = new PanelForm())
                {
                    panel.ShowPanel(Screen.PrimaryScreen);
                    Settings.Glass = true;
                    panel.OnSettingsChanged();
                    var bitmap = typeof(PanelForm)
                        .GetField("glass", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel);
                    Assert.IsInstanceOfType(bitmap, typeof(Bitmap), "C06: glass is still missing until the next opening.");
                }
            }
            finally
            {
                Settings.Glass = glass;
                Settings.HidePanelInCapture = capture;
                Settings.LiveBlur = live;
                Settings.Animations = animations;
            }
        });
    }

    [TestMethod]
    [TestCategory("Review")]
    public void Q24_01_SerializedSettingsWithLongUnicodePathsStayWithinLoadLimit()
    {
        var usage = Settings.Usage;
        var order = Settings.Order;
        try
        {
            Settings.Usage = new Dictionary<string, long[]>(StringComparer.OrdinalIgnoreCase);
            Settings.Order = new List<string>();
            string prefix = @"C:\Desktop\" + new string('\u6587', 240);
            for (int i = 0; i < Settings.MaxUsage; i++)
            {
                string key = prefix + i + ".txt";
                Settings.Usage.Add(key, new long[] { 1, DateTime.Now.Ticks });
                Settings.Order.Add(key);
            }
            var serialize = typeof(Settings).GetMethod("Serialize", BindingFlags.NonPublic | BindingFlags.Static);
            var lines = (string[])serialize.Invoke(null, null);
            int bytes = Encoding.UTF8.GetByteCount(string.Join("\r\n", lines) + "\r\n");
            Assert.IsTrue(bytes <= Settings.MaxFileSize, "Q24-01 : " + bytes + " octets produits pour une limite de " + Settings.MaxFileSize + ".");
        }
        finally
        {
            Settings.Order = order;
            Settings.Usage = usage;
        }
    }

    [TestMethod]
    [TestCategory("Review")]
    public void Q24_02_RecentlyUsedOldFileRemainsExcludedFromArchiving()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".txt");
        var usage = Settings.Usage;
        var isCandidate = typeof(Archiver).GetMethod("IsCandidate", BindingFlags.NonPublic | BindingFlags.Static);
        DateTime cutoff = DateTime.UtcNow.AddDays(-60);
        try
        {
            File.WriteAllText(path, "BureauCache QA");
            File.SetCreationTimeUtc(path, cutoff.AddDays(-1));
            File.SetLastWriteTimeUtc(path, cutoff.AddDays(-1));
            Settings.Usage = new Dictionary<string, long[]>(StringComparer.OrdinalIgnoreCase);
            long now = DateTime.Now.Ticks;
            Settings.Usage[path] = new long[] { 1, now - TimeSpan.TicksPerDay };
            for (int i = 0; i < Settings.MaxUsage; i++)
                Settings.Usage.Add("newer-" + i, new long[] { 1, now });

            Assert.IsFalse((bool)isCandidate.Invoke(null, new object[] { path, cutoff }));
            Settings.PruneUsage();
            Assert.IsFalse((bool)isCandidate.Invoke(null, new object[] { path, cutoff }),
                "Q24-02 : le fichier ouvert hier est devenu candidat au rangement.");
        }
        finally
        {
            Settings.Usage = usage;
            File.Delete(path);
        }
    }

    [TestMethod]
    [TestCategory("Review")]
    public void Q24_03_PreviousTrialCannotConfirmAReplacementWithALingeringMarker()
    {
        string folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string exe = Path.Combine(folder, "BureauCache.exe");
        string staged = Path.Combine(folder, "staged.exe");
        try
        {
            File.WriteAllText(exe, "first trial");
            File.WriteAllText(exe + ".old", "stable");
            Assert.AreEqual(Updater.StartState.Trial, Updater.BeginStart(exe));
            File.WriteAllText(staged, "second trial");
            using (File.Open(exe + ".trial", FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                try { Updater.ReplaceExecutable(staged, exe); }
                catch (IOException)
                {
                    Assert.AreEqual("first trial", File.ReadAllText(exe));
                    Assert.AreEqual("stable", File.ReadAllText(exe + ".old"));
                    return;
                }
                Assert.AreEqual("second trial", File.ReadAllText(exe));
                Updater.ConfirmStart(exe);
                Assert.IsTrue(File.Exists(exe + ".old"), "Q24-03 : le processus de la première version a supprimé le filet de la seconde.");
                Assert.AreEqual("stable", File.ReadAllText(exe + ".old"));
            }
        }
        finally { Directory.Delete(folder, true); }
    }

    [TestMethod]
    [TestCategory("Review")]
    [Ignore("Q24-04 : exige Explorer et une session de bureau isolée pour inscrire une AppBar temporaire.")]
    public void Q24_04_RefusedRegistrationDoesNotLeaveThePanelEffectivelyPinned()
    {
        RunSta(() =>
        {
            bool previous = Settings.Pinned;
            using (var panel = new PanelForm())
            {
                var data = new Native.APPBARDATA
                {
                    cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Native.APPBARDATA)),
                    hWnd = panel.Handle,
                    uCallbackMessage = Native.RegisterWindowMessage("BureauCacheReviewAppBar")
                };
                try
                {
                    Assert.AreNotEqual(UIntPtr.Zero, Native.SHAppBarMessage(0, ref data),
                        "Le Shell doit accepter la première inscription de contrôle.");
                    Settings.Pinned = true;
                    panel.ShowPinned(Screen.PrimaryScreen);
                    bool registered = (bool)typeof(PanelForm)
                        .GetField("appbar", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel);
                    Assert.IsTrue(registered || !Settings.Pinned,
                        "Q24-04 : l'état utilisé par l'interface et la fermeture automatique indique encore un épinglage.");
                }
                finally
                {
                    Native.SHAppBarMessage(1, ref data);
                    Settings.Pinned = previous;
                }
            }
        });
    }

    [DataTestMethod]
    [TestCategory("Review")]
    [DataRow("https://github.com/Owner/Repo.git", "Owner/Repo")]
    [DataRow("git@github.com:Owner/Repo.git", "Owner/Repo")]
    [DataRow("ssh://git@github.com/Owner/Repo", "Owner/Repo")]
    [DataRow("ssh://git@ssh.github.com:443/Owner/Repo.git", "Owner/Repo")]
    [DataRow("https://notgithub.com/Owner/Repo.git", "REFUSED")]
    public void Q24_06_SignerOnlyAcceptsSupportedGitHubOrigins(string origin, string expected)
    {
        string source = ReadSourceBlock(@"outils\signer-version.ps1", "if ($origine -notmatch", "Write-Host \"Dépôt GitHub");
        string command = "$origine = $env:BC_REVIEW_INPUT\ntry {\n" + source
            + "\n[Console]::WriteLine($repo)\n} catch { [Console]::WriteLine('REFUSED') }";
        Assert.AreEqual(expected, RunSignerSnippet(command, origin), "Q24-06 : cible déduite pour " + origin);
    }

    [DataTestMethod]
    [TestCategory("Review")]
    [DataRow("  ", "ACCEPTED")]
    [DataRow("\u00A0", "REFUSED")]
    public void Q24_07_SignerUsesTheSameHashSeparatorsAsTheClient(string separator, string expected)
    {
        string source = ReadSourceBlock(@"outils\signer-version.ps1", "$shaRelu =", "gh release edit");
        // Comme dans le script : l'empreinte produite localement est dans $tmp, celle relue sur GitHub dans $tmp\verification
        string command = "$hex = 'a' * 64\n$tmp = Join-Path ([IO.Path]::GetTempPath()) ([Guid]::NewGuid().ToString('N'))\n"
            + "$verif = Join-Path $tmp 'verification'\n[void][IO.Directory]::CreateDirectory($verif)\n"
            + "[IO.File]::WriteAllText((Join-Path $tmp 'BureauCache.exe.sha256'), \"$hex  BureauCache.exe\", (New-Object Text.ASCIIEncoding))\n"
            + "$path = Join-Path $verif 'BureauCache.exe.sha256'\n"
            + "try {\n[IO.File]::WriteAllText($path, $hex + $env:BC_REVIEW_INPUT + 'BureauCache.exe')\n" + source
            + "\n[Console]::WriteLine('ACCEPTED')\n} catch { [Console]::WriteLine('REFUSED') }\n"
            + "finally { [IO.Directory]::Delete($tmp, $true) }";
        Assert.AreEqual(expected, RunSignerSnippet(command, separator), "Q24-07 : validation du séparateur après l'empreinte.");
    }

    [TestMethod]
    [TestCategory("Review")]
    public void Q24_08_GenericExitDoesNotConfirmAnUnqualifiedTrial()
    {
        string source = ReadSourceBlock(@"src\App\App.cs", "protected override void ExitThreadCore()", "base.ExitThreadCore()");
        Assert.IsFalse(Regex.IsMatch(source, @"if\s*\(\s*Updater\.TrialStarted\s*\)\s*(?:try\s*)?\{\s*Updater\.ConfirmStart\s*\("),
            "Q24-08 : le nettoyage générique confirme l'essai sur le seul état TrialStarted, y compris après une erreur WinForms.");
    }

    static string ReadSourceBlock(string relativePath, string first, string next)
    {
        var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (dir != null)
        {
            string path = Path.Combine(dir.FullName, relativePath);
            if (File.Exists(path))
            {
                string source = File.ReadAllText(path);
                int start = source.IndexOf(first, StringComparison.Ordinal);
                Assert.IsTrue(start >= 0, "Le début du bloc a changé dans " + relativePath + ".");
                int end = source.IndexOf(next, start, StringComparison.Ordinal);
                Assert.IsTrue(end > start, "La fin du bloc a changé dans " + relativePath + ".");
                return source.Substring(start, end - start);
            }
            dir = dir.Parent;
        }
        Assert.Fail("Fichier introuvable dans la copie du dépôt testée : " + relativePath);
        return null;
    }

    static string RunSignerSnippet(string script, string input)
    {
        string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes("$ProgressPreference = 'SilentlyContinue'\n" + script));
        var info = new ProcessStartInfo("powershell.exe", "-NoProfile -NonInteractive -EncodedCommand " + encoded);
        info.UseShellExecute = false;
        info.CreateNoWindow = true;
        info.RedirectStandardOutput = true;
        info.EnvironmentVariables["BC_REVIEW_INPUT"] = input;
        using (var process = Process.Start(info))
        {
            if (!process.WaitForExit(15000))
            {
                process.Kill();
                Assert.Fail("Le contrôle PowerShell a dépassé 15 secondes.");
            }
            Assert.AreEqual(0, process.ExitCode);
            return process.StandardOutput.ReadToEnd().Trim();
        }
    }

    static void RunSta(Action action)
    {
        Exception error = null;
        Thread thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { error = ex; }
        });
        thread.IsBackground = true;
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(15)), "The UI test exceeded 15 seconds.");
        if (error != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
    }
}
