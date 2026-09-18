using UnityEngine;

/// <summary>
/// 웨이브 하나에 소환될 보스 목록 정의
/// StageData.waves[] 배열에 담겨 사용됨
/// </summary>

[CreateAssetMenu(fileName = "WaveData", menuName = "Game/Wave/WaveData")]
public class WaveData : ScriptableObject
{
    [Tooltip("Inspector / Editor 툴에서 식별용 이름")]
    public string waveName = "Wave";

    [Tooltip("이 웨이브에서 한 마리씩 순서대로 소환될 보스 목록. 보스별 BossData를 연결하세요.")]
    public BossEntry[] bosses = new BossEntry[1];
}
