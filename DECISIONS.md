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

## D-002 — Tests dans un projet séparé qui référence BureauCache.exe (2026-11-23) · Statut : Acceptée

**Contexte :** aucun test automatisé n'existait. La logique pure (`Calc`, `Search`, normalisation, validation de noms) est testable sans interface.
**Décision :** le dossier `tests/BureauCache.Tests` contient un projet SDK (MSTest, cible `net48`) qui **référence le `BureauCache.exe` produit par `build.cmd`**. Un `InternalsVisibleTo("BureauCache.Tests")` dans `AssemblyInfo.cs` lui donne accès aux classes internes. Le projet relance `build.cmd` avant chaque compilation des tests (`-p:SkipAppBuild=true` pour l'éviter). Les tests tournent en série (`DoNotParallelize`), à cause de l'état statique de l'application. Le workflow `.github/workflows/tests.yml` les lance sur chaque PR.
**Alternatives écartées :** inclure les sources par lien. `Calc` dépend de `L`, qui dépend de `Settings`, et ainsi de suite : il faudrait entraîner presque toute l'application, compilée par un autre compilateur que celui du binaire distribué.
**Conséquences :** les tests portent sur le binaire réel. Le .NET SDK n'est nécessaire que pour les tests. Un test qui modifie un état statique (`L.En`, `Settings`) doit le restaurer, par exemple avec `LanguageScope`. Les tests sont écrits en C# 7.3 (valeur par défaut de `net48`), le code de `src/` reste en C# 5.
