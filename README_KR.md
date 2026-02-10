# AasExcelToXml 한국어 개발 가이드

## 1) 전체 파이프라인 개요 (엑셀 → XML)
1. `ExcelSpecReader`가 엑셀/시트에서 행(`SpecRow`)을 읽습니다.
2. `SpecGrouper`가 행을 AAS/서브모델/엘리먼트로 그룹핑하고 참조/관계를 정규화합니다.
3. `Converter`가 옵션(버전, 카테고리 상수, 경고 출력)을 적용해 Writer를 호출합니다.
4. `AasV2XmlWriter` 또는 `AasV3XmlWriter`가 실제 AAS XML 구조를 생성합니다.
5. 경고는 출력 폴더의 `warnings.txt`에 저장됩니다.

---

## 2) Relationship idShort 규칙 (중요)
- **원칙:** Relationship의 `idShort`는 **엑셀 D열(`Property_Eng`) 값 그대로** 사용해야 합니다.
- 금지 사항:
  - `first/second` 기반 이름 합성 (`A_to_B`) 금지
  - `Rel_` 접두사 제거/치환 금지
  - 의미가 바뀌는 임의 문자열 변환 금지
- 허용 사항:
  - idShort 안전성 보장을 위한 최소 정규화(공백/특수문자 → `_`)

### 적용 위치
- `AasExcelToXml.Core/SpecGrouper.cs`
  - `ParseElement(...)`
  - `ResolveRelationshipIdShort(...)`

---

## 3) setting.xml(설정) 구조와 alias 확장
WPF 설정은 `SettingsService`를 통해 로드/저장됩니다. 일반적으로 사용자 프로필(AppData) 위치를 사용합니다.

대표 설정 항목:
- 마지막 출력 폴더 기억 여부
- UI 언어
- 시트 선택 값
- category constant 채우기 옵션

alias/매핑 규칙을 확장할 때는 다음 순서로 진행하세요.
1. 설정 모델(`AppSettings`)에 필드 추가
2. `SettingsService` 직렬화/역직렬화 반영
3. `MainWindow`에서 옵션을 `ConvertOptions`로 전달
4. `Converter`/`SpecGrouper`에서 실제 동작 반영

---

## 4) category 옵션 / 시트 선택 / 경고 로그
- **시트 선택:** `MainWindow`의 시트 콤보박스 선택값이 `Converter.Convert(..., sheetName)`로 전달됩니다.
- **category constant:** 입력 category가 비어 있을 때만 상수값으로 채웁니다.
  - 구현 위치: `Converter.ApplyCategorySettings(...)`
- **경고 로그 위치:** 기본적으로 출력 XML과 같은 폴더의 `warnings.txt`.

---

## 5) 자주 수정할 파일 TOP 5 + 영향도
1. `AasExcelToXml.Core/SpecGrouper.cs`
   - 영향: 엘리먼트 분류, idShort 규칙, Relationship/Entity 해석 전체.
2. `AasExcelToXml.Core/Converter.cs`
   - 영향: 파이프라인 순서, 옵션 반영, warnings 파일 정책.
3. `AasExcelToXml.Core/AasV3XmlWriter.cs`
   - 영향: AAS 3.0 XML 태그 구조/순서.
4. `AasExcelToXml.Core/AasV2XmlWriter.cs`
   - 영향: AAS 2.0 XML 태그 구조/호환성.
5. `AasExcelToXml.Wpf/MainWindow.xaml.cs`
   - 영향: UI 입력값이 Core 옵션으로 전달되는 경로.

---

## 6) 회귀 체크리스트
- Relationship `idShort`에 `_to_`가 생성되지 않는가?
- `Rel_`, `Ent_` 접두사가 입력 의도대로 유지되는가?
- 엑셀에 없는 데이터를 임의 생성하지 않는가?
- 경고는 `warnings.txt`로 남고 변환은 가능한 범위에서 진행되는가?
