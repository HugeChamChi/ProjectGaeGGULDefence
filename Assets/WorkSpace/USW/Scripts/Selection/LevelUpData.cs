using UnityEngine;
using UnityEngine.Serialization;

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
public class LevelUpData : ScriptableObject
{
    /// <summary>1 = 조합형 정의. 0은 Editor 이관 입력 또는 역사 검사의 임시 fixture 전용이다.</summary>
    [HideInInspector] public int EffectSchemaVersion;
    /// <summary>새 저작 형식. 버전1에서만 읽고 기존 필드와 동시 적용하지 않는다.</summary>
    public SelectionCardDefinition Composition = new();

    [Header("Identity")]
    public int              chooseId;
    public string           chooseName;
    [FormerlySerializedAs("grade")]
    public Tier             tier = Tier.Rare;   // 레벨업 카드 등급: 레어/에픽/레전더리 (일반 없음)
    public float            spawnRate;
    [TextArea] public string description;
    [TextArea] public string simpleDescription;

    [Header("Tribe Filter (비어있으면 항상 등장)")]
    public UnitTribe[]      applicableTribes;

    [Header("Primary Effect")]
    [HideInInspector] public LevelUpEffectType primaryEffect;
    [HideInInspector] public float             primaryValue;

    [Header("Secondary Effect (다중강화 전용)")]
    [HideInInspector] public LevelUpEffectType secondaryEffect;
    [HideInInspector] public float             secondaryValue;

    [Header("Special Effect")]
    [HideInInspector] public LevelUpSpecialEffect specialEffect;
    [HideInInspector] public float                specialValue;

    [Header("Drone Selection (선택 사항)")]
    [HideInInspector] public DroneSelectionEffect droneEffect;

    [Header("Display")]
    public Sprite   icon;
    public Sprite[] animationFrames;
    public float    frameRate = 12f;

}
