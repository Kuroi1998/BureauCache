# Compilation de référence d'un tag (décision D-004, revue QA-21).
# À charger avec : . "$PSScriptRoot\compilation-commun.ps1"
#
# Le compilateur C# du .NET Framework n'est pas déterministe, et sa version diffère entre ce PC et les machines de
# GitHub Actions : l'exe de la CI ne peut pas être comparé à une compilation locale (essai fait sur la 3.3 publiée :
# environ 81 000 octets d'écart). La version publiée est donc celle compilée ici.

# Compile les sources d'un tag avec les options de build.cmd, SANS exécuter de script du dépôt : le code de ce tag
# n'est que lu (git archive), jamais lancé. Renvoie le chemin de l'exe produit.
function New-CompilationDeReference([string]$Depot, [string]$Tag, [string]$Dossier) {
    $zip = Join-Path $Dossier 'sources.zip'
    $sources = Join-Path $Dossier 'sources'
    git -C $Depot archive --format=zip -o $zip $Tag -- src BureauCache.ico
    if ($LASTEXITCODE -ne 0) { throw "Lecture des sources du tag $Tag impossible." }
    Expand-Archive $zip $sources
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
    $exe = Join-Path $Dossier 'reference.exe'
    Push-Location $sources
    try {
        # Mêmes options que build.cmd (à garder alignées)
        & $csc -nologo -codepage:65001 -target:winexe -optimize -warn:4 -win32icon:BureauCache.ico "-out:$exe" `
            -r:Microsoft.CSharp.dll -r:System.Core.dll -r:Microsoft.VisualBasic.dll -r:System.Device.dll -recurse:src\*.cs | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "La compilation de référence du tag $Tag a échoué." }
    } finally { Pop-Location }
    return $exe
}
