using System;
using DG.Tweening;
using UnityEngine;

/// <summary>메인 카메라를 흔드는 연출 전용 이펙트. 게임 상태(데미지 등)에는 관여하지 않는다.</summary>
[Serializable]
[DisplayName("카메라 흔들림")]
public class CameraShakeEffect : IAdditionalEffect
{
    [Tooltip("흔들리는 시간(초)")]
    [KoreanLabel("지속 시간")]
    public float duration = 0.2f;
    [Tooltip("흔들리는 세기")]
    [KoreanLabel("세기")]
    public float strength = 0.3f;
    [Tooltip("흔들리는 진동수")]
    [KoreanLabel("진동수")]
    public int vibrato = 20;

    public void Apply(UnitBase caster, BossBase target, Vector3 hitPosition)
    {
        Transform cam = Camera.main != null ? Camera.main.transform : null;
        if (cam == null) return;

        cam.DOKill(true);
        cam.DOShakePosition(duration, strength, vibrato, 90, false, true);
    }
}
