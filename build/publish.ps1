$ErrorActionPreference = "Stop"

dotnet publish ..\AasExcelToXml.Wpf -c Release -r win-x64 --self-contained false /p:PublishSingleFile=true
