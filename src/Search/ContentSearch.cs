// BureauCache — ContentSearch
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

// =====================================================================
//  Recherche dans le contenu des fichiers du bureau (D-010)
// =====================================================================
// Interroge l'index de Windows Search (fournisseur OLE DB « Search.CollatorDSO », via ADODB en COM tardif) :
// aucune indexation maison, aucune lecture des fichiers par BureauCache. La portée est limitée aux dossiers
// du bureau. Si l'index est arrêté ou absent, la recherche renvoie simplement une liste vide.
static class ContentSearch
{
    public const int MinLength = 3, MaxResults = 20, MaxTermLength = 60, MaxDepth = 2;

    // Garde lettres, chiffres, espaces et quelques séparateurs sûrs ; tout ce qui pourrait sortir de la chaîne
    // SQL ou de la syntaxe CONTAINS (guillemets, apostrophes, parenthèses, astérisques, opérateurs) disparaît.
    public static string Sanitize(string term)
    {
        if (string.IsNullOrEmpty(term)) return "";
        StringBuilder sb = new StringBuilder();
        bool space = false;
        foreach (char c in Search.ReplaceInvalidUnicode(term))
        {
            if (char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '.')
            {
                sb.Append(c); space = false;
            }
            else if (!space && sb.Length > 0) { sb.Append(' '); space = true; }
            if (sb.Length >= MaxTermLength) break;
        }
        return sb.ToString().Trim();
    }

    // Le panneau ne cherche dans le contenu que pour un vrai mot : ni préfixe spécial (= ? >), ni calcul, ni terme trop court
    public static bool Applies(string q)
    {
        if (string.IsNullOrEmpty(q)) return false;
        q = q.Trim();
        if (q.StartsWith("=") || q.StartsWith("?") || q.StartsWith(">")) return false;
        return Sanitize(q).Length >= MinLength;
    }

    // Chemin d'un dossier sous la forme attendue par SCOPE (file:C:/Users/…), apostrophes doublées
    static string Scope(string folder)
    {
        return "SCOPE='file:" + folder.TrimEnd('\\').Replace('\\', '/').Replace("'", "''") + "'";
    }

    // Requête SQL de Windows Search, ou null si le terme est inutilisable. Chaque mot devient un préfixe
    // (« fact* ») et tous les mots doivent figurer dans le fichier.
    public static string BuildQuery(string term, IEnumerable<string> folders, int max)
    {
        string clean = Sanitize(term);
        if (clean.Length < MinLength) return null;
        List<string> scopes = (folders ?? Enumerable.Empty<string>()).Where(f => !string.IsNullOrEmpty(f)).Select(Scope).ToList();
        if (scopes.Count == 0) return null;
        string words = string.Join(" AND ", clean.Split(' ').Where(w => w.Length > 0).Select(w => "\"" + w + "*\"").ToArray());
        // System.ItemUrl et non System.ItemPathDisplay : ce dernier est traduit (« C:\Utilisateurs\…\Bureau »)
        return "SELECT TOP " + Math.Max(1, Math.Min(max, 200)) + " System.ItemUrl FROM SystemIndex WHERE (" +
               string.Join(" OR ", scopes.ToArray()) + ") AND CONTAINS(*, '" + words + "') ORDER BY System.Search.Rank DESC";
    }

    // « file:C:/Users/a/Desktop/x.txt » → chemin Windows, ou null pour toute autre forme d'adresse
    public static string PathFromUrl(string url)
    {
        if (string.IsNullOrEmpty(url) || !url.StartsWith("file:", StringComparison.OrdinalIgnoreCase)) return null;
        string p = Uri.UnescapeDataString(url.Substring(5).TrimStart('/')).Replace('/', '\\');
        return p.Length > 2 && p[1] == ':' ? p : null;
    }

    // Fichier du bureau, ou d'un dossier du bureau à au plus MaxDepth niveaux : la documentation enfouie dans un
    // logiciel portable posé sur le bureau ne remonte pas
    public static bool NearDesktop(string path, IEnumerable<string> folders)
    {
        foreach (string f in folders)
        {
            string root = f.TrimEnd('\\') + "\\";
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue;
            return path.Substring(root.Length).Count(c => c == '\\') < MaxDepth;
        }
        return false;
    }

    // Fichier présent, ni caché ni système (desktop.ini), et pas un raccourci : ceux-là sont trouvés par leur nom
    static bool Visible(string p)
    {
        try
        {
            string ext = Path.GetExtension(p);
            if (string.Equals(ext, ".lnk", StringComparison.OrdinalIgnoreCase) || string.Equals(ext, ".url", StringComparison.OrdinalIgnoreCase)) return false;
            FileAttributes a = File.GetAttributes(p);
            return (a & (FileAttributes.Hidden | FileAttributes.System | FileAttributes.Directory)) == 0;
        }
        catch { return false; }
    }

    // Chemins des fichiers du bureau dont le contenu contient le terme. À appeler hors du fil de l'interface.
    public static List<string> Find(string term, int max)
    {
        Sandbox.Guard("index Windows Search");
        List<string> res = new List<string>();
        string[] folders = Paths.Desktops;
        string sql = BuildQuery(term, folders, max * 3);
        if (sql == null) return res;
        Type t = Type.GetTypeFromProgID("ADODB.Connection");
        if (t == null) return res;
        dynamic conn = null, rs = null;
        try
        {
            conn = Activator.CreateInstance(t);
            conn.Open("Provider=Search.CollatorDSO;Extended Properties='Application=Windows';");
            rs = conn.Execute(sql);
            while (!(bool)rs.EOF && res.Count < max)
            {
                string p = PathFromUrl(rs.Fields.Item(0).Value as string);
                if (p != null && NearDesktop(p, folders) && Visible(p)) res.Add(p);
                rs.MoveNext();
            }
        }
        catch { }
        finally
        {
            try { if (rs != null) rs.Close(); } catch { }
            try { if (conn != null) conn.Close(); } catch { }
            try { if (rs != null) System.Runtime.InteropServices.Marshal.FinalReleaseComObject(rs); } catch { }
            try { if (conn != null) System.Runtime.InteropServices.Marshal.FinalReleaseComObject(conn); } catch { }
        }
        return res;
    }
}
