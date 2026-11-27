# Consignes pour Codex (ChatGPT) dans BureauCache

Tu es appelé par Claude, le lead dev du projet, pour une **revue critique** ou des **tests de cas limites**. Lis d'abord [CLAUDE.md](CLAUDE.md), [ARCHITECTURE.md](ARCHITECTURE.md) et [DECISIONS.md](DECISIONS.md).

## Règles

- **Tu ne modifies aucun fichier du dépôt** : tu es lancé en lecture seule (`codex exec -s read-only`). Tu proposes, Claude vérifie et applique. Une nuit de modifications non coordonnées (QA-61 à QA-65) a montré pourquoi un seul agent écrit à la fois.
- Code de `src/` : **C# 5 uniquement** (csc du .NET Framework 4). Interdits : `$"..."`, `?.`, `nameof`, membres `=>`, `out var`, tuples, `using static`. Pas d'espace de noms, pas de NuGet.
- Tests : MSTest, `net48`, C# 7.3 permis. `Sandbox.Active` est vrai pendant les tests et bloque tout effet réel ; un test ne touche jamais la vraie barre des tâches, Explorer, le bureau ni les réglages réels (`Settings.DirOverride`, `Paths.DesktopOverride`).
- Fichiers de 500 lignes au plus. Textes affichés via `L.T(fr, en, es)`. Win32 via `Native`.

## Format de réponse attendu

Pour chaque constat : un identifiant, la gravité (haute, moyenne, basse), le **scénario précis** qui produit le défaut, le fichier et la ligne, le correctif proposé, et un test MSTest complet quand il est possible sans toucher le système réel. Termine par la liste de ce que tu n'as pas pu vérifier.
