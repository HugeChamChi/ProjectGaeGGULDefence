using UnityEngine;

/// <summary>
/// 레벨업 선택지 하나의 데이터 (시트 choose_id 3000~3059 대응)
///
/// grade     : Normal/Rare/Epic/Legend (Tier 재사용)
/// spawnRate : 등장 가중치 (Normal 0.020, Rare 0.015, Legend 0.010)
/// applicableTribes : 해당 부족이 그리드에 존재할 때만 선택지에 등장 (비어있으면 항상 등장)
/// primaryEffect/primaryValue   : 주 스탯 효과
/// secondaryEffect/secondaryValue : 부 스탯 효과 (다중강화 전용)
/// specialEffect/specialValue   : 특수 로직 효과
/// </summary>
[CreateAssetMenu(fileName = "LevelUpData", menuName = "Game/LevelUpData")]
public class LevelUpData : ScriptableObject
{
    [Header("Identity")]
    public int              chooseId;
    public string           chooseName;
    public Tier             tier;
    public float            spawnRate;
    [TextArea] public string description;

    [Header("Tribe Filter (비어있으면 항상 등장)")]
    public UnitTribe[]      applicableTribes;

    [Header("Primary Effect")]
    public LevelUpEffectType primaryEffect;
    public float             primaryValue;   // 양수=증가, 음수=감소

    [Header("Secondary Effect (다중강화 전용)")]
    public LevelUpEffectType secondaryEffect;
    public float             secondaryValue;

    [Header("Special Effect")]
    public LevelUpSpecialEffect specialEffect;
    public float                specialValue;

    [Header("Display")]
    public Sprite   icon;
    public Sprite[] animationFrames;
    public float    frameRate = 12f;
}
