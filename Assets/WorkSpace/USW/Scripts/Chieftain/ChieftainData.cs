using UnityEngine;

/// <summary>
/// 족장 한 명의 인게임 데이터 — HSD의 ChiefData(아웃게임)와 인게임 유닛 사이의 브리지
///
/// chieftainId  : HSD ChiefData.Id와 동일한 int 값으로 매칭
/// unitType     : UnitFactory.CreateUnit(unitType) 호출용
/// 생성: 우클릭 → Create → Game/ChieftainData
/// </summary>
[CreateAssetMenu(fileName = "ChieftainData", menuName = "Game/ChieftainData")]
public class ChieftainData : ScriptableObject
{
    /// <summary>ID 기반 족장 선택 경로에서 사용하는 독립 레벨업 풀.</summary>
    public LevelUpPoolData LevelUpPool;
    /// <summary>ID 기반 선택도 유닛을 생성하지 않고 알팡 액티브를 사용할 수 있다.</summary>
    public AlphanSkillData AlphanSkill;

    [Header("Info")]
    public int    chieftainId;   // HSD ChiefData.Id와 일치
    public string chieftainName;
    public int    unitType;      // UnitFactory.CreateUnit(unitType) 참조
}
