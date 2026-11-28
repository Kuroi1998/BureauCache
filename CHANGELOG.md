# Nouveautés

## Non publié

- Les erreurs de réglages, de mise à jour et de nettoyage sont désormais consignées dans `avertissements.log`, à côté des réglages. Ce fichier ne contient ni titres de fenêtres, ni contenus, ni chemins, et il est supprimé à la désinstallation.

## 3.11.1 — Barre personnalisable et panneaux

- La barre ne reconstruit ses boutons qu’en cas de changement utile. Les icônes sont chargées après le premier dessin, avec les boutons visibles avant le menu « +N ». Les durées du démarrage de la barre sont conservées dans `demarrage-barre.csv`.

- Au changement de langue, les Paramètres sont dessinés avant d’apparaître. Au démarrage, la barre personnalisée attend Explorer s’il tarde et affiche les épingles avant de charger les fenêtres ouvertes.

- Les Paramètres gardent le contenu de l’onglet actif lors d’un changement de langue, y compris en plein écran. Le catalogue de recherche du panneau est chargé à la première requête, et une seconde instance ouvre les Paramètres sans relire le profil.

- Les Paramètres évitent de recalculer les résultats, la disposition et le thème quand ils n’ont pas changé. Les curseurs répondent immédiatement et regroupent la propagation des changements. L’analyse du bureau est réservée au volet de rangement et s’exécute sans analyses concurrentes.

- Dans les Paramètres, `Ctrl+F` retrouve la dernière recherche et son résultat sélectionné. Les sous-onglets et les sélecteurs s’étalent sur plusieurs lignes quand l’espace manque.

- Fenêtre Paramètres réorganisée en sous-onglets. `Ctrl+F` recherche un réglage par mots-clés, avec ou sans accents, et ouvre directement sa rubrique.

- La vérification automatique des mises à jour reprend après un recul de l'horloge. Un numéro de version GitHub invalide déclenche une erreur de vérification explicite.

- Un champ de réglages contenant un caractère de contrôle susceptible de fausser le fichier INI bloque l'enregistrement. Un profil inaccessible reste protégé en écriture.
- La confirmation d'une mise à jour vérifie le chemin et le SHA-256 de l'exécutable avant de supprimer la version de secours.

- Le nettoyage et le rangement vérifient les dossiers parents pour éviter les redirections par jonction. Une annulation de rangement bloquée par une jonction conserve son journal.
- Deux mises à jour installées avant le redémarrage conservent la dernière version stable. Une restauration de réglages qui échoue maintient la protection d'un profil illisible.

- Les outils Windows intégrés sont ouverts depuis leurs chemins système. La désinstallation différée isole son chemin de la commande et les raccourcis personnels du menu Démarrer sont conservés lors d'un import.
- Les mises à jour valident le dépôt et les adresses de téléchargement, puis lisent les fichiers de version dans le JSON GitHub. La recherche de contenu écarte les chemins spéciaux retournés par l'index.

- Les listes Wi-Fi et Bluetooth restent ouvertes si la barre des tâches se déplace. Les paramètres rapides reprennent leur taille si l'espace disponible augmente.
- Les notifications effacées ne réapparaissent plus à cause d'une lecture en cours. Les touches fléchées du calendrier repartent du mois affiché.
- La calculatrice refuse les résultats intermédiaires non finis et peut relire les très grands nombres qu'elle affiche.
- Correctifs pour le calendrier, l’actualisation et le défilement des notifications, les noms des raccourcis personnels, le fond en verre, le masquage automatique et le réglage du volume à la molette.
- Thèmes prêts en un clic dans Paramètres > Apparence (Classique, Minuit, Forêt, Prune, Ambre, Ardoise), teinte des fonds au choix, couleur d'accent qui peut suivre celle de Windows, et taille de l'interface (90 à 130 %).
- Menu Démarrer à la carte : chaque partie (recherche, compte, BureauCache, Windows, session, alimentation) peut être masquée, une tuile se masque d'un clic droit, et la section « Mes raccourcis » accueille jusqu'à 12 applications, dossiers ou sites de ton choix.
- Barre des tâches, suite : fond uni, en verre (le fond d'écran flouté) ou teinté de la couleur d'accent ; masquage automatique ; fenêtres d'une même application regroupées sur un seul bouton ; clic du milieu au choix (nouvelle fenêtre ou fermer) ; molette sur l'icône du son pour régler le volume.
- Nouvelle page « Barre des tâches » dans les Paramètres : taille de la barre (petite, normale, grande), icônes à gauche ou centrées comme Windows 11, titres des fenêtres à côté des icônes comme Windows 10, et choix des éléments affichés (réseau et son, cloche, date, secondes). La barre change en direct.
- Le menu Démarrer de la barre BureauCache permet de quitter BureauCache. Ses textes ne sont plus coupés (« Téléchargements », « Panneau de configuration », « Paramètres de BureauCache ») : les colonnes s'élargissent selon la langue et l'échelle de l'écran.
- La cloche de la barre BureauCache ouvre un panneau de notifications assorti au thème de l'application : les notifications de Windows regroupées par application, à effacer une par une ou toutes, un clic ouvrant l'application.
- L'horloge ouvre son propre panneau : l'heure avec les secondes, la météo (clic pour les détails) et le calendrier du mois, avec navigation au clavier et à la souris.

## 3.11 — Paramètres rapides et menu Démarrer

- Les Paramètres rapides affichent les réseaux Wi-Fi et les appareils Bluetooth dans deux vues dédiées. Le panneau donne davantage de place aux libellés.
- Les icônes réseau et son de la barre BureauCache ouvrent un panneau de paramètres rapides assorti au thème de l’application, avec volume, luminosité selon l’écran, batterie, lecture multimédia et raccourcis Windows. La cloche ouvre les notifications.
- La pastille rouge de Discord compte les alertes reçues depuis la dernière activation de sa fenêtre lorsque Discord ne transmet pas sa propre pastille.
- Le menu de BureauCache (clic droit sur son icône ou sur la barre BureauCache) ouvre le Gestionnaire des tâches.
- Visite guidée refaite : 6 écrans, avec la nouvelle barre des tâches (activable depuis la visite), le menu Démarrer, la recherche dans le contenu des fichiers, le rangement automatique, la météo, le nettoyage et le presse-papiers.
- Menu Démarrer plus clair : sections « BureauCache » (panneau, Paramètres de BureauCache, visite guidée), « Windows » (Paramètres Windows, Explorateur, Gestionnaire des tâches, Documents, Téléchargements, Panneau de configuration), « Session Windows » et « Alimentation », chacune avec une courte légende.
- Une fenêtre réduite rouverte depuis la barre BureauCache revient en plein écran si elle l'était, au lieu de revenir en petit. Même chose pour la fenêtre des Paramètres.
- Paramètres rapides : le curseur de luminosité règle l'écran où se trouve la barre, la clé Wi-Fi brute de 64 chiffres hexadécimaux est acceptée et le mot de passe saisi est effacé quand on passe à une autre fenêtre.
- La visite guidée suit l'état de la barre des tâches quand on la rétablit ou l'active depuis son menu.
- Les outils Windows (Gestionnaire des tâches, Panneau de configuration, consoles) sont toujours lancés depuis le dossier système.

## 3.10.2 — Barre des tâches BureauCache

**Nouvelle barre des tâches** (option « Remplacer la barre des tâches », Paramètres → Général, désactivée par défaut)

- La barre de Windows est cachée tant que BureauCache est ouvert et remplacée par la sienne : bouton Démarrer, fenêtres ouvertes, Paramètres rapides, notifications et horloge.
- Fenêtres ouvertes avec de grandes icônes : clic pour afficher une fenêtre (ou la réduire si elle est déjà devant), clic droit pour la fermer. La fenêtre active est soulignée, une fenêtre qui demande ton attention passe en orange, et « +N » regroupe celles qui ne tiennent pas.
- Au survol d'une fenêtre, un aperçu en direct s'affiche : clic pour y aller, croix pour la fermer.
- Épingle tes applications (clic droit, « Épingler à la barre des tâches ») : elles restent même fermées, et un clic les lance. Maj + clic ou clic du milieu ouvre une nouvelle fenêtre. Glisse les icônes pour les réordonner.
- Nouveau menu Démarrer : recherche (le texte tapé part dans le panneau), ta photo et ton nom, raccourcis (applications, Paramètres, Explorateur, Documents, Téléchargements, Gestionnaire des tâches), Verrouiller, Se déconnecter, Mettre en veille, Redémarrer et Arrêter, avec « Mettre à jour et… » quand Windows a des mises à jour en attente.
- La barre de Windows revient toujours : en quittant BureauCache, par clic droit sur la barre puis « Rétablir la barre des tâches de Windows », et même si BureauCache plante.
- Arrêt, redémarrage, mise en veille et fermeture d'une fenêtre expliquent pourquoi Windows refuse, au lieu d'échouer sans rien dire.

**Corrections**

- Un fichier supprimé depuis le panneau puis restauré depuis la Corbeille de Windows revient bien sur le bureau.
- Renommer un dossier en changeant seulement ses majuscules fonctionne.
- Si tes réglages sont momentanément illisibles au démarrage (antivirus, synchronisation), BureauCache ne les écrase plus par les valeurs par défaut.
- Le nettoyage ne touche plus à un dossier temporaire inhabituel.
- Une mise à jour que Windows refuse de lancer remet l'ancienne version en place.
- Quand le rangement automatique est actif, un import de réglages garde les favoris et les dates d'utilisation qui protègent tes fichiers ; il met aussi la météo à jour si le lieu change.
- Annuler un rangement vers un dossier OneDrive fonctionne.
- Les sauvegardes de réglages momentanément verrouillées ne sont plus supprimées par la rotation automatique.
- Le panneau respecte la fermeture automatique quand il perd le focus et annule proprement un glisser-déposer pendant un rechargement.
- La calculatrice reconnaît davantage d'expressions (puissances avec `**`, nombres négatifs) ; les anciens résultats de recherche dans les fichiers disparaissent quand la recherche change ; les aperçus du presse-papiers gardent les emoji et les accents.
- La météo écarte les codes invalides, et le relevé d'encombrement signale les tailles qu'il n'a pas pu lire.

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
