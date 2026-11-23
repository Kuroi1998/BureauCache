# Tâches

_Statuts : À faire · En cours · Revue · Fait. Porteurs : Toi · Claude · Gemini · ChatGPT._
_Une tâche terminée passe dans « Fait » avec sa date, puis disparaît à la version suivante (l'historique reste dans git et le CHANGELOG)._

## Tableau

| ID | Titre | Statut | Porteur | Branche | Lien |
|---|---|---|---|---|---|
| T-001 | Mettre en place le projet de tests (`tests/`) | Revue | Claude | `chore/T-001-tests` | D-002 |
| T-002 | Tests de `Calc` (séparateurs FR/EN, exposants, limites) | Revue | ChatGPT puis Claude | `chore/T-001-tests` | QA-03, R-16 |
| T-007 | Calculatrice : séparateurs de milliers et virgule après le point | À faire | Claude | — | R-19, R-16 |
| T-008 | Calculatrice : convention de `-2^2` et précision des petits nombres | À faire (décision humaine pour R-20) | Toi puis Claude | — | R-20, R-21 |
| T-003 | Rangement : ne pas archiver un dossier dont l'activité n'est pas confirmée | À faire | Claude | — | R-01 |
| T-004 | Rangement concurrent d'une opération utilisateur sur le même élément | À faire | Claude | — | R-03 |
| T-005 | Politique de confiance des mises à jour (signature indépendante ?) | À faire | Gemini (recherche) puis Claude | — | R-04 |
| T-006 | Conserver l'ancienne version jusqu'à un démarrage réussi | À faire | Claude | — | R-07 |

Les autres risques ouverts (R-05 à R-18) restent dans [AUDIT_QA_BureauCache.md](AUDIT_QA_BureauCache.md) jusqu'à leur planification.

---

## Tâche en cours : T-001 — Projet de tests

**Objectif :** pouvoir tester automatiquement la logique de l'application, en local et en CI.
**Critères d'acceptation :**
- [x] `dotnet test tests/BureauCache.Tests` compile l'exe avec build.cmd puis lance les tests
- [x] Premiers tests de `Calc` (opérateurs, nombres, fonctions, refus, format, aller-retour)
- [x] Workflow `.github/workflows/tests.yml` sur les pull requests et sur `main`
- [x] Compile avec build.cmd sans nouvel avertissement
- [ ] Workflow vu vert sur GitHub
**Fichiers concernés :** `tests/BureauCache.Tests/*`, `src/Properties/AssemblyInfo.cs`, `.github/workflows/tests.yml`
**Décisions liées :** D-002
**Journal :**
- 2026-11-23 · Claude · Projet créé : 52 tests réussis, 5 ignorés. Les tests ignorés montrent le défaut R-19 (séparateur de milliers), suivi en T-007.
- 2026-11-23 · ChatGPT · T-002 : `CalcEdgeCaseTests.cs`, 61 cas dont 18 échecs présumés.
- 2026-11-23 · Claude · Revue de T-002 : les 18 échecs sont reproduits. 14 sont retenus (R-16, R-19, R-20, R-21) et 4 écartés parce qu'ils supposaient une spécification non voulue : refuser `1,234.5` en français, accepter `50%%`, exiger 64 paires de parenthèses. Suite totale : 97 réussis, 19 ignorés.

<!-- Recopier ce bloc pour la tâche active :

## Tâche en cours : T-00X — Titre
**Objectif :** une phrase.
**Critères d'acceptation :**
- [ ] …
- [ ] Compile avec build.cmd sans nouvel avertissement
- [ ] CHANGELOG mis à jour si visible par l'utilisateur
**Fichiers concernés :** …
**Décisions liées :** D-xxx
**Questions ouvertes :** …
**Journal :**
- AAAA-MM-JJ · Porteur · ce qui a été fait / vérifié
-->
