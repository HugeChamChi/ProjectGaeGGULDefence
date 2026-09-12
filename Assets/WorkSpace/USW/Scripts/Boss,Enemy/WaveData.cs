using UnityEngine;

/// <summary>
/// 웨이브 하나에 소환될 보스 목록 정의
/// StageData.waves[] 배열에 담겨 사용됨
/// </summary>

[System.Serializable]
public class BossEntry
{
    [Tooltip("보스 아이콘")]
    public Sprite bossIcon;

    [Tooltip("보스 프리팹 — 프리팹 내 BossBase 서브클래스에 patterns 설정")]
    public GameObject prefab;

    [Tooltip("이 보스의 HP (WaveManager가 주입)")]
    public int hp = 500;
    /// <summary>보스 기본 방어력. 시트 defense가 있으면 시트 값 우선.</summary>
    [Min(0)] public double Defense;
}

[CreateAssetMenu(fileName = "WaveData", menuName = "Game/Wave/WaveData")]
public class WaveData : ScriptableObject
{
    [Tooltip("Inspector / Editor 툴에서 식별용 이름")]
    public string waveName = "Wave";

    [Tooltip("이 웨이브에서 동시에 소환될 보스 목록")]
    public BossEntry[] bosses = new BossEntry[1];
}
