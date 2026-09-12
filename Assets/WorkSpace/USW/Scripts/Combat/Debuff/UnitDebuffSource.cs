/// <summary>런타임 유닛 생성 없이 데이터와 등급으로 조회하는 UI/도구 어댑터.</summary>
public sealed class UnitDebuffSource : IDebuffSource
{
    private readonly UnitData _data;
    private readonly Tier _tier;
    /// <summary>조회할 데이터와 등급을 고정한다.</summary>
    public UnitDebuffSource(UnitData data, Tier tier) { _data = data; _tier = tier; }
    /// <inheritdoc />
    public bool TryGetDebuffBinding(out DebuffBinding binding)
    {
        binding = _data != null ? _data.DebuffBindings.Get(_tier) : default;
        return binding.IsConfigured;
    }
}
