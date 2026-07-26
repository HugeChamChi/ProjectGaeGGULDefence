/// <summary>등급(Tier)에 따라 달라질 수 있는 int 값 공급자. SelectableReference로 고정값/등급별값 중 선택한다.</summary>
public interface IScaledInt
{
    int Get(Tier tier);
}
