using UnityEngine;

/// <summary>
/// 레벨업 선택지 하나의 데이터
///
/// 생성: 우클릭 → Create → Game/LevelUpData
/// animationFrames: 선택 시 재생할 스프라이트 시트 프레임 배열
/// icon: 기본 커버 이미지 (선택 전 표시)
/// frameRate: 초당 재생할 프레임 수 (기본 12)
/// </summary>
[CreateAssetMenu(fileName = "LevelUpData", menuName = "Game/LevelUpData")]
public class LevelUpData : ScriptableObject
{
    public LevelUpEffectType effectType;
    public float             value;            // 퍼센트 값 (예: 10 = 10%)
    public Sprite            icon;             // 선택 전 커버 이미지
    public Sprite[]          animationFrames;  // 선택 시 재생할 스프라이트 배열
    public float             frameRate = 12f;  // 초당 프레임 수
    [TextArea] public string description;
}
