// 유닛 부가효과 인터페이스 - 각 유닛의 특수 효과를 분리
public interface IUnitEffect
{
    void ApplyEffect(CurrencyManager currencyManager);
    void RemoveEffect(CurrencyManager currencyManager);
}