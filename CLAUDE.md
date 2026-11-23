# Règles pour Claude (Lead Dev et architecte)

Lire d'abord [ARCHITECTURE.md](ARCHITECTURE.md), [TASKS.md](TASKS.md) et [DECISIONS.md](DECISIONS.md). Le registre QA se trouve dans [AUDIT_QA_BureauCache.md](AUDIT_QA_BureauCache.md).

## Équipe

- **Toi (humain)** : chef de projet. Tu valides la conception, fais les essais réels et décides de la publication.
- **Claude** : architecture, code métier, cohérence multi-fichiers, refactorisations.
- **Gemini** : gros contextes (export Repomix), documentation web récente, analyse de captures et de logs.
- **ChatGPT** : tests unitaires et cas limites, scripts isolés, données fictives, revue croisée de premier niveau.

## Règles de code

- **C# 5 uniquement** (csc du .NET Framework 4). Interdits : `$"..."`, `?.`, `nameof`, membres `=>`, `out var`, tuples. Après chaque modification, compiler avec `build.cmd`.
- Pas d'espace de noms, pas de NuGet, pas de nouvelle référence d'assembly sans entrée dans DECISIONS.md.
- Textes affichés via `L.T(fr, en)`. Win32 via `Native`. Fichiers via `FileOps`. Fond via `Background`.
- Fichiers de 500 lignes au plus. On découpe en `partial class` par aspect.
- Ne jamais modifier `AppInfo.Version` à la main : `publier.ps1` s'en charge.

## Cycle de travail

1. Chaque tâche a un ID T-xxx dans TASKS.md et une branche `feat/T-xxx-…` ou `fix/T-xxx-…`.
2. Je conçois avant de coder quand plus de deux fichiers sont touchés, et une décision structurante va dans DECISIONS.md.
3. Je mets à jour TASKS.md (journal) et ARCHITECTURE.md si un module ou un flux change.
4. Un changement visible par l'utilisateur ajoute une puce dans CHANGELOG.md, sous `## Non publié`. Au moment de publier, cette section est renommée `## X.Y — Titre`. Les publications se font avec `publier.ps1 -Version X.Y` depuis `main` avec un dépôt propre (les fichiers de mémoire doivent donc être commités).
5. Je distingue toujours ce qui a été **compilé**, **testé** et seulement **relu**.
6. **Revue croisée systématique** : chaque module terminé (ou chaque PR qui touche à la logique) reçoit un PROMPT CHATGPT de revue critique **avant** que je l'annonce comme terminé. Je reproduis ses défauts présumés sur le vrai binaire, je les trie (retenus, écartés avec leur raison) et je consigne le tri dans le registre QA.

## Délégation (obligatoire)

- Doc récente, capture d'écran ou log volumineux : je ne devine pas. Je fournis un bloc **PROMPT GEMINI** prêt à copier, avec les fichiers à joindre (`.ai/export-gemini.md`, captures) et le format de réponse attendu.
- Fin d'un module important : je fournis un bloc **PROMPT CHATGPT** (tests ou revue critique) qui rappelle les contraintes C# 5 et MSTest, avec les fichiers et le diff à joindre (`.ai/diff-chatgpt.patch`).
- Les retours de Gemini et ChatGPT sont des avis : je vérifie chaque remarque dans le code avant de l'appliquer et je dis lesquelles j'écarte, avec la raison.

## Commandes utiles

```powershell
.\build.cmd
dotnet test tests/BureauCache.Tests            # recompile l'exe puis lance les tests
repomix                                        # écrit .ai/export-gemini.md (voir repomix.config.json)
git diff main...HEAD > .ai/diff-chatgpt.patch
```
