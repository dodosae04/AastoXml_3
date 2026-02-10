using System.Text.RegularExpressions;

namespace AasExcelToXml.Core;

// [역할] 엑셀 행(SpecRow) 목록을 AAS 변환용 중간 모델(AasEnvironmentSpec)로 그룹핑/정규화한다.
// [입력] ExcelSpecReader에서 읽은 행 데이터(List<SpecRow>).
// [출력] AAS/서브모델/엘리먼트 트리와 경고 정보를 담은 AasEnvironmentSpec + SpecDiagnostics.
// [수정 포인트] Relationship idShort 규칙은 ParseElement + ResolveRelationshipIdShort에서 제어한다.
public static class SpecGrouper
{

    /// <summary>
    /// 엑셀 행을 AAS 구조로 묶고, 누락 참조/중복 idShort 같은 진단 정보를 함께 생성한다.
    /// </summary>
    /// <param name="rows">엑셀에서 읽어온 원시 행 목록.</param>
    /// <param name="diagnostics">스킵/참조 누락/중복 관련 경고를 누적하는 진단 객체.</param>
    /// <returns>XML Writer가 그대로 소비할 수 있는 환경 스펙.</returns>
    /// <remarks>
    /// 이 메서드 흐름을 수정하면 전체 출력 구조(AAS/서브모델/엘리먼트 배치)가 바뀐다.
    /// 특히 Relationship 이름 규칙은 ParseElement에서 결정된 값이 최종 XML까지 전달된다.
    /// </remarks>
    public static AasEnvironmentSpec BuildEnvironmentSpec(List<SpecRow> rows, out SpecDiagnostics diagnostics)
    {
        diagnostics = new SpecDiagnostics();

        // 엑셀에서 AAS/Submodel/Collection 칸이 비는 경우가 흔하므로 "이전 값 이어받기" 규칙을 적용
        string currentAas = string.Empty;
        string currentSm = string.Empty;
        string currentCol = string.Empty;

        var normalized = new List<SpecRow>();
        foreach (var r in rows)
        {
            var normalizedAas = string.IsNullOrWhiteSpace(r.Aas) ? string.Empty : r.Aas.Trim();
            if (!string.IsNullOrWhiteSpace(normalizedAas) && !string.Equals(currentAas, normalizedAas, StringComparison.Ordinal))
            {
                currentAas = normalizedAas;
                // AAS가 바뀌면 Submodel/Collection을 리셋
                currentSm = string.Empty;
                currentCol = string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(r.Submodel) && !string.Equals(currentSm, r.Submodel, StringComparison.Ordinal))
            {
                currentSm = r.Submodel;
                // Submodel이 바뀌면 Collection을 리셋
                currentCol = string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(r.Collection))
            {
                currentCol = r.Collection;
            }

            normalized.Add(r with { Aas = currentAas, Submodel = currentSm, Collection = currentCol });
        }

        var assetBuilders = new List<AasBuilder>();
        foreach (var row in normalized)
        {
            if (string.IsNullOrWhiteSpace(row.Aas) || string.IsNullOrWhiteSpace(row.Submodel))
            {
                continue;
            }

            var elementName = string.IsNullOrWhiteSpace(row.PropEng) ? row.PropKor : row.PropEng;
            if (string.IsNullOrWhiteSpace(elementName))
            {
                continue;
            }

            var aasBuilder = assetBuilders.FirstOrDefault(a => string.Equals(a.Name, row.Aas, StringComparison.Ordinal));
            if (aasBuilder is null)
            {
                aasBuilder = new AasBuilder(row.Aas);
                assetBuilders.Add(aasBuilder);
            }

            var normalizedSubmodel = NormalizeSubmodelName(row.Submodel);
            var submodelBuilder = aasBuilder.Submodels.FirstOrDefault(s => string.Equals(s.Name, normalizedSubmodel, StringComparison.Ordinal));
            if (submodelBuilder is null)
            {
                submodelBuilder = new SubmodelBuilder(normalizedSubmodel, row.Submodel);
                aasBuilder.Submodels.Add(submodelBuilder);
            }

            var element = ParseElement(row, diagnostics);
            submodelBuilder.Elements.Add(element);
        }

        var aasNames = new HashSet<string>(assetBuilders.Select(a => NormalizeAssetIdShort(a.Name)), StringComparer.Ordinal);
        foreach (var aasBuilder in assetBuilders)
        {
            foreach (var submodelBuilder in aasBuilder.Submodels)
            {
                var uniqueElements = EnsureUniqueIdShorts(submodelBuilder.Elements, diagnostics, aasBuilder.Name, submodelBuilder.Name);
                submodelBuilder.Elements.Clear();
                submodelBuilder.Elements.AddRange(uniqueElements);

                var elementIdShorts = new HashSet<string>(submodelBuilder.Elements.Select(e => e.IdShort), StringComparer.Ordinal);

                var entityIdShorts = submodelBuilder.Elements
                    .Where(e => e.Kind == ElementKind.Entity)
                    .Select(e => e.IdShort)
                    .ToList();

                for (var i = 0; i < submodelBuilder.Elements.Count; i++)
                {
                    var element = submodelBuilder.Elements[i];
                    if (element.Kind == ElementKind.Entity && !string.IsNullOrWhiteSpace(element.ReferenceTarget))
                    {
                        var normalizedTarget = NormalizeReferenceValue(element.ReferenceTarget);
                        if (!string.IsNullOrWhiteSpace(normalizedTarget) && !IsIri(normalizedTarget))
                        {
                            var resolved = ResolveCanonicalName(aasNames, normalizedTarget);
                            if (!string.IsNullOrWhiteSpace(resolved))
                            {
                                element = element with { ReferenceTarget = resolved };
                                submodelBuilder.Elements[i] = element;
                            }
                            else
                            {
                                diagnostics.MissingEntityReferences.Add($"AAS={aasBuilder.Name}, Submodel={submodelBuilder.Name}, Entity={element.IdShort}, Reference={normalizedTarget}");
                            }
                        }
                    }

                    if (element.Kind == ElementKind.Relationship && element.Relationship is not null)
                    {
                        var first = NormalizeReferenceValue(element.Relationship.First);
                        var second = NormalizeReferenceValue(element.Relationship.Second);
                        var correctedFirst = first;
                        var correctedSecond = second;
                        if (!string.IsNullOrWhiteSpace(first))
                        {
                            var resolved = ResolveCanonicalName(entityIdShorts, first);
                            if (!string.IsNullOrWhiteSpace(resolved))
                            {
                                correctedFirst = resolved;
                            }
                            else
                            {
                                diagnostics.MissingRelationshipReferences.Add($"AAS={aasBuilder.Name}, Submodel={submodelBuilder.Name}, Relationship={element.IdShort}, First={first}");
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(second))
                        {
                            var resolved = ResolveCanonicalName(entityIdShorts, second);
                            if (!string.IsNullOrWhiteSpace(resolved))
                            {
                                correctedSecond = resolved;
                            }
                            else
                            {
                                diagnostics.MissingRelationshipReferences.Add($"AAS={aasBuilder.Name}, Submodel={submodelBuilder.Name}, Relationship={element.IdShort}, Second={second}");
                            }
                        }

                        if (correctedFirst != first || correctedSecond != second)
                        {
                            element = element with { Relationship = new RelationshipSpec(correctedFirst ?? string.Empty, correctedSecond ?? string.Empty) };
                            submodelBuilder.Elements[i] = element;
                        }
                    }
                }
            }
        }

        ResolveReferenceElements(assetBuilders, diagnostics);

        var assets = assetBuilders.Select(a =>
            new AasSpec(
                Name: a.Name,
                IdShort: NormalizeAssetIdShort(a.Name),
                Submodels: a.Submodels.Select(sm =>
                    new SubmodelSpec(
                        Name: sm.DisplayName,
                        IdShort: NormalizeIdShort(sm.Name),
                        Elements: sm.Elements,
                        Category: sm.Elements.Select(e => e.Category).FirstOrDefault(c => !string.IsNullOrWhiteSpace(c))
                    )).ToList()
            )).ToList();

        return new AasEnvironmentSpec(assets);
    }

    /// <summary>
    /// 한 행을 Property/Entity/Relationship/ReferenceElement 등 실제 요소 타입으로 판정한다.
    /// </summary>
    /// <param name="row">현재 엑셀 행.</param>
    /// <param name="diagnostics">경고 기록 객체.</param>
    /// <returns>분류/정규화가 적용된 단일 ElementSpec.</returns>
    /// <remarks>
    /// 수정 포인트:
    /// - Relationship idShort는 반드시 엑셀 D열(Property_Eng) 기반 fallback(idShort)을 유지한다.
    /// - PropType=Entity + [first]/[second] 패턴으로 Relationship로 승격되는 경우도 동일 규칙을 적용한다.
    /// </remarks>
    private static ElementSpec ParseElement(SpecRow row, SpecDiagnostics diagnostics)
    {
        var displayName = string.IsNullOrWhiteSpace(row.PropKor) ? row.PropEng : row.PropKor;
        var baseName = string.IsNullOrWhiteSpace(row.PropEng) ? row.PropKor : row.PropEng;
        var idShortBase = baseName;
        var idShort = NormalizeElementIdShort(row, idShortBase);
        if (IsDocumentationInputRow(row.Submodel, baseName))
        {
            return new ElementSpec(
                Collection: NormalizeCollectionIdShort(row.Collection),
                IdShort: idShort,
                DisplayNameKo: displayName,
                Kind: ElementKind.DocumentationInput,
                ValueType: "string",
                Value: row.Value,
                Uom: row.Uom,
                ReferenceTarget: null,
                Relationship: null,
                Category: row.Category
            );
        }

        // 규칙 4) Property_Eng가 "[Robot_body Reference] Rotation_angle_of_body" 형태면 ReferenceElement로 처리
        var referenceMatch = Regex.Match(row.PropEng ?? string.Empty, @"^\[(?<target>.+?)\s*Reference\]\s*(?<name>.+)$", RegexOptions.IgnoreCase);
        if (referenceMatch.Success)
        {
            var target = referenceMatch.Groups["target"].Value.Trim();
            var name = referenceMatch.Groups["name"].Value.Trim();
            return new ElementSpec(
                Collection: NormalizeCollectionIdShort(row.Collection),
                IdShort: NormalizeIdShort(name),
                DisplayNameKo: displayName,
                Kind: ElementKind.ReferenceElement,
                ValueType: "string",
                Value: row.Value,
                Uom: row.Uom,
                ReferenceTarget: null,
                Relationship: null,
                ReferenceTargetAasIdShort: NormalizeAssetIdShort(target),
                ReferenceTargetSubmodelHint: NormalizeIdShort(row.Submodel),
                Category: row.Category
            );
        }

        // 규칙 2) PropType이 Entity인데 [first]/[second] 패턴이 있으면 Relationship로 분류한다.
        if (string.Equals(row.PropType, "Entity", StringComparison.OrdinalIgnoreCase))
        {
            var relationship = ParseRelationship(row.Value);
            if (relationship is not null)
            {
                var relationshipIdShort = ResolveRelationshipIdShort(relationship, idShort);
                return new ElementSpec(
                    Collection: NormalizeCollectionIdShort(row.Collection),
                    IdShort: relationshipIdShort,
                    DisplayNameKo: displayName,
                    Kind: ElementKind.Relationship,
                    ValueType: "string",
                    Value: row.Value,
                    Uom: row.Uom,
                    ReferenceTarget: null,
                    Relationship: relationship,
                    Category: row.Category
                );
            }

            var entityTarget = ParseTaggedValue(row.Value, "Reference") ?? row.Value;
            return new ElementSpec(
                Collection: NormalizeCollectionIdShort(row.Collection),
                IdShort: idShort,
                DisplayNameKo: displayName,
                Kind: ElementKind.Entity,
                ValueType: "string",
                Value: row.Value,
                Uom: row.Uom,
                ReferenceTarget: NormalizeReferenceValue(entityTarget),
                Relationship: null,
                Category: row.Category
            );
        }

        // 규칙 3) PropType이 Relationship이면 Value 안의 [first]/[second] 정보를 파싱
        if (string.Equals(row.PropType, "Relationship", StringComparison.OrdinalIgnoreCase))
        {
            var relationship = ParseRelationship(row.Value);
            var relationshipIdShort = relationship is null
                ? idShort
                : ResolveRelationshipIdShort(relationship, idShort);
            return new ElementSpec(
                Collection: NormalizeCollectionIdShort(row.Collection),
                IdShort: relationshipIdShort,
                DisplayNameKo: displayName,
                Kind: ElementKind.Relationship,
                ValueType: "string",
                Value: row.Value,
                Uom: row.Uom,
                ReferenceTarget: null,
                Relationship: relationship,
                Category: row.Category
            );
        }

        // 규칙 1) 일반 Property는 타입에 따라 valueType을 결정
        return new ElementSpec(
            Collection: NormalizeCollectionIdShort(row.Collection),
            IdShort: idShort,
            DisplayNameKo: displayName,
            Kind: ElementKind.Property,
            ValueType: NormalizeValueType(row.PropType),
            Value: row.Value,
            Uom: row.Uom,
            ReferenceTarget: null,
            Relationship: null,
            Category: row.Category
        );
    }

    private static bool HasRelationshipHint(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var normalized = name.Trim().ToLowerInvariant();
        return normalized.StartsWith("rel_", StringComparison.Ordinal)
            || normalized.StartsWith("rel-", StringComparison.Ordinal)
            || normalized.StartsWith("relationship", StringComparison.Ordinal)
            || normalized.StartsWith("relation", StringComparison.Ordinal);
    }

    private static string? ParseTaggedValue(string value, string tag)
    {
        var match = Regex.Match(value ?? string.Empty, $@"\[{tag}\]\s*(.+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static RelationshipSpec? ParseRelationship(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var first = NormalizeReferenceValue(ParseTaggedValue(value, "first"));
        var second = NormalizeReferenceValue(ParseTaggedValue(value, "second"));
        if (string.IsNullOrWhiteSpace(first) && string.IsNullOrWhiteSpace(second))
        {
            return null;
        }

        return new RelationshipSpec(first ?? string.Empty, second ?? string.Empty);
    }

    private static string? NormalizeReferenceValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (IsIri(trimmed))
        {
            return trimmed;
        }

        return NormalizeIdShort(trimmed);
    }

    /// <summary>
    /// Relationship idShort를 결정한다.
    /// </summary>
    /// <param name="relationship">파싱된 first/second 관계 정보(검증/참조 보정 용도).</param>
    /// <param name="fallback">엑셀 D열(Property_Eng)에서 생성한 기본 idShort.</param>
    /// <returns>항상 fallback을 반환한다.</returns>
    /// <remarks>
    /// 요구사항상 Relationship 이름은 엑셀 값 "그대로"를 사용해야 하므로,
    /// first/second 기반 합성(예: A_to_B)이나 접두사 변형은 절대 수행하지 않는다.
    /// 이름 안전성은 이미 fallback 생성 시 NormalizeElementIdShort가 처리한다.
    /// </remarks>
    private static string ResolveRelationshipIdShort(RelationshipSpec relationship, string fallback)
    {
        return fallback;
    }

    private static bool IsIri(string value)
    {
        return value.StartsWith("urn:", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeAssetIdShort(string raw)
    {
        return NormalizeIdShort(raw);
    }

    private static string NormalizeElementIdShort(SpecRow row, string raw)
    {
        if (string.Equals(row.PropType, "Entity", StringComparison.OrdinalIgnoreCase)
            || string.Equals(row.PropType, "Relationship", StringComparison.OrdinalIgnoreCase))
        {
            return NormalizeAssetIdShort(raw);
        }

        return NormalizeIdShort(raw);
    }

    private static string NormalizeValueType(string propType)
    {
        var type = propType?.Trim().ToLowerInvariant() ?? string.Empty;
        if (type.Contains("double") || type.Contains("float") || type.Contains("decimal"))
        {
            return "double";
        }

        if (type.Contains("int"))
        {
            return "int";
        }

        if (type.Contains("bool"))
        {
            return "boolean";
        }

        return "string";
    }

    // idShort는 공백/특수문자에 취약하므로 안전한 문자로 정규화 필요
    private static string NormalizeIdShort(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "Unnamed";
        }

        var trimmed = raw.Trim();
        var normalized = new string(trimmed.Select(ch =>
            char.IsLetterOrDigit(ch) ? ch : '_'
        ).ToArray());

        normalized = Regex.Replace(normalized, "_{2,}", "_").Trim('_');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalized = "Unnamed";
        }

        // idShort는 숫자로 시작하면 파서에서 문제가 발생할 수 있어 접두어를 붙인다
        if (char.IsDigit(normalized[0]))
        {
            normalized = "_" + normalized;
        }

        return normalized;
    }

    // Collection은 비어있을 때 "Unnamed"를 만들지 않도록 별도로 처리한다.
    private static string NormalizeCollectionIdShort(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        return raw.Trim();
    }

    private static string NormalizeSubmodelName(string name)
    {
        return NormalizeIdShort(name);
    }

    private sealed class AasBuilder
    {
        public AasBuilder(string name)
        {
            Name = name;
            Submodels = new List<SubmodelBuilder>();
        }

        public string Name { get; }
        public List<SubmodelBuilder> Submodels { get; }
    }

    private sealed class SubmodelBuilder
    {
        public SubmodelBuilder(string name, string displayName)
        {
            Name = name;
            DisplayName = displayName;
            Elements = new List<ElementSpec>();
        }

        public string Name { get; }
        public string DisplayName { get; }
        public List<ElementSpec> Elements { get; }
    }

    private static List<ElementSpec> EnsureUniqueIdShorts(List<ElementSpec> elements, SpecDiagnostics diagnostics, string aasName, string submodelName)
    {
        var counterByCollection = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
        var result = new List<ElementSpec>(elements.Count);
        foreach (var element in elements)
        {
            if (element.Kind == ElementKind.DocumentationInput)
            {
                result.Add(element);
                continue;
            }

            var idShort = element.IdShort;
            var collectionKey = element.Collection ?? string.Empty;
            if (!counterByCollection.TryGetValue(collectionKey, out var counter))
            {
                counter = new Dictionary<string, int>(StringComparer.Ordinal);
                counterByCollection[collectionKey] = counter;
            }

            if (counter.TryGetValue(idShort, out var count))
            {
                var newCount = count + 1;
                counter[idShort] = newCount;
                var suffix = $"_{newCount}";
                var updatedIdShort = idShort + suffix;
                var scope = string.IsNullOrWhiteSpace(collectionKey) ? "Direct" : collectionKey;
                result.Add(element with { IdShort = updatedIdShort });
            }
            else
            {
                counter[idShort] = 1;
                result.Add(element);
            }
        }

        return result;
    }

    private static string? ResolveCanonicalName(IEnumerable<string> candidates, string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var normalizedInput = NormalizeMatchKey(input);
        var exactMatches = candidates.Where(c => NormalizeMatchKey(c) == normalizedInput).ToList();
        if (exactMatches.Count == 1)
        {
            return exactMatches[0];
        }

        if (exactMatches.Count > 1)
        {
            return null;
        }

        var bestDistance = int.MaxValue;
        string? best = null;
        var bestCount = 0;
        foreach (var candidate in candidates)
        {
            var distance = LevenshteinDistance(normalizedInput, NormalizeMatchKey(candidate), 1);
            if (distance <= 1)
            {
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                    bestCount = 1;
                }
                else if (distance == bestDistance)
                {
                    bestCount++;
                }
            }
        }

        return bestCount == 1 ? best : null;
    }

    private static void ResolveReferenceElements(List<AasBuilder> assetBuilders, SpecDiagnostics diagnostics)
    {
        var aasByIdShort = assetBuilders.ToDictionary(a => NormalizeAssetIdShort(a.Name), a => a, StringComparer.Ordinal);
        foreach (var aas in assetBuilders)
        {
            var aasIdShort = NormalizeAssetIdShort(aas.Name);
            foreach (var submodel in aas.Submodels)
            {
                var submodelIdShort = NormalizeIdShort(submodel.Name);
                for (var i = 0; i < submodel.Elements.Count; i++)
                {
                    var element = submodel.Elements[i];
                    if (element.Kind != ElementKind.ReferenceElement || string.IsNullOrWhiteSpace(element.ReferenceTargetAasIdShort))
                    {
                        continue;
                    }

                    var targetAasIdShort = element.ReferenceTargetAasIdShort!;
                    if (!aasByIdShort.TryGetValue(targetAasIdShort, out var targetAas))
                    {
                        diagnostics.MissingRelationshipReferences.Add($"ReferenceElement 대상 AAS 미확인: AAS={aasIdShort}, Submodel={submodelIdShort}, TargetAAS={targetAasIdShort}, Property={element.IdShort}");
                        continue;
                    }

                    var hint = element.ReferenceTargetSubmodelHint;
                    var preferred = !string.IsNullOrWhiteSpace(hint)
                        ? targetAas.Submodels.FirstOrDefault(sm => string.Equals(NormalizeIdShort(sm.Name), hint, StringComparison.Ordinal)
                            && ContainsProperty(sm, element.IdShort))
                        : null;

                    var targetSubmodel = preferred ?? targetAas.Submodels.FirstOrDefault(sm => ContainsProperty(sm, element.IdShort));
                    if (targetSubmodel is null)
                    {
                        diagnostics.MissingRelationshipReferences.Add($"ReferenceElement 대상 미확인: AAS={aasIdShort}, Submodel={submodelIdShort}, TargetAAS={targetAasIdShort}, Property={element.IdShort}");
                        continue;
                    }

                    var resolved = new ResolvedReference(
                        targetAasIdShort,
                        NormalizeIdShort(targetSubmodel.Name),
                        element.IdShort,
                        "Property");
                    submodel.Elements[i] = element with { ResolvedReference = resolved };
                }
            }
        }
    }

    private static bool ContainsProperty(SubmodelBuilder submodel, string propertyIdShort)
    {
        return submodel.Elements.Any(e => e.Kind == ElementKind.Property
            && string.Equals(e.IdShort, propertyIdShort, StringComparison.Ordinal));
    }

    private static bool IsDocumentationInputRow(string submodelName, string? propName)
    {
        var normalizedSubmodel = NormalizeSubmodelName(submodelName);
        if (!string.Equals(normalizedSubmodel, "Documentation", StringComparison.Ordinal))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(propName))
        {
            return false;
        }

        var key = NormalizeMatchKey(propName);
        return key.Contains("documentname")
            || key.Contains("documenttitle")
            || key == "title"
            || key.Contains("documenttype")
            || key.Contains("documentclass")
            || key.Contains("doctype")
            || key.Contains("documentfile")
            || key.Contains("filepath")
            || key.Contains("digitalfile");
    }

    private static string NormalizeMatchKey(string value)
    {
        var trimmed = value.Trim();
        var filtered = new string(trimmed.Where(char.IsLetterOrDigit).ToArray());
        return filtered.ToLowerInvariant();
    }

    private static int LevenshteinDistance(string source, string target, int maxDistance)
    {
        if (Math.Abs(source.Length - target.Length) > maxDistance)
        {
            return maxDistance + 1;
        }

        var costs = new int[target.Length + 1];
        for (var j = 0; j <= target.Length; j++)
        {
            costs[j] = j;
        }

        for (var i = 1; i <= source.Length; i++)
        {
            costs[0] = i;
            var prevCost = i - 1;
            var minInRow = costs[0];
            for (var j = 1; j <= target.Length; j++)
            {
                var currentCost = costs[j];
                var substitutionCost = source[i - 1] == target[j - 1] ? 0 : 1;
                costs[j] = Math.Min(Math.Min(costs[j] + 1, costs[j - 1] + 1), prevCost + substitutionCost);
                prevCost = currentCost;
                if (costs[j] < minInRow)
                {
                    minInRow = costs[j];
                }
            }

            if (minInRow > maxDistance)
            {
                return maxDistance + 1;
            }
        }

        return costs[target.Length];
    }
}
