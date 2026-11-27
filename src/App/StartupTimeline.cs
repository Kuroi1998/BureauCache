using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;

// Les jalons proviennent du fil d'interface. Seule l'écriture finale se fait en arrière-plan.
static class StartupTimeline
{
    const string FileName = "demarrage-barre.csv";
    static Stopwatch clock;
    static string run;
    static readonly List<string> rows = new List<string>();
    static readonly HashSet<string> marked = new HashSet<string>(StringComparer.Ordinal);

    public static void Start(Stopwatch startupClock)
    {
        clock = startupClock;
        run = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        rows.Clear();
        marked.Clear();
        Mark("profil_charge");
    }

    public static void Mark(string phase)
    {
        if (clock == null || !marked.Add(phase)) return;
        rows.Add(run + "," + phase + "," + clock.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture));
    }

    public static void Finish(string phase)
    {
        if (clock == null) return;
        Mark(phase);
        clock = null;
        string data = string.Join(Environment.NewLine, rows) + Environment.NewLine;
        string path = Path.Combine(Settings.Dir, FileName);
        Background.Run(() => FileOps.AppendBoundedLog(path, "utc,phase,ms" + Environment.NewLine, data));
    }
}
