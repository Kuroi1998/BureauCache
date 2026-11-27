// BureauCache — ItemKind, DesktopItem, Catalog, Group, VRow
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

// =====================================================================
//  Éléments affichés dans le panneau
// =====================================================================
enum ItemKind { Desktop, App, Recent, Setting, Command, Calc, Web, Clip }

class DesktopItem
{
    public string Key, Name, Target, Args, AutoCategory, Detail, Keywords;
    string normName, normFor;
    string[] normKeywords;

    // Nom et mots-clés normalisés (sans accents ni majuscules), calculés une seule fois pour la recherche
    public string NormName
    {
        get
        {
            if (normFor != Name) { normName = Search.Norm(Name); normFor = Name; }
            return normName;
        }
    }

    public string[] NormKeywords
    {
        get
        {
            if (normKeywords == null)
                normKeywords = Keywords == null ? new string[0]
                    : Keywords.Split(' ').Select(Search.Norm).Where(k => k.Length > 0).Distinct().ToArray();
            return normKeywords;
        }
    }
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
    public static readonly string[] BuiltInGlyphs = { Glyphs.Folder, Glyphs.Game, Glyphs.Apps, Glyphs.Page };
    public static readonly string[] PickerGlyphs = {
        Glyphs.Star, Glyphs.Game, Glyphs.Apps, Glyphs.Page, Glyphs.Folder, Glyphs.Library, Glyphs.Palette, Glyphs.Laptop, Glyphs.Monitor, Glyphs.Mouse,
        Glyphs.Keyboard, Glyphs.Book, Glyphs.School, Glyphs.Photo, Glyphs.Home, Glyphs.World, Glyphs.Flag, Glyphs.Box, Glyphs.User, Glyphs.Clock
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
        return c != null ? c.Glyph : Glyphs.AllItems;
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
