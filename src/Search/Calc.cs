// BureauCache — Calc
using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

static class Calc
{
    const int MaxLength = 200;
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
            if (s.Length == 0 || s.Length > MaxLength) return false;
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
        double magnitude = Math.Abs(v);
        if (magnitude > 0 && magnitude < 1e-9)
            return v.ToString("G15", L.Culture);
        if (magnitude < 1e15 && Math.Abs(v - Math.Round(v)) < 1e-10) return Math.Round(v).ToString("#,0", L.Culture);
        return v.ToString("#,0.##########", L.Culture);
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
            Enter();
            double b = Unary();
            if (Eat('^')) b = Math.Pow(b, Factor());
            depth--;
            return b;
        }

        double Unary()
        {
            Enter();
            double v;
            if (Eat('-')) v = -Unary();
            else if (Eat('+')) v = Unary();
            else
            {
                v = Primary();
                if (Eat('%')) v /= 100;
            }
            depth--;
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
