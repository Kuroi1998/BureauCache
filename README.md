# BureauCache

Un bureau Windows propre : les icônes sont cachées et réapparaissent dans un panneau élégant quand la souris touche le bord de l'écran, ou avec un raccourci clavier. Le panneau sert aussi de lanceur : applications, fichiers récents, paramètres Windows, calculs et recherche web.

## Installation

1. Télécharge `BureauCache.exe` depuis la [dernière version](https://github.com/Kuroi1998/BureauCache/releases/latest).
2. Lance-le, puis clique sur **Installer** dans Paramètres > Général.

Aucun droit administrateur n'est nécessaire. Le programme se met ensuite à jour tout seul : il vérifie chaque jour s'il existe une nouvelle version, contrôle l'empreinte SHA-256 du fichier téléchargé, puis te demande avant de l'installer.

## Fonctionnalités

- **Panneau au bord de l'écran** (gauche ou droite, tout le bord ou une partie), **raccourci clavier** configurable (Ctrl+Alt+B par défaut), désactivé automatiquement pendant les jeux en plein écran.
- **Recherche universelle** tolérante aux fautes de frappe : bureau, applications du menu Démarrer et du Store, fichiers récents, paramètres Windows et commandes système.
  - `= 12*7` : calculatrice (Entrée copie le résultat)
  - `? météo` : recherche web
  - `>` : commandes système (verrouiller, veille, redémarrer…)
- **Organisation** : favoris, récents, catégories automatiques (Dossiers, Jeux, Applications, Fichiers) et personnalisées, glisser-déposer, badge « Nouveau », rangement automatique des fichiers inutilisés.
- **Apparence** : thème clair/sombre/système, effet verre avec flou en direct, vue liste ou grille, aperçu au survol, widgets (heure, disque, corbeille).
- **Icône près de l'horloge** : clic pour la liste, double-clic pour les paramètres, menu complet, point d'information (mise à jour, nouveaux fichiers) ; réduire la fenêtre la range près de l'horloge.
- **Ergonomie** : visite guidée, annulation (Ctrl+Z), raccourcis clavier (F1 pour la liste), panneau redimensionnable et mode épinglé.
- **Sécurité** : un processus de surveillance réaffiche les icônes du bureau si le programme plante.
- Français et anglais.

## Compiler

Aucun outil à installer : Windows contient déjà le compilateur C# du .NET Framework 4.

```bat
build.cmd
```

## Organisation du code

Le code est réparti par thème dans `src/` (aucun fichier ne dépasse 500 lignes) :

| Dossier | Contenu |
|---|---|
| `src/App` | Démarrage (`Program`), application et icône de notification (`App`), raccourci clavier global |
| `src/Core` | Réglages, installation, mises à jour, rangement automatique, langue, appels Windows (`Native`), utilitaires communs (`Helpers`) |
| `src/Data` | Éléments affichés (`Items`), chargement du bureau, des applications et des fichiers récents (`Loader`), paramètres Windows et commandes (`SystemCatalog`) |
| `src/Search` | Recherche tolérante aux fautes (`Search`) et calculatrice (`Calc`) |
| `src/Shell` | Icônes et miniatures (`Icons`), raccourcis et actions du shell Windows (`ShellUtil`) |
| `src/UI` | Thème et dessin (`Theme`, `Glyphs`), contrôles, boîtes de dialogue, visite guidée |
| `src/UI/Panel` | Le panneau : `PanelForm.*` (fenêtre, données, actions, clavier, effet verre), liste des éléments (`ItemsView.*`), aperçu et notifications |
| `src/UI/Settings` | La fenêtre de paramètres, une partie par fichier (`SettingsForm.*`) |
| `src/Properties` | Nom, copyright et **numéro de version** (`AppInfo.Version`, seul endroit où il est écrit) |

Les grandes fenêtres sont découpées avec `partial class` : chaque fichier `Classe.Partie.cs` contient une partie de la même classe.

## Publier une nouvelle version

1. Ajoute une section `## X.Y — Titre` en haut de [CHANGELOG.md](CHANGELOG.md) (le titre apparaît sur GitHub).
2. Lance :

```powershell
.\publier.ps1 -Version X.Y
```

Le script met à jour le numéro de version dans `src/Properties/AssemblyInfo.cs`, compile, crée le commit et le tag `vX.Y`, puis pousse le tout. GitHub Actions compile alors le programme, calcule son empreinte SHA-256 et crée la version **en brouillon**.

3. Une fois la compilation terminée, signe et publie :

```powershell
.\outils\signer-version.ps1 -Version X.Y
```

Le script télécharge l'exe du brouillon, contrôle sa version et son empreinte, le signe avec la clé privée (demande du mot de passe), ajoute `BureauCache.exe.sig` puis publie. Les installations existantes proposeront alors la version, et ne l'installeront que si la signature est valide. La clé privée ne passe jamais par GitHub : un compte GitHub piraté ne suffit donc pas à diffuser une fausse mise à jour. Les clés se créent une seule fois avec `outils\creer-cles.ps1`.

## Licence

BureauCache est distribué sous licence [MIT](LICENSE) : tu peux l'utiliser, le modifier et le redistribuer librement, en conservant la mention de copyright.
