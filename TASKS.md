# Tâches

_Statuts : À faire · En cours · Revue · Fait. Porteurs : Toi · Claude · Gemini · ChatGPT._
_Une tâche terminée passe dans « Fait » avec sa date, puis disparaît à la version suivante (l'historique reste dans git et le CHANGELOG)._

## Tableau

| ID | Titre | Statut | Porteur | Branche | Lien |
|---|---|---|---|---|---|
| T-001 | Mettre en place le projet de tests (`tests/`) | Revue | Claude | `chore/T-001-tests` | D-002 |
| T-002 | Tests de `Calc` (séparateurs FR/EN, exposants, limites) | Revue | ChatGPT puis Claude | `chore/T-001-tests` | QA-03, R-16 |
| T-007 | Calculatrice : séparateurs de milliers et virgule après le point | Revue | Claude | `fix/T-007-calc-separateurs` | QA-13 (ex R-19, R-16) |
| T-008 | Calculatrice : convention de `-2^2` et précision des petits nombres | Revue | Claude | `fix/T-007-calc-separateurs` | QA-14, QA-15, D-003 |
| T-003 | Rangement : ne pas archiver un dossier dont l'activité n'est pas confirmée | Revue | Claude | `fix/T-003-T-004-rangement` | QA-16 (ex R-01) |
| T-004 | Rangement concurrent d'une opération utilisateur sur le même élément | Revue (essai manuel à faire) | Claude puis Toi | `fix/T-003-T-004-rangement` | QA-17 (ex R-03) |
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
- [x] Workflow vu vert sur GitHub
**Fichiers concernés :** `tests/BureauCache.Tests/*`, `src/Properties/AssemblyInfo.cs`, `.github/workflows/tests.yml`
**Décisions liées :** D-002
**Journal :**
- 2026-11-23 · Claude · Projet créé : 52 tests réussis, 5 ignorés. Les tests ignorés montrent le défaut R-19 (séparateur de milliers), suivi en T-007.
- 2026-11-23 · ChatGPT · T-002 : `CalcEdgeCaseTests.cs`, 61 cas dont 18 échecs présumés.
- 2026-11-23 · Claude · Revue de T-002 : les 18 échecs sont reproduits. 14 sont retenus (R-16, R-19, R-20, R-21) et 4 écartés parce qu'ils supposaient une spécification non voulue : refuser `1,234.5` en français, accepter `50%%`, exiger 64 paires de parenthèses. Suite totale : 97 réussis, 19 ignorés.
- 2026-11-23 · Claude · T-007 : `Calc.NormalizeNumbers` lit chaque nombre séparément. R-19 et R-16 sont corrigés (QA-13). `12,34.5` est désormais refusé (décision R-16 tranchée), et un nombre au format français collé en anglais est accepté, par symétrie. Suite : 130 réussis, 3 ignorés (R-20, R-21).
- 2026-11-23 · Toi · R-20 : notation mathématique retenue (D-003).
- 2026-11-23 · Claude · T-008 : `-2^2 = -4` (QA-14) et notation scientifique sous 1e-4 (QA-15). Suite : 145 réussis, 0 ignoré.
- 2026-11-23 · Claude · T-003 : `Archiver.ActivityOf` parcourt tout le dossier (budget de 5 000 entrées ; sinon pas de rangement). 9 tests. T-004 : rangement reporté si le panneau est occupé ou si une annulation est en attente, et revérification avant chaque déplacement. Suite : 154 réussis.
- 2026-11-23 · ChatGPT · Revue croisée de T-003 et T-004 : 11 remarques et 7 tests (`ArchiverReviewTests.cs`).
- 2026-11-23 · Claude · Tri de la revue, avec reproduction sur le binaire réel, y compris de vraies jonctions et le changement d'heure. **6 défauts corrigés (QA-18)** : évaluation juste avant chaque déplacement, report sous fenêtre modale, jonctions et liens, comparaison en UTC, journal enregistré immédiatement, dossier d'archive OneDrive. **4 remarques écartées** : la date de dernier accès n'est pas fiable (limite assumée R-22) ; une archive renommée devient un dossier ordinaire ; le remplacement du journal par le dernier lot est voulu (« Annuler le dernier rangement ») ; une date future protège le dossier par prudence. **Restent ouverts** : la performance sur le fil d'interface et OneDrive « à la demande » (R-23). Suite : 161 réussis.

**Essai manuel T-004 (à faire par toi, sur un bureau de test) :** activer le rangement à 30 jours, placer sur le bureau un fichier daté de plus de 30 jours, puis l'ouvrir en renommage depuis le panneau (F2) **au moment** du rangement, soit 1 min après le démarrage de BureauCache. Laisser le dialogue ouvert plus d'une minute. Résultat attendu : rien n'est rangé pendant que le dialogue est ouvert. Après validation et la fin du délai d'annulation (7 s), le rangement a lieu à la minute suivante.

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
