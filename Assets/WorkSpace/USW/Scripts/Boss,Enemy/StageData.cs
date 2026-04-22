using UnityEngine;

/// <summary>
/// 스테이지 하나의 전체 웨이브 묶음 (ScriptableObject)
/// WaveManager가 참조하여 순서대로 웨이브를 진행
/// </summary>
[CreateAssetMenu(fileName = "StageData", menuName = "Game/Stage/StageData")]
public class StageData : ScriptableObject
{
    [Tooltip("스테이지 이름 (Editor 툴 표시용)")]
    public string stageName = "Stage 1";

    [Tooltip("순서대로 진행될 웨이브 SO 배열")]
    public WaveData[] waves;
}
