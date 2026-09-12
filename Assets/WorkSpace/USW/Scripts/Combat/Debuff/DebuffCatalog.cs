using System;
using System.Collections.Generic;

/// <summary>불변 정의 조회. 새 정의 집합은 검증 성공 후 원자적으로 교체한다.</summary>
public sealed class DebuffCatalog
{
    private Dictionary<int, DebuffDefinition> _definitions = new Dictionary<int, DebuffDefinition>();
    /// <summary>로컬 SO를 초기 정의로 로딩한다.</summary>
    public DebuffCatalog(DebuffSettings settings) { Replace(settings.CreateDefinitions()); }
    /// <summary>정의가 있으면 반환한다.</summary>
    public bool TryGet(int id, out DebuffDefinition definition) => _definitions.TryGetValue(id, out definition);
    /// <summary>유효 정의 집합 생성. 초기 버전은 종류/중첩 그룹별 하나의 정의만 허용한다.</summary>
    public static Dictionary<int, DebuffDefinition> Validate(IEnumerable<DebuffDefinition> definitions)
    {
        var result = new Dictionary<int, DebuffDefinition>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var groups = new HashSet<string>(StringComparer.Ordinal);
        var kinds = new HashSet<DebuffKind>();
        foreach (var definition in definitions)
        {
            if (definition == null || result.ContainsKey(definition.Id) || !keys.Add(definition.Key) ||
                !groups.Add(definition.StackGroup) || !kinds.Add(definition.Kind))
                throw new ArgumentException("Duplicate debuff ID/key/group/kind.");
            result.Add(definition.Id, definition);
        }
        if (result.Count == 0) throw new ArgumentException("Debuff catalog is empty.");
        return result;
    }
    /// <summary>런 진입 전에만 호출한다. 기존 활성 효과는 불변 정의를 유지한다.</summary>
    public void Replace(IEnumerable<DebuffDefinition> definitions) => _definitions = Validate(definitions);
}
