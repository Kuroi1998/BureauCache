# Tâches

_Statuts : À faire · En cours · Revue · Fait. Porteurs : Toi · Claude · Gemini · ChatGPT._
_Une tâche terminée passe dans « Fait » avec sa date, puis disparaît à la version suivante (l'historique reste dans git et le CHANGELOG)._

## Tableau

| ID | Titre | Statut | Porteur | Branche | Lien |
|---|---|---|---|---|---|
| T-020 | Sauvegarde automatique des réglages (D-007) | Fait (3.9) ; essai réel fait sur la 3.9 installée | Claude puis Toi | PR #19 | D-007, QA-32, QA-33 |
| T-019 | Bugs d'interface : image figée du panneau, dialogues, Paramètres (QA-30, QA-31) | Fait (3.8) ; essais manuels encore à faire | Claude puis Toi | PR #17 | QA-30 |
| T-018 | Verbes du shell sur un fil dédié, liste des écrans suivie (QA-28, QA-29) | Fait (3.8) ; essais manuels encore à faire | Claude | PR #16 | QA-28 |
| T-017 | Risques ouverts du registre (QA-26, QA-27) | Fait (3.8) ; essais manuels encore à faire | Claude | PR #15 | QA-26 |
| T-016 | Corrections de la revue globale 3.7.1 (QA-24, QA-25) | Fait (3.8) ; essais manuels encore à faire | Claude | PR #14 | QA-24 |
| T-015 | Espagnol et visibilité du panneau en partage d’écran | Revue (310 tests réussis, 6 tests de jonctions ignorés, bascule contrôlée dans une capture Windows) | ChatGPT | - | D-006, tests de localisation et de capture |
| T-014 | Refonte de la fenêtre Paramètres | Revue (navigation, défilement, clavier, contraste et changement de langue couverts par des tests ciblés) | ChatGPT | — | `src/UI/Settings`, `SettingsUiTests.cs` |
| T-001 | Mettre en place le projet de tests (`tests/`) | Fait (3.4) | Claude | PR #5 | D-002 |
| T-002 | Tests de `Calc` (séparateurs FR/EN, exposants, limites) | Fait (3.4) | ChatGPT puis Claude | PR #5 | QA-03 |
| T-007 | Calculatrice : séparateurs de milliers et virgule après le point | Fait (3.4) | Claude | PR #6 | QA-13 |
| T-008 | Calculatrice : convention de `-2^2` et précision des petits nombres | Fait (3.4) | Toi puis Claude | PR #6 | QA-14, QA-15, D-003 |
| T-003 | Rangement : ne pas archiver un dossier dont l'activité n'est pas confirmée | Fait (3.4) | Claude | PR #7 | QA-16, QA-18 |
| T-004 | Rangement concurrent d'une opération utilisateur sur le même élément | Fait (3.4) ; essai manuel encore à faire | Claude puis Toi | PR #7 | QA-17, QA-18 |
| T-006 | Conserver l'ancienne version jusqu'à un démarrage réussi | Fait (3.4) | Claude | PR #8 | QA-19, QA-21 |
| T-005 | Mises à jour signées par une clé hors de GitHub | Fait (3.4, publiée signée le 2026-11-23) | Gemini, Toi, Claude | PR #9 | QA-20, QA-21, D-004 |
| T-009 | Observer la mise à jour 3.3 → 3.4 sur une vraie installation (démarrage, `.old` effacé après 10 s) | Fait (2026-11-23 : 3.4 installée à 20:04, `.old` effacé) | Toi | — | QA-19, QA-20 |
| T-011 | Le bord n'ouvre plus la liste quand le bureau est au premier plan (après réduction près de l'horloge) | Fait (3.5, vérifié dans l'application) | Toi puis Claude | PR #11 | QA-22 |
| T-012 | Météo : tuile, détail sur 3 jours, lieu par recherche ou position Windows | Revue (revue ChatGPT faite, QA-23 ; essai dans l'application à faire) | Gemini, ChatGPT, Claude | `feat/meteo-nettoyage` | D-005, QA-23 |
| T-013 | Nettoyage rapide : catégories à cocher, analyse, confirmation, depuis la tuile Disque | Revue (revue ChatGPT faite, QA-23 ; premier nettoyage réel à faire) | Gemini, ChatGPT, Claude | `feat/meteo-nettoyage` | D-005, QA-23 |
| T-010 | Vérifier sur un dépôt de test qu'une installation 3.4 refuse une version non signée ou mal signée | À faire | Toi puis Claude | — | QA-20 |

Les autres risques ouverts (R-10 et R-23 en partie, R-22, R-24, R-25) restent dans [AUDIT_QA_BureauCache.md](AUDIT_QA_BureauCache.md) jusqu'à leur planification.

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
- 2026-11-23 · Claude · T-016 : revue globale de 3.7.1 par ChatGPT triée (11 retenus, C02 et C05 écartés) puis corrigée (QA-24). Suite : 331 réussis, 1 ignoré.
- 2026-11-24 · Claude · T-016 : revue de QA-24 par ChatGPT, 8 constats retenus et corrigés, Q24-05 écarté (QA-25). Suite : 342 réussis, 2 ignorés.
- 2026-11-24 · Claude · T-017 : risques R-05, R-06, R-08, R-11, R-13, R-15, R-17, R-18 corrigés, R-10 et R-23 en partie (QA-26). R-12 laissé ouvert. Suite : 345 réussis, 2 ignorés.
- 2026-11-24 · Claude · T-017 : revue de QA-26 par ChatGPT, 9 constats retenus et corrigés, Q26-10 et Q26-11 écartés (QA-27). Suite : 347 réussis, 3 ignorés.
- 2026-11-24 · Claude · T-018 : R-12 corrigé (fil STA dédié aux verbes du shell), R-10 complété (liste des écrans suivie) (QA-28). Suite : 350 réussis, 3 ignorés.
- 2026-11-24 · Claude · T-018 : revue de QA-28 par ChatGPT, 6 constats retenus et corrigés (QA-29). Suite : 356 réussis, 4 ignorés.
- 2026-11-24 · Claude · T-019 : image figée du panneau diagnostiquée sur la session réelle et corrigée, dialogues et Paramètres corrigés (QA-30). Suite : 358 réussis, 4 ignorés.
- 2026-11-24 · Toi · T-019 : correctif de l'image figée vérifié sur le binaire compilé.
- 2026-11-24 · Claude · T-019 : revue de QA-30 par ChatGPT, 8 constats retenus et corrigés (QA-31). Suite : 369 réussis, 5 ignorés.
- 2026-11-24 · Claude · Incident QA-32 : un test de Claude a écrasé les vrais réglages ; tests isolés dans un dossier temporaire. Suite : 369 réussis, 5 ignorés.
- 2026-11-24 · Toi · Version 3.8 publiée et signée (T-016 à T-019, PR #14 à #17).
- 2026-11-24 · Claude · T-020 : sauvegarde quotidienne des réglages (7 jours), restauration réversible depuis Paramètres → Avancé, import réversible (D-007). Suite : 375 réussis, 5 ignorés.
- 2026-11-24 · Claude · T-020 : revue de D-007 par ChatGPT, 10 constats retenus et corrigés, D007-12 écarté (QA-33). Suite : 387 réussis, 5 ignorés.
- 2026-11-24 · Toi · Version 3.9 publiée et signée (T-020, PR #19).
- 2026-11-24 · Toi · T-020 : essai réel sur la 3.9 installée réussi (copie du jour, restauration, retour en arrière).
- 2026-11-23 · Claude · Projet créé : 52 tests réussis, 5 ignorés. Les tests ignorés montrent le défaut R-19 (séparateur de milliers), suivi en T-007.
- 2026-11-23 · ChatGPT · T-002 : `CalcEdgeCaseTests.cs`, 61 cas dont 18 échecs présumés.
- 2026-11-23 · Claude · Revue de T-002 : les 18 échecs sont reproduits. 14 sont retenus (R-16, R-19, R-20, R-21) et 4 écartés parce qu'ils supposaient une spécification non voulue : refuser `1,234.5` en français, accepter `50%%`, exiger 64 paires de parenthèses. Suite totale : 97 réussis, 19 ignorés.
- 2026-11-23 · Claude · T-007 : `Calc.NormalizeNumbers` lit chaque nombre séparément. R-19 et R-16 sont corrigés (QA-13). `12,34.5` est désormais refusé (décision R-16 tranchée), et un nombre au format français collé en anglais est accepté, par symétrie. Suite : 130 réussis, 3 ignorés (R-20, R-21).
- 2026-11-23 · Toi · R-20 : notation mathématique retenue (D-003).
- 2026-11-23 · Claude · T-008 : `-2^2 = -4` (QA-14) et notation scientifique sous 1e-4 (QA-15). Suite : 145 réussis, 0 ignoré.
- 2026-11-23 · Claude · T-003 : `Archiver.ActivityOf` parcourt tout le dossier (budget de 5 000 entrées ; sinon pas de rangement). 9 tests. T-004 : rangement reporté si le panneau est occupé ou si une annulation est en attente, et revérification avant chaque déplacement. Suite : 154 réussis.
- 2026-11-23 · ChatGPT · Revue croisée de T-003 et T-004 : 11 remarques et 7 tests (`ArchiverReviewTests.cs`).
- 2026-11-23 · Claude · Tri de la revue, avec reproduction sur le binaire réel, y compris de vraies jonctions et le changement d'heure. **6 défauts corrigés (QA-18)** : évaluation juste avant chaque déplacement, report sous fenêtre modale, jonctions et liens, comparaison en UTC, journal enregistré immédiatement, dossier d'archive OneDrive. **4 remarques écartées** : la date de dernier accès n'est pas fiable (limite assumée R-22) ; une archive renommée devient un dossier ordinaire ; le remplacement du journal par le dernier lot est voulu (« Annuler le dernier rangement ») ; une date future protège le dossier par prudence. **Restent ouverts** : la performance sur le fil d'interface et OneDrive « à la demande » (R-23). Suite : 161 réussis.
- 2026-11-23 · Claude · T-006 : la nouvelle version est à l'essai tant que `.old` existe, avec retour automatique si un démarrage n'atteint pas 10 s (QA-19). 7 tests. Suite : 168 réussis. T-005 : recherche confiée à Gemini.
- 2026-11-23 · Gemini · T-005 : comparatif des signatures disponibles en 2026 ; recommandation d'une signature RSA détachée.
- 2026-11-23 · Claude · Vérification de Gemini : la vérification RSA-SHA256 fonctionne avec le fournisseur par défaut sur cette machine (l'avertissement « type 24 impératif » est exagéré ; le type 24 est gardé par prudence). Ses recommandations sont retenues, avec trois ajouts : clé hors OneDrive, clé de secours, signature exigée pour le dépôt officiel seulement.
- 2026-11-23 · Toi · Choix (D-004) : RSA détachée ; compilation par la CI et signature en local ; clé chiffrée avec une copie de secours.
- 2026-11-23 · Claude · Code (`Updater.VerifySignature`, `UpdateKeys`), CI en brouillon, scripts `outils\creer-cles.ps1` et `outils\signer-version.ps1`. 15 tests ; scripts essayés avec des valeurs de test jetables (relecture, mauvais mot de passe, fichier altéré, signature acceptée par le vrai exe). Seul le garde-fou `Cles_embarquees_valides` échoue, en attente des clés.

- 2026-11-23 · Toi · Clés créées avec `creer-cles.ps1` (2 × RSA-3072, `principale.cle` dans `%USERPROFILE%\BureauCache-cles`, `secours.cle` retirée du PC).
- 2026-11-23 · Claude · Contrôle : 2 clés publiques distinctes de 3072 bits dans `UpdateKeys.cs`, aucune partie privée ; suite complète : 186 réussis.
- 2026-11-23 · ChatGPT · Revue de sécurité de T-005 et T-006 : 13 remarques et 5 tests.
- 2026-11-23 · Claude · Tri de la revue (QA-21). L'exe de la CI ne peut pas être comparé à une compilation locale (81 000 octets d'écart mesurés sur la 3.3). **6 défauts corrigés** : signature d'une compilation locale (choix du chef de projet), confirmation limitée au processus à l'essai, `.old` gardé lors d'une seconde mise à jour, essai sans marqueur, retour arrière interrompu, BOM. **Risques résiduels consignés** : R-24 (transition depuis la 3.3) et R-25 (scripts locaux, exe lancé à la main, dépôt personnalisé, écriture locale, blocage, mémoire). Suite : 192 réussis.

**Reste pour T-005 :** publier une première version signée (3.4) avec `publier.ps1` puis `outils\signer-version.ps1`, et vérifier qu'une installation 3.4 refuse une version non signée (essai sur un dépôt de test).

**Essai manuel T-006 (machine de test) :** copier une ancienne version en `BureauCache.exe.old` à côté de l'exe installé, puis créer un fichier vide `BureauCache.exe.trial`. Lancer BureauCache : le message « La version précédente a été restaurée » doit s'afficher, puis l'ancienne version démarre. Sans le fichier `.trial`, attendre 10 s : `.old` doit disparaître.

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
