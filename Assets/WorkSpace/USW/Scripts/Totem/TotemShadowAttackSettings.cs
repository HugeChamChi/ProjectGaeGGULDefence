using System;
using UnityEngine;

/// <summary>그림자 공격 지연과 반투명 분신의 표시 설정.</summary>
[Serializable]
public class TotemShadowAttackSettings
{
    /// <summary>원본 각 발사로부터 재현까지 걸리는 게임 시간(초).</summary>
    [Min(0)] public float DelaySeconds = 0.2f;
    /// <summary>공격 분신의 표시 시간(초).</summary>
    [Min(0.01f)] public float VisualSeconds = 0.35f;
    /// <summary>분신 색상과 투명도.</summary>
    public Color Tint = new Color(0.35f, 0.25f, 0.5f, 0.45f);
    /// <summary>원본 공격 위치에 더하는 시각적 오프셋.</summary>
    public Vector3 VisualOffset = new Vector3(0.15f, 0.1f, 0f);
}
