namespace GaeGGUL.Tutorial
{
    /// <summary>Ordered lessons in the first in-game tutorial.</summary>
    public enum IngameTutorialStage
    {
        FirstSummon = 1, BossEntrance, ThreeSummons, Merge, LevelUp,
        OpenUpgrade, UpgradeSlots, ChiefSkill, TotemChoice, OpenInventory,
        PlaceTotem, MoveTotem, RotateTotem,
        TimeLimit, ObserveCombat, ExperienceGauge,
        /// <summary>≡ 메뉴 안내(딜 기여도·선택지 보관함·설정). 회전 수업 뒤, 마지막 전투 직전에 둔다.</summary>
        OptionsMenu
    }
}
