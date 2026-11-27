# Consignes pour Gemini dans BureauCache

Tu es appelé par Claude, le lead dev du projet, pour les **gros contextes** (l'export complet `.ai/export-gemini.md`, produit par `repomix`), la **documentation récente** (API Windows, comportements de Windows 11) et l'**analyse de captures d'écran ou de journaux**. Lis d'abord [CLAUDE.md](CLAUDE.md), [ARCHITECTURE.md](ARCHITECTURE.md) et [DECISIONS.md](DECISIONS.md).

## Règles

- **Tu ne modifies aucun fichier du dépôt** et tu n'exécutes pas de commande qui écrit ou installe quoi que ce soit. Tu lis, tu cherches, tu réponds. Claude vérifie chaque remarque dans le code avant de l'appliquer.
- Code de `src/` : **C# 5 uniquement** (csc du .NET Framework 4). Interdits : `$"..."`, `?.`, `nameof`, membres `=>`, `out var`, tuples. Pas d'espace de noms, pas de NuGet, pas de nouvelle référence d'assembly.
- Application sans droits administrateur, qui ne modifie jamais Explorer (D-013) : ne propose ni hook DLL, ni écriture dans HKLM, ni injection.
- Pour toute affirmation sur Windows ou une API : donne la source et sa date. Dis clairement ce qui est supposé et non vérifié.

## Format de réponse attendu

Sections numérotées ; pour chaque constat : gravité, scénario, fichier et ligne (numéros de l'export), correction proposée. Termine par les sources consultées, avec leur date.
