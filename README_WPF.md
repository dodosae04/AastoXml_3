# AasExcelToXml WPF 배포 안내

## 1) WPF 앱 Publish

```powershell
cd build
./publish.ps1
```

Publish 출력은 아래 폴더에 생성됩니다.

```
AasExcelToXml.Wpf\bin\Release\net8.0-windows\win-x64\publish
```

## 2) Inno Setup 설치 파일 생성

1. `installer/AasExcelToXml.iss`를 Inno Setup에서 열어 실행합니다.
2. 출력 파일 이름은 `AasExcelToXmlSetup.exe`이며, `Output` 폴더에 생성됩니다.

## 3) 참고 사항

- WPF 앱은 `AasExcelToXml.Wpf.exe`입니다.
- Settings는 `%AppData%\AasExcelToXml\settings.json`에 저장됩니다.
