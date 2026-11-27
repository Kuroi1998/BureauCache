# Décisions d'architecture

_Une entrée par décision, courte. On n'efface rien : une décision remplacée est marquée « Remplacée par D-xxx »._

<!-- Modèle :
## D-00X — Titre (AAAA-MM-JJ) · Statut : Proposée / Acceptée / Remplacée par D-xxx
**Contexte :** pourquoi la question se pose.
**Décision :** ce qu'on fait.
**Alternatives écartées :** et pourquoi.
**Conséquences :** ce que ça impose ensuite.
-->

## D-013 — Barre des tâches BureauCache à la place de celle de Windows (2026-11-27) · Statut : Proposée

**Contexte :** le chef de projet veut que BureauCache, une fois lancé, cache la barre des tâches de Windows et la remplace par la sienne : bouton Démarrer, fenêtres ouvertes, horloge, compte, alimentation, et le bouton « ^ » avec les **vraies icônes de notification** des applications (version B). Exigence : ne jamais modifier Explorer.
**Décision proposée :**
- **Option désactivée par défaut** (`TaskbarOn`, Paramètres → Bureau), avec un aperçu et un retour immédiat à la barre Windows.
- **Masquer sans modifier Explorer.** On active le masquage automatique officiel (`SHAppBarMessage`, `ABM_SETSTATE`) pour rendre la place aux fenêtres, puis on cache la fenêtre `Shell_TrayWnd` (`ShowWindow`). Le message `TaskbarCreated` (Explorer redémarré) relance le masquage. Pas d'injection, pas de modification d'Explorer ni de son registre.
- **Restauration garantie.** Avant tout changement, l'état d'origine est écrit **immédiatement** dans un fichier `barre.etat` du dossier des réglages (pas par l'écriture groupée de `Settings`). On restaure à la sortie normale (`Program.Main`, comme les icônes), par le **processus de surveillance existant** si BureauCache plante (`Watchdog` appelle déjà `Native.RestoreDesktopIcons`), par `--restore`, et au démarrage si `barre.etat` est resté là. Un menu « Rétablir la barre Windows » est toujours présent dans l'icône de BureauCache.
- **Notre barre** est une fenêtre AppBar (`ABM_NEW`, `ABM_SETPOS`) en bas de l'écran principal : les fenêtres agrandies s'arrêtent au-dessus. Elle s'efface devant une application en plein écran (`ABN_FULLSCREENAPP`). Écrans secondaires : leur barre Windows reste visible dans un premier temps.
- **Menu Démarrer.** *Révisé le 2026-11-27 à la demande du chef de projet, après l'essai de T-032 : un menu propre à la barre plutôt qu'un pied ajouté au panneau.* Le bouton Démarrer ouvre un menu (`StartMenuForm`) : photo et nom de la session ; raccourcis « Applications et recherche » (le panneau, qui reste le lanceur), Paramètres et Explorateur ; compte et alimentation : nom et photo de session, Verrouiller, Se déconnecter, Changer d'utilisateur, Veille, Redémarrer, Arrêter. Arrêt et redémarrage passent par `InitiateShutdown`, avec l'installation des mises à jour en attente quand Windows en a.
- **Fenêtres ouvertes** : `SetWinEventHook` en mode `WINEVENT_OUTOFCONTEXT` (rien n'est injecté dans les autres programmes), plus `RegisterShellHookWindow` pour le clignotement. Fenêtres masquées par DWM (`DWMWA_CLOAKED`) exclues, ce qui gère les applis du Store et les autres bureaux virtuels. Les titres de fenêtres ne sont jamais journalisés.
- **Zone de notification (version B)** : un **hôte d'icônes** sur un **fil dédié** (pas le fil d'interface : un panneau occupé ne doit jamais bloquer les applications qui appellent `Shell_NotifyIcon`). Il reçoit les icônes des applications ; quand on le désactive, une diffusion de `TaskbarCreated` les renvoie à Explorer (voir le complément ci-dessous). Les icônes système de Windows 11 (volume, réseau, batterie) ne passent pas par ce mécanisme : des boutons ouvrent les Paramètres rapides et les Notifications de Windows. Ce morceau commence par un **prototype jetable** ; les détails (ordre des fenêtres, messages, version de Windows) viennent d'une recherche de Gemini vérifiée sur le PC, et feront l'objet d'un complément à cette décision.
- **Un seul processus**, avec le fil dédié ci-dessus et la surveillance existante. *Révisé par rapport à l'idée d'un second processus évoquée en discussion :* le processus de surveillance couvre déjà le plantage, un seul processus écrit `settings.ini`, et la barre ouvre le panneau sans communication entre processus.
- **Pas de hook clavier** dans cette décision. La touche Windows continue d'ouvrir le menu Windows ; la rediriger fera l'objet d'une décision séparée, si on la veut.
**Alternatives écartées :** modifier ou injecter Explorer (casse à chaque mise à jour, alertes antivirus) ; tuer `explorer.exe` (perte du bureau, de l'Explorateur et des icônes système) ; lire les icônes d'Explorer depuis l'extérieur (impossible proprement sous Windows 11, la zone est en XAML) ; un second processus (voir ci-dessus) ; un menu Démarrer séparé du panneau (doublon du lanceur existant).
**Conséquences :** nouveau dossier `src/Taskbar` et nouvelles déclarations dans `Native` (fichier partiel `Native.Taskbar.cs`). Quatre étapes, chacune validée à l'écran avant la suivante (T-032 à T-035). Le bac à sable des tests doit bloquer tout masquage réel de la barre. Risque nouveau : une application qui garde l'ancienne adresse de la zone de notification peut perdre son icône jusqu'à son redémarrage ; c'est à mesurer pendant le prototype.
**Complément après la recherche de Gemini (T-035, 2026-11-27)** : six points vérifiés à la source dans le code de ManagedShell (`TrayService.cs`, `NativeMethods.Shell32.cs`), le reste est à confirmer par le prototype sur le PC.
- **Mécanisme retenu** : une fenêtre de classe `Shell_TrayWnd` (et son enfant `TrayNotifyWnd`) sans `WS_VISIBLE`, placée `HWND_TOPMOST`, avec la barre d'Explorer placée `HWND_BOTTOM` au démarrage. `Shell_NotifyIcon` trouve la barre par `FindWindow`, donc par l'ordre Z : c'est la nôtre qui reçoit le `WM_COPYDATA`. Un minuteur de 100 ms relève notre fenêtre si Explorer repasse devant (vérifié).
- **Format reçu** : signature, message `NIM_*`, puis `NOTIFYICONDATA` avec des handles sur 32 bits (`uint`), quel que soit l'appelant (vérifié). Conversion par `unchecked((IntPtr)(int)valeur)`. On copie au plus la taille connue dans un tampon mis à zéro, selon `cbData`. L'icône est copiée (`CopyIcon`) pendant le message, puis dessinée plus tard.
- **Pas de retransmission des icônes à Explorer** : ManagedShell ne la fait pas quand il traite le message (vérifié), et elle provoquerait des bulles en double. À la place, `TaskbarCreated` est diffusé au démarrage (les applications réenregistrent leurs icônes chez nous) et à l'arrêt (elles retournent chez Explorer). **La surveillance la diffuse aussi après un plantage.**
- **Nous devenons aussi le destinataire de `SHAppBarMessage`** (`dwData = 0`) pour toutes les applications, y compris la nôtre. Conséquences : `ABM_GETTASKBARPOS` et `ABM_GETSTATE` sont répondus avec **notre** barre, sinon les applications croient la barre Windows en masquage automatique ; les autres messages sont renvoyés à Explorer en recréant la mémoire partagée (`SHAllocShared`). **Ordre imposé** : masquer la barre Windows et enregistrer notre AppBar **avant** de prendre `Shell_TrayWnd`, et faire l'inverse à l'arrêt.
- **Position des icônes** (`dwData = 3`, `Shell_NotifyIconGetRect`) : répondue localement, pour que les menus des applications s'ouvrent au-dessus de notre icône.
- **Clics** : `AllowSetForegroundWindow` vers le processus de l'icône, puis `SendNotifyMessage` (jamais bloquant), avec l'encodage `wParam`/`lParam` propre à `NOTIFYICON_VERSION_4`, et `NIN_SELECT` / `WM_CONTEXTMENU` après le relâchement pour les versions 3 et plus.
- **Autres messages** renvoyés à Explorer par `SendMessageTimeout` (500 ms, `SMTO_ABORTIFHUNG`) au lieu du `SendMessage` de ManagedShell, sauf `WM_USER + 372`, envoyé par `PostMessage` (vérifié dans le code ; selon ManagedShell, l'envoyer de façon synchrone fait planter Explorer en boucle).
- **Icônes système** : depuis Windows 11 24H2, l'icône Réseau n'arrive plus par `Shell_NotifyIcon` (issues RetroBar citées par Gemini, non vérifiées). On garde les boutons qui ouvrent les Paramètres rapides et les Notifications ; des indicateurs propres (réseau, son, batterie) feront l'objet d'une étape à part.
- **Écartés** : la lecture de la zone d'Explorer par `OpenProcess`, `VirtualAllocEx` et `ReadProcessMemory` (ne marche plus sous Windows 11, et ressemble à une injection pour les antivirus) ; la lecture d'une structure 64 bits « brute » de 984 octets (`shell32` ne l'envoie jamais, cas impossible à tester).
- **Risques acceptés** : faux positifs d'antivirus pour une fenêtre de classe `Shell_TrayWnd` (signalés par l'auteur de RetroBar ; la signature Authenticode reste un point hors code) ; icônes perdues pour les applications qui ignorent `TaskbarCreated` ; icône de Windows Defender parfois inerte si elle démarre avant la barre.

## D-012 — Refonte visuelle « Windows 11 » en quatre étapes (2026-11-27) · Statut : Acceptée

**Contexte :** cahier des charges de refonte du chef de projet (panneau et Paramètres), confronté au code et aux captures réelles (T-031).
**Décision :** quatre étapes, chacune validée à l'écran avant la suivante. (1) Défauts visibles : noms de tuiles coupés aux espaces et tirets avec « … », presse-papiers en liste compacte (`Group.AsList`), « Ville, Pays » dans la tuile météo, « 130 Go libres » sur une ligne et barre de 4 px, Corbeille et Ce PC absents des Récents. (2) Style commun : rayons, espacements et bordure de 1 px (blanc à 6–10 %) définis dans `Theme`, survol des cartes et tuiles. (3) Paramètres : boutons du bas secondaire et discret, bouton « danger » sobre, onglet actif plus discret, cadre de focus seulement au clavier, barre de titre sombre (attribut 19 en plus du 20) et couleur de légende `DWMWA_CAPTION_COLOR` (attribut 35, Windows 11) sur le fond de `Theme`, pour l'utilisateur qui affiche la couleur d'accentuation dans les barres de titre. (4) Icônes : `= ? >` en glyphes, Segoe Fluent Icons si présente, sinon Segoe MDL2 Assets.
**Alternatives écartées :** Mica ou Acrylic par DWM (zone cliente transparente, incompatible avec le dessin maison ; l'effet verre existant reste) ; une classe `UiTheme` séparée (doublon de `Theme`) ; supprimer le cadre de focus (nécessaire au clavier et aux lecteurs d'écran, T-030).
**Conséquences :** aucun changement de logique métier ; chaque étape a ses tests et un essai réel sur le binaire.

## D-011 — Presse-papiers et bureaux virtuels (2026-11-27) · Statut : Acceptée

**Contexte :** deux demandes du chef de projet (T-026). Windows a déjà un historique du presse-papiers (Win+V), et affiche les mêmes icônes du bureau sur tous les bureaux virtuels.
**Décision proposée — presse-papiers :**
- Une section « Presse-papiers » dans le panneau, **désactivée par défaut** (Paramètres → Recherche), qui garde les **20 derniers textes** copiés ; un clic les recopie, un clic droit les retire.
- **Texte seulement, en mémoire seulement** : rien n'est écrit sur le disque ni dans les sauvegardes, tout disparaît à la fermeture. Les copies que les gestionnaires de mots de passe marquent comme privées (formats `ExcludeClipboardContentFromMonitorProcessing`, `CanIncludeInClipboardHistory` à 0, `Clipboard Viewer Ignore`) sont ignorées. Textes de plus de 10 000 caractères ignorés.
- Écoute par `AddClipboardFormatListener` (via `Native`), pas de lecture en boucle.
**Décision proposée — bureaux virtuels :**
- Le panneau s'ouvre **toujours sur le bureau virtuel actif** : avant l'affichage, il est déplacé sur le bureau de la fenêtre au premier plan par `IVirtualDesktopManager` (API documentée : `GetWindowDesktopId`, `MoveWindowToDesktop`). Épinglé, il suit le changement de bureau.
- Pas de favoris propres à chaque bureau : connaître le bureau actif demanderait une interface interne de Windows, non documentée, qui change d'une version à l'autre.
**Alternatives écartées :** historique persistant (mots de passe et données sensibles sur le disque) ; images dans l'historique (mémoire, et Win+V le fait déjà) ; interface interne `IVirtualDesktopManagerInternal` (instable entre versions de Windows).
**Conséquences :** nouvelle option `ClipboardOn` ; `PanelForm` gagne une source « presse-papiers » ; la fenêtre d'écoute vit sur le fil d'interface.

## D-010 — Recherche dans le contenu et encombrement du bureau (2026-11-27) · Statut : Acceptée

**Contexte :** la recherche du panneau ne regarde que les noms. On veut retrouver un fichier du bureau par son texte, et voir ce qui encombre le bureau (T-025).
**Décision proposée — recherche dans le contenu :**
- On interroge l'**index de recherche de Windows** (fournisseur `Search.CollatorDSO`, par ADO en COM, sans nouvelle référence d'assembly), limité au **bureau** (`SCOPE`) : `CONTAINS` sur le texte des documents que Windows sait lire (txt, PDF, Word, Excel…). Vérifié sur le PC du chef de projet : l'index répond et le bureau y est indexé.
- Une section **« Dans le contenu »** s'ajoute aux résultats pour une recherche de 3 caractères ou plus (hors préfixes `=`, `?`, `>`), en arrière-plan, 300 ms après la dernière frappe, sans les éléments déjà trouvés par leur nom. Le terme est nettoyé avant d'entrer dans la requête (guillemets et apostrophes neutralisés).
- Option **« Chercher aussi dans le contenu des fichiers »** (Paramètres → Recherche), activée par défaut. Si l'index est arrêté ou si le bureau n'est pas indexé, la section ne s'affiche pas.
**Décision proposée — encombrement :**
- Une section **« Encombrement du bureau »** dans Paramètres → Organisation, calculée en arrière-plan : nombre d'éléments et taille totale, les 5 plus volumineux, et combien sont inutilisés depuis 30 et 90 jours (même définition que le rangement). Un bouton mène à « Ranger maintenant ».
**Alternatives écartées :** lire nous-mêmes le contenu des fichiers (PDF, Word : il faudrait des bibliothèques) ; chercher dans tout le disque (lent, et hors du rôle du panneau) ; une fenêtre de statistiques séparée (la page Organisation est l'endroit où l'on agit ensuite).
**Conséquences :** nouvelle option `SearchContent` ; `ContentSearch` dans `src/Search`, appelée sur un fil de fond ; le bac à sable bloque l'index réel pendant les tests (la requête est testée sur sa construction et le nettoyage du terme).

## D-009 — Règles de rangement (2026-11-27) · Statut : Acceptée

**Contexte :** le rangement automatique (D-005, `Archiver`) ne regarde que l'ancienneté : tout élément inutilisé depuis N jours part dans un seul dossier d'archive. On veut aussi pouvoir dire « les captures d'écran vont dans Images après 7 jours » ou « les .zip partent dans Archives ».
**Décision proposée :**
- Une **règle** = un nom, un **filtre** (extensions, par exemple `png jpg`, ou un mot contenu dans le nom, par exemple `Capture`), un **âge minimal** en jours (0 = dès que le fichier est là depuis une heure) et une **destination**.
- **Destinations permises** : un sous-dossier du dossier d'archive, ou un dossier connu de l'utilisateur (Images, Documents, Téléchargements, Vidéos, Musique), éventuellement avec un sous-dossier. Pas de chemin libre : on ne déplace jamais vers un lecteur réseau, un dossier système ou un dossier synchronisé inconnu.
- **Fichiers seulement** (pas les dossiers), posés directement sur le bureau, avec les mêmes protections que le rangement actuel : jamais les favoris, les raccourcis, les fichiers cachés ou système, ni un fichier utilisé ou modifié depuis l'âge de la règle ; évaluation juste avant chaque déplacement ; pas pendant une opération en cours dans le panneau.
- **Ordre** : les règles passent avant le rangement par ancienneté, dans l'ordre de la liste ; la première qui correspond l'emporte.
- **Annulation** : chaque passage est journalisé comme aujourd'hui (`ArchiveLog`) et « Annuler le dernier rangement » remet tout en place, règles comprises.
- **Interface** : Paramètres → Organisation → « Règles de rangement » : liste (nom, filtre, âge, destination), boutons Ajouter, Modifier, Supprimer, et un dialogue d'édition avec un **aperçu** (« 3 fichiers du bureau correspondent maintenant »).
- **Activation** : les règles ne s'appliquent que si le rangement automatique est activé (même interrupteur, même confirmation), et « Ranger maintenant » les applique aussi.
**Alternatives écartées :** chemin de destination libre (risque de déplacer vers un endroit inattendu ou partagé) ; expressions régulières (trop techniques pour la plupart des utilisateurs) ; règles sur les dossiers (un dossier contient des fichiers d'âges différents, déjà traité par le rangement par ancienneté).
**Conséquences :** nouveau réglage `Settings.ArchiveRules` sérialisé dans `settings.ini` (donc sauvegardé et restaurable, D-007) ; `Archiver` gagne une étape de règles avant l'ancienneté ; les tests passent par le bureau et les dossiers isolés du bac à sable.

## D-008 — Échelle par écran (DPI par moniteur) (2026-11-27) · Statut : Proposée, reportée

**Contexte :** l'application est « DPI système » : l'échelle (`Ui.Scale`) est lue au démarrage et vaut pour toutes les fenêtres. Sur deux écrans de zoom différent, Windows étire l'image sur l'un d'eux, ce qui la rend floue (R-10).
**Décision proposée :** passer en DPI par moniteur V2 (`SetProcessDpiAwarenessContext`) ; remplacer l'échelle globale par une échelle propre à chaque fenêtre (le panneau, les Paramètres, chaque dialogue), recalculée sur `WM_DPICHANGED`, avec recréation des polices et des images mises en cache à la nouvelle taille.
**Pourquoi reportée :** tout le dessin passe par l'échelle globale `Ui.S` ; le changement touche chaque fenêtre et chaque contrôle dessiné. Le chef de projet n'a qu'un écran aujourd'hui : aucun bénéfice immédiat pour un risque élevé. À reprendre avec deux écrans de zoom différent pour l'essai (tâche T-027).
**Alternatives écartées :** l'option `DpiAwareness` de `app.config` (WinForms 4.7+) : elle ne suffit pas pour une interface dessinée à la main avec une échelle globale.

## D-007 — Sauvegarde automatique des réglages (2026-11-24) · Statut : Acceptée

**Contexte :** `settings.ini` est réécrit en entier à chaque changement, et une seule écriture fautive efface tout sans retour (incident QA-32 : favoris, ordre, catégories et langue perdus).
**Décision :** à la première sauvegarde de chaque jour, le fichier précédent, s'il est valide (mêmes critères que `Load` : taille ≤ 2 Mio, ligne `HideIcons`), est copié dans `sauvegardes\settings-AAAA-MM-JJ.ini`. On garde les 7 plus récentes. Restaurer une sauvegarde ou importer un fichier copie d'abord l'état actuel dans `sauvegardes\settings-avant-restauration.ini` (un seul emplacement), lui-même restaurable. Restauration depuis Paramètres → Avancé. Une sauvegarde qui échoue ne bloque jamais l'enregistrement. La désinstallation supprime le dossier.
**Alternatives écartées :** une copie à chaque écriture (une rafale de mauvaises écritures écraserait les bonnes copies en quelques secondes) ; le paramètre de sauvegarde de `File.Replace` (une seule version, la précédente) ; une sauvegarde dans OneDrive (données personnelles envoyées hors du PC sans demande).
**Conséquences :** jusqu'à 8 fichiers de 2 Mio au plus dans le dossier des réglages. Le code passe par `SettingsBackup` ; les tests utilisent le dossier isolé (`Settings.DirOverride`). **Complément après revue (QA-33) :** la restauration et l'import sont transactionnels. L'état en mémoire est écrit à part, la source est chargée sur des listes vides puis enregistrée, et le point « avant restauration » n'est remplacé qu'à la fin. Au moindre échec, la mémoire et le disque reviennent à l'état initial. La rotation ne compte que les copies lisibles, ne supprime jamais la copie du jour et se relance à chaque enregistrement.

## D-001 — Compilation par csc.exe du .NET Framework, C# 5, sans dépendance (2026-11-23) · Statut : Acceptée

**Contexte :** l'application doit pouvoir être compilée sur n'importe quel Windows sans rien installer (`build.cmd`).
**Décision :** on conserve le compilateur fourni avec Windows, sans `.csproj` ni NuGet. Le code de `src/` reste en C# 5.
**Alternatives écartées :** projet SDK `dotnet build`. Il apporterait un C# récent, mais imposerait une installation au contributeur et à la CI.
**Conséquences :** tout code proposé par une IA doit être relu pour la syntaxe C# 5. `build.cmd` est le test de compilation de référence.

## D-005 — Météo et nettoyage rapide (2026-11-23) · Statut : Acceptée

**Contexte :** le chef de projet demande deux fonctions : une météo qui montre le temps du lieu de l'utilisateur, où qu'il soit dans le monde, avec une image ; et un nettoyage des fichiers inutiles qui ne touche jamais à Windows. Gemini a fait la recherche (services, chemins de cache, pièges), puis Claude l'a vérifiée en réel.
**Décisions (choix du chef de projet, détails techniques de Claude) :**
- **Météo** : prévisions **Open-Meteo** (gratuit, sans clé, attribution CC BY 4.0 affichée dans le détail) ; lieu choisi par **recherche de ville** (géocodage Open-Meteo) ou **position Windows** (`GeoCoordinateWatcher`, nouvelle référence `System.Device.dll`) nommée par **BigDataCloud** (gratuit, sans clé, conçu pour les applications clientes). **Désactivée par défaut** : rien n'est envoyé tant que l'utilisateur n'a pas choisi un lieu. Une tuile dans le panneau (icône dessinée, température, lieu, min/max), un détail au clic (ressenti, vent, 3 jours), une actualisation toutes les 30 min au plus, des unités °C/°F selon la région. Les réponses JSON sont lues par un petit analyseur interne, sans bibliothèque (D-001). Les hôtes autorisés sont listés (`Http.FetchText`), les redirections contrôlées et les tailles limitées.
- **Nettoyage** : depuis le **clic droit sur la tuile Disque**. Six catégories : cochées par défaut, les fichiers temporaires de plus de 24 h et les caches des navigateurs et applications ; décochées, les rapports d'erreurs, les miniatures et la corbeille. Analyse d'abord, puis confirmation. Règles : uniquement sous `%LOCALAPPDATA%`, `%APPDATA%` et `%TEMP%`, jamais dans Windows, Program Files ou OneDrive ; suppression fichier par fichier, sans jamais suivre une jonction ni un lien ; fichiers « en ligne uniquement » ignorés ; cache d'une application ignoré tant qu'elle est ouverte.
**Alternatives écartées :** géolocalisation par adresse IP (envoi de l'IP à un tiers, position approximative) ; Nominatim (politique d'usage défavorable aux applications distribuées) ; services à clé API (la clé serait publique dans le code) ; cache de **Spotify** (son dossier peut contenir la musique téléchargée hors ligne) ; `JavaScriptSerializer` (nouvelle référence inutile).
**Conséquences :** `build.cmd` et `outils/compilation-commun.ps1` référencent `System.Device.dll` (les deux doivent rester alignés). Sur un PC sans Wi-Fi, Windows peut ne fournir aucune position (constaté sur le PC du chef de projet) : la recherche de ville est le chemin principal. Si Open-Meteo ou BigDataCloud changent leurs conditions, la météo devra être revue.

## D-004 — Mises à jour signées par une clé hors de GitHub (2026-11-23) · Statut : Acceptée

**Contexte :** la mise à jour vérifiait seulement un SHA-256 publié par le même dépôt que l'exe : un compte ou un dépôt GitHub compromis pouvait remplacer les deux (R-04). La recherche de Gemini (T-005) a comparé les options disponibles en 2026 pour un développeur individuel en France, sans budget.
**Décision (chef de projet, sur recommandation de Gemini et de Claude) :**
- **Signature RSA-3072 détachée** (PKCS#1 v1.5, SHA-256) dans `BureauCache.exe.sig`, vérifiée par `Updater.VerifySignature` avec les API du framework (`RSACryptoServiceProvider`, fournisseur 24).
- **GitHub Actions** vérifie que le tag compile et crée un **brouillon**. `outils/signer-version.ps1` **recompile le tag sur le PC du développeur**, remplace l'exe du brouillon par cette compilation, la signe et publie. La clé privée ne va jamais sur GitHub. *Révisé après la revue QA-21 : dans un premier temps, c'était l'exe de la CI qui était signé. Or un GitHub compromis peut glisser un exe piégé dans le brouillon, et la comparaison avec une compilation locale est impossible, car les versions du compilateur diffèrent (environ 81 000 octets d'écart, mesurés sur la 3.3). Le chef de projet a choisi de signer la compilation locale.*
- **Deux clés publiques embarquées** (`src/Core/UpdateKeys.cs`) : la principale, et une clé de secours rangée hors ligne pour pouvoir changer de clé. Les clés privées sont chiffrées par mot de passe (PBKDF2-SHA256, AES-256, HMAC), stockées hors OneDrive (`%USERPROFILE%\BureauCache-cles`) et copiées sur une clé USB.
- **Dépôt officiel seulement** : un dépôt personnalisé (fork) n'a pas la clé de l'éditeur. Pour lui, la vérification reste le SHA-256, et la confirmation d'installation l'indique.
**Alternatives écartées :** Azure Trusted Signing (inaccessible aux particuliers de l'UE, payant) ; Certum (environ 50 à 70 € par an) ; SignPath Foundation (gratuit, mais certificat au nom de la fondation, délai d'admission, et un workflow compromis peut demander une signature : à reconsidérer plus tard pour SmartScreen seulement) ; attestations GitHub (aucune protection contre un dépôt compromis, invérifiables en C# 5) ; Ed25519 (absent du .NET Framework).
**Conséquences :** à partir de la 3.4, toute version du dépôt officiel doit être signée, sinon elles ne sont ni proposées ni installées. La 3.3 installe la 3.4 comme avant (SHA-256 seul) : cette première transition n'est **pas** protégée contre un GitHub déjà compromis (risque résiduel R-24). Les scripts de signature s'exécutent depuis la copie locale du dépôt : il faut relire tout changement de `outils/` avant de les lancer (R-25). Perdre les deux clés privées ou le mot de passe bloque les mises à jour automatiques : seul un téléchargement manuel resterait possible. SmartScreen n'est pas affecté.

## D-003 — Calculatrice : la puissance avant le signe (2026-11-23) · Statut : Acceptée

**Contexte :** `-2^2` valait 4, parce que le moins unaire était appliqué avant la puissance (R-20).
**Décision (chef de projet) :** on suit la notation mathématique : `-2^2 = -(2^2) = -4`. La puissance reste associative à droite, l'exposant peut être signé (`2^-1`), et le pourcentage s'applique avant la puissance (`10%^2 = 0,01`).
**Alternatives écartées :** la convention d'Excel (4), moins attendue dans un lanceur qui ressemble à une calculatrice de poche ou à un moteur de recherche.
**Conséquences :** un calcul `-x^n` copié depuis Excel peut donner un résultat différent. Les tests de `CalcTests` fixent ce comportement.

## D-002 — Tests dans un projet séparé qui référence BureauCache.exe (2026-11-23) · Statut : Acceptée

**Contexte :** aucun test automatisé n'existait. La logique pure (`Calc`, `Search`, normalisation, validation de noms) est testable sans interface.
**Décision :** le dossier `tests/BureauCache.Tests` contient un projet SDK (MSTest, cible `net48`) qui **référence le `BureauCache.exe` produit par `build.cmd`**. Un `InternalsVisibleTo("BureauCache.Tests")` dans `AssemblyInfo.cs` lui donne accès aux classes internes. Le projet relance `build.cmd` avant chaque compilation des tests (`-p:SkipAppBuild=true` pour l'éviter). Les tests tournent en série (`DoNotParallelize`), à cause de l'état statique de l'application. Le workflow `.github/workflows/tests.yml` les lance sur chaque PR.
**Alternatives écartées :** inclure les sources par lien. `Calc` dépend de `L`, qui dépend de `Settings`, et ainsi de suite : il faudrait entraîner presque toute l'application, compilée par un autre compilateur que celui du binaire distribué.
**Conséquences :** les tests portent sur le binaire réel. Le .NET SDK n'est nécessaire que pour les tests. Un test qui modifie un état statique (`L.CurrentLanguage`, `Settings`) doit le restaurer, par exemple avec `LanguageScope`. Les tests sont écrits en C# 7.3 (valeur par défaut de `net48`), le code de `src/` reste en C# 5.


## D-006 - Langue espagnole et capture du panneau

Les traductions restent embarquées dans `L.T(fr, en, es)`. Les identifiants existants 0 à 2 sont préservés et 3 désigne l’espagnol. L’anglais sert de repli pour une traduction espagnole absente. La culture d’interface du système reste disponible pour le choix automatique.

Le panneau est visible par défaut dans les captures compatibles de l’écran entier. Le masquage est un choix explicite. Le flou dynamique réutilise cette exclusion si elle réussit. Avec le panneau visible en capture, une image du fond est prise avant son affichage.

Source Win32 : [SetWindowDisplayAffinity](https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-setwindowdisplayaffinity).
