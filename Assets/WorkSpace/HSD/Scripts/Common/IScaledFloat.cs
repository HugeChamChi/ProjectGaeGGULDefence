/// <summary>등급(Tier)에 따라 달라질 수 있는 float 값 공급자. SelectableReference로 고정값/등급별값 중 선택한다.</summary>
public interface IScaledFloat
{
    float Get(Tier tier);
}
