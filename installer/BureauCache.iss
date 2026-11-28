; BureauCache — installateur Inno Setup 6 (T-056)
;
; Compilé par outils\construire-installateur.ps1, qui passe les paramètres ci-dessous :
;   ISCC.exe /DAppVersion=3.11.1 /DSourceExe=<chemin de BureauCache.exe signé> [/DSIGN]
; /DSIGN n'est passé que si une signature est configurée (voir installer\README.md).
;
; Choix (détaillés dans installer\README.md) :
;  - installation par utilisateur, sans administrateur, dans %LOCALAPPDATA%\Programs\BureauCache :
;    c'est le dossier attendu par Installer.Dir et par le moteur de mise à jour (Updater.ReplaceExecutable) ;
;  - AppId = BureauCache : Inno écrit donc la clé « Applications installées » BureauCache_is1, celle que
;    Installer (Installation.cs) utilise aussi, pour une seule entrée ;
;  - raccourcis aux emplacements que l'application retire elle-même à la désinstallation ;
;  - fermeture propre avant installation ou désinstallation : événement Local\BureauCache_Quit, puis attente
;    du mutex Local\BureauCache_SingleInstance, après un --restore qui rend la barre Windows.

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
AppCopyright=Copyright (c) 2026 Kuroi1998, licence MIT
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
VersionInfoDescription=Installateur de BureauCache
LicenseFile=..\LICENSE
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
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

[Tasks]
Name: "bureau"; Description: "Créer un raccourci sur le Bureau"; GroupDescription: "Raccourcis :"; Flags: unchecked
Name: "demarrage"; Description: "Lancer BureauCache quand je me connecte à Windows"; GroupDescription: "Au démarrage :"; Flags: unchecked

[Files]
Source: "{#SourceExe}"; DestDir: "{app}"; DestName: "BureauCache.exe"; Flags: ignoreversion

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
; Même format que Startup.Set : "chemin entre guillemets", dans la clé Run de l'utilisateur
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "BureauCache"; ValueData: """{app}\BureauCache.exe"""; Tasks: demarrage; Flags: uninsdeletevalue

[Run]
Filename: "{app}\BureauCache.exe"; Description: "Lancer BureauCache"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent

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

function T(fr, en: String): String;
begin
  if ActiveLanguage = 'french' then Result := fr else Result := en;
end;

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

function InitializeSetup(): Boolean;
var
  release: Cardinal;
begin
  Result := True;
  if (not RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', release)) or
     (release < NET48_RELEASE) then
  begin
    MsgBox(T('BureauCache a besoin de .NET Framework 4.8 ou plus récent. Installe-le depuis le site de Microsoft, puis relance cet installateur.',
             'BureauCache requires .NET Framework 4.8 or later. Install it from Microsoft''s website, then run this installer again.'),
           mbCriticalError, MB_OK);
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
    MsgBox(T('Choisis un dossier dans ton profil (par exemple %LOCALAPPDATA%\Programs\BureauCache). La racine d''un disque, Program Files et Windows ne sont pas acceptés.',
             'Choose a folder in your profile (for example %LOCALAPPDATA%\Programs\BureauCache). The root of a drive, Program Files and Windows are not accepted.'),
           mbError, MB_OK);
    Result := False;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if not StopBureauCache() then
    Result := T('BureauCache est encore ouvert. Ferme-le depuis son icône de la zone de notification, puis relance l''installation.',
                'BureauCache is still running. Close it from its notification-area icon, then run the setup again.');
end;

function InitializeUninstall(): Boolean;
begin
  Result := StopBureauCache();
  if not Result then
    MsgBox(T('BureauCache est encore ouvert. Ferme-le depuis son icône de la zone de notification, puis relance la désinstallation.',
             'BureauCache is still running. Close it from its notification-area icon, then uninstall again.'),
           mbError, MB_OK);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    // Par défaut, les réglages sont conservés : seule une réponse « Oui » les supprime
    if MsgBox(T('Supprimer aussi tes réglages, tes sauvegardes et le journal (dossier %APPDATA%\BureauCache) ?',
                'Also delete your settings, backups and log (the %APPDATA%\BureauCache folder)?'),
              mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
      DelTree(ExpandConstant('{userappdata}\BureauCache'), True, True, True);
  end;
end;
