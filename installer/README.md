# Installateur et signature de BureauCache

Ce dossier contient l'installateur Windows (`BureauCache.iss`, Inno Setup 6). La signature est gérée par
`outils\signer-code.ps1` et intégrée à `outils\signer-version.ps1`. Aucun certificat ni mot de passe n'est dans le dépôt.

## 1. Choix techniques

**Outil : Inno Setup 6.** Il produit un `.exe` léger, se pilote en ligne de commande (`ISCC.exe`), et signe l'installateur
et son désinstalleur par un simple appel à `signtool`. WiX produit un MSI plus lourd et orienté administrateur, sans gain
pour une application par utilisateur.

**Mode : par utilisateur, sans administrateur.** L'installateur copie `BureauCache.exe` dans
`%LOCALAPPDATA%\Programs\BureauCache`. C'est exactement le dossier attendu par `Installer.Dir` et par le moteur de mise à
jour : `Updater.ReplaceExecutable` remplace l'exe sur place, sans élévation. Une installation pour tous les utilisateurs
(`Program Files`) empêcherait ce remplacement sans administrateur : elle est donc écartée.

**Entrée « Applications installées ».** Inno écrit `HKCU\...\Uninstall\BureauCache_is1` (AppId = `BureauCache`). `Installer`
(`src\Core\Installation.cs`) utilise désormais cette même clé, pour qu'il n'y ait qu'une entrée. L'ancienne clé `BureauCache`
des versions 3.x est supprimée au démarrage suivant. Le bouton « Désinstaller » de Windows lance le désinstalleur de
l'application (`--uninstall`), qui sait aussi restaurer la barre Windows.

**Fermeture propre.** Avant une installation ou une désinstallation, l'installateur :
1. lance `BureauCache.exe --restore` (rend la barre Windows et les icônes) ;
2. signale l'événement `Local\BureauCache_Quit` (même mécanisme que `--uninstall`) ;
3. attend au plus 5 s que le mutex `Local\BureauCache_SingleInstance` disparaisse.
Si l'application tourne toujours, l'installation ou la désinstallation s'arrête avec un message clair.

**Mises à jour existantes.** Avant chaque installation, l'installateur retire `.old`, `.trial`, `.bad` et `.replaced` : le
nouvel essai et le retour arrière repartent de zéro. Les réglages (`%APPDATA%\BureauCache`) ne sont jamais touchés à
l'installation.

**Désinstallation.** Elle retire le programme, les raccourcis et le lancement automatique. Les réglages, sauvegardes et
journal (`%APPDATA%\BureauCache`) sont **conservés par défaut** ; une question explicite permet de les supprimer.

**Prérequis.** L'installateur vérifie .NET Framework 4.8 (clé `Release` ≥ 528040) avant de commencer.
Windows 10 1809 (build 17763) ou plus récent est exigé.

## 2. Où se place la signature dans la publication

La version publiée n'est pas la compilation de la CI : `signer-version.ps1` recompile le tag sur ton PC, puis :

1. **signe l'exe** avec le certificat de code (Authenticode) ;
2. **recalcule** son empreinte, puis le **signe en RSA** pour la mise à jour (`BureauCache.exe.sig`) : la signature RSA porte
   donc sur le fichier réellement publié, signé ou non ;
3. **construit l'installateur** avec cet exe, puis le **signe** ;
4. publie `BureauCache.exe`, `.sha256`, `.sig`, `BureauCache-Setup-X.Y.Z.exe` et son `.sha256`.

Sans certificat configuré, le script l'indique et demande une confirmation avant de publier une version non signée.

## 3. Quel certificat choisir

| Option | Coût indicatif | Compatible avec la publication locale | Remarques |
|---|---|---|---|
| **Azure Trusted Signing** (recommandé) | environ 10 $/mois (à vérifier sur la page officielle) | Oui : `signtool` avec le module `Azure.CodeSigning.Dlib.dll`, sur ton PC | Certificat à courte durée géré par Microsoft, pas de jeton à conserver. L'identification d'un développeur individuel n'est pas ouverte partout : vérifie ta région avant de t'inscrire. |
| **SignPath Foundation** | gratuit pour les projets open source qui remplissent leurs critères | Non, sans changer la publication : la signature se fait dans une CI (GitHub Actions) | Adapté si la signature passe en CI. Contraire au principe de D-004 (signature locale), donc à décider séparément. |
| **Certificat OV d'une autorité** (`.pfx` ou jeton matériel) | 100 à 300 € par an environ | Oui | Depuis 2023, la clé doit être sur un jeton ou un HSM. La réputation SmartScreen se construit lentement. |
| **Certificat EV** | plus cher | Oui | Depuis 2024, il ne donne plus de réputation immédiate à SmartScreen : son avantage est surtout l'identité vérifiée. |

**Recommandation pour BureauCache :** Azure Trusted Signing, en mode `azure`, car la signature reste sur ton PC comme le
prévoit D-004 : aucun secret n'a besoin d'aller dans GitHub.

**Important.** Une signature valide ne garantit pas l'acceptation immédiate. SmartScreen et Smart App Control tiennent
compte de la réputation du programme et de son éditeur, qui se construit avec le temps et les téléchargements. Aucun
certificat ne permet de contourner ces contrôles, et il ne faut jamais utiliser de certificat auto-signé pour y
prétendre.

## 4. Étapes sur ton PC (mode Azure Trusted Signing)

1. Crée un compte Azure, puis une ressource « Trusted Signing », un profil de certificat et une identité validée
   (suis la documentation officielle de Microsoft ; vérifie l'éligibilité avant).
2. Télécharge le paquet NuGet `Microsoft.Trusted.Signing.Client` et note le chemin de `Azure.CodeSigning.Dlib.dll`
   (dans le dossier `bin\x64` du paquet).
3. Crée `metadata.json` **hors du dépôt**, par exemple dans `%USERPROFILE%\.bureaucache\signature\metadata.json` :

   ```json
   {
     "Endpoint": "https://<region>.codesigning.azure.net/",
     "CodeSigningAccountName": "<nom du compte>",
     "CertificateProfileName": "<nom du profil>"
   }
   ```

4. Installe Inno Setup 6 :

   ```powershell
   winget install JRSoftware.InnoSetup
   ```

5. Connecte-toi à Azure sur ce PC :

   ```powershell
   az login
   ```

6. Définis les variables pour ta session PowerShell (ou dans ton profil, **sans** y mettre de secret) :

   ```powershell
   $env:BC_SIGN_MODE = 'azure'
   $env:BC_AZURE_DLIB = 'C:\chemin\vers\Azure.CodeSigning.Dlib.dll'
   $env:BC_AZURE_METADATA = "$env:USERPROFILE\.bureaucache\signature\metadata.json"
   ```

7. Teste la chaîne avant une vraie publication : compile l'installateur à partir de l'exe de ton dépôt, puis vérifie la signature.

   ```powershell
   .\build.cmd
   .\outils\construire-installateur.ps1 -Version 3.11.1 -Exe .\BureauCache.exe
   ```

8. Pour une publication, lance `.\outils\signer-version.ps1 -Version X.Y.Z` comme d'habitude.

## 5. Autres modes

- **`store`** : certificat installé dans le magasin Windows (OV, EV ou jeton). Variable : `BC_CERT_SHA1` (empreinte du certificat).
- **`pfx`** : fichier `.pfx`. Variables : `BC_PFX_PATH` et `BC_PFX_PASSWORD`. Le mot de passe est lu depuis l'environnement,
  jamais écrit dans un fichier, mais `signtool` le reçoit sur sa ligne de commande : préfère `store` ou `azure`.
- **Aucune variable** : build non signé, avec avertissement. La publication demande une confirmation explicite.

Variables facultatives : `BC_TIMESTAMP_URL` (serveur d'horodatage RFC 3161), `BC_SIGNTOOL` (chemin de `signtool.exe`),
`BC_ISCC` (chemin de `ISCC.exe`).

## 6. Et GitHub Actions ?

Le workflow `release.yml` ne signe rien, et ce n'est pas un oubli : il construit seulement le brouillon, car la signature
doit rester locale (D-004). Pour une signature en CI (par exemple SignPath), il faudrait une étape dédiée et un
choix séparé, à décider avant toute modification de `release.yml`.

## 7. Vérifications à faire

- Compiler l'installateur sur une machine propre (Windows 10 et Windows 11, .NET 4.8 présent).
- Installer, lancer, vérifier le Menu Démarrer, le Bureau (si coché), et l'entrée « Applications installées ».
- Mettre à jour depuis l'application (`.trial`, `.old`) puis désinstaller : vérifier que la barre Windows revient.
- Vérifier la signature : `signtool verify /pa /all installer\...`, ou clic droit > Propriétés > Signatures numériques.
- Lancer l'installateur avec une vraie signature sur une machine où SmartScreen est actif, et noter son comportement.
