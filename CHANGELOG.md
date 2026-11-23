# Nouveautés

## Non publié

- Mises à jour signées : chaque nouvelle version est signée avec une clé qui ne passe jamais par GitHub, et BureauCache refuse d'installer une version dont la signature n'est pas valide. Même un compte GitHub piraté ne suffirait pas à diffuser une fausse mise à jour.
- Mises à jour plus sûres : si une nouvelle version n'arrive pas à démarrer, BureauCache revient automatiquement à la version précédente au lancement suivant.
- Rangement automatique plus prudent : un dossier n'est rangé que si **tout** son contenu, sous-dossiers compris, est inutilisé. Un dossier trop volumineux pour être vérifié reste sur le bureau.
- Rangement automatique : il attend la fin d'un renommage, d'une copie ou d'une suppression en cours (et de son délai d'annulation), ainsi que la fermeture des fenêtres de confirmation, avant de se lancer.
- Rangement automatique : un dossier qui contient une jonction ou un lien symbolique n'est plus rangé, et le rangement fonctionne avec un dossier d'archive OneDrive.
- Calculatrice : un résultat copié avec Entrée se relit correctement, même avec des milliers (« 1 234,5 », « 1,000 »). En anglais, « 1,000 » ne donne plus 1.
- Calculatrice : une saisie aux séparateurs incohérents (« 1.234,5 », « 12,34.5 ») est refusée au lieu de donner un résultat faux. Un nombre collé au format de l'autre langue reste compris.
- Calculatrice : `-2^2` vaut -4, comme en mathématiques (le signe s'applique après la puissance). Écrire `(-2)^2` pour obtenir 4.
- Calculatrice : les très petits résultats gardent leur précision et s'affichent en notation scientifique (`1,04E-09` au lieu de `0,000000001`).

## 3.3 — Icône de notification et réduction

- Réduire (ou fermer) la fenêtre de paramètres la range près de l'horloge, au lieu de la barre des tâches. Une bulle explique la première fois où BureauCache continue de tourner.
- Clic sur l'icône : ouvrir ou fermer la liste. Double-clic : les paramètres. Clic droit : le menu.
- Menu de l'icône plus fiable quand aucune fenêtre n'est ouverte : il passe au premier plan et s'ouvre au-dessus de la barre des tâches.
- Menu complété : version, épingler le panneau, rechercher ou installer une mise à jour, visite guidée, raccourci clavier affiché.
- Icône qui informe : point orange quand une mise à jour est disponible, point vert quand de nouveaux fichiers sont arrivés sur le bureau, détails dans l'infobulle.
- Paramètres > Général > « Icône près de l'horloge » : indique si l'icône est cachée dans la flèche ^ et ouvre le réglage de Windows pour l'afficher.
- Option « Démarrage discret » : plus de bulle d'information au lancement.

## 3.2 — Optimisation, sécurité et code

Version de maintenance : aucune nouvelle fonction visible, mais un programme plus rapide, plus sûr et plus facile à faire évoluer.

**Optimisation**
- Recherche plus rapide : les noms normalisés (sans accents ni majuscules) sont calculés une seule fois au lieu de l'être à chaque touche.
- Lecture des icônes environ 100 fois plus rapide (copie mémoire au lieu d'une lecture pixel par pixel).
- Polices partagées au lieu d'être recréées à chaque dessin du panneau.
- Flou en direct : les images de travail sont réutilisées au lieu d'être recréées toutes les 120 ms.
- Détection du bord de l'écran : la liste des écrans est mise en cache (elle était recréée 50 fois par seconde).
- Les icônes devenues inutiles après un passage liste ↔ grille sont libérées de la mémoire.
- Réglages enregistrés de façon groupée : un glissement de curseur ne provoque plus des dizaines d'écritures sur le disque.

**Sécurité et fiabilité**
- Mises à jour : téléchargements limités aux serveurs HTTPS de GitHub, nom de fichier temporaire aléatoire, taille maximale et vérification que la version téléchargée est bien celle annoncée (en plus de l'empreinte SHA-256).
- Les DLL système ne sont plus cherchées à côté de l'exe (protection contre les DLL piégées dans Téléchargements).
- Réglages écrits de façon atomique : un arrêt brutal ne peut plus laisser un fichier corrompu.
- Renommage : les noms dangereux (« .. », CON, NUL, AUX…) sont refusés.
- Calculatrice : profondeur et longueur limitées (une expression comme « ((((((… » ne peut plus faire planter le programme).
- La désinstallation ne supprime que le dossier d'installation attendu.
- Correction : si le chargement des éléments échouait, le panneau pouvait rester bloqué sur « Chargement… ».

**Code**
- Le code est réparti en 37 fichiers rangés par thème (`src/App`, `Core`, `Data`, `Search`, `Shell`, `UI`), aucun ne dépassant 500 lignes (l'ancien fichier unique en faisait 6 100).
- Utilitaires communs pour les boîtes de message, les fichiers, les chemins et les tâches de fond (fin des doublons).
- Icônes nommées (`Glyphs.Settings`…) au lieu de codes illisibles.
- Numéro de version écrit à un seul endroit (`src/Properties/AssemblyInfo.cs`).

## 3.1

- Visite guidée au premier lancement (et à revoir depuis Paramètres > Général).
- Barre d'annulation : suppression, renommage, favoris et changements de catégorie s'annulent d'un clic ou avec Ctrl+Z. Les suppressions passent quelques secondes en attente avant la corbeille.
- Nouveaux raccourcis : Ctrl+1…9 (favoris), Ctrl+F, F2, Suppr, Tab, Ctrl+Entrée, Ctrl+P, Ctrl+G, Maj+F10 et F1 pour l'aide.
- Panneau redimensionnable en tirant son bord (double-clic pour revenir à la taille d'origine).
- Mode épinglé : le panneau reste ouvert et Windows lui réserve la place, comme la barre des tâches.
- « Annuler le dernier rangement » dans Paramètres > Organisation.
- Message des nouveautés après chaque mise à jour.

## 3.0

- Lanceur universel : applications, fichiers récents, paramètres Windows, commandes système, calculatrice et recherche web.
- Recherche tolérante aux fautes de frappe.
- Catégories personnalisées, badge « Nouveau » et rangement automatique des fichiers inutilisés.
- Aperçu au survol, flou en direct et widgets (heure, disque, corbeille).
- Export et import des réglages, mises à jour automatiques, version anglaise.
