using UnityEngine;

/// <summary>사운드 실험실의 공통 타이밍과 비교 음량. 실제 전투 밸런스와 분리한다.</summary>
[CreateAssetMenu(menuName = "USW/Fx Lab/Disigman Sound Settings")]
public sealed class DisigmanRailgunSoundSettings : ScriptableObject
{
    /// <summary>원래 레일건 시각 연출과 동일한 충전 시간.</summary>
    public float ChargeSeconds = .4f;
    /// <summary>DSP 버퍼를 확보하는 예약 재생 선행 시간.</summary>
    public float ScheduleLeadSeconds = .08f;
    /// <summary>모든 시안에 동일하게 적용할 시작 음량.</summary>
    [Range(0f, 1f)] public float PreviewVolume = .65f;
    /// <summary>반복 청취 사이의 간격.</summary>
    public float RepeatSeconds = 3.03f;
    /// <summary>원래 스킬 연출의 크기 배율.</summary>
    public float SkillVisualScale = 1.3f;
    /// <summary>반동 거리.</summary>
    public float Recoil = .08f;
    /// <summary>반동 복귀 시간.</summary>
    public float RecoilSeconds = .15f;
    /// <summary>발사 그림 유지 시간.</summary>
    public float FirePoseSeconds = .2f;
}
