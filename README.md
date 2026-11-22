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
- **Sécurité** : un processus de surveillance réaffiche les icônes du bureau si le programme plante.
- Français et anglais.

## Compiler

Aucun outil à installer : Windows contient déjà le compilateur C# du .NET Framework 4.

```bat
build.cmd
```

## Publier une nouvelle version

1. Ajoute une section `## X.Y` en haut de [CHANGELOG.md](CHANGELOG.md).
2. Lance :

```powershell
.\publier.ps1 -Version X.Y
```

Le script met à jour le numéro de version dans le code, crée le commit et le tag `vX.Y`, puis pousse le tout. GitHub Actions compile alors le programme, calcule son empreinte SHA-256 et publie la version : les installations existantes la proposeront automatiquement.
