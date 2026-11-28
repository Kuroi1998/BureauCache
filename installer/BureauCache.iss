; BureauCache — installateur Inno Setup 6 (T-056, T-058)
;
; Compilé par outils\construire-installateur.ps1, qui passe les paramètres ci-dessous :
;   ISCC.exe /DAppVersion=3.11.1 /DSourceExe=<chemin de BureauCache.exe signé> [/DSIGN]
; /DSIGN n'est passé que si une signature est configurée (voir installer\README.md).
;
; Choix (détaillés dans installer\README.md) :
;  - installation par utilisateur, sans administrateur, dossier au choix (par défaut %LOCALAPPDATA%\Programs\BureauCache) ;
;  - AppId = BureauCache : Inno écrit la clé « Applications installées » BureauCache_is1, celle qu'Installer utilise ;
;  - raccourcis aux emplacements que l'application retire elle-même à la désinstallation ;
;  - fermeture propre avant installation ou désinstallation : --restore, événement Local\BureauCache_Quit,
;    puis attente du mutex Local\BureauCache_SingleInstance ;
;  - français, anglais et espagnol : tous les textes propres à BureauCache sont dans [CustomMessages].

#ifndef AppVersion
  #error "Passer /DAppVersion=X.Y.Z à ISCC (voir outils\construire-installateur.ps1)"
#endif
#ifndef SourceExe
  #error "Passer /DSourceExe=chemin de BureauCache.exe à ISCC"
#endif

#define AppName "BureauCache"
#define AppPublisher "BureauCache"
#define AppUrl "https://github.com/Kuroi1998/BureauCache"

[Setup]
AppId=BureauCache
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}
AppUpdatesURL={#AppUrl}/releases
AppCopyright=Copyright (c) 2026 Kuroi1998 - MIT
AppComments=BureauCache {#AppVersion}
DefaultDirName={localappdata}\Programs\BureauCache
DisableProgramGroupPage=yes
DisableDirPage=no
PrivilegesRequired=lowest
UsePreviousAppDir=yes
MinVersion=10.0.17763
OutputDir=..\dist
OutputBaseFilename=BureauCache-Setup-{#AppVersion}
SetupIconFile=..\BureauCache.ico
UninstallDisplayIcon={app}\BureauCache.exe
UninstallDisplayName=BureauCache {#AppVersion}
VersionInfoVersion={#AppVersion}.0
VersionInfoCompany={#AppPublisher}
VersionInfoProductName={#AppName}
VersionInfoDescription=BureauCache - installateur
LicenseFile=..\LICENSE
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
WizardResizable=no
WizardImageFile=images\bandeau.bmp
WizardSmallImageFile=images\petit.bmp
ShowLanguageDialog=yes
CloseApplications=no
RestartApplications=no
ChangesAssociations=no
#ifdef SIGN
; Signature Authenticode de l'installateur et de son désinstalleur, par la commande « bc » passée à ISCC
SignTool=bc
SignedUninstaller=yes
#endif

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[CustomMessages]
french.Bienvenue=Ce programme installe BureauCache sur ton ordinateur. BureauCache garde ton bureau propre en rangeant les icônes sur le côté de l'écran.%n%nTu peux choisir le dossier d'installation à l'étape suivante.
english.Bienvenue=This program installs BureauCache on your computer. BureauCache keeps your desktop tidy by tucking the icons at the edge of the screen.%n%nYou can choose the installation folder in the next step.
spanish.Bienvenue=Este programa instala BureauCache en tu equipo. BureauCache mantiene tu escritorio ordenado guardando los iconos en el borde de la pantalla.%n%nPuedes elegir la carpeta de instalación en el siguiente paso.

french.ChoisirDossierTitre=Choisir le dossier d'installation de BureauCache
english.ChoisirDossierTitre=Choose the BureauCache installation folder
spanish.ChoisirDossierTitre=Elegir la carpeta de instalación de BureauCache

french.DossierRefuse=Choisis un dossier de ton profil, par exemple %LOCALAPPDATA%\Programs\BureauCache. La racine d'un disque, Program Files et Windows ne sont pas acceptés.
english.DossierRefuse=Choose a folder in your profile, for example %LOCALAPPDATA%\Programs\BureauCache. The root of a drive, Program Files and Windows are not accepted.
spanish.DossierRefuse=Elige una carpeta de tu perfil, por ejemplo %LOCALAPPDATA%\Programs\BureauCache. No se aceptan la raíz de una unidad, Program Files ni Windows.

french.NetRequis=BureauCache a besoin de .NET Framework 4.8 ou plus récent. Installe-le depuis le site de Microsoft, puis relance cet installateur.
english.NetRequis=BureauCache requires .NET Framework 4.8 or later. Install it from the Microsoft website, then run this installer again.
spanish.NetRequis=BureauCache necesita .NET Framework 4.8 o posterior. Instálalo desde el sitio de Microsoft y vuelve a ejecutar este instalador.

french.FermerInstallation=BureauCache est encore ouvert. Ferme-le depuis son icône de la zone de notification, puis relance l'installation.
english.FermerInstallation=BureauCache is still running. Close it from its notification-area icon, then run the setup again.
spanish.FermerInstallation=BureauCache sigue abierto. Ciérralo desde su icono del área de notificación y vuelve a ejecutar la instalación.

french.FermerDesinstallation=BureauCache est encore ouvert. Ferme-le depuis son icône de la zone de notification, puis relance la désinstallation.
english.FermerDesinstallation=BureauCache is still running. Close it from its notification-area icon, then uninstall again.
spanish.FermerDesinstallation=BureauCache sigue abierto. Ciérralo desde su icono del área de notificación y vuelve a iniciar la desinstalación.

french.SupprimerDonnees=Supprimer aussi tes réglages, tes sauvegardes et le journal (dossier %APPDATA%\BureauCache) ?
english.SupprimerDonnees=Also delete your settings, backups and log (the %APPDATA%\BureauCache folder)?
spanish.SupprimerDonnees=¿Eliminar también tu configuración, tus copias de seguridad y el registro (carpeta %APPDATA%\BureauCache)?

french.TacheBureau=Créer un raccourci sur le Bureau
english.TacheBureau=Create a desktop shortcut
spanish.TacheBureau=Crear un acceso directo en el escritorio

french.TacheRaccourcis=Raccourcis
english.TacheRaccourcis=Shortcuts
spanish.TacheRaccourcis=Accesos directos

french.TacheDemarrage=Lancer BureauCache quand je me connecte à Windows
english.TacheDemarrage=Start BureauCache when I sign in to Windows
spanish.TacheDemarrage=Iniciar BureauCache al iniciar sesión en Windows

french.TacheDemarrageGroupe=Démarrage automatique
english.TacheDemarrageGroupe=Automatic start
spanish.TacheDemarrageGroupe=Inicio automático

french.LancerApp=Lancer BureauCache
english.LancerApp=Launch BureauCache
spanish.LancerApp=Iniciar BureauCache

[Tasks]
Name: "bureau"; Description: "{cm:TacheBureau}"; GroupDescription: "{cm:TacheRaccourcis}"; Flags: unchecked
Name: "demarrage"; Description: "{cm:TacheDemarrage}"; GroupDescription: "{cm:TacheDemarrageGroupe}"; Flags: unchecked

[Files]
Source: "{#SourceExe}"; DestDir: "{app}"; DestName: "BureauCache.exe"; Flags: ignoreversion
; Sélecteur de dossier moderne, extrait seulement pendant l'installation (voir InitializeWizard)
Source: "choisir-dossier.ps1"; DestDir: "{tmp}"; Flags: dontcopy

[InstallDelete]
; Restes de mises à jour faites dans l'application (Updater) : l'essai et le retour arrière repartent de zéro
Type: files; Name: "{app}\BureauCache.exe.old"
Type: files; Name: "{app}\BureauCache.exe.trial"
Type: files; Name: "{app}\BureauCache.exe.bad"
Type: files; Name: "{app}\BureauCache.exe.replaced"

[Icons]
; Même nom que celui que l'application retire à la désinstallation (Installer.StartMenuLnk : Programs\BureauCache.lnk)
Name: "{userprograms}\BureauCache"; Filename: "{app}\BureauCache.exe"; WorkingDir: "{app}"
Name: "{userdesktop}\BureauCache"; Filename: "{app}\BureauCache.exe"; WorkingDir: "{app}"; Tasks: bureau

[Registry]
; Même format que Startup.Set : « chemin entre guillemets », dans la clé Run de l'utilisateur
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "BureauCache"; ValueData: """{app}\BureauCache.exe"""; Tasks: demarrage; Flags: uninsdeletevalue

[Run]
Filename: "{app}\BureauCache.exe"; Description: "{cm:LancerApp}"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent

; Pas de [UninstallDelete] sur {app} : le dossier choisi peut contenir des fichiers de l'utilisateur. Inno ne retire
; que les fichiers qu'il a installés, et les dossiers qu'il a créés s'ils sont vides.

[Code]
const
  EVENT_MODIFY_STATE = $0002;
  SYNCHRONIZE = $00100000;
  NET48_RELEASE = 528040;

function OpenEventW(dwDesiredAccess: DWORD; bInheritHandle: Boolean; lpName: String): THandle;
  external 'OpenEventW@kernel32.dll stdcall';
function SetEvent(hEvent: THandle): Boolean;
  external 'SetEvent@kernel32.dll stdcall';
function OpenMutexW(dwDesiredAccess: DWORD; bInheritHandle: Boolean; lpName: String): THandle;
  external 'OpenMutexW@kernel32.dll stdcall';
function CloseHandle(hObject: THandle): Boolean;
  external 'CloseHandle@kernel32.dll stdcall';

// Rend la barre Windows, puis demande à BureauCache de se fermer et attend le mutex (au plus 5 s).
// Vrai si l'application ne tourne plus.
function StopBureauCache(): Boolean;
var
  h, m: THandle;
  rc, i: Integer;
  exe: String;
begin
  exe := ExpandConstant('{app}\BureauCache.exe');
  if FileExists(exe) then Exec(exe, '--restore', '', SW_HIDE, ewWaitUntilTerminated, rc);
  h := OpenEventW(EVENT_MODIFY_STATE, False, 'Local\BureauCache_Quit');
  if h <> 0 then
  begin
    SetEvent(h);
    CloseHandle(h);
  end;
  m := 0;
  for i := 1 to 50 do
  begin
    m := OpenMutexW(SYNCHRONIZE, False, 'Local\BureauCache_SingleInstance');
    if m = 0 then Break;
    CloseHandle(m);
    Sleep(100);
  end;
  Result := (m = 0);
end;

// Bouton « Parcourir » : ouvre la boîte moderne de Windows (avec « Nouveau dossier ») et recopie le choix
procedure ParcourirDossier(Sender: TObject);
var
  picker, sortie: String;
  texte: AnsiString;
  rc: Integer;
begin
  // Fichier « dontcopy » : il n'existe dans {tmp} qu'après extraction explicite
  ExtractTemporaryFile('choisir-dossier.ps1');
  picker := ExpandConstant('{tmp}\choisir-dossier.ps1');
  sortie := ExpandConstant('{tmp}\dossier-choisi.txt');
  DeleteFile(sortie);
  Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
       '-NoProfile -ExecutionPolicy Bypass -STA -File "' + picker + '" -Sortie "' + sortie +
       '" -Depart "' + WizardForm.DirEdit.Text + '" -Titre "' + CustomMessage('ChoisirDossierTitre') + '"',
       '', SW_HIDE, ewWaitUntilTerminated, rc);
  if FileExists(sortie) then
  begin
    // Lu en ANSI, comme le fichier écrit par choisir-dossier.ps1
    if LoadStringFromFile(sortie, texte) and (Trim(texte) <> '') then
      WizardForm.DirEdit.Text := Trim(String(texte));
    DeleteFile(sortie);
  end;
end;

procedure InitializeWizard();
begin
  // Le bouton « Parcourir » de la page du dossier ouvre le sélecteur moderne au lieu de la boîte de Inno
  WizardForm.DirBrowseButton.OnClick := @ParcourirDossier;
  WizardForm.WelcomeLabel2.Caption := CustomMessage('Bienvenue');
end;

function InitializeSetup(): Boolean;
var
  release: Cardinal;
begin
  Result := True;
  if (not RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', release)) or
     (release < NET48_RELEASE) then
  begin
    MsgBox(CustomMessage('NetRequis'), mbCriticalError, MB_OK);
    Result := False;
  end;
end;

// Dossier choisi par l'utilisateur : refuse la racine d'un disque et les dossiers système, que l'installation
// par utilisateur (sans administrateur) ne peut de toute façon pas écrire
function DossierAccepte(dir: String): Boolean;
var
  bas: String;
begin
  bas := Lowercase(RemoveBackslashUnlessRoot(dir));
  Result := True;
  if Length(bas) <= 3 then Result := False;
  if Pos(Lowercase(ExpandConstant('{pf}')), bas) = 1 then Result := False;
  if Pos(Lowercase(ExpandConstant('{pf32}')), bas) = 1 then Result := False;
  if Pos(Lowercase(ExpandConstant('{win}')), bas) = 1 then Result := False;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if (CurPageID = wpSelectDir) and (not DossierAccepte(WizardDirValue)) then
  begin
    MsgBox(CustomMessage('DossierRefuse'), mbError, MB_OK);
    Result := False;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if not StopBureauCache() then
    Result := CustomMessage('FermerInstallation');
end;

function InitializeUninstall(): Boolean;
begin
  Result := StopBureauCache();
  if not Result then
    MsgBox(CustomMessage('FermerDesinstallation'), mbError, MB_OK);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    // Par défaut, les réglages sont conservés : seule une réponse « Oui » les supprime
    if MsgBox(CustomMessage('SupprimerDonnees'), mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
      DelTree(ExpandConstant('{userappdata}\BureauCache'), True, True, True);
  end;
end;
