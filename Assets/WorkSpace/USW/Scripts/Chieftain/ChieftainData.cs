using UnityEngine;

/// <summary>
/// 족장 한 명의 데이터 — 아웃게임 선택 및 인게임 배치에 사용
/// 생성: 우클릭 → Create → Game/ChieftainData
/// </summary>
[CreateAssetMenu(fileName = "ChieftainData", menuName = "Game/ChieftainData")]
public class ChieftainData : ScriptableObject
{
    [Header("Info")]
    public string chieftainId;    // 백엔드 저장 키 (예: "ninja_chief_01")
    public string chieftainName;
    public int    unitType;       // UnitFactory.CreateUnit(unitType) 참조
    public Sprite icon;           // 아웃게임 선택 UI용
}
