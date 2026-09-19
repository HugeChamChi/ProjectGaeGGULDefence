using System;
using UnityEngine;

/// <summary>그림자 공격 지연과 반투명 분신의 표시 설정.</summary>
[Serializable]
public class TotemShadowAttackSettings
{
    /// <summary>원본 각 발사로부터 재현까지 걸리는 게임 시간(초).</summary>
    [Min(0)] public float DelaySeconds = 0.2f;
    /// <summary>이전 에셋 호환 필드. 상시 부착 외형의 수명에는 사용하지 않는다.</summary>
    [HideInInspector, Min(0.01f)] public float VisualSeconds = 0.35f;
    /// <summary>범위 진입 시 알파 보간 시간. 공격마다 다시 시작하지 않는다.</summary>
    [Min(0f)] public float FadeSeconds = 0.08f;
    /// <summary>분신 색상과 투명도.</summary>
    public Color Tint = new Color(0.35f, 0.25f, 0.5f, 0.45f);
    /// <summary>원본 공격 위치에 더하는 시각적 오프셋.</summary>
    public Vector3 VisualOffset = new Vector3(0.15f, 0.1f, 0f);
}
