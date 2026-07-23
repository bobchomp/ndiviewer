; Inno Setup script for NDI Viewer.
;
; Expects the published .NET application to already exist in ..\publish
; (produced by `dotnet publish -r win-x64 --self-contained true -o publish`)
; and is compiled with the app version passed on the command line, e.g.:
;
;   iscc /DMyAppVersion=1.2.3 installer\setup.iss
;
#ifndef MyAppVersion
  #define MyAppVersion "0.0.0"
#endif

#define MyAppName "NDI Viewer"
#define MyAppPublisher "NDI Viewer Project"
#define MyAppURL "https://ndi.video"
#define MyAppExeName "NdiViewer.exe"
#define PublishDir "..\publish"

[Setup]
AppId={{6C0B6A6E-4B2C-4C43-9C9E-4E2B4B7A9C9E}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=NdiViewer-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
UninstallDisplayIcon={app}\{#MyAppExeName}
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[Code]
// --- NDI Runtime detection -------------------------------------------------
// NDI Viewer talks to the NDI network stack through the free, separately
// distributed "NDI Runtime". We never bundle NDI's own binaries in this
// installer; instead we detect whether the runtime is already present and,
// if not, offer to install it via winget (the same mechanism documented by
// NDI/Vizrt at https://ndi.link/NDIRedistV6).

function IsNdiRuntimeInstalled: Boolean;
var
  ProgramFiles: string;
begin
  Result := False;

  if (GetEnv('NDI_RUNTIME_DIR_V6') <> '') or (GetEnv('NDI_RUNTIME_DIR_V5') <> '') or
     (GetEnv('NDI_RUNTIME_DIR_V4') <> '') or (GetEnv('NDI_RUNTIME_DIR') <> '') then
  begin
    Result := True;
    Exit;
  end;

  ProgramFiles := ExpandConstant('{commonpf64}');
  if ProgramFiles = '' then
    ProgramFiles := ExpandConstant('{commonpf}');

  if DirExists(ProgramFiles + '\NDI\NDI 6 Runtime\Bin\x64') or
     DirExists(ProgramFiles + '\NDI\NDI 5 Runtime\Bin\x64') or
     DirExists(ProgramFiles + '\NDI\NDI 6 Tools\Bin') or
     DirExists(ProgramFiles + '\NDI\NDI 5 Tools\Bin') then
  begin
    Result := True;
  end;
end;

function IsWingetAvailable: Boolean;
var
  ResultCode: Integer;
begin
  Result := Exec(ExpandConstant('{cmd}'), '/C where winget.exe', '', SW_HIDE,
    ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
end;

procedure InstallNdiRuntime;
var
  ResultCode: Integer;
  Installed: Boolean;
begin
  Installed := False;

  if IsWingetAvailable then
  begin
    if Exec('winget.exe',
         'install --exact --silent --id NDI.NDIRuntime --accept-package-agreements --accept-source-agreements',
         '', SW_SHOW, ewWaitUntilTerminated, ResultCode) then
    begin
      Installed := (ResultCode = 0);
    end;
  end;

  if not Installed then
  begin
    if MsgBox(
         'NDI Viewer needs the free NDI Runtime to find and receive NDI video on your ' +
         'network, and it could not be installed automatically.' + #13#10 + #13#10 +
         'Click OK to open the NDI Runtime download page in your browser. Install it, ' +
         'then start NDI Viewer.',
         mbInformation, MB_OKCANCEL) = IDOK then
    begin
      ShellExec('open', 'https://ndi.link/NDIRedistV6', '', '', SW_SHOW, ewNoWait, ResultCode);
    end;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    if not IsNdiRuntimeInstalled then
      InstallNdiRuntime;
  end;
end;
