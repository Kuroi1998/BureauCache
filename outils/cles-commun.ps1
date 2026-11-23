# Fonctions communes aux scripts de signature des mises à jour (décision D-004).
# À charger avec : . "$PSScriptRoot\cles-commun.ps1"
#
# Format d'un fichier de clé privée (.cle) :
#   "BCK1" | sel (16 octets) | IV (16) | clé XML chiffrée en AES-256-CBC | HMAC-SHA256 (32) de tout ce qui précède
# Les clés de chiffrement et d'authentification sont dérivées du mot de passe par PBKDF2-SHA256 (600 000 itérations).

$Script:Magie = [Text.Encoding]::ASCII.GetBytes('BCK1')
$Script:Iterations = 600000

function Get-DossierClesParDefaut {
    # Le profil utilisateur n'est pas synchronisé par OneDrive (contrairement à Documents et au Bureau)
    Join-Path $env:USERPROFILE 'BureauCache-cles'
}

function Test-CheminOneDrive([string]$Chemin) {
    $complet = [IO.Path]::GetFullPath($Chemin).TrimEnd('\') + '\'
    foreach ($racine in @($env:OneDrive, $env:OneDriveConsumer, $env:OneDriveCommercial)) {
        if ([string]::IsNullOrEmpty($racine)) { continue }
        $r = [IO.Path]::GetFullPath($racine).TrimEnd('\') + '\'
        if ($complet.StartsWith($r, [StringComparison]::OrdinalIgnoreCase)) { return $true }
    }
    return $false
}

function ConvertTo-OctetsMotDePasse([Security.SecureString]$MotDePasse) {
    $ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($MotDePasse)
    try { return [Text.Encoding]::UTF8.GetBytes([Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr)) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr) }
}

function Get-ClesDerivees([byte[]]$MotDePasse, [byte[]]$Sel) {
    $kdf = New-Object Security.Cryptography.Rfc2898DeriveBytes($MotDePasse, $Sel, $Script:Iterations, [Security.Cryptography.HashAlgorithmName]::SHA256)
    try { return , $kdf.GetBytes(64) } finally { $kdf.Dispose() }
}

function Protect-Cle([string]$Xml, [byte[]]$MotDePasse) {
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
    $sel = New-Object byte[] 16; $rng.GetBytes($sel)
    $iv = New-Object byte[] 16; $rng.GetBytes($iv)
    $k = Get-ClesDerivees $MotDePasse $sel
    $aes = [Security.Cryptography.Aes]::Create()
    $aes.Key = $k[0..31]; $aes.IV = $iv
    $clair = [Text.Encoding]::UTF8.GetBytes($Xml)
    $chiffre = $aes.CreateEncryptor().TransformFinalBlock($clair, 0, $clair.Length)
    [Array]::Clear($clair, 0, $clair.Length)
    $corps = $Script:Magie + $sel + $iv + $chiffre
    $hmac = New-Object Security.Cryptography.HMACSHA256(, [byte[]]$k[32..63])
    $mac = $hmac.ComputeHash([byte[]]$corps)
    [Array]::Clear($k, 0, $k.Length)
    return , [byte[]]($corps + $mac)
}

function Unprotect-Cle([byte[]]$Donnees, [byte[]]$MotDePasse) {
    if ($Donnees.Length -lt 4 + 16 + 16 + 16 + 32 -or [Text.Encoding]::ASCII.GetString($Donnees, 0, 4) -ne 'BCK1') {
        throw "Ce fichier n'est pas une clé BureauCache."
    }
    $sel = $Donnees[4..19]; $iv = $Donnees[20..35]
    $corps = $Donnees[0..($Donnees.Length - 33)]
    $mac = $Donnees[($Donnees.Length - 32)..($Donnees.Length - 1)]
    $k = Get-ClesDerivees $MotDePasse ([byte[]]$sel)
    $hmac = New-Object Security.Cryptography.HMACSHA256(, [byte[]]$k[32..63])
    $attendu = $hmac.ComputeHash([byte[]]$corps)
    $diff = 0; for ($i = 0; $i -lt 32; $i++) { $diff = $diff -bor ($attendu[$i] -bxor $mac[$i]) }
    if ($diff -ne 0) { [Array]::Clear($k, 0, $k.Length); throw "Mot de passe incorrect ou fichier de clé altéré." }
    $aes = [Security.Cryptography.Aes]::Create()
    $aes.Key = $k[0..31]; $aes.IV = [byte[]]$iv
    $chiffre = [byte[]]$Donnees[36..($Donnees.Length - 33)]
    $clair = $aes.CreateDecryptor().TransformFinalBlock($chiffre, 0, $chiffre.Length)
    [Array]::Clear($k, 0, $k.Length)
    try { return [Text.Encoding]::UTF8.GetString($clair) } finally { [Array]::Clear($clair, 0, $clair.Length) }
}

function New-Rsa {
    $rsa = New-Object Security.Cryptography.RSACryptoServiceProvider(3072, (New-Object Security.Cryptography.CspParameters(24)))
    $rsa.PersistKeyInCsp = $false
    return $rsa
}

function Get-ClesPubliquesDuCode([string]$Depot) {
    $fichier = Join-Path $Depot 'src\Core\UpdateKeys.cs'
    $texte = [IO.File]::ReadAllText($fichier)
    return @([regex]::Matches($texte, '"(<RSAKeyValue>.*?</RSAKeyValue>)"') | ForEach-Object { $_.Groups[1].Value })
}

# Même vérification que Updater.VerifySignature côté application
function Test-Signature([byte[]]$Empreinte, [string]$SignatureBase64, [string[]]$ClesPubliques) {
    $sig = [Convert]::FromBase64String($SignatureBase64.Trim())
    $oid = [Security.Cryptography.CryptoConfig]::MapNameToOID('SHA256')
    foreach ($cle in $ClesPubliques) {
        $rsa = New-Object Security.Cryptography.RSACryptoServiceProvider((New-Object Security.Cryptography.CspParameters(24)))
        $rsa.PersistKeyInCsp = $false
        try {
            $rsa.FromXmlString($cle)
            if ($rsa.KeySize -ge 3072 -and $rsa.VerifyHash($Empreinte, $oid, $sig)) { return $true }
        } finally { $rsa.Dispose() }
    }
    return $false
}
