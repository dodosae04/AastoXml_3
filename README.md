# 엑셀/CSV → AAS 2.0/3.0 XML 변환기

이 프로젝트는 엑셀(xlsx) 또는 CSV 파일의 사양 데이터를 읽어 AAS 2.0 또는 AAS 3.0 XML로 변환하는 도구입니다. CLI와 간단한 GUI를 함께 제공하며, 사용자가 버전을 선택하면 해당 버전에 맞는 Writer가 동작합니다.【F:AasExcelToXml.Core/AasXmlWriterFactory.cs†L1-L13】【F:AasExcelToXml.Core/AasV2XmlWriter.cs†L1-L286】【F:AasExcelToXml.Core/AasV3XmlWriter.cs†L1-L260】

정답(골든) XML은 개발/테스트/분석용 파일이며, 변환 실행의 필수 의존성이 아닙니다. 정답 파일이 없어도 변환은 정상 수행됩니다.【F:AasExcelToXml.Core/AasXmlWriterFactory.cs†L1-L13】【F:AasExcelToXml.Core/Converter.cs†L1-L30】

## 설치 및 빌드

```bash
dotnet build
```

## GUI 사용법 (WinForms)

1. Visual Studio에서 `AasExcelToXml.Gui`를 시작 프로젝트로 선택합니다.
2. 폼이 뜨면 입력 파일이 비어 있는 경우 자동으로 파일 선택 창이 1회 표시됩니다.
3. `파일 선택` 버튼으로 입력 파일(xlsx/csv)을 선택합니다.
4. XLSX일 때만 시트명을 입력합니다(기본값: `사양시트`).
5. `AAS 버전`에서 2.0 또는 3.0을 선택합니다.
6. `저장 위치 선택`으로 출력 파일 경로를 지정합니다. 기본 파일명은 버전에 따라 `.aas2.xml` 또는 `.aas3.xml`로 채워집니다.
7. `변환 시작`을 누르면 완료 메시지가 표시됩니다.

GUI는 Core의 변환 함수를 직접 호출하여 처리합니다.【F:AasExcelToXml.Gui/MainForm.cs†L1-L247】【F:AasExcelToXml.Core/Converter.cs†L1-L30】

### Visual Studio에서 F5 실행 시 GUI가 뜨지 않는 경우

1. 솔루션 탐색기에서 `AasExcelToXml.Gui` 프로젝트를 우클릭합니다.
2. `시작 프로젝트로 설정`을 선택합니다.
3. F5로 실행합니다.

CLI가 시작 프로젝트인 경우에도 간단한 파일 선택 UI가 뜨도록 개선되어 있으나,
GUI 작업을 하려면 시작 프로젝트를 GUI로 설정하는 것이 가장 확실합니다.【F:AasExcelToXml.Gui/MainForm.cs†L1-L247】【F:AasExcelToXml.Cli/Program.cs†L1-L309】

## CLI 사용법

### 기본 실행(인자 없음)

인자가 없으면 자동 변환을 하지 않고, 파일 선택/저장 위치를 묻는 간단한 UI가 표시됩니다.
XLSX를 고르면 시트명 입력 창이 추가로 표시되며, AAS 2.0/3.0 선택창이 이어서 표시됩니다.

```bash
dotnet run --project AasExcelToXml.Cli
```

### 경로 지정 실행

```bash
dotnet run --project AasExcelToXml.Cli -- "입력.xlsx" "출력.xml"
```

이때는 다이얼로그를 띄우지 않고 즉시 변환합니다.

### 옵션 (AAS 2.0/3.0 선택)

- `--repoOut`: 기본 출력 위치를 레포 루트의 `out` 폴더로 변경합니다.
- `--sheet` 또는 `--sheet=시트명`: XLSX용 시트명을 지정합니다.
- `--version 2` 또는 `--version 3`: AAS 2.0/3.0을 선택합니다(기본값: 2.0).
- `--allDocs` 또는 `--allDocuments`: Documentation 문서가 여러 건일 때 전부 출력합니다(기본값: 1건만).

```bash
dotnet run --project AasExcelToXml.Cli -- --repoOut
```

```bash
dotnet run --project AasExcelToXml.Cli -- --sheet "사양시트"
```

```bash
dotnet run --project AasExcelToXml.Cli -- --version 3 "입력.xlsx" "출력.xml"
```

```bash
dotnet run --project AasExcelToXml.Cli -- --allDocs "입력.xlsx" "출력.xml"
```

CLI 역시 Core의 변환 함수를 호출하며, 인자 모드에서는 지정한 출력 경로로 결과가 생성됩니다.
대화형 모드에서는 사용자가 선택한 저장 위치에 결과 파일이 생성됩니다.【F:AasExcelToXml.Cli/Program.cs†L1-L309】【F:AasExcelToXml.Core/Converter.cs†L1-L30】

## 입력 엑셀/CSV 형식

### 시트 이름 (XLSX만 해당)

- 기본 시트명은 `사양시트`입니다.
- 다른 시트명인 경우, 사용 가능한 시트 목록을 한국어로 안내하는 예외가 발생합니다.【F:AasExcelToXml.Core/ExcelSpecReader.cs†L17-L43】

### 필수 헤더(별칭 지원)

| 필수 컬럼 | 대표 헤더 |
| --- | --- |
| AAS | `AAS`, `Asset (AAS)`, `Asset(AAS)` |
| Submodel | `Submodel`, `Sub Model` |
| SubmodelCollection | `SubmodelCollection`, `Submodel Collection` |
| Property_Kor | `Property_Kor`, `Property(Kor)` |
| Property_Eng | `Property_Eng`, `Property(Eng)` |
| Property type | `Property type`, `Property Type` |
| Value | `Value`, `값` |

선택 컬럼:

- `UOM` (없어도 동작)

헤더 alias를 지원하며, 공백/언더스코어 제거와 대소문자 무시 규칙을 적용합니다.【F:AasExcelToXml.Core/ExcelSpecReader.cs†L44-L204】

## 처리 규칙 요약

### 1) 빈 칸 이어받기(fill-forward)

엑셀의 AAS/Submodel/Collection 칸이 비어 있는 경우 이전 값을 이어받습니다.  
AAS가 바뀌면 Submodel/Collection을 리셋하고, Submodel이 바뀌면 Collection을 리셋합니다.【F:AasExcelToXml.Core/SpecGrouper.cs†L8-L45】

### 2) 요소 분류 규칙

- 일반 Property: `Property type`이 일반 타입이면 Property로 처리
- Entity: `Property type`이 `Entity`이면 Entity로 처리하되, 값에 `[first]/[second]` 패턴이 있으면 Relationship로 재분류
- Relationship: `Property type`이 `Relationship`이면 Relationship로 처리
- ReferenceElement: `Property_Eng`가 `[Robot_body Reference] Rotation_angle_of_body` 형태인 경우 ReferenceElement로 처리

각 규칙은 한국어 주석으로 코드에 명시되어 있습니다.【F:AasExcelToXml.Core/SpecGrouper.cs†L83-L180】

#### Documentation 입력행 분리 규칙

Documentation Submodel에서 `Document_name/Document_type/Document_file_path`에 해당하는 행은 문서 스켈레톤 생성 전용 입력으로 취급합니다. 해당 행은 실제 AAS Property로 생성하지 않으며, 중복 idShort 경고를 줄이기 위해 별도의 `DocumentationInput` 요소로 분리합니다.【F:AasExcelToXml.Core/SpecGrouper.cs†L163-L280】【F:AasExcelToXml.Core/AasV2XmlWriter.cs†L135-L178】【F:AasExcelToXml.Core/AasV3XmlWriter.cs†L118-L176】

### 3) idShort 정규화

공백/특수문자가 포함된 idShort는 AAS 파서에서 문제가 발생할 수 있으므로, 영문/숫자 외 문자는 `_`로 치환하고 연속 `_`를 정리합니다. 숫자로 시작하면 `_`를 접두어로 붙입니다.【F:AasExcelToXml.Core/SpecGrouper.cs†L210-L244】

### 4) Submodel 이름 정규화

샘플 AASX 네이밍과 맞추기 위한 특수 매핑을 적용합니다.【F:AasExcelToXml.Core/SpecGrouper.cs†L246-L261】

## 출력 구조

### AAS 2.0

AAS 2.0 선택 시 출력 XML은 AAS V2 네임스페이스(`http://www.admin-shell.io/aas/2/0`)를 사용하며 다음 섹션을 구성합니다.

1. `assetAdministrationShells`
2. `assets`
3. `submodels`
4. `conceptDescriptions` (비어 있어도 생성)

key/referenceElement/relationship 구조를 V2 샘플 형식에 맞추고, submodelElements 하위에 `submodelElement` 래퍼를 적용했습니다. identification은 `<identification idType="IRI">VALUE</identification>` 형태로 출력합니다.【F:AasExcelToXml.Core/AasV2XmlWriter.cs†L1-L286】

### AAS 3.0

AAS 3.0 선택 시 출력 XML은 **기본 네임스페이스 방식**(`https://admin-shell.io/aas/3/0`)으로 직렬화합니다. AASX Package Explorer가 prefix 형태(`aas:`)를 파싱하지 못하는 문제를 피하기 위해, `xmlns="..."` 형태만 사용합니다.【F:AasExcelToXml.Core/AasV3XmlWriter.cs†L1-L119】

정답 XML에서 추출한 골든 프로파일(`Templates/golden_profile_aas3.json`)을 기준으로 reference/semanticId 표현 방식과 요소 순서를 맞춥니다. Documentation Submodel은 AAS3 전용 프로파일(`Templates/golden_doc_profile_v3.json`)을 우선 적용합니다.【F:AasExcelToXml.Core/AasV3XmlWriter.cs†L1-L324】【F:AasExcelToXml.Core/Aas3ProfileLoader.cs†L1-L82】【F:AasExcelToXml.Core/DocumentationProfileLoader.cs†L1-L63】

추가로 AAS3의 `valueType`은 XSD 타입(`xs:string`, `xs:double` 등)으로 보정합니다. 엑셀의 Property type이 비어 있거나 불명확한 경우에도 기본값을 넣어 AASXPE 호환성을 높였습니다.【F:AasExcelToXml.Core/AasV3XmlWriter.cs†L170-L210】【F:AasExcelToXml.Core/ValueTypeMapper.cs†L1-L36】

## 정답 구조 자동 추출(GoldenInspector)

정답 XML에서 핵심 구조 규칙을 추출해 JSON/요약 문서로 저장하는 도구입니다. 변환 실행에는 필요하지 않으며, 개발/회귀 테스트 용도로만 사용합니다.

```bash
dotnet run --project tools/AasGoldenInspector -- \"정답_aas2.xml\" \"정답_aas3.xml\"
```

실행 결과는 레포 루트의 `artifacts/` 폴더에 저장됩니다.

- `artifacts/golden_schema_aas2.json`
- `artifacts/golden_schema_aas3.json`
- `artifacts/summary.md`

## Documentation 프로파일 추출(VDI2770 기반)

Documentation Submodel의 Document01/DocumentVersion01 구조를 정답 XML에서 자동 추출해 JSON으로 저장하는 도구입니다. 변환 실행에는 필요하지 않으며, 개발/테스트용입니다.

```bash
dotnet run --project tools/ExtractGoldenDocProfile -- \"정답_aas2.xml\" --version 2
dotnet run --project tools/ExtractGoldenDocProfile -- \"정답_aas3.xml\" --version 3
```

기본 출력 위치는 AAS2는 `Templates/golden_doc_profile_v2.json`, AAS3는 `Templates/golden_doc_profile_v3.json`입니다. 변환 시 해당 파일이 존재하면 Documentation 스켈레톤 생성에 반영됩니다. 파일이 없으면 VDI2770 기본 스펙 기반의 폴백 구조를 사용합니다.【F:AasExcelToXml.Core/DocumentationProfileLoader.cs†L1-L63】【F:AasExcelToXml.Core/DocumentationProfile.cs†L1-L214】

### Documentation 값 매핑/오버라이드

- DocumentId는 문서명/유형/파일명 기반의 해시로 **결정론적으로 생성**합니다.
- 필요하면 `artifacts/doc_overrides.json`에 오버라이드 규칙을 추가해 DocumentId/DocumentClassId 등을 지정할 수 있습니다.
- 오버라이드는 선택 사항이며, 파일이 없으면 자동 규칙만 사용됩니다.

```json
{
  "overrides": [
    {
      "matchNameContains": "Hanuri",
      "documentId": "1000000001",
      "documentClassId": "CATALOG",
      "documentClassName": "Catalog",
      "documentClassificationSystem": "VDI2770",
      "documentVersionId": "01",
      "language": "kr"
    }
  ]
}
```

오버라이드 파일은 개발 편의를 위한 선택 기능이며, 기본 변환 로직은 엑셀 입력만으로 동작합니다.【F:AasExcelToXml.Core/DocumentationOverrides.cs†L1-L107】【F:AasExcelToXml.Core/DocumentIdGenerator.cs†L1-L23】【F:AasExcelToXml.Core/DocumentationSkeletonBuilderV2.cs†L297-L470】

## AAS3 골든 프로파일 추출

AAS 3.0 정답 XML에서 reference 표현 방식/요소 순서를 추출해 JSON으로 저장하는 도구입니다.

```bash
dotnet run --project tools/ExtractGoldenAas3Profile -- \"정답_aas3.xml\"
```

기본 출력 위치는 `Templates/golden_profile_aas3.json`입니다. 출력된 프로파일은 AAS3 writer가 reference/semanticId/요소 순서를 맞추는 데 사용합니다.【F:tools/ExtractGoldenAas3Profile/Program.cs†L1-L199】【F:AasExcelToXml.Core/Aas3ProfileLoader.cs†L1-L82】

## 정답 스켈레톤 템플릿 자동 추출(권장)

정답 AAS2/AAS3 XML에서 Documentation 스켈레톤과 AAS3 프로파일을 **한 번에** 추출해 `Templates/` 폴더로 저장하는 도구입니다. 템플릿은 구조/필드 목록/순서를 고정하기 위한 것이며, 변환 시에는 엑셀 값만 주입됩니다.

```bash
dotnet run --project tools/ExtractGoldenTemplates -- --aas2 \"정답_aas2.xml\" --aas3 \"정답_aas3.xml\"
```

정답 XML이 아직 준비되지 않았다면 기본 폴백 스켈레톤을 출력할 수도 있습니다.

```bash
dotnet run --project tools/ExtractGoldenTemplates -- --fallback
```

`Templates/`가 갱신되면 바로 변환을 실행해 AASX Package Explorer에서 결과를 확인합니다.

## AAS3 구조 Diff 도구

정답 AAS3 XML과 현재 출력 XML을 비교해 누락된 요소/하위 노드/valueType 문제를 리포트합니다.

```bash
dotnet run --project tools/AasGoldenDiff -- --version 3 "정답_aas3.xml" "출력_aas3.xml"
```

리포트는 `artifacts/golden_diff_report_aas3.json`에 저장됩니다.【F:tools/AasGoldenDiff/Program.cs†L1-L82】【F:AasExcelToXml.Core/Aas3GoldenDiffAnalyzer.cs†L1-L113】

## 테스트 실행

```bash
dotnet test
```

- 정답 XML이 없으면 정답 비교 테스트는 자동으로 Skip 됩니다.
- `artifacts/golden_schema_*.json`이 없으면 구조 규칙 테스트는 Skip 됩니다.【F:AasExcelToXml.Tests/GoldenFileTests.cs†L1-L101】【F:AasExcelToXml.Tests/GoldenSchemaTests.cs†L1-L214】

## AASXPE 호환 포인트

- **환경 내부 참조는 local=true**  
  AASXPE는 `local=true`인 key를 기준으로 같은 환경 내 assets/submodels/submodelElements를 연결합니다.  
  assetRef, submodelRefs, relationshipElement, referenceElement에서 환경 내부 대상은 local=true로 기록합니다.【F:AasExcelToXml.Core/AasV2XmlWriter.cs†L1-L245】
- **submodelRefs/submodelRef 구조 사용**  
  Shell의 submodel 참조는 V2 샘플과 동일하게 `submodelRefs` → `submodelRef` → `keys` 구조를 사용합니다.  
  `submodels/reference` 구조는 AASXPE에서 인식되지 않습니다.【F:AasExcelToXml.Core/AasV2XmlWriter.cs†L1-L120】
- **description(langString@lang) 사용**  
  V2 스타일의 description을 사용하여 AASXPE가 텍스트를 안정적으로 표시하도록 합니다.【F:AasExcelToXml.Core/AasV2XmlWriter.cs†L1-L220】

## 경고 리포트(warnings.txt)

변환이 끝나면 출력 폴더에 `warnings.txt`가 생성됩니다. GUI/CLI에서도 경고 건수를 요약해서 알려줍니다. 다음과 같은 입력 오류를 요약합니다.

- 깨진 참조(Entity): Entity의 Reference 대상 AAS가 실제 목록에 없을 때
- 깨진 참조(Relationship): Relationship의 first/second가 실제 요소 idShort에 없을 때
- 타입 불일치: PropType이 Entity인데 값이 Relationship 패턴인 경우
- 중복 idShort: 같은 Submodel/Collection 안에서 idShort가 중복된 경우
- AAS3 구조 검증: semanticId/reference/keys/qualifiers/category 기본 구조가 비어 있거나 잘못된 경우

경고가 뜨는 경우 엑셀의 AAS 이름 철자, 요소 이름, 관계 대상 표기를 다시 확인하세요. 정답 파일 유무와 무관하게 변환 자체는 계속 진행됩니다.【F:AasExcelToXml.Core/SpecDiagnostics.cs†L1-L58】【F:AasExcelToXml.Core/Converter.cs†L1-L30】

## 코드 정리 메모

- Documentation 출력이 정답과 달라지는 원인이 되었던 `qualifiers` 래퍼/`valueType` 문자열 기반 placeholder를 제거하고, JSON 프로파일 기반으로 구조를 재구성했습니다. 이는 정답 XML을 파싱해 스켈레톤을 생성하는 구조로 전환하기 위한 정리입니다.【F:AasExcelToXml.Core/DocumentationProfile.cs†L1-L214】【F:AasExcelToXml.Core/DocumentationSkeletonBuilderV2.cs†L1-L281】

## 출력 파일 위치 안내

- GUI: 저장 위치 선택 대화상자에서 지정한 경로에 저장됩니다.
- CLI(인자 모드): 지정한 출력 경로에 저장됩니다. 출력 경로가 없으면 입력 파일과 같은 폴더에 `.aas2.xml` 또는 `.aas3.xml`로 저장됩니다.
- CLI(대화형 모드): 사용자가 선택한 저장 위치에 저장됩니다.【F:AasExcelToXml.Cli/Program.cs†L1-L309】【F:AasExcelToXml.Gui/MainForm.cs†L1-L247】

## 자주 발생하는 오류 및 해결 방법

1. **시트명이 다릅니다**  
   - 기본 시트명은 `사양시트`입니다. 다른 시트명일 경우 오류 메시지에 표시되는 목록을 확인하세요.【F:AasExcelToXml.Core/ExcelSpecReader.cs†L17-L43】

2. **헤더명이 다릅니다**  
   - 헤더 alias를 지원하므로 표에 정의된 대표 헤더 중 하나로 맞추면 됩니다.【F:AasExcelToXml.Core/ExcelSpecReader.cs†L125-L204】

3. **Sample 파일이 출력 폴더에 복사되지 않습니다**  
   - `AasExcelToXml.Cli/Sample/**` 파일이 빌드 결과로 복사되도록 설정되어 있습니다. 빌드 산출물 폴더에 `Sample`이 존재하는지 확인하세요.【F:AasExcelToXml.Cli/AasExcelToXml.Cli.csproj†L11-L15】
