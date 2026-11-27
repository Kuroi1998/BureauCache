// BureauCache — Calc
using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

static class Calc
{
    const int MaxLength = 200;
    public static bool LooksLikeMath(string s)
    {
        if (s == null) return false;
        s = s.Trim();
        // Même limite que TryEval : un long texte collé n'est pas analysé pour rien (SEC-03)
        if (s.Length < 2 || s.Length > MaxLength || !s.Any(char.IsDigit)) return false;
        if (!Regex.IsMatch(s, @"^[\d\s.,+\-*/^()×÷x%]+$")) return false;
        return Regex.IsMatch(s, @"[\d)%]\s*(?:\*\*|[+\-*/^×÷x])\s*(?:[+\-]\s*)*[\d(.,]") || s.EndsWith("%");
    }

    public static bool TryEval(string s, out double v)
    {
        v = 0;
        try
        {
            s = s.Trim().ToLowerInvariant();
            if (s.Length == 0 || s.Length > MaxLength) return false;
            s = NormalizeNumbers(s, L.CurrentLanguage);
            if (s == null) return false;
            s = s.Replace("×", "*").Replace("÷", "/").Replace("**", "^");
            Parser p = new Parser { S = s };
            v = p.Expr();
            p.Skip();
            return p.I == s.Length && !double.IsNaN(v) && !double.IsInfinity(v);
        }
        catch { return false; }
    }

    // Groupes de milliers par virgule : « 1,234 », « 12,345,678 » (pas de groupe de tête nul comme « 0,500 »)
    static readonly Regex CommaGroups = new Regex(@"^[1-9]\d{0,2}(,\d{3})+$");
    static readonly Regex DotGroups = new Regex(@"^[1-9]\d{0,2}(\.\d{3})+$");

    // Réécrit chaque nombre de l'expression en notation invariante (chiffres et point décimal).
    // Accepte le groupement par espace (normale, insécable U+00A0 ou fine U+202F, comme le produit Format
    // en français) et par virgule (anglais). Renvoie null si un nombre a des séparateurs incohérents.
    // En espagnol, accepte aussi les points de groupement quand la notation est sans ambiguïté.
    internal static string NormalizeNumbers(string s, int language)
    {
        StringBuilder sb = new StringBuilder(s.Length);
        int i = 0;
        while (i < s.Length)
        {
            char c = s[i];
            bool starts = char.IsDigit(c) || ((c == '.' || c == ',') && i + 1 < s.Length && char.IsDigit(s[i + 1]));
            if (!starts) { sb.Append(c); i++; continue; }
            StringBuilder raw = new StringBuilder();
            bool spaced = false, separator = false;
            while (i < s.Length)
            {
                char d = s[i];
                if (char.IsDigit(d)) raw.Append(d);
                else if (d == '.' || d == ',') { raw.Append(d); separator = true; }
                else if (IsGroupSpace(d) && !separator && GroupFollows(s, i + 1)) { raw.Append(' '); spaced = true; }
                else break;
                i++;
            }
            string number = Number(raw.ToString(), spaced, language);
            if (number == null) return null;
            sb.Append(number);
        }
        return sb.ToString();
    }

    static bool IsGroupSpace(char c) { return c == ' ' || c == ' ' || c == ' '; }

    // Un espace ne groupe des milliers que s'il est suivi d'exactement trois chiffres
    static bool GroupFollows(string s, int k)
    {
        if (k + 3 > s.Length) return false;
        for (int j = k; j < k + 3; j++) if (!char.IsDigit(s[j])) return false;
        return k + 3 == s.Length || !char.IsDigit(s[k + 3]);
    }

    static string Number(string raw, bool spaced, int language)
    {
        if (spaced)
        {
            if (raw.IndexOf(' ') > 3) return null;          // « 1234 567 » : groupe de tête trop long
            raw = raw.Replace(" ", "");
        }
        int dot = raw.IndexOf('.');
        int lastDot = raw.LastIndexOf('.');
        int commas = raw.Count(ch => ch == ',');
        if (language == L.Spanish && !spaced && dot >= 0)
        {
            int comma = raw.IndexOf(',');
            if (commas == 1 && comma > lastDot)
            {
                string integer = raw.Substring(0, comma);
                return comma < raw.Length - 1 && DotGroups.IsMatch(integer)
                    ? integer.Replace(".", "") + "." + raw.Substring(comma + 1)
                    : null;
            }
            if (commas == 0 && dot != lastDot && DotGroups.IsMatch(raw)) return raw.Replace(".", "");
        }
        if (dot != lastDot) return null;
        if (commas == 0) return raw;
        if (spaced && (commas > 1 || dot >= 0)) return null; // espaces de groupement : la virgule est décimale
        if (dot >= 0)
        {
            // Le point est décimal : les virgules ne peuvent que grouper la partie entière (« 1,234.5 »).
            // Hors notation espagnole, la virgule ne peut précéder le point que pour grouper les milliers.
            string integer = raw.Substring(0, dot);
            return CommaGroups.IsMatch(integer) ? integer.Replace(",", "") + raw.Substring(dot) : null;
        }
        if (commas > 1) return CommaGroups.IsMatch(raw) ? raw.Replace(",", "") : null;
        // Une seule virgule : groupement en anglais si elle précède exactement trois chiffres (« 1,000 »),
        // virgule décimale sinon (« 1,5 », et toujours en français, où Format groupe par espace).
        if (language == L.English && !spaced && CommaGroups.IsMatch(raw)) return raw.Replace(",", "");
        return raw.Replace(',', '.');
    }

    public static string Format(double v)
    {
        double magnitude = Math.Abs(v);
        CultureInfo culture = L.Culture;
        if (L.CurrentLanguage == L.Spanish)
        {
            // Le point reste un séparateur décimal admis à la saisie.
            culture = (CultureInfo)culture.Clone();
            culture.NumberFormat.NumberGroupSeparator = "\u00A0";
        }
        // Sous 1e-4, les 10 décimales du format fixe ne garderaient que quelques chiffres significatifs
        // (1,04e-9 s'affichait 0,000000001) : on passe en notation scientifique à 15 chiffres.
        if (magnitude > 0 && magnitude < 1e-4)
            return v.ToString("G15", culture);
        if (magnitude < 1e15 && Math.Abs(v - Math.Round(v)) < 1e-10) return Math.Round(v).ToString("#,0", culture);
        return v.ToString("#,0.##########", culture);
    }

    class Parser
    {
        public string S;
        public int I;
        int depth;
        const int MaxDepth = 64; // évite un débordement de pile avec "((((((…" ou "------…"

        public void Skip() { while (I < S.Length && char.IsWhiteSpace(S[I])) I++; }
        bool Eat(char c) { Skip(); if (I < S.Length && S[I] == c) { I++; return true; } return false; }
        void Enter() { if (++depth > MaxDepth) throw new FormatException(); }

        public double Expr()
        {
            Enter();
            double v = Term();
            while (true)
            {
                if (Eat('+')) v += Term();
                else if (Eat('-')) v -= Term();
                else { depth--; return v; }
            }
        }

        double Term()
        {
            double v = Unary();
            while (true)
            {
                if (Eat('*') || Eat('x')) v *= Unary();
                else if (Eat('/')) v /= Unary();
                else return v;
            }
        }

        // Le signe s'applique après la puissance, comme en mathématiques : -2^2 = -(2^2) = -4 (D-003)
        double Unary()
        {
            Enter();
            double v;
            if (Eat('-')) v = -Unary();
            else if (Eat('+')) v = Unary();
            else v = Power();
            depth--;
            return v;
        }

        // Puissance associative à droite (2^3^2 = 2^9) ; l'exposant peut être signé (2^-1)
        double Power()
        {
            Enter();
            double b = Primary();
            if (Eat('%')) b /= 100;
            if (Eat('^')) b = Math.Pow(b, Unary());
            depth--;
            return b;
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
            if (I > st)
            {
                if (I < S.Length && S[I] == 'e')
                {
                    I++;
                    if (I < S.Length && (S[I] == '+' || S[I] == '-')) I++;
                    int exponentStart = I;
                    while (I < S.Length && char.IsDigit(S[I])) I++;
                    if (I == exponentStart) throw new FormatException();
                }
                return double.Parse(S.Substring(st, I - st), CultureInfo.InvariantCulture);
            }
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
