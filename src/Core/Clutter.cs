// BureauCache — Clutter
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// =====================================================================
//  Encombrement du bureau (D-010)
// =====================================================================
// Statistiques en lecture seule pour Paramètres → Organisation : nombre d'éléments, taille totale, les plus
// volumineux et les inutilisés depuis 30 et 90 jours. Mêmes exclusions et même notion d'ancienneté que le
// rangement (raccourcis, éléments cachés, dossier d'archive et favoris ne comptent pas comme « inutilisés »).
static class Clutter
{
    public const int Largest = 5;

    public class Report
    {
        public int Count, Unused30, Unused90;
        public long Bytes;
        public bool Partial;   // un dossier trop grand ou illisible : taille minorée
        public bool Unreadable;   // le bureau lui-même n'a pas pu être lu (QA42-04)
        public List<KeyValuePair<string, long>> Biggest = new List<KeyValuePair<string, long>>();
    }

    // Tous les arguments sont relevés sur le fil d'interface ; le parcours ne lit aucun réglage (comme CountCandidates)
    public static Report Measure(string desk, HashSet<string> favorites, Dictionary<string, long[]> usage, string archiveFolder, DateTime nowUtc)
    {
        Report r = new Report();
        string[] entries;
        try { entries = Directory.GetFileSystemEntries(desk); }
        catch { r.Partial = r.Unreadable = true; return r; }
        DateTime limit30 = nowUtc.AddDays(-30), limit90 = nowUtc.AddDays(-90);
        List<KeyValuePair<string, long>> sizes = new List<KeyValuePair<string, long>>();
        foreach (string p in entries)
        {
            try
            {
                if (!Archiver.PassesFilters(p, archiveFolder)) continue;
                r.Count++;
                bool partial;
                long size = SizeOf(p, Archiver.MaxScan, out partial);
                if (partial) r.Partial = true;
                r.Bytes += size;
                sizes.Add(new KeyValuePair<string, long>(p, size));
                if (favorites.Contains(p)) continue;
                DateTime used = DateTime.MinValue;
                long[] u;
                if (usage.TryGetValue(p, out u)) used = new DateTime(u[1], DateTimeKind.Local).ToUniversalTime();
                if (used < limit30 && Archiver.ActivityOf(p, limit30, Archiver.MaxScan) == Archiver.Activity.Old) r.Unused30++;
                else continue;
                if (used < limit90 && Archiver.ActivityOf(p, limit90, Archiver.MaxScan) == Archiver.Activity.Old) r.Unused90++;
            }
            catch { r.Partial = true; }
        }
        r.Biggest = sizes.Where(k => k.Value > 0).OrderByDescending(k => k.Value).Take(Largest).ToList();
        return r;
    }

    // Taille d'un fichier, ou d'un dossier jusqu'à maxEntries éléments (les liens ne sont pas suivis)
    internal static long SizeOf(string path, int maxEntries, out bool partial)
    {
        partial = false;
        long total = 0;
        int seen = 0;
        try
        {
            FileAttributes attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.Directory) == 0) return new FileInfo(path).Length;
            if ((attributes & FileAttributes.ReparsePoint) != 0) return 0;
            Stack<DirectoryInfo> pending = new Stack<DirectoryInfo>();
            pending.Push(new DirectoryInfo(path));
            while (pending.Count > 0)
                foreach (FileSystemInfo child in pending.Pop().EnumerateFileSystemInfos())
                {
                    if (++seen > maxEntries) { partial = true; return total; }
                    if ((child.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                    FileInfo file = child as FileInfo;
                    if (file != null) total += file.Length;
                    else pending.Push((DirectoryInfo)child);
                }
        }
        catch { partial = true; }
        return total;
    }
}
