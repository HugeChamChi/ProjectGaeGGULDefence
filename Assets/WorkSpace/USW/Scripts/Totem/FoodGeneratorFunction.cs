using System;

/// <summary>
/// 끝없는 수확류 토템 전용 — 셀 버프가 아니라 토템 스스로 일정 주기마다 식량을 생성한다.
/// TotemFoodGenerator가 totemData.functions에서 이 항목을 찾아 interval/amount를 읽어 타이머를 돌린다.
/// 셀 단위로 적용되는 게 아니므로 Apply()는 아무 일도 하지 않는다.
/// </summary>
[Serializable]
public class FoodGeneratorFunction : ITotemFunction
{
    public float interval = 10f;
    public float amount = 30f;

    public void Apply(TotemBase totem, GridCell cell, TotemBuffManager buffManager) { }
}
