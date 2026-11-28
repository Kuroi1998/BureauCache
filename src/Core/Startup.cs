// BureauCache — Startup (lancement avec Windows), T-055 : séparé de Installation.cs
using System;
using System.Windows.Forms;
using Microsoft.Win32;

// Lancement avec Windows : une valeur dans HKCU\...\Run (sans droits administrateur).
static class Startup
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string Name = "BureauCache";

    public static bool Enabled
    {
        get
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey))
                    return k != null && k.GetValue(Name) != null;
            }
            // Clé illisible : l'option est présentée comme désactivée, sans risque d'écriture
            catch (Exception) { return false; }
        }
    }

    public static void Set(bool on, string exe = null)
    {
        Sandbox.Guard("démarrage avec Windows (HKCU\\" + RunKey + ")");
        using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RunKey))
        {
            if (on) k.SetValue(Name, "\"" + (exe ?? Application.ExecutablePath) + "\"");
            else k.DeleteValue(Name, false);
        }
    }
}
