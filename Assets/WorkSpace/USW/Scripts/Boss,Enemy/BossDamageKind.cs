/// <summary>보스가 받은 피해의 종류. 데미지 표시(색상) 구분용이며 피해 계산에는 영향이 없다.</summary>
public enum BossDamageKind
{
    Normal = 0,
    Critical = 1,
    Burn = 2,
    /// <summary>족장 집결 사격 적중. 숫자를 따로 모아 고정 개수로 나눠 띄운다 (L40).</summary>
    Rally = 3,
}
