# Boîte « Choisir un dossier » moderne de Windows (IFileOpenDialog), avec le bouton « Nouveau dossier » (T-058).
# Appelée par l'installateur : elle écrit le dossier choisi dans -Sortie (encodage ANSI, lu par Inno Setup), ou rien si
# l'utilisateur annule. Lancement : powershell -STA -File choisir-dossier.ps1 -Sortie <fichier> -Depart <dossier> -Titre <texte>
param(
    [string]$Sortie,
    [string]$Depart = '',
    [string]$Titre = 'Choisir un dossier',
    [switch]$Verifier
)
$ErrorActionPreference = 'Stop'

$code = @"
using System;
using System.Runtime.InteropServices;

public static class SelecteurDossier
{
    [ComImport, Guid("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7")]
    class FileOpenDialogRCW { }

    [ComImport, Guid("42f85136-db7e-439c-85f1-e4075d135fc8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IFileDialog
    {
        [PreserveSig] int Show(IntPtr parent);
        [PreserveSig] int SetFileTypes(uint cFileTypes, IntPtr rgFilterSpec);
        [PreserveSig] int SetFileTypeIndex(uint iFileType);
        [PreserveSig] int GetFileTypeIndex(out uint piFileType);
        [PreserveSig] int Advise(IntPtr pfde, out uint pdwCookie);
        [PreserveSig] int Unadvise(uint dwCookie);
        [PreserveSig] int SetOptions(uint fos);
        [PreserveSig] int GetOptions(out uint pfos);
        [PreserveSig] int SetDefaultFolder(IShellItem psi);
        [PreserveSig] int SetFolder(IShellItem psi);
        [PreserveSig] int GetFolder(out IShellItem ppsi);
        [PreserveSig] int GetCurrentSelection(out IShellItem ppsi);
        [PreserveSig] int SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        [PreserveSig] int GetFileName(out IntPtr pszName);
        [PreserveSig] int SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
        [PreserveSig] int SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
        [PreserveSig] int SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
        [PreserveSig] int GetResult(out IShellItem ppsi);
        [PreserveSig] int AddPlace(IShellItem psi, uint fdap);
        [PreserveSig] int SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
        [PreserveSig] int Close(int hr);
        [PreserveSig] int SetClientGuid(ref Guid guid);
        [PreserveSig] int ClearClientData();
        [PreserveSig] int SetFilter(IntPtr pFilter);
    }

    [ComImport, Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IShellItem
    {
        [PreserveSig] int BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
        [PreserveSig] int GetParent(out IShellItem ppsi);
        [PreserveSig] int GetDisplayName(uint sigdnName, out IntPtr ppszName);
        [PreserveSig] int GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
        [PreserveSig] int Compare(IShellItem psi, uint hint, out int piOrder);
    }

    const uint FOS_PICKFOLDERS = 0x00000020;
    const uint FOS_FORCEFILESYSTEM = 0x00000040;
    const uint SIGDN_FILESYSPATH = 0x80058000;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern int SHCreateItemFromParsingName([MarshalAs(UnmanagedType.LPWStr)] string pszPath, IntPtr pbc, ref Guid riid, out IShellItem ppv);

    [DllImport("ole32.dll")]
    static extern void CoTaskMemFree(IntPtr pv);

    // Vérification sans affichage : le composant COM existe et peut être créé
    public static bool Disponible()
    {
        object dialogue = new FileOpenDialogRCW();
        Marshal.ReleaseComObject(dialogue);
        return true;
    }

    // Renvoie le dossier choisi, ou null si l'utilisateur annule
    public static string Choisir(string depart, string titre)
    {
        IFileDialog dlg = (IFileDialog)new FileOpenDialogRCW();
        uint options;
        dlg.GetOptions(out options);
        dlg.SetOptions(options | FOS_PICKFOLDERS | FOS_FORCEFILESYSTEM);
        dlg.SetTitle(titre);
        if (!string.IsNullOrEmpty(depart))
        {
            Guid iid = typeof(IShellItem).GUID;
            IShellItem dossier;
            if (SHCreateItemFromParsingName(depart, IntPtr.Zero, ref iid, out dossier) == 0) dlg.SetFolder(dossier);
        }
        if (dlg.Show(IntPtr.Zero) != 0) return null;   // annulé (0x800704C7) ou erreur
        IShellItem resultat;
        if (dlg.GetResult(out resultat) != 0) return null;
        IntPtr texte;
        if (resultat.GetDisplayName(SIGDN_FILESYSPATH, out texte) != 0) return null;
        string chemin = Marshal.PtrToStringUni(texte);
        CoTaskMemFree(texte);
        return chemin;
    }
}
"@

Add-Type -TypeDefinition $code -Language CSharp

if ($Verifier) {
    if ([SelecteurDossier]::Disponible()) { Write-Output 'OK' }
    exit 0
}
if (-not $Sortie) { throw 'Paramètre -Sortie requis.' }

$choisi = [SelecteurDossier]::Choisir($Depart, $Titre)
if ($choisi) {
    # ANSI (page de codes du système) : c'est ce que lit LoadStringFromFile d'Inno Setup
    [IO.File]::WriteAllText($Sortie, $choisi, [Text.Encoding]::Default)
}
