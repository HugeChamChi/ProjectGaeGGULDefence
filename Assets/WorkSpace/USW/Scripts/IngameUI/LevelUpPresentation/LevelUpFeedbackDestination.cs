/// <summary>획득 연출의 목적지. 조합 카드는 유닛과 족장 버튼을 함께 지정할 수 있다.</summary>
[System.Flags]
public enum LevelUpFeedbackDestination
{
    None = 0,
    Units = 1,
    ChiefSkill = 2
}
