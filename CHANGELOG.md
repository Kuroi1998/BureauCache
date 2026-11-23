# Nouveautés

## Non publié

- Les réglages ne peuvent plus être perdus quand l'historique d'utilisation devient très long. Les éléments utilisés depuis plus de 180 jours sont oubliés en premier.
- Quitter BureauCache juste après une mise à jour ne fait plus revenir à l'ancienne version au démarrage suivant.
- Panneau épinglé : l'espace réservé à l'écran suit la largeur ajustée à la souris. Si Windows refuse l'épinglage, un message le dit.
- L'effet verre s'applique tout de suite quand on l'active panneau ouvert.
- La calculatrice reconnaît « 1 + ,5 ». Les erreurs de téléchargement s'affichent dans la langue choisie.
- Les délais d'ouverture et de fermeture du panneau respectent la durée réglée, même quand Windows est chargé.
- Quand une version plus récente est publiée mais pas encore installable, BureauCache le dit au lieu d'annoncer « Tu as la dernière version ».
- La désinstallation indique ce qui n'a pas pu être supprimé.
- L'écran choisi pour le bord est reconnu même après avoir débranché ou réordonné les écrans.
- La visite guidée reste utilisable sur un petit écran avec un zoom élevé.
- L'aperçu au survol ne disparaît plus pendant un rechargement, et la page Organisation ne fige plus la fenêtre pendant l'analyse du bureau.

## 3.7.1 — Espagnol et stabilisation du panneau

- Interface disponible en espagnol. Le changement de langue conserve la rubrique active et son défilement. La calculatrice accepte les nombres espagnols copiés avec un séparateur de milliers.
- Le panneau est visible par défaut en partage d’écran entier. Le réglage de masquage reste disponible. En mode visible, le redimensionnement conserve le fond flouté.
- Les réglages de largeur, de côté et des widgets s’appliquent directement au panneau ouvert.
- La recherche réutilise ses catalogues quand leurs réglages restent identiques.

## 3.7 — Refonte des paramètres

- Fenêtre Paramètres redessinée et redimensionnable. Les six rubriques ont un en-tête fixe, les descriptions s’adaptent à la largeur et les réglages se parcourent au clavier.

## 3.6 — Météo et nettoyage rapide

**Météo**
- Nouvelle tuile météo dans le panneau : une icône dessinée (soleil, nuages, pluie, neige, orage…), la température, le lieu et les températures du jour. Un clic affiche le ressenti, le vent et les prévisions sur 3 jours.
- Le lieu se choisit en tapant le nom de sa ville (partout dans le monde) ou en laissant Windows détecter la position. La météo est désactivée tant qu'aucun lieu n'est choisi : rien n'est envoyé sans ton accord. Données Open-Meteo.com (CC BY 4.0).

**Nettoyage rapide**
- Clic droit sur la tuile Disque, puis « Nettoyer… » : fichiers temporaires, caches des navigateurs (Edge, Chrome, Brave, Vivaldi, Opera, Firefox) et des applications (Discord, Teams, Steam, VS Code), et au choix les rapports d'erreurs, les miniatures et la corbeille.
- Analyse avec les tailles, puis confirmation. Windows, les programmes, tes documents, tes mots de passe et ton historique ne sont jamais touchés. Le cache d'une application ouverte est ignoré.

## 3.5 — Ouverture depuis le bureau

- Le panneau s'ouvre de nouveau au bord de l'écran quand le bureau est au premier plan, par exemple après avoir réduit BureauCache près de l'horloge. Avec l'option « Pas en plein écran », Windows signalait le bureau comme « occupé » et BureauCache le prenait pour un jeu en plein écran. Les vrais jeux et les présentations restent détectés.

## 3.4 — Mises à jour signées et fiabilité

**Mises à jour**
- Mises à jour signées : chaque nouvelle version est signée avec une clé qui ne passe jamais par GitHub, et BureauCache refuse d'installer une version dont la signature n'est pas valide. Même un compte GitHub piraté ne suffirait pas à diffuser une fausse mise à jour.
- Retour automatique : si une nouvelle version n'arrive pas à démarrer, BureauCache revient à la version précédente au lancement suivant.
- Téléchargement plus strict : redirections contrôlées une à une, taille limitée, version téléchargée identique à celle annoncée, remplacement de l'exe sans risque de le perdre.
- Un dépôt de mises à jour personnalisé est signalé à l'installation, et le résultat d'une vérification faite pour un autre dépôt est ignoré.

**Rangement automatique**
- Plus prudent : un dossier n'est rangé que si **tout** son contenu, sous-dossiers compris, est inutilisé. Un dossier trop volumineux pour être vérifié, ou qui contient une jonction ou un lien symbolique, reste sur le bureau.
- Il attend la fin d'un renommage, d'une copie ou d'une suppression en cours (et de son délai d'annulation), ainsi que la fermeture des fenêtres de confirmation.
- Chaque élément est revérifié juste avant d'être déplacé, et le journal d'annulation est enregistré immédiatement.
- Fonctionne avec un bureau synchronisé par OneDrive.

**Calculatrice**
- Un résultat copié avec Entrée se relit correctement, même avec des milliers (« 1 234,5 », « 1,000 ») ou en notation scientifique. En anglais, « 1,000 » ne donne plus 1.
- Une saisie aux séparateurs incohérents (« 1.234,5 », « 12,34.5 ») est refusée au lieu de donner un résultat faux. Un nombre collé au format de l'autre langue reste compris.
- `-2^2` vaut -4, comme en mathématiques. Écrire `(-2)^2` pour obtenir 4.
- Les très petits résultats gardent leur précision (`1,04E-09` au lieu de `0,000000001`).

**Corrections**
- Annuler (Ctrl+Z) une suppression ou un renommage qui échoue garde la possibilité de réessayer.
- Un fichier illisible sur le bureau n'empêche plus d'afficher les autres.
- Curseurs et sélecteurs des paramètres : un clic droit ne change plus la valeur, et un glissement interrompu est bien relâché.
- Le panneau se referme au bon endroit après avoir été redimensionné.
- Icône de notification : un double-clic n'ouvre plus aussi la liste.
- Paramètres : un échec d'enregistrement ou d'import est signalé, et le nombre d'éléments des catégories personnalisées reste à jour.
- La désinstallation en ligne de commande s'arrête proprement si BureauCache est encore ouvert.
- Moins de mémoire consommée par les icônes et les menus.

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
