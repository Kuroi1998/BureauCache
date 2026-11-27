// BureauCache — Native : fond d'écran de Windows, pour l'effet verre de la barre (T-043, D-019)
using System;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

static partial class Native
{
    const uint SPI_GETDESKWALLPAPER = 0x73;
    public const int SPI_SETDESKWALLPAPER = 0x14;

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool SystemParametersInfo(uint action, uint param, StringBuilder value, uint winIni);

    // Chemin de l'image du fond d'écran, ou null (couleur unie, diaporama non encore converti)
    public static string WallpaperPath()
    {
        if (Sandbox.Active) return null;
        try
        {
            StringBuilder path = new StringBuilder(520);
            if (!SystemParametersInfo(SPI_GETDESKWALLPAPER, (uint)path.Capacity, path, 0)) return null;
            string p = path.ToString();
            return string.IsNullOrEmpty(p) ? null : p;
        }
        catch (Exception) { return null; }
    }

    // Mise en place du fond d'écran (Paramètres > Personnalisation) : 0 remplir, 1 ajuster, 2 étirer, 3 centrer, 4 mosaïque, 5 étendre
    public static int WallpaperStyle()
    {
        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop"))
            {
                if (key == null) return 0;
                if (Convert.ToString(key.GetValue("TileWallpaper", "0")) == "1") return 4;
                switch (Convert.ToString(key.GetValue("WallpaperStyle", "10")))
                {
                    case "6": return 1;
                    case "2": return 2;
                    case "0": return 3;
                    case "22": return 5;
                    default: return 0;
                }
            }
        }
        catch (Exception) { return 0; }
    }
}
