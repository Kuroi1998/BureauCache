# Décisions d'architecture

_Une entrée par décision, courte. On n'efface rien : une décision remplacée est marquée « Remplacée par D-xxx »._

<!-- Modèle :
## D-00X — Titre (AAAA-MM-JJ) · Statut : Proposée / Acceptée / Remplacée par D-xxx
**Contexte :** pourquoi la question se pose.
**Décision :** ce qu'on fait.
**Alternatives écartées :** et pourquoi.
**Conséquences :** ce que ça impose ensuite.
-->

## D-001 — Compilation par csc.exe du .NET Framework, C# 5, sans dépendance (2026-11-23) · Statut : Acceptée

**Contexte :** l'application doit pouvoir être compilée sur n'importe quel Windows sans rien installer (`build.cmd`).
**Décision :** on conserve le compilateur fourni avec Windows, sans `.csproj` ni NuGet. Le code de `src/` reste en C# 5.
**Alternatives écartées :** projet SDK `dotnet build`. Il apporterait un C# récent, mais imposerait une installation au contributeur et à la CI.
**Conséquences :** tout code proposé par une IA doit être relu pour la syntaxe C# 5. `build.cmd` est le test de compilation de référence.

## D-002 — Tests dans un projet séparé (2026-11-23) · Statut : Proposée

**Contexte :** aucun test automatisé n'existe. La logique pure (`Calc`, `Search`, normalisation, validation de noms) est testable sans interface.
**Décision proposée :** un dossier `tests/BureauCache.Tests` avec un projet SDK (MSTest, cible `net48`) qui inclut les fichiers sources testés par lien. L'exécution se fait par `dotnet test`. Le dossier `tests/` reste hors de `src/` pour ne pas entrer dans la compilation de l'exe.
**À trancher :** inclusion des sources par lien ou référence à `BureauCache.exe`. Faut-il ajouter `dotnet test` à la CI ?
**Conséquences :** le .NET SDK devient nécessaire pour les tests seulement. La compilation de l'application reste inchangée.
