# Architecture de BureauCache

_Mise à jour : 2026-11-23 · Version : 3.3 · Source de vérité partagée entre Claude, Gemini et ChatGPT_

## Vue d'ensemble

BureauCache cache les icônes du bureau Windows et les réaffiche dans un panneau qui sort du bord de l'écran, au survol ou par un raccourci clavier. Le panneau sert aussi de lanceur : applications, fichiers récents, paramètres Windows, calculatrice et recherche web. Les fonctions complètes sont décrites dans le [README](README.md).

## Contraintes techniques (non négociables)

| Contrainte | Conséquence pour le code |
|---|---|
| Compilé par `csc.exe` du .NET Framework 4 (`build.cmd`) | **C# 5 au maximum.** Interdits : `$"..."`, `?.`, `??=`, `nameof`, membres `=>`, `out var`, tuples, `using static`, initialiseurs de propriétés auto. |
| Pas de `.csproj` ni de NuGet | Références limitées à `Microsoft.CSharp`, `System.Core` et `Microsoft.VisualBasic` en plus des références par défaut. Toute dépendance externe est exclue. |
| Tous les `src/**/*.cs` compilés ensemble (`-recurse`) | Pas d'espace de noms : les classes sont globales. Un nouveau fichier est inclus automatiquement. |
| Windows uniquement, application WinForms | Interop Win32 centralisée dans `Native` (`src/Core/Native.cs`). |
| Bilingue français et anglais | Chaque texte affiché passe par `L.T("français", "english")`. |
| Sans droits administrateur | Installation dans le profil utilisateur, sans écriture dans HKLM. |

## Modules

| Dossier | Responsabilité | Types principaux | Dépend de |
|---|---|---|---|
| `src/App` | Point d'entrée, instance unique, surveillance, cycle de vie, icône de notification, raccourci global | `Program`, `App` (partial : `App.cs`, `App.Tray.cs`), `HotkeyWindow` | Core, UI |
| `src/Core` | Réglages, installation et démarrage, mises à jour, rangement automatique, langue, Win32, utilitaires | `Settings`, `SettingsBackup`, `Installer`, `Startup`, `Updater`, `Archiver`, `L`, `Native`, `Paths`, `FileOps`, `Dialogs`, `Background` | — |
| `src/Data` | Modèle des éléments, chargement du bureau, des applications et des récents, catalogue système | `DesktopItem`, `ItemKind`, `Catalog`, `Group`, `VRow`, `Loader`, `SystemCatalog` | Core, Shell |
| `src/Search` | Recherche tolérante aux fautes, calculatrice | `Search`, `Calc` (+ `Calc.Parser`) | Data, Core (`L.Culture`) |
| `src/Shell` | Icônes, miniatures et cache, verbes et raccourcis du shell ; fil STA dédié aux verbes de « Plus d'options » | `Icons`, `IconCache`, `ShellUtil`, `ShellWorker` | Core |
| `src/UI` | Thème et dessin, glyphes, dialogues, visite guidée | `Theme`, `Ui`, `Menus`, `Glyphs`, `ThemedDialog`, `BackupDialog`, `WelcomeForm` | Core |
| `src/UI/Controls` | Contrôles dessinés à la main | `Toggle`, `Segmented`, `Slider`, `Swatches`, `HotkeyBox`, `FlatButton`, `SideNav`… | UI |
| `src/UI/Panel` | Panneau principal | `PanelForm` (partial : `.cs`, `.Window`, `.Data`, `.Actions`, `.Input`), `ItemsView` (partial : `.cs`, `.Input`), `PreviewForm`, `ToastControl` | Data, Search, Shell, UI |
| `src/UI/Settings` | Fenêtre de paramètres et disposition des six rubriques | `SettingsForm` (partial : `.cs`, `.Pages`, `.Values`, `.Advanced`, `.Layout`), `SettingsNavigation`, `SettingsActions` | Core, UI |
| `src/Properties` | Métadonnées et **seul emplacement du numéro de version** | `AppInfo.Version` | — |

## Conventions de code

- Une classe par responsabilité. Les grandes fenêtres sont découpées en `partial class` avec un fichier `Classe.Aspect.cs` par aspect.
- Aucun fichier ne dépasse **500 lignes**. Au-delà d'environ 400 lignes, on prévoit un découpage.
- Les réglages sont copiés une fois par jour avant leur première réécriture, dans `sauvegardes\` (7 jours, `SettingsBackup`, D-007). Restaurer ou importer met d'abord l'état actuel de côté. Les tests isolent le dossier des réglages (`Settings.DirOverride`).
- Les tâches de fond passent par `Background` (`src/Core/Helpers.cs`). Le retour sur le fil UI se fait par `BeginInvoke`, et l'UI n'est jamais touchée depuis un autre fil.
- Les opérations sur fichiers passent par `FileOps`. Les noms sont validés (noms réservés Windows refusés).
- Les réglages se modifient par `Settings`. L'écriture est groupée par minuteur et atomique.
- Les messages à l'utilisateur passent par `Dialogs`, les textes par `L.T(fr, en)`.
- Commentaires en français.

## Flux critiques

1. **Démarrage** (`Program.Main`) : restriction de la recherche des DLL à System32, prise en compte du DPI, `Settings.Load`, mutex `Local\BureauCache_SingleInstance`. Une seconde instance signale `ShowSettings` puis se ferme. Ensuite : nettoyage de l'ancienne version, lancement du processus de surveillance (`--watchdog <pid>`), `Application.Run(new App(...))`, puis restauration des icônes à la sortie.
2. **Ouverture du panneau** : le minuteur `App.Poll` détecte le bord de l'écran ou le raccourci, appelle `PanelForm.ShowPanel(screen)`, puis `RequestLoad` (chargement en tâche de fond par `Loader`), puis `ItemsView.SetGroups`.
3. **Recherche** : la frappe déclenche `Search` sur les noms normalisés. Un préfixe `=` appelle `Calc`, `?` lance la recherche web et `>` affiche les commandes de `SystemCatalog`.
4. **Rangement automatique** : `App.Poll` arrive à échéance (1 min après le démarrage, puis toutes les 6 h) et appelle `App.RunArchive`. Celui-ci est reporté d'une minute tant que le panneau est occupé ou qu'une annulation est en attente. Ensuite, `Archiver.RunNow` sélectionne les éléments dont l'ancienneté est confirmée (`ActivityOf`, qui parcourt tout le contenu d'un dossier) et les déplace dans le dossier d'archive. Le journal permet d'annuler depuis les paramètres (`Archiver.Undo`). « Ranger maintenant » passe par `Archiver.Run`, qui revérifie chaque élément.
5. **Mise à jour** : `Updater.Check` interroge l'API GitHub Releases, `Updater.Download` télécharge en HTTPS depuis GitHub et vérifie le SHA-256 et la version, puis l'application se remplace (l'ancienne version est gardée en `.old`) et se relance (`App.RelaunchPath`). La nouvelle version est « à l'essai » : `Updater.BeginStart` pose un marqueur `.trial`, et `App.Poll` appelle `ConfirmStart` après 10 s, ce qui efface `.old`. Un démarrage qui trouve un essai non confirmé restaure `.old` (`RollBack`).
6. **Publication** : `publier.ps1 -Version X.Y` (depuis `main` avec un dépôt propre) pousse le tag `vX.Y`. `.github/workflows/release.yml` compile, puis crée un **brouillon** avec l'exe, le `.sha256` et les notes tirées de `CHANGELOG.md`. Ensuite, `outils/signer-version.ps1` (en local, avec la clé privée) recompile le tag sur le PC (`compilation-commun.ps1`, sans exécuter de script du dépôt), remplace l'exe du brouillon, ajoute `BureauCache.exe.sig` (RSA-3072, SHA-256) et publie. `Updater` exige cette signature pour le dépôt officiel, contre les clés publiques de `src/Core/UpdateKeys.cs` (D-004).

7. **Météo** (D-005) : quand le panneau s'affiche, `RefreshWidgetData` appelle `RefreshWeatherIfDue`. Si la dernière météo date de plus de 30 min, `Weather.RefreshAsync` interroge Open-Meteo en arrière-plan (`Http.FetchText`, hôtes autorisés). La tuile (`PanelForm.Weather.cs`) dessine l'icône (`WeatherIcons`) ; un clic ouvre `WeatherDetailDialog`, un clic droit propose « Changer de lieu… » (`WeatherPlaceDialog` : recherche Open-Meteo ou `Weather.Locate`, c'est-à-dire la position Windows nommée par BigDataCloud).
8. **Nettoyage** (D-005) : clic droit sur la tuile Disque, puis `CleanupDialog`. `Cleaner.Categories` construit les cibles, `Cleaner.Measure` les analyse en arrière-plan et `Cleaner.Clean` supprime fichier par fichier. Les règles de sécurité sont dans `IsAllowed` et `Files` : racines autorisées, zones interdites, jonctions, fichiers en ligne uniquement, âge minimal et applications ouvertes.

## Langue et capture du panneau

La classe `L` résout le choix Système, français, anglais ou espagnol. Elle garde une culture explicite pour le formatage et utilise l’anglais si un texte espagnol manque. `App.OnSettingsChanged` compare les identifiants de langue pour recharger le panneau et recréer les Paramètres en préservant la page active.

`PanelForm` applique l’affinité Win32 correspondant au seul réglage `HidePanelInCapture`. Le flou continu exige une exclusion effective et un panneau détaché. Avec le panneau visible en capture, l’effet verre utilise une image du fond prise avant l’ouverture.

## Tests

`tests/BureauCache.Tests` (MSTest, `net48`) référence le vrai `BureauCache.exe` grâce à `InternalsVisibleTo` (voir D-002). La commande `dotnet test tests/BureauCache.Tests` recompile l'exe puis lance les tests. La CI les exécute sur chaque pull request (`.github/workflows/tests.yml`). Un test qui modifie la langue passe par `LanguageScope`.

## Points sensibles connus

Le registre à jour se trouve dans [AUDIT_QA_BureauCache.md](AUDIT_QA_BureauCache.md) (corrections QA-xx, risques ouverts R-xx). Les risques élevés concernent le rangement automatique (R-01, R-03) et la confiance des mises à jour (R-04).
