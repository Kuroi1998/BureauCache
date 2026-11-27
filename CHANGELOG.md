# Nouveautés

## Non publié

- Le panneau annule un glisser-déposer quand la liste est reconstruite et respecte le réglage de fermeture automatique lors de la perte du focus.
- La restauration d’une sauvegarde antérieure aux épingles restaure une barre sans épingles, avec possibilité de retrouver le profil précédent.

- La calculatrice reconnaît automatiquement davantage d’expressions déjà prises en charge, notamment les puissances avec `**` et les opérandes négatifs.
- Les anciens résultats de recherche dans les fichiers disparaissent au changement de requête. Les aperçus du presse-papiers conservent les emoji et les accents à la coupure.
- La météo rejette les codes invalides et le rapport d’encombrement signale les tailles manquantes.

- Le menu Démarrer défile sur les écrans où il manque de hauteur. L’aperçu reste ouvert pendant le passage de l’icône à la vignette.
- Les sauvegardes de réglages temporairement verrouillées sont préservées pendant la rotation automatique.

- La barre vérifie qu’Explorer a bien masqué ou rendu sa barre des tâches. Relâcher une épingle sur Démarrer n’ouvre plus le menu par erreur.

- Nouvelle option « Remplacer la barre des tâches » (Paramètres → Général, désactivée par défaut) : la barre de Windows est cachée tant que BureauCache est ouvert et remplacée par une barre BureauCache avec un bouton Démarrer (ouvre le panneau), les Paramètres rapides, les notifications et l'horloge. Version d'essai : les fenêtres ouvertes et les icônes près de l'horloge arriveront ensuite.
- La barre BureauCache affiche les fenêtres ouvertes, avec de grandes icônes : clic pour l'afficher (ou la réduire si elle est déjà devant), clic droit pour la fermer. La fenêtre active est soulignée, une fenêtre qui demande ton attention est en orange, et « +N » regroupe celles qui ne tiennent pas.
- Épingle tes applications à la barre (clic droit, « Épingler à la barre des tâches ») : elles restent même fermées, et un clic les lance. Maj + clic ou clic du milieu ouvre une nouvelle fenêtre.
- Arrêt, redémarrage, mise en veille et fermeture d'une fenêtre expliquent maintenant pourquoi Windows refuse, au lieu d'échouer sans rien dire.
- La barre ne se fige plus si une application ne répond pas, garde l'ordre de tes fenêtres d'un bureau virtuel à l'autre, et survit à un changement d'échelle de l'écran.
- « Mettre en veille » fonctionne aussi sur les PC en veille moderne. Une fenêtre dont l'icône n'était pas prête à l'ouverture la reçoit ensuite. Les applications épinglées portent leur vrai nom.
- Glisse les icônes de la barre pour les réordonner : l'ordre des applications épinglées est retenu.
- Au survol d'une fenêtre dans la barre, un aperçu en direct s'affiche au-dessus : clic pour y aller, croix pour la fermer.
- Nouveau menu Démarrer dans la barre, en grand : une recherche (le texte tapé part dans le panneau), ta photo et ton nom, Documents, Téléchargements, Gestionnaire des tâches, accès au panneau (applications et recherche), aux Paramètres et à l'Explorateur, puis Verrouiller, Se déconnecter, Mettre en veille, Redémarrer et Arrêter (avec « Mettre à jour et… » quand Windows a des mises à jour en attente).
- La barre de Windows revient toujours : en quittant BureauCache, par clic droit sur la barre puis « Rétablir la barre des tâches de Windows », et même si BureauCache plante.

## 3.10.1 — Refonte UIX

- Importer des réglages ne change plus le dépôt ni la vérification des mises à jour, l'historique du presse-papiers, le rangement automatique ni le journal d'annulation : ces choix restent ceux de ton PC, et la fenêtre dit lesquels ont été gardés.
- Une mise à jour venant d'un dépôt non officiel est signalée comme non signée, et « Non » est proposé par défaut.
- La météo n'envoie plus qu'une position arrondie à environ 1 km.
- Une copie faite pendant qu'une autre application occupe le presse-papiers est relue un instant plus tard au lieu d'être perdue.
- Premiers pas utilisables au clavier (Tab, Espace, Entrée), avec la touche F1 signalée.
- Les lecteurs d'écran (Narrateur, NVDA) annoncent les interrupteurs, choix, curseurs et boutons, et l'élément sélectionné dans le panneau.
- Textes secondaires plus contrastés en thème clair ; le mode Contraste élevé de Windows est respecté.
- Les erreurs réseau sont expliquées simplement (pas de connexion, service indisponible…) au lieu d'un message technique.
- Le texte du nettoyage n'est plus coupé quand il fait plusieurs lignes.
- Les noms des icônes ne sont plus coupés au milieu d'un mot : la coupure se fait aux espaces et aux tirets, sinon le nom finit par « … ».
- Le presse-papiers s'affiche en liste compacte, avec un aperçu du texte et un bouton « Copier » au survol.
- La météo affiche simplement « Ville, Pays », et l'espace disque « 130 Go libres » sur une ligne.
- La Corbeille et Ce PC n'apparaissent plus dans les Récents.
- Style plus net : cartes, widgets et icônes ont une bordure fine et un survol plus visible ; le champ de recherche s'illumine quand on y tape.
- Paramètres : la barre de titre suit le thème même quand Windows colore les barres de titre ; boutons plus sobres (Aperçu, Quitter, Désinstaller) ; le cadre en pointillés n'apparaît plus qu'au clavier.
- Icônes plus nettes avec la police Segoe Fluent Icons de Windows 11 ; les pastilles de la recherche sont des icônes (calculatrice, aide, commandes).
- Importer des réglages ne change pas non plus les règles d'un rangement automatique déjà actif.
- En mode Contraste élevé, l'élément sélectionné prend les couleurs de sélection de Windows.

## 3.10 — Règles de rangement, recherche dans le contenu, presse-papiers

- Moins de fenêtres à valider : restaurer une sauvegarde ou importer des réglages ne redemande plus de confirmation (c'est réversible), et les résultats (export, import, restauration, rangement, nettoyage) s'affichent directement dans la fenêtre.
- Un échec d'annulation s'affiche dans le bandeau du panneau.
- Trois pastilles dans le champ de recherche : « = » pour calculer, « ? » pour chercher sur le web, « > » pour les commandes système.
- Infobulles sur les boutons du haut et les tuiles. Le nettoyage rapide est aussi dans Paramètres → Avancé.
- Messages plus utiles quand le bureau est vide ou qu'une recherche ne trouve rien. Rappel des raccourcis Ctrl+1 à 9 des favoris.
- Les confirmations et messages ont les couleurs de BureauCache. Pour éteindre ou redémarrer, « Non » est sélectionné par défaut.
- Le lieu de la météo se cherche directement dans Paramètres → Apparence, avec « Détecter ma position ».
- Le détail de la météo ne bloque plus le bureau : il se ferme quand on clique ailleurs.
- F6 permet d'atteindre au clavier les boutons et les tuiles du haut du panneau (Entrée pour ouvrir, touche Menu pour les options).
- Règles de rangement (Paramètres → Organisation) : par exemple, les captures d'écran vont dans Images\Captures après 7 jours, les fichiers .zip dans Téléchargements. Un aperçu montre les fichiers concernés, et « Annuler le dernier rangement » les remet en place.
- La recherche trouve aussi les documents du bureau par un mot qu'ils contiennent (groupe « Dans le contenu », grâce à l'index de Windows). Option dans Paramètres → Recherche.
- Paramètres → Organisation montre l'encombrement du bureau : nombre d'éléments, place occupée, éléments inutilisés depuis 30 et 90 jours, les plus volumineux.
- Le rangement ne déplace plus un fichier encore ouvert par un programme, et l'annulation ne prend jamais un autre fichier mis à la place de celui qui a été rangé.
- Historique du presse-papiers (à activer dans Paramètres → Recherche) : les 20 derniers textes copiés, dans le panneau ; un clic les recopie. Gardés en mémoire seulement, et les copies des gestionnaires de mots de passe sont ignorées.
- Avec plusieurs bureaux virtuels, le panneau s'ouvre sur le bureau actif ; épinglé, il suit quand on change de bureau.

## 3.9 — Sauvegarde des réglages

- Sauvegarde automatique des réglages : une copie par jour, sur les 7 derniers jours. Paramètres → Avancé → « Restaurer une sauvegarde… ». Restaurer ou importer met d'abord les réglages actuels de côté, pour pouvoir revenir en arrière.

## 3.8 — Fiabilité et interface

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
- « Plus d'options » ne fige plus le panneau sur un fichier réseau ou pris en charge par une extension lente.
- La liste des écrans des Paramètres suit les écrans branchés ou débranchés.
- Plus d'image figée du panneau sur le bord de l'écran après un changement de réglage.
- Les fenêtres de dialogue se manipulent entièrement au clavier, et leurs textes ne chevauchent plus les boutons.
- Le nettoyage rapide ne peut plus être fermé pendant la suppression.
- La météo ne s'active qu'une fois un lieu choisi. Effacer l'historique des récents ne fait plus ranger des fichiers utilisés récemment.

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
