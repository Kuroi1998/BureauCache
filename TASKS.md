# Tâches

_Statuts : À faire · En cours · Revue · Fait. Porteurs : Toi · Claude · Gemini · ChatGPT._
_Une tâche terminée passe dans « Fait » avec sa date, puis disparaît à la version suivante (l'historique reste dans git et le CHANGELOG)._

## Tableau

| ID | Titre | Statut | Porteur | Branche | Lien |
|---|---|---|---|---|---|
| T-021 | Garde-fous des tests, modales allégées, optimisations (Gemini, lot 1) | Fait (3.10) ; essais réels faits sur le binaire et la 3.10 installée | Claude | PR #21 | QA-34, QA-35 |
| T-022 | Gemini, lot 2 : découverte des fonctions, infobulles, états vides, confirmations système thématisées | Fait (3.10) ; essais réels faits sur le binaire et la 3.10 installée | Claude | PR #22 | QA-34, QA-36, QA-38 |
| T-023 | Gemini, lot 3 : météo intégrée aux Paramètres, détail non modal, clavier dans le panneau, DPI par écran (R-10) | Fait (3.10) ; essais réels faits sur le binaire et la 3.10 installée | Claude | PR #22 | QA-34, QA-37 |
| T-024 | Règles de rangement (par type ou par nom, destination choisie) | Fait (3.10) ; essais réels faits sur le binaire et la 3.10 installée | Claude | PR #22 | D-009, QA-39, QA-40 |
| T-025 | Recherche dans le contenu des fichiers, statistiques d'encombrement | Fait (3.10) ; essais réels faits sur le binaire et la 3.10 installée | Claude | PR #22 | D-010, QA-41, QA-43 |
| T-026 | Historique du presse-papiers, panneau sur le bureau virtuel actif | Fait (3.10) ; essais réels faits sur le binaire et la 3.10 installée | Claude | PR #22 | D-011, QA-42, QA-43 |
| T-027 | Échelle par écran (DPI par moniteur) | Reporté (un seul écran aujourd'hui) | Claude | — | D-008, R-10 |
| T-028 | Audit global : sécurité, fonctionnalités, environnement, expérience, failles (Claude, Gemini, ChatGPT) | Fait (3.10.1) ; essais réels faits sur le binaire et la 3.10.1 installée | Claude, Gemini, ChatGPT | PR #24 | QA-44, QA-45, QA-46 |
| T-029 | Durcissement de sécurité issu de l'audit (import, désinstallation, SEC-01 à 12) | Fait (3.10.1) ; essais réels faits sur le binaire et la 3.10.1 installée | Claude | PR #24 | QA-44, QA-46, QA-47, QA-53 |
| T-030 | Expérience issue de l'audit (Premiers pas au clavier, lecteurs d'écran, contraste, erreurs réseau) | Fait (3.10.1) ; essais réels faits sur le binaire et la 3.10.1 installée | Claude | PR #24 | QA-45, QA-48, QA-53 |
| T-031 | Refonte visuelle « Windows 11 » en quatre étapes | Fait (3.10.1) ; essais réels faits sur le binaire et la 3.10.1 installée | Claude puis Toi | PR #24 | D-012, QA-49 à QA-53 |
| T-032 | Barre BureauCache, étape 1 : masquer et restaurer la barre Windows, barre AppBar, bouton Démarrer (ouvre le panneau), horloge | Fait (3.10.2) ; essais réels restants listés dans QA-67 et QA-68 | Claude puis Toi | PR #26 | D-013, QA-54, QA-55 |
| T-033 | Barre, étape 2 : fenêtres ouvertes (activer, réduire, fermer, clignotement, bureaux virtuels) | Fait (3.10.2) ; essais réels restants listés dans QA-67 et QA-68 | Claude puis Toi | PR #26 | D-013, QA-56 |
| T-034 | Barre, étape 3 : menu Démarrer avec compte et alimentation | Fait (3.10.2) ; essais réels restants listés dans QA-67 et QA-68 | Claude puis Toi | PR #26 | D-013, QA-56 |
| T-035 | Barre, étape 4 : bouton « ^ » avec les vraies icônes de notification (prototype, recherche Gemini, puis version finale) | À faire | Gemini, Claude, Toi | — | D-013 |
| T-036 | Chasse aux bugs hors de la barre : mises à jour, installation, panneau, Paramètres, recherche, presse-papiers, météo (Codex en lecture seule, puis Claude) | Fait (3.10.2) ; essais réels restants listés dans QA-67 et QA-68 | Codex, Claude | PR #26 | QA-68 |
| T-037 | Gestionnaire des tâches dans le menu de BureauCache (icône et barre) | Fait (3.11) ; essai réel à faire | Claude puis Toi | PR #28 | — |
| T-038 | Visite guidée à jour (6 écrans) ; fenêtre réduite rouverte dans son état d'avant (plein écran compris) | Fait (3.11) ; essai réel à faire | Claude puis Toi | PR #28 | — |
| T-039 | Menu Démarrer : sections BureauCache / Windows / Session / Alimentation | Fait (3.11) ; essai réel à faire | Claude puis Toi | PR #28 | — |
| T-040 | Paramètres rapides propres à la barre (volume, luminosité, multimédia, Wi-Fi et Bluetooth) | Fait (3.11) ; essais matériels à faire (luminosité sur deux écrans, appairage Bluetooth), D-017 à valider ensuite | Gemini, ChatGPT, Toi | PR #28 | D-017, QA-70 |
| T-041 | Badges (« 1 » de Discord) et barres de progression des applications dans la barre BureauCache | Prototype en pause (D-016) : routage désactivé dans la 3.11, diagnostic avec Discord à faire ; compteur d'alertes Discord publié | Claude puis Toi | PR #28 | D-016 |
| T-042 | Panneau Notifications et calendrier assorti à la barre (remplace Windows + N sur l'horloge et la cloche) | Conception faite ; recherche Gemini sur la lecture des notifications en cours | Gemini, Claude, Toi | `feat/T-042-notifications` | D-018 |
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
- 2026-11-24 · Claude · T-021 : analyse de Gemini triée ; lot 1 appliqué (bac à sable des tests, modales allégées, cache du bord, découpage de App et Settings) (QA-34). Suite : 387 réussis.
- 2026-11-26 · Claude · QA-32 rectifié : l'écrasement n'avait touché que la copie du bac à sable de Claude ; les vrais réglages étaient intacts (vérifié par les sauvegardes de la 3.9).
- 2026-11-26 · Claude · T-021 : revue du lot 1 par ChatGPT, 10 constats retenus et corrigés, QA34-04 écarté (QA-35). Suite : 394 réussis.
- 2026-11-27 · Claude · T-022 : pastilles = ? > dans la recherche, infobulles, indice de menu sur les tuiles, nettoyage dans Paramètres → Avancé, états vides utiles, rappel Ctrl+1…9, boîtes de message aux couleurs de l'application (QA-36). Suite : 398 réussis.
- 2026-11-27 · Claude · T-023 (1/2) : lieu de la météo cherché dans les Paramètres, détail météo non modal, F6 pour les boutons et tuiles du haut (QA-37). Suite : 403 réussis hors revue de T-022 en attente.
- 2026-11-27 · Claude · T-022 : revue par ChatGPT, 8 constats retenus et corrigés (QA-38). Suite : 407 réussis.
- 2026-11-27 · Toi · Échelle par écran reportée (D-008, T-027). Règles de rangement conçues (D-009), à valider.
- 2026-11-27 · Toi puis Claude · D-009 validée ; T-024 codée : règles par extension ou par mot du nom, âge, destination parmi les dossiers connus, liste et dialogue avec aperçu dans Organisation, annulation comprise (QA-39). Suite : 412 réussis.
- 2026-11-27 · Toi · D-010 et D-011 validées.
- 2026-11-27 · Claude · Revue de T-023 et T-024 par ChatGPT : 7 échecs reproduits, tous retenus et corrigés (QA-40).
- 2026-11-27 · Claude · T-025 : recherche dans le contenu par l'index Windows (groupe « Dans le contenu »), encombrement du bureau dans Organisation (QA-41). Suite : 432 réussis.
- 2026-11-27 · Claude · T-026 : historique du presse-papiers en mémoire (copies privées ignorées), panneau ouvert sur le bureau virtuel actif et qui suit quand il est épinglé (QA-42). Suite : 441 réussis.
- 2026-11-27 · Claude · Revue de T-025 et T-026 par ChatGPT : 8 échecs reproduits, tous retenus et corrigés ; journal de rangement fondé sur l'identité NTFS, conservé si la destination manque, retour arrière si l'enregistrement échoue (QA-43). Suite : 449 réussis.
- 2026-11-27 · Claude · PR #20, #21 et #22 fusionnées (T-022 à T-026 en une seule PR pour éviter les fermetures en cascade des PR empilées) ; CI au vert.
- 2026-11-27 · Toi · Version 3.10 publiée et signée (T-021 à T-026) ; mise à jour installée depuis la 3.9.
- 2026-11-27 · Toi puis Claude · T-028 : audit global lancé. Premier passage de Claude : 4 constats (AUD-01 à 04, dont un dépôt de mise à jour modifiable par un fichier de réglages importé), QA-44 ; prompts de Gemini et ChatGPT fournis.
- 2026-11-27 · Gemini puis Claude · T-028 : 14 constats de Gemini vérifiés dans le code ; 9 retenus à corriger, 2 hors code (signature Authenticode, documentation), 1 rattaché à T-027, 2 en partie, 3 écartés (QA-45).
- 2026-11-27 · ChatGPT puis Claude · T-028 : 12 constats SEC ; 3 échecs reproduits, 7 constats retenus au total, 5 écartés faute de frontière de droits (même utilisateur), registre en lecture seule écarté (QA-46).
- 2026-11-27 · Toi · Les deux lots de corrections de l'audit sont validés (T-029 sécurité, T-030 expérience).
- 2026-11-27 · Claude · T-029 : l'import garde les choix sensibles de ce PC, désinstallation sans AutoRun, SEC-01 à 03, 08, 12, position météo à 1 km, nouvel essai du presse-papiers (QA-47). Suite : 457 réussis.
- 2026-11-27 · Claude · T-030 : Premiers pas au clavier et F1, lecteurs d'écran sur les contrôles dessinés et la liste, contraste AA et mode Contraste élevé, pied de page du nettoyage, erreurs réseau traduites, attribution météo (QA-48). Suite : 464 réussis.
- 2026-11-27 · Toi · Plan de refonte validé (D-012) ; à l'étape 3, couleur de la légende par `DWMWA_CAPTION_COLOR`.
- 2026-11-27 · Claude · T-031, étape 1 : noms de tuiles sans coupure de mot, presse-papiers en liste, « Ville, Pays », disque sur une ligne, Récents sans éléments système (QA-49). Suite : 467 réussis.
- 2026-11-27 · Toi · Étape 1 validée à l'écran.
- 2026-11-27 · Claude · T-031, étape 2 : jetons de style (bordure fine, rayons) appliqués aux widgets, à la recherche, aux tuiles, au presse-papiers et aux cartes des Paramètres (QA-50). Suite : 468 réussis.
- 2026-11-27 · Toi · Étape 2 validée à l'écran.
- 2026-11-27 · Claude · T-031, étape 3 : barre de titre (attributs 19, 20, 35, 36), boutons secondaire, discret et danger sobre, onglet actif translucide, focus seulement au clavier (QA-51). Suite : 470 réussis.
- 2026-11-27 · Toi · Étape 3 validée à l'écran.
- 2026-11-27 · Claude · T-031, étape 4 : Segoe Fluent Icons avec repli sur MDL2, pastilles = ? > en icônes (QA-52). Suite : 472 réussis.
- 2026-11-27 · ChatGPT puis Claude · Revue de T-029 à T-031 : 9 constats, tous retenus et corrigés (QA-53). Suite : 482 réussis.
- 2026-11-27 · Toi · Publication demandée en 3.10.1 (Refonte UIX).
- 2026-11-27 · Claude · PR #24 fusionnée (T-028 à T-031 en une seule PR) ; CI au vert.
- 2026-11-27 · Toi · Version 3.10.1 (Refonte UIX) publiée et signée ; mise à jour installée depuis la 3.10.
- 2026-11-27 · Claude · T-032 à T-035 : conception de la barre BureauCache proposée (D-013).
- 2026-11-27 · Gemini puis Claude · T-035 : recherche sur l'hôte d'icônes de notification ; six points vérifiés dans le code de ManagedShell. Retenus : ordre Z, format 32 bits, `TaskbarCreated` sans retransmission des icônes, réponse à `SHAppBarMessage`, clics. Écartés : lecture de la mémoire d'Explorer, structure 64 bits « brute » (complément de D-013).
- 2026-11-27 · Toi · D-013 validée ; T-032 lancée.
- 2026-11-27 · Claude · T-032 : `WindowsBar` (fichier `barre.etat` écrit avant tout changement, restauration à la sortie, en fin de session, par la surveillance et par `--restore`), `BarForm` (AppBar, Démarrer, Paramètres rapides, horloge), option avec confirmation, menu « Rétablir la barre des tâches de Windows », import protégé (QA-54). Compilé ; suite : 489 réussis. Essai réel à faire.
- 2026-11-27 · ChatGPT puis Claude · Revue de T-032 : 12 constats, 5 échecs reproduits ; 9 retenus, 2 en partie, 1 écarté (BAR-04) ; copie de l'état en mémoire, restauration constatée, surveillance obligatoire, réservation vérifiée (QA-55). Suite : 497 réussis.
- 2026-11-27 · Toi · Essai de T-032 : la barre s'affiche (Démarrer, Paramètres rapides, horloge). Demande : fenêtres ouvertes dans la barre et un menu Démarrer propre avec compte et alimentation (D-013 révisée).
- 2026-11-27 · Claude · T-033 et T-034 : `WindowList` (événements hors contexte, filtre testé, clignotement), boutons des fenêtres avec icône, trait actif, « +N », menu Fermer ; `StartMenuForm` (photo et nom, raccourcis, Verrouiller, Se déconnecter, Veille, Redémarrer, Arrêter avec mises à jour en attente et démarrage rapide) (QA-56). Compilé ; suite : 506 réussis.
- 2026-11-27 · Toi · Essai de T-033 et T-034 : fenêtres et menu visibles ; menu trop petit (textes coupés) ; demande d'un aperçu au survol.
- 2026-11-27 · ChatGPT puis Claude · Tests de revue `WindowStartReviewTests.cs` (BAR-03, 04, 05, 12) : 6 échecs reproduits, tous corrigés. Texte de la revue non reçu : les autres constats restent à trier (QA-57).
- 2026-11-27 · Claude · Menu Démarrer agrandi (600 px à 100 %, tuiles de 96 px) avec recherche et 6 raccourcis ; aperçu en direct par vignette DWM au survol (QA-57). Suite : 515 réussis.
- 2026-11-27 · Toi puis Claude · Icônes de la barre jugées trop petites : barre de 56 px (au lieu de 48), icônes des fenêtres de 32 px (au lieu de 24), logo, icônes de droite et horloge agrandis (QA-58). Suite : 515 réussis.
- 2026-11-27 · Toi puis Claude · Menu Démarrer : textes des tuiles coupés en bas (« Applications et recherche », « Gestionnaire des tâches ») ; icône et texte centrés ensemble selon la hauteur mesurée du texte, menu réduit (540 px, tuiles de 88 px) (QA-59). Suite : 516 réussis.
- 2026-11-27 · Toi puis Claude · Épingles et déplacement des icônes (D-014) : `TaskbarModel` (épingles d'abord, clé AppUserModelID ou exe, un bouton par fenêtre), clic droit Épingler/Détacher/Nouvelle fenêtre, glisser pour réordonner, épingles gardées à l'import (QA-60). Suite : 525 réussis.
- 2026-11-27 · T-032 à T-034 · Cinq régressions reproduites et corrigées dans le remplacement de la barre, les épingles et les clics. Résultats et limites dans QA-61.
- 2026-11-27 · T-032 à T-034, D-007 · Corrections de la restauration Windows, du suivi des fenêtres, des aperçus, du menu Démarrer et de la rotation des sauvegardes. Détails dans QA-62.
- 2026-11-27 · Correctifs de stabilité de la calculatrice, de la recherche dans les fichiers, du presse-papiers, du parseur JSON, de la météo et du relevé d’encombrement. Détails dans QA-63.
- 2026-11-27 · Correctifs de l’historique d’utilisation, de la restauration, du glisser-déposer et du cycle de vie du panneau. Surveillance de la barre vérifiée aussi pendant son fonctionnement. Détails dans QA-64.
- 2026-11-27 · Audit du dépôt local et correctifs du rangement, du catalogue, du chargement WinForms, de la météo, du cache d’icônes et des scripts CI et publication. Inventaire dans QA-65.
- 2026-11-27 · Claude · Lot externe QA-61 à QA-65 relu et commité à part. Chasse aux bugs de la barre : icônes retentées, hôtes sans clé, noms d'applications par Windows, fermeture différée du menu, veille moderne, fin de session annulée (QA-66). Suite : 570 réussis.
- 2026-11-27 · Toi · Gemini non connecté (connexion Google fermée aux particuliers, clé en prépaiement sans crédit) : Codex seul est appelé directement, Gemini par prompt (D-015).
- 2026-11-27 · Codex puis Claude · Revue de la barre en lecture seule : 13 constats, tous retenus (certains en partie), 4 reproduits sur l'ancien code (QA-67). Suite : 582 réussis.
- 2026-11-27 · Claude puis Codex · T-036 : corbeille restaurée au bon endroit, renommage de casse ; revue Codex hors barre : 10 constats, 9 retenus, 1 écarté (QA-68). Suite : 603 réussis.
- 2026-11-27 · Toi · Publication demandée en 3.10.2 (Barre des tâches BureauCache) : T-032 à T-034 et T-036 réunis dans la PR #26.
- 2026-11-27 · Claude · PR #26 fusionnée (CI verte), `publier.ps1 -Version 3.10.2` : commit et tag `v3.10.2` envoyés, brouillon compilé par GitHub Actions.
- 2026-11-27 · Toi · Version 3.10.2 (Barre des tâches BureauCache) signée et publiée : exe, empreinte et signature en ligne.
- 2026-11-27 · Toi puis Claude · T-037 : « Gestionnaire des tâches » ajouté au menu de BureauCache, sous « Paramètres… », avec le rappel Ctrl + Maj + Échap.
- 2026-11-27 · Toi puis Claude · T-038 : visite guidée refaite en 6 écrans (panneau, raccourci, recherche, organisation, barre des tâches, petits plus), textes de l'option barre partagés avec les Paramètres (`TaskbarConsent`) et mis à jour ; fenêtre réduite rouverte en plein écran si elle l'était (barre et Paramètres). Suite : 608 réussis.
- 2026-11-27 · Toi puis Claude · T-038, essai : texte de la visite rogné (phrase principale sur 3 lignes cachée sous les réglages, conseils coupés en bas). Mise en page mesurée : chaque bloc sous le précédent selon la hauteur réelle de son texte, fenêtre à la hauteur du plus long écran ; test anti-chevauchement en français, anglais et espagnol. Suite : 609 réussis.
- 2026-11-27 · Toi puis Claude · T-039 : menu Démarrer en sections étiquetées avec légende ; « Paramètres de BureauCache » et « Visite guidée » ajoutés, « Paramètres » devient « Paramètres Windows », Panneau de configuration ajouté ; tuiles de raccourcis compactes. Suite : 611 réussis. Demande suivante : des Paramètres rapides propres à la barre (conception à valider).
- 2026-11-27 · Toi · T-040 : version 1 proposée (API Win32 documentées, boutons vers les Paramètres Windows pour le reste) ; recherche Gemini demandée d'abord sur Bluetooth, mode Avion, Éclairage nocturne, lecture en cours et luminosité depuis C# 5.
- 2026-11-27 · Toi puis Claude · T-041 : prototype des badges validé sur le principe (exception à D-013 acceptée, D-016 proposée). Propriété `TaskbandHWND` posée avec filet (`badges.etat`, restauration à la sortie, par la surveillance, `--restore`, fin de session) ; badge dessiné en haut à droite de l'icône, progression verte, jaune ou rouge sous l'icône (échelle 0 à 65 534, comme RetroBar). Compilé ; suite : 616 réussis. Essai réel avec Discord à faire.
- 2026-11-27 · ChatGPT puis Claude · Paramètres rapides (T-040, D-017 proposée) et compteur d'alertes Discord relus et commités ; 6 tests cassés réparés (doubles). Toi : compteur d'alertes Discord gardé, en secours du vrai badge. Suite : 628 réussis.
- 2026-11-27 · Claude · Grande passe de corrections (QA-69) : lecture bornée de la description des badges (plantage possible), liste des fenêtres qui ne réénumère plus à chaque événement, relectures du volume et de la luminosité espacées. Revue Codex reportée (quota épuisé). Suite : 633 réussis.
- 2026-11-27 · Claude · Revue Codex (QA-70, CDX-40 à 50) : 9 constats retenus en tout ou en partie (outils système lancés par chemin complet, restauration des badges qui ne perd plus la valeur d’Explorer, luminosité de l’écran de la barre, appairage, opérations WinRT fermées, curseurs, mot de passe Wi-Fi, visite, clé WPA2 brute), 2 écartés. Suite : 639 réussis.
- 2026-11-27 · Toi puis Claude · Publication 3.11 décidée : journal temporaire des badges retiré, routage `TaskbandHWND` désactivé (`TaskbandRoute.Enabled`) jusqu’au diagnostic ; le compteur d’alertes Discord reste actif.
- 2026-11-27 · Toi · 3.11 publiée et signée (PR #28, tag v3.11, exe, .sha256 et .sig) : T-037 à T-040 faits.
- 2026-11-27 · Toi puis Claude · T-042 : panneau Notifications et calendrier demandé. Conception (D-018) : calendrier en code .NET seul ; notifications et Ne pas déranger seulement par API publique, sinon renvoi vers Windows. Prompt Gemini fourni (`.ai/prompt-gemini-T042.md`).
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
