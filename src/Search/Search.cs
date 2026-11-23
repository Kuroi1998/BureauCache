// BureauCache — Search
using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

// =====================================================================
//  Recherche floue et calculatrice
// =====================================================================
static class Search
{
    static readonly Regex InvalidUnicode = new Regex(@"[\uD800-\uDBFF](?![\uDC00-\uDFFF])|(?<![\uD800-\uDBFF])[\uDC00-\uDFFF]|[\uFFFE\uFFFF]");

    public static string ReplaceInvalidUnicode(string s) { return InvalidUnicode.Replace(s, "\uFFFD"); }

    public static string Norm(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        string d = ReplaceInvalidUnicode(s).Normalize(NormalizationForm.FormD);
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
    public static int Score(string q, string name) { return ScoreNormalized(q, Norm(name)); }

    // Même calcul, avec un nom déjà normalisé (mis en cache dans DesktopItem)
    public static int ScoreNormalized(string q, string n)
    {
        if (q.Length == 0) return 0;
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
                    int prefixLength = Math.Min(len, w.Length);
                    if (Math.Abs(qc.Length - prefixLength) > tol) continue;
                    int d = Osa(qc, w.Substring(0, prefixLength));
                    if (d < best) best = d;
                }
            if (best <= tol) return 350 - best * 30;
        }
        return -1;
    }
}
