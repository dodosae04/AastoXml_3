; Inno Setup script template for AAS Converter (WPF)

[Setup]
AppName=AAS Converter
AppVersion=1.0.0
AppPublisher=AasExcelToXml
DefaultDirName={pf}\AasExcelToXml
DefaultGroupName=AAS Converter
OutputBaseFilename=AasExcelToXmlSetup
Compression=lzma
SolidCompression=yes

[Files]
Source: "..\AasExcelToXml.Wpf\bin\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\AAS Converter"; Filename: "{app}\AasExcelToXml.Wpf.exe"
