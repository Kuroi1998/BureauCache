# Crée les deux clés de signature des mises à jour de BureauCache (décision D-004). À lancer UNE SEULE FOIS, par toi.
#   - clé principale : signe chaque version (outils\signer-version.ps1) ;
#   - clé de secours : à ranger hors ligne (clé USB), elle ne sert que si la principale est perdue ou compromise.
# Les clés privées sont chiffrées par ton mot de passe et écrites hors de OneDrive. Les clés publiques sont écrites
# dans src\Core\UpdateKeys.cs, à commiter.
# Usage : .\outils\creer-cles.ps1  [-Dossier D:\autre\dossier]
param([string]$Dossier)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\cles-commun.ps1"
$depot = Split-Path $PSScriptRoot -Parent
if (-not $Dossier) { $Dossier = Get-DossierClesParDefaut }

if (Test-CheminOneDrive $Dossier) { throw "Refusé : $Dossier est synchronisé par OneDrive. Choisis un dossier hors OneDrive avec -Dossier." }
$principale = Join-Path $Dossier 'principale.cle'
$secours = Join-Path $Dossier 'secours.cle'
if ((Test-Path $principale) -or (Test-Path $secours)) { throw "Des clés existent déjà dans $Dossier. Les remplacer couperait les mises à jour : rien n'a été modifié." }
if ((Get-ClesPubliquesDuCode $depot).Count -gt 0) { throw "src\Core\UpdateKeys.cs contient déjà des clés : rien n'a été modifié." }

Write-Host "Les clés privées seront chiffrées avec un mot de passe. Choisis-en un long (phrase de passe) et note-le en lieu sûr."
Write-Host "Sans ce mot de passe ou sans les fichiers de clé, plus aucune mise à jour automatique ne pourra être publiée."
$mdp1 = Read-Host -AsSecureString "Mot de passe"
$mdp2 = Read-Host -AsSecureString "Confirme le mot de passe"
$o1 = ConvertTo-OctetsMotDePasse $mdp1
$o2 = ConvertTo-OctetsMotDePasse $mdp2
try {
    if ($o1.Length -lt 12) { throw "Mot de passe trop court (12 caractères minimum)." }
    if ([Convert]::ToBase64String($o1) -ne [Convert]::ToBase64String($o2)) { throw "Les deux mots de passe sont différents." }

    New-Item -ItemType Directory -Force $Dossier | Out-Null
    $publiques = @()
    foreach ($fichier in @($principale, $secours)) {
        $rsa = New-Rsa
        try {
            [IO.File]::WriteAllBytes($fichier, (Protect-Cle $rsa.ToXmlString($true) $o1))
            # Vérifie aussitôt que la clé se relit avec ce mot de passe
            $relue = Unprotect-Cle ([IO.File]::ReadAllBytes($fichier)) $o1
            if ($relue -notmatch '<D>') { throw "La clé écrite ne se relit pas correctement." }
            $publiques += $rsa.ToXmlString($false)
        } finally { $rsa.Dispose() }
    }
} finally {
    [Array]::Clear($o1, 0, $o1.Length); [Array]::Clear($o2, 0, $o2.Length)
}

$lignes = ($publiques | ForEach-Object { "        `"$_`"," }) -join "`r`n"
$code = @"
// BureauCache — clés publiques des mises à jour
// Fichier généré par outils\creer-cles.ps1 (décision D-004). Ne pas modifier à la main.
// Une version du dépôt officiel n'est installée que si sa signature (BureauCache.exe.sig) correspond à l'une de ces clés.
// La clé principale signe les versions ; la clé de secours, rangée hors ligne, permet d'en changer si la principale
// est perdue ou compromise. Les clés privées ne sont jamais dans le dépôt.
static class UpdateKeys
{
    public static readonly string[] Trusted =
    {
$lignes
    };
}
"@
[IO.File]::WriteAllText((Join-Path $depot 'src\Core\UpdateKeys.cs'), $code, (New-Object Text.UTF8Encoding $true))

Write-Host ""
Write-Host "Clés créées dans $Dossier" -ForegroundColor Green
Write-Host "  principale.cle : reste sur ce PC, sert à signer chaque version."
Write-Host "  secours.cle    : COPIE-LA sur une clé USB rangée à part, puis SUPPRIME-LA de ce PC."
Write-Host "  Fais aussi une copie de principale.cle sur la même clé USB."
Write-Host "src\Core\UpdateKeys.cs contient maintenant les deux clés publiques : préviens Claude pour la suite."
