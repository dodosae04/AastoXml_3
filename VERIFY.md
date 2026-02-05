# 검증 절차

아래 단계는 개발/테스트 용도의 수동 검증 절차입니다. 정답 XML이 없어도 변환은 가능하지만, 정답과의 구조 비교를 위해 필요합니다.

## 1) Documentation 프로파일 추출

```bash
dotnet run --project tools/ExtractGoldenDocProfile -- "정답_aas2.xml" --version 2
dotnet run --project tools/ExtractGoldenDocProfile -- "정답_aas3.xml" --version 3
```

- 결과: `artifacts/golden_doc_profile_v2.json`, `artifacts/golden_doc_profile_v3.json`
- 파일이 존재하면 변환 시 Documentation 스켈레톤에 자동 반영됩니다.

## 2) AAS3 골든 프로파일 추출

```bash
dotnet run --project tools/ExtractGoldenAas3Profile -- "정답_aas3.xml"
```

- 결과: `artifacts/golden_profile_aas3.json`

## 3) 엑셀 → AAS2 변환

```bash
dotnet run --project AasExcelToXml.Cli -- --version 2 "입력.xlsx" "출력.aas2.xml"
```

## 4) 정답 비교 리포트 생성

```bash
dotnet run --project tools/AasGoldenDiff -- "정답_aas2.xml" "출력.aas2.xml"
```

- 결과: `artifacts/golden_diff_report.json`

## 5) AASX Package Explorer 확인 포인트

- Documentation/Document01의 필드 순서와 타입이 정답과 동일한지 확인
- DocumentVersion01 내부에 Language01, Title/Resumen/KeyWords, DigitalFile 등이 빠짐없이 있는지 확인
- valueType이 `<aas:valueType />` 형태로 비어 있는지 확인
- qualifier가 `<aas:qualifier />` 단일 태그로 존재하는지 확인
- semanticId/valueId 키가 VDI2770 URI/IRDI로 채워져 있는지 확인
