// BureauCache — palettes des fonds et thèmes prêts en un clic (T-045, D-019)
using System;
using System.Drawing;
using Microsoft.Win32;

// Une palette teinte légèrement les fonds (fenêtres, cartes, surfaces, lignes) d'une couleur ; le texte garde ses
// couleurs, donc son contraste. Un thème prêt règle d'un coup palette, accent, mode clair ou sombre et fond de barre.
static class Palettes
{
    internal sealed class Palette
    {
        public string Fr, En, Es;
        public Color Tint;
        public float Strength;
        public string Name { get { return L.T(Fr, En, Es); } }
    }

    internal sealed class Preset
    {
        public string Fr, En, Es;
        public int Palette, Mode, BarStyle;   // Mode : 0 système, 1 sombre, 2 clair (comme Settings.ThemeMode)
        public Color Accent;
        public string Name { get { return L.T(Fr, En, Es); } }
    }

    public static readonly Palette[] All =
    {
        new Palette { Fr = "Classique", En = "Classic", Es = "Clásico" },
        new Palette { Fr = "Ardoise", En = "Slate", Es = "Pizarra", Tint = Color.FromArgb(90, 112, 145), Strength = 0.12f },
        new Palette { Fr = "Océan", En = "Ocean", Es = "Océano", Tint = Color.FromArgb(20, 100, 175), Strength = 0.14f },
        new Palette { Fr = "Forêt", En = "Forest", Es = "Bosque", Tint = Color.FromArgb(40, 135, 80), Strength = 0.12f },
        new Palette { Fr = "Prune", En = "Plum", Es = "Ciruela", Tint = Color.FromArgb(135, 60, 155), Strength = 0.12f },
        new Palette { Fr = "Ambre", En = "Amber", Es = "Ámbar", Tint = Color.FromArgb(185, 120, 40), Strength = 0.11f },
    };

    // Accents pris dans la palette des Paramètres (Swatches.Presets), pour qu'ils y restent cochés
    public static readonly Preset[] Presets =
    {
        new Preset { Fr = "Classique", En = "Classic", Es = "Clásico", Palette = 0, Mode = 0, BarStyle = 0, Accent = Color.FromArgb(59, 130, 246) },
        new Preset { Fr = "Minuit", En = "Midnight", Es = "Medianoche", Palette = 2, Mode = 1, BarStyle = 1, Accent = Color.FromArgb(13, 148, 136) },
        new Preset { Fr = "Forêt", En = "Forest", Es = "Bosque", Palette = 3, Mode = 1, BarStyle = 2, Accent = Color.FromArgb(22, 163, 74) },
        new Preset { Fr = "Prune", En = "Plum", Es = "Ciruela", Palette = 4, Mode = 1, BarStyle = 2, Accent = Color.FromArgb(139, 92, 246) },
        new Preset { Fr = "Ambre", En = "Amber", Es = "Ámbar", Palette = 5, Mode = 2, BarStyle = 0, Accent = Color.FromArgb(249, 115, 22) },
        new Preset { Fr = "Ardoise", En = "Slate", Es = "Pizarra", Palette = 1, Mode = 2, BarStyle = 0, Accent = Color.FromArgb(59, 130, 246) },
    };

    internal static Palette Current { get { return All[Math.Max(0, Math.Min(All.Length - 1, Settings.Palette))]; } }

    // Fond teinté selon la palette choisie (en clair, la teinte est plus discrète)
    internal static Color Apply(Color c) { return Apply(c, Current, Settings.Dark); }

    // La teinte est ramenée à la luminosité du thème (foncée en sombre, pâle en clair) : la nuance change, presque
    // pas la clarté, et le texte garde son contraste (essai : l'ardoise brute éclaircissait les surfaces sombres)
    internal static Color Apply(Color c, Palette p, bool dark)
    {
        if (p.Strength <= 0) return c;
        Color tint = dark ? Theme.Mix(Color.Black, p.Tint, 0.35f) : Theme.Mix(Color.White, p.Tint, 0.30f);
        return Theme.Mix(c, tint, p.Strength * 2f);
    }

    // Le thème prêt qui correspond aux réglages actuels, ou -1
    internal static int CurrentPreset()
    {
        for (int i = 0; i < Presets.Length; i++)
        {
            Preset p = Presets[i];
            if (p.Palette == Settings.Palette && p.Mode == Settings.ThemeMode && p.Accent.ToArgb() == Settings.Accent.ToArgb() && !Settings.AccentFromWindows)
                return i;
        }
        return -1;
    }

    internal static void ApplyPreset(int index)
    {
        Preset p = Presets[index];
        Settings.Palette = p.Palette;
        Settings.ThemeMode = p.Mode;
        Settings.Accent = p.Accent;
        Settings.AccentFromWindows = false;
        Settings.TaskbarStyle = p.BarStyle;
    }

    // Couleur d'accent de Windows (Paramètres > Personnalisation > Couleurs), ou null
    internal static Color? ReadWindowsAccent()
    {
        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM"))
            {
                object v = key == null ? null : key.GetValue("AccentColor");
                if (!(v is int)) return null;
                int abgr = (int)v;   // 0xAABBGGRR
                return Color.FromArgb(abgr & 0xFF, (abgr >> 8) & 0xFF, (abgr >> 16) & 0xFF);
            }
        }
        catch (Exception) { return null; }
    }
}
