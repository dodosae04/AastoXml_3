# AAS Converter GUI

## Publish

Run the publish script from the repo root:

```powershell
./build/publish.ps1
```

This produces a single-file publish output under:

```
AasExcelToXml.Gui/bin/Release/net8.0-windows/win-x64/publish
```

## Create an installer (Inno Setup)

1. Install Inno Setup.
2. Open `installer/AasExcelToXml.iss` in Inno Setup.
3. Ensure the publish output exists (see the publish step above).
4. Build the installer in Inno Setup.

The template expects the publish output path shown above and produces an installer named `AasExcelToXmlSetup.exe` by default.
