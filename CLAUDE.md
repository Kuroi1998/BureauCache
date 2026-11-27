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
- Ne jamais manipuler les clés privées de signature ni leur mot de passe (`*.cle`, D-004) : seul le chef de projet lance `creer-cles.ps1` et `signer-version.ps1`. `src/Core/UpdateKeys.cs` ne contient que des clés publiques.

## Cycle de travail

1. Chaque tâche a un ID T-xxx dans TASKS.md et une branche `feat/T-xxx-…` ou `fix/T-xxx-…`.
2. Je conçois avant de coder quand plus de deux fichiers sont touchés, et une décision structurante va dans DECISIONS.md.
3. Je mets à jour TASKS.md (journal) et ARCHITECTURE.md si un module ou un flux change.
4. Un changement visible par l'utilisateur ajoute une puce dans CHANGELOG.md, sous `## Non publié`. Au moment de publier, cette section est renommée `## X.Y — Titre`. Les publications se font avec `publier.ps1 -Version X.Y` depuis `main` avec un dépôt propre (les fichiers de mémoire doivent donc être commités).
5. Je distingue toujours ce qui a été **compilé**, **testé** et seulement **relu**.
6. **Revue croisée systématique** : chaque module terminé (ou chaque PR qui touche à la logique) reçoit un PROMPT CHATGPT de revue critique **avant** que je l'annonce comme terminé. Je reproduis ses défauts présumés sur le vrai binaire, je les trie (retenus, écartés avec leur raison) et je consigne le tri dans le registre QA.

## Délégation (obligatoire)

- Doc récente, capture d'écran ou log volumineux : je ne devine pas. Je fais appel à **Gemini**, avec l'export `.ai/export-gemini.md` (régénéré par `repomix` juste avant) ou les captures, et le format de réponse attendu.
- Fin d'un module important : je fais appel à **Codex (ChatGPT)** pour des tests ou une revue critique qui rappelle les contraintes C# 5 et MSTest, sur le diff `.ai/diff-chatgpt.patch`.
- Les retours de Gemini et ChatGPT sont des avis : je vérifie chaque remarque dans le code avant de l'appliquer et je dis lesquelles j'écarte, avec la raison.

### Appel direct depuis le terminal

Gemini CLI et Codex CLI sont installés (npm, profil utilisateur). Je les lance moi-même, **toujours en lecture seule** : un seul agent écrit dans le dépôt, moi. Les nuits de modifications non coordonnées (QA-61 à QA-65) ne doivent pas se reproduire. Ils lisent leurs consignes dans [GEMINI.md](GEMINI.md) et [AGENTS.md](AGENTS.md).

```powershell
repomix                                                                   # export à jour avant Gemini
gemini --approval-mode plan -p "…"                                        # Gemini, lecture seule
git diff main...HEAD > .ai/diff-chatgpt.patch
codex exec -s read-only "…"                                               # Codex, lecture seule
```

- Les appels utilisent les comptes du chef de projet : je dis quand j'en lance un et pourquoi (une revue par module, une recherche par question ouverte), sans boucle.
- Si un outil n'est pas connecté ou échoue, je reviens au bloc **PROMPT GEMINI** ou **PROMPT CHATGPT** prêt à copier.
- Je ne leur demande jamais d'écrire, de lancer `--yolo`, `-s workspace-write` ou `--dangerously-bypass-approvals-and-sandbox`, et je ne leur transmets aucun secret (clés de signature, mots de passe).

## Commandes utiles

```powershell
.\build.cmd
dotnet test tests/BureauCache.Tests            # recompile l'exe puis lance les tests
.\outils\signer-version.ps1 -Version X.Y       # après publier.ps1 et la CI : signe et publie le brouillon (D-004)
repomix                                        # écrit .ai/export-gemini.md (voir repomix.config.json)
git diff main...HEAD > .ai/diff-chatgpt.patch
```
