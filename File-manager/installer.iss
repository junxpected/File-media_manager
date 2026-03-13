[Setup]
AppName=Asset Explorer
AppVersion=1.0
AppPublisher=Шах
DefaultDirName={autopf}\Asset Explorer
DefaultGroupName=Asset Explorer
OutputBaseFilename=AssetExplorer_Setup
Compression=lzma
SolidCompression=yes
WizardStyle=modern
SetupIconFile=icon.ico

[Languages]
Name: "ukrainian"; MessagesFile: "compiler:Languages\Ukrainian.isl"

[Tasks]
Name: "desktopicon"; Description: "Створити ярлик на робочому столі"; GroupDescription: "Додаткові параметри"
Name: "startup"; Description: "Запускати при старті Windows (згорнуто)"; GroupDescription: "Додаткові параметри"

[Files]
Source: "D:\File-manager_with_status\File-manager\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\Asset Explorer"; Filename: "{app}\File-manager.exe"
Name: "{group}\Видалити Asset Explorer"; Filename: "{uninstallexe}"
Name: "{commondesktop}\Asset Explorer"; Filename: "{app}\File-manager.exe"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "AssetExplorer"; ValueData: """{app}\File-manager.exe"" --minimized"; Flags: uninsdeletevalue; Tasks: startup

[Run]
Filename: "{app}\File-manager.exe"; Description: "Запустити Asset Explorer"; Flags: nowait postinstall skipifsilent