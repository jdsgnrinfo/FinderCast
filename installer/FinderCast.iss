; Instalador de FinderCast (Inno Setup 6). Lo compila scripts/publicar.ps1 a partir de la versión
; portable ya publicada en artifacts/FinderCast-<versión>-portable/, y deja el resultado en
; artifacts/FinderCast-<versión>-setup.exe.
;
; Se instala solo para el usuario actual (en %LocalAppData%\Programs\FinderCast), sin pedir
; permisos de administrador, con acceso en el menú Inicio y desinstalador en Configuración.

#ifndef Version
  #error Falta la versión: iscc /DVersion=X.Y.Z /DSource=<carpeta portable> FinderCast.iss
#endif
#ifndef Source
  #error Falta la carpeta de la versión portable: /DSource=...
#endif

[Setup]
; Identificador fijo: con él cada versión nueva actualiza la instalada en lugar de duplicarla.
AppId={{4AD21CBC-C2C1-4168-A9C0-140C56591AE4}
AppName=FinderCast
AppVersion={#Version}
AppVerName=FinderCast {#Version}
AppPublisher=Jotadsgnr
AppPublisherURL=https://github.com/jdsgnrinfo/FinderCast
AppSupportURL=https://github.com/jdsgnrinfo/FinderCast/issues
AppUpdatesURL=https://github.com/jdsgnrinfo/FinderCast/releases
AppCopyright=Copyright (c) 2026 Jotadsgnr. Licencia MIT.
VersionInfoVersion={#Version}
PrivilegesRequired=lowest
DefaultDirName={autopf}\FinderCast
DisableProgramGroupPage=yes
DisableDirPage=auto
LicenseFile={#Source}\LICENSE.txt
OutputDir=..\artifacts
OutputBaseFilename=FinderCast-{#Version}-setup
SetupIconFile=..\src\FinderCast\Assets\FinderCast.ico
UninstallDisplayIcon={app}\FinderCast.exe
UninstallDisplayName=FinderCast
WizardStyle=modern
; El .exe ya va comprimido por dentro: comprimirlo otra vez apenas ahorra.
Compression=lzma2/fast
SolidCompression=no
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
; Para actualizar, se cierra el FinderCast que esté en marcha (vive en la bandeja).
CloseApplications=force
RestartApplications=no

[Languages]
Name: "es"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "{#Source}\FinderCast.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#Source}\LICENSE.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#Source}\THIRD-PARTY-NOTICES.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\FinderCast"; Filename: "{app}\FinderCast.exe"

[Registry]
; FinderCast se apunta él mismo al inicio de Windows; al desinstalar se borra esa entrada.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "FinderCast"; ValueType: none; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run"; ValueName: "FinderCast"; ValueType: none; Flags: uninsdeletevalue

[Run]
Filename: "{app}\FinderCast.exe"; Description: "{cm:LaunchProgram,FinderCast}"; Flags: nowait postinstall skipifsilent
; Actualización desde los ajustes (instalación silenciosa): se vuelve a abrir en segundo plano.
Filename: "{app}\FinderCast.exe"; Parameters: "--background"; Flags: nowait; Check: WizardSilent

[UninstallRun]
; Cerrar el que esté en marcha antes de borrar el .exe.
Filename: "{sys}\taskkill.exe"; Parameters: "/F /IM FinderCast.exe"; Flags: runhidden; RunOnceId: "CerrarFinderCast"

[UninstallDelete]
; Los ajustes (%APPDATA%\FinderCast) se conservan, por si se vuelve a instalar.
Type: dirifempty; Name: "{app}"
