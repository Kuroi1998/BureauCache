# Décisions d'architecture

_Une entrée par décision, courte. On n'efface rien : une décision remplacée est marquée « Remplacée par D-xxx »._

<!-- Modèle :
## D-00X — Titre (AAAA-MM-JJ) · Statut : Proposée / Acceptée / Remplacée par D-xxx
**Contexte :** pourquoi la question se pose.
**Décision :** ce qu'on fait.
**Alternatives écartées :** et pourquoi.
**Conséquences :** ce que ça impose ensuite.
-->

## D-007 — Sauvegarde automatique des réglages (2026-11-24) · Statut : Acceptée

**Contexte :** `settings.ini` est réécrit en entier à chaque changement, et une seule écriture fautive efface tout sans retour (incident QA-32 : favoris, ordre, catégories et langue perdus).
**Décision :** à la première sauvegarde de chaque jour, le fichier précédent, s'il est valide (mêmes critères que `Load` : taille ≤ 2 Mio, ligne `HideIcons`), est copié dans `sauvegardes\settings-AAAA-MM-JJ.ini`. On garde les 7 plus récentes. Restaurer une sauvegarde ou importer un fichier copie d'abord l'état actuel dans `sauvegardes\settings-avant-restauration.ini` (un seul emplacement), lui-même restaurable. Restauration depuis Paramètres → Avancé. Une sauvegarde qui échoue ne bloque jamais l'enregistrement. La désinstallation supprime le dossier.
**Alternatives écartées :** une copie à chaque écriture (une rafale de mauvaises écritures écraserait les bonnes copies en quelques secondes) ; le paramètre de sauvegarde de `File.Replace` (une seule version, la précédente) ; une sauvegarde dans OneDrive (données personnelles envoyées hors du PC sans demande).
**Conséquences :** jusqu'à 8 fichiers de 2 Mio au plus dans le dossier des réglages. Le code passe par `SettingsBackup` ; les tests utilisent le dossier isolé (`Settings.DirOverride`).

## D-001 — Compilation par csc.exe du .NET Framework, C# 5, sans dépendance (2026-11-23) · Statut : Acceptée

**Contexte :** l'application doit pouvoir être compilée sur n'importe quel Windows sans rien installer (`build.cmd`).
**Décision :** on conserve le compilateur fourni avec Windows, sans `.csproj` ni NuGet. Le code de `src/` reste en C# 5.
**Alternatives écartées :** projet SDK `dotnet build`. Il apporterait un C# récent, mais imposerait une installation au contributeur et à la CI.
**Conséquences :** tout code proposé par une IA doit être relu pour la syntaxe C# 5. `build.cmd` est le test de compilation de référence.

## D-005 — Météo et nettoyage rapide (2026-11-23) · Statut : Acceptée

**Contexte :** le chef de projet demande deux fonctions : une météo qui montre le temps du lieu de l'utilisateur, où qu'il soit dans le monde, avec une image ; et un nettoyage des fichiers inutiles qui ne touche jamais à Windows. Gemini a fait la recherche (services, chemins de cache, pièges), puis Claude l'a vérifiée en réel.
**Décisions (choix du chef de projet, détails techniques de Claude) :**
- **Météo** : prévisions **Open-Meteo** (gratuit, sans clé, attribution CC BY 4.0 affichée dans le détail) ; lieu choisi par **recherche de ville** (géocodage Open-Meteo) ou **position Windows** (`GeoCoordinateWatcher`, nouvelle référence `System.Device.dll`) nommée par **BigDataCloud** (gratuit, sans clé, conçu pour les applications clientes). **Désactivée par défaut** : rien n'est envoyé tant que l'utilisateur n'a pas choisi un lieu. Une tuile dans le panneau (icône dessinée, température, lieu, min/max), un détail au clic (ressenti, vent, 3 jours), une actualisation toutes les 30 min au plus, des unités °C/°F selon la région. Les réponses JSON sont lues par un petit analyseur interne, sans bibliothèque (D-001). Les hôtes autorisés sont listés (`Http.FetchText`), les redirections contrôlées et les tailles limitées.
- **Nettoyage** : depuis le **clic droit sur la tuile Disque**. Six catégories : cochées par défaut, les fichiers temporaires de plus de 24 h et les caches des navigateurs et applications ; décochées, les rapports d'erreurs, les miniatures et la corbeille. Analyse d'abord, puis confirmation. Règles : uniquement sous `%LOCALAPPDATA%`, `%APPDATA%` et `%TEMP%`, jamais dans Windows, Program Files ou OneDrive ; suppression fichier par fichier, sans jamais suivre une jonction ni un lien ; fichiers « en ligne uniquement » ignorés ; cache d'une application ignoré tant qu'elle est ouverte.
**Alternatives écartées :** géolocalisation par adresse IP (envoi de l'IP à un tiers, position approximative) ; Nominatim (politique d'usage défavorable aux applications distribuées) ; services à clé API (la clé serait publique dans le code) ; cache de **Spotify** (son dossier peut contenir la musique téléchargée hors ligne) ; `JavaScriptSerializer` (nouvelle référence inutile).
**Conséquences :** `build.cmd` et `outils/compilation-commun.ps1` référencent `System.Device.dll` (les deux doivent rester alignés). Sur un PC sans Wi-Fi, Windows peut ne fournir aucune position (constaté sur le PC du chef de projet) : la recherche de ville est le chemin principal. Si Open-Meteo ou BigDataCloud changent leurs conditions, la météo devra être revue.

## D-004 — Mises à jour signées par une clé hors de GitHub (2026-11-23) · Statut : Acceptée

**Contexte :** la mise à jour vérifiait seulement un SHA-256 publié par le même dépôt que l'exe : un compte ou un dépôt GitHub compromis pouvait remplacer les deux (R-04). La recherche de Gemini (T-005) a comparé les options disponibles en 2026 pour un développeur individuel en France, sans budget.
**Décision (chef de projet, sur recommandation de Gemini et de Claude) :**
- **Signature RSA-3072 détachée** (PKCS#1 v1.5, SHA-256) dans `BureauCache.exe.sig`, vérifiée par `Updater.VerifySignature` avec les API du framework (`RSACryptoServiceProvider`, fournisseur 24).
- **GitHub Actions** vérifie que le tag compile et crée un **brouillon**. `outils/signer-version.ps1` **recompile le tag sur le PC du développeur**, remplace l'exe du brouillon par cette compilation, la signe et publie. La clé privée ne va jamais sur GitHub. *Révisé après la revue QA-21 : dans un premier temps, c'était l'exe de la CI qui était signé. Or un GitHub compromis peut glisser un exe piégé dans le brouillon, et la comparaison avec une compilation locale est impossible, car les versions du compilateur diffèrent (environ 81 000 octets d'écart, mesurés sur la 3.3). Le chef de projet a choisi de signer la compilation locale.*
- **Deux clés publiques embarquées** (`src/Core/UpdateKeys.cs`) : la principale, et une clé de secours rangée hors ligne pour pouvoir changer de clé. Les clés privées sont chiffrées par mot de passe (PBKDF2-SHA256, AES-256, HMAC), stockées hors OneDrive (`%USERPROFILE%\BureauCache-cles`) et copiées sur une clé USB.
- **Dépôt officiel seulement** : un dépôt personnalisé (fork) n'a pas la clé de l'éditeur. Pour lui, la vérification reste le SHA-256, et la confirmation d'installation l'indique.
**Alternatives écartées :** Azure Trusted Signing (inaccessible aux particuliers de l'UE, payant) ; Certum (environ 50 à 70 € par an) ; SignPath Foundation (gratuit, mais certificat au nom de la fondation, délai d'admission, et un workflow compromis peut demander une signature : à reconsidérer plus tard pour SmartScreen seulement) ; attestations GitHub (aucune protection contre un dépôt compromis, invérifiables en C# 5) ; Ed25519 (absent du .NET Framework).
**Conséquences :** à partir de la 3.4, toute version du dépôt officiel doit être signée, sinon elles ne sont ni proposées ni installées. La 3.3 installe la 3.4 comme avant (SHA-256 seul) : cette première transition n'est **pas** protégée contre un GitHub déjà compromis (risque résiduel R-24). Les scripts de signature s'exécutent depuis la copie locale du dépôt : il faut relire tout changement de `outils/` avant de les lancer (R-25). Perdre les deux clés privées ou le mot de passe bloque les mises à jour automatiques : seul un téléchargement manuel resterait possible. SmartScreen n'est pas affecté.

## D-003 — Calculatrice : la puissance avant le signe (2026-11-23) · Statut : Acceptée

**Contexte :** `-2^2` valait 4, parce que le moins unaire était appliqué avant la puissance (R-20).
**Décision (chef de projet) :** on suit la notation mathématique : `-2^2 = -(2^2) = -4`. La puissance reste associative à droite, l'exposant peut être signé (`2^-1`), et le pourcentage s'applique avant la puissance (`10%^2 = 0,01`).
**Alternatives écartées :** la convention d'Excel (4), moins attendue dans un lanceur qui ressemble à une calculatrice de poche ou à un moteur de recherche.
**Conséquences :** un calcul `-x^n` copié depuis Excel peut donner un résultat différent. Les tests de `CalcTests` fixent ce comportement.

## D-002 — Tests dans un projet séparé qui référence BureauCache.exe (2026-11-23) · Statut : Acceptée

**Contexte :** aucun test automatisé n'existait. La logique pure (`Calc`, `Search`, normalisation, validation de noms) est testable sans interface.
**Décision :** le dossier `tests/BureauCache.Tests` contient un projet SDK (MSTest, cible `net48`) qui **référence le `BureauCache.exe` produit par `build.cmd`**. Un `InternalsVisibleTo("BureauCache.Tests")` dans `AssemblyInfo.cs` lui donne accès aux classes internes. Le projet relance `build.cmd` avant chaque compilation des tests (`-p:SkipAppBuild=true` pour l'éviter). Les tests tournent en série (`DoNotParallelize`), à cause de l'état statique de l'application. Le workflow `.github/workflows/tests.yml` les lance sur chaque PR.
**Alternatives écartées :** inclure les sources par lien. `Calc` dépend de `L`, qui dépend de `Settings`, et ainsi de suite : il faudrait entraîner presque toute l'application, compilée par un autre compilateur que celui du binaire distribué.
**Conséquences :** les tests portent sur le binaire réel. Le .NET SDK n'est nécessaire que pour les tests. Un test qui modifie un état statique (`L.CurrentLanguage`, `Settings`) doit le restaurer, par exemple avec `LanguageScope`. Les tests sont écrits en C# 7.3 (valeur par défaut de `net48`), le code de `src/` reste en C# 5.


## D-006 - Langue espagnole et capture du panneau

Les traductions restent embarquées dans `L.T(fr, en, es)`. Les identifiants existants 0 à 2 sont préservés et 3 désigne l’espagnol. L’anglais sert de repli pour une traduction espagnole absente. La culture d’interface du système reste disponible pour le choix automatique.

Le panneau est visible par défaut dans les captures compatibles de l’écran entier. Le masquage est un choix explicite. Le flou dynamique réutilise cette exclusion si elle réussit. Avec le panneau visible en capture, une image du fond est prise avant son affichage.

Source Win32 : [SetWindowDisplayAffinity](https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-setwindowdisplayaffinity).
