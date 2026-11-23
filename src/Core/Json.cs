// BureauCache — Json
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

// =====================================================================
//  Petit analyseur JSON strict (RFC 8259), sans dépendance (D-001, revue QA-23).
//  Objets : Dictionary<string, object> ; tableaux : List<object> ;
//  nombres : double ; chaînes : string ; true/false : bool ; null : null.
//  Un document invalide lève FormatException.
// =====================================================================
static class Json
{
    const int MaxDepth = 64;

    public static object Parse(string text)
    {
        if (text == null) throw new FormatException();
        int i = 0;
        object v = Value(text, ref i, 0);
        Skip(text, ref i);
        if (i != text.Length) throw new FormatException("Texte après la valeur JSON.");
        return v;
    }

    // Accès sûrs : null ou NaN si absent ou d'un autre type
    public static Dictionary<string, object> Obj(object o, string key)
    {
        Dictionary<string, object> d = o as Dictionary<string, object>;
        object v;
        return d != null && d.TryGetValue(key, out v) ? v as Dictionary<string, object> : null;
    }

    public static List<object> List(object o, string key)
    {
        Dictionary<string, object> d = o as Dictionary<string, object>;
        object v;
        return d != null && d.TryGetValue(key, out v) ? v as List<object> : null;
    }

    public static double Num(object o, string key)
    {
        Dictionary<string, object> d = o as Dictionary<string, object>;
        object v;
        return d != null && d.TryGetValue(key, out v) && v is double ? (double)v : double.NaN;
    }

    public static string Str(object o, string key)
    {
        Dictionary<string, object> d = o as Dictionary<string, object>;
        object v;
        return d != null && d.TryGetValue(key, out v) ? v as string : null;
    }

    static void Skip(string s, ref int i)
    {
        while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\n' || s[i] == '\r')) i++;
    }

    static object Value(string s, ref int i, int depth)
    {
        if (depth > MaxDepth) throw new FormatException("JSON trop imbriqué.");
        Skip(s, ref i);
        if (i >= s.Length) throw new FormatException("Fin inattendue.");
        char c = s[i];
        if (c == '{') return Object(s, ref i, depth);
        if (c == '[') return Array(s, ref i, depth);
        if (c == '"') return String(s, ref i);
        if (c == '-' || (c >= '0' && c <= '9')) return Number(s, ref i);
        if (Word(s, ref i, "true")) return true;
        if (Word(s, ref i, "false")) return false;
        if (Word(s, ref i, "null")) return null;
        throw new FormatException("Valeur JSON invalide.");
    }

    static bool Word(string s, ref int i, string w)
    {
        if (string.CompareOrdinal(s, i, w, 0, w.Length) != 0) return false;
        i += w.Length;
        return true;
    }

    static Dictionary<string, object> Object(string s, ref int i, int depth)
    {
        Dictionary<string, object> d = new Dictionary<string, object>(StringComparer.Ordinal);
        i++;
        Skip(s, ref i);
        if (i < s.Length && s[i] == '}') { i++; return d; }
        while (true)
        {
            Skip(s, ref i);
            if (i >= s.Length || s[i] != '"') throw new FormatException("Clé attendue.");
            string key = String(s, ref i);
            Skip(s, ref i);
            if (i >= s.Length || s[i] != ':') throw new FormatException("« : » attendu.");
            i++;
            d[key] = Value(s, ref i, depth + 1);
            Skip(s, ref i);
            if (i < s.Length && s[i] == ',') { i++; continue; }
            if (i < s.Length && s[i] == '}') { i++; return d; }
            throw new FormatException("« , » ou « } » attendu.");
        }
    }

    static List<object> Array(string s, ref int i, int depth)
    {
        List<object> l = new List<object>();
        i++;
        Skip(s, ref i);
        if (i < s.Length && s[i] == ']') { i++; return l; }
        while (true)
        {
            l.Add(Value(s, ref i, depth + 1));
            Skip(s, ref i);
            if (i < s.Length && s[i] == ',') { i++; continue; }
            if (i < s.Length && s[i] == ']') { i++; return l; }
            throw new FormatException("« , » ou « ] » attendu.");
        }
    }

    // -?(0|[1-9]\d*)(\.\d+)?([eE][+-]?\d+)?
    static double Number(string s, ref int i)
    {
        int start = i;
        if (s[i] == '-') i++;
        if (i < s.Length && s[i] == '0') i++;
        else if (!Digits(s, ref i)) throw new FormatException("Nombre invalide.");
        if (i < s.Length && s[i] == '.') { i++; if (!Digits(s, ref i)) throw new FormatException("Nombre invalide."); }
        if (i < s.Length && (s[i] == 'e' || s[i] == 'E'))
        {
            i++;
            if (i < s.Length && (s[i] == '+' || s[i] == '-')) i++;
            if (!Digits(s, ref i)) throw new FormatException("Exposant invalide.");
        }
        // .NET Framework lève OverflowException pour 1e999 (au lieu de renvoyer l'infini) : TryParse renvoie faux
        double v;
        if (!double.TryParse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out v) ||
            double.IsNaN(v) || double.IsInfinity(v)) throw new FormatException("Nombre hors limites.");
        return v;
    }

    static bool Digits(string s, ref int i)
    {
        int start = i;
        while (i < s.Length && s[i] >= '0' && s[i] <= '9') i++;
        return i > start;
    }

    static string String(string s, ref int i)
    {
        StringBuilder sb = new StringBuilder();
        i++;
        while (i < s.Length)
        {
            char c = s[i++];
            if (c == '"') return sb.ToString();
            if (c != '\\') { sb.Append(c); continue; }
            if (i >= s.Length) break;
            char e = s[i++];
            switch (e)
            {
                case '"': case '\\': case '/': sb.Append(e); break;
                case 'b': sb.Append('\b'); break;
                case 'f': sb.Append('\f'); break;
                case 'n': sb.Append('\n'); break;
                case 'r': sb.Append('\r'); break;
                case 't': sb.Append('\t'); break;
                case 'u':
                    int code;
                    if (i + 4 > s.Length || !int.TryParse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code))
                        throw new FormatException("Échappement \\u invalide.");
                    sb.Append((char)code);
                    i += 4;
                    break;
                default: throw new FormatException("Échappement invalide.");
            }
        }
        throw new FormatException("Chaîne non terminée.");
    }

    // Décode le contenu d'une chaîne JSON (sans les guillemets)
    public static string Decode(string content)
    {
        int i = 0;
        return String("\"" + content + "\"", ref i);
    }
}
