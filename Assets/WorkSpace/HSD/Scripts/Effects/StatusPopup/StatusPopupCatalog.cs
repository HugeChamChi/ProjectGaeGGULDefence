using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 상태 팝업 관리 표 (사용자 요청 2026-10-23). 효과 종류마다 문구·색·화살표 방향·재표시 대기 시간을 한곳에서 정한다.
/// 색은 같은 효과의 기존 이펙트 색과 맞춘다(예: 공격력 = 빨강, 공격속도 = 초록). 모양·타이밍은 style이 정한다.
/// 글꼴은 정적 아틀라스라 새 문구를 넣으면 StatusPopupJuaFont에 글자를 추가해야 한다.
/// </summary>
[CreateAssetMenu(menuName = "GGD/UI/Status Popup Catalog", fileName = "StatusPopupCatalog")]
public class StatusPopupCatalog : ScriptableObject
{
    /// <summary>효과 한 종류의 표시 방법.</summary>
    [Serializable]
    public class Entry
    {
        public StatusPopupKind Kind;
        [Tooltip("표시 문구 (예: 공격속도). 화살표는 따로 붙는다")]
        public string Text;
        [Tooltip("true = ▲, false = ▼")]
        public bool ArrowUp = true;
        [Tooltip("글자·화살표 색. 같은 효과의 기존 이펙트 색과 맞춘다")]
        public Color Color = Color.white;
        [Tooltip("같은 대상에 같은 효과를 다시 띄우기까지 최소 간격(초). 0이면 매번")]
        [Min(0f)] public float Cooldown;
    }

    [Tooltip("모양·타이밍 (실험실에서 고른 C안)")]
    public StatusPopupStyle Style;
    public List<Entry> Entries = new List<Entry>();

    [Header("대상 크기에 맞춘 글자 크기")]
    [Tooltip("배율 1이 되는 대상의 월드 높이(일반 유닛 그림 높이)")]
    [Min(0.01f)] public float ReferenceHeight = 1.65f;
    [Min(0.1f)] public float MinScale = 0.55f;
    [Min(0.1f)] public float MaxScale = 1.1f;

    /// <summary>종류에 맞는 표시 방법. 없으면 false.</summary>
    public bool TryGet(StatusPopupKind kind, out Entry entry)
    {
        foreach (var candidate in Entries)
        {
            if (candidate != null && candidate.Kind == kind && !string.IsNullOrEmpty(candidate.Text))
            {
                entry = candidate;
                return true;
            }
        }
        entry = null;
        return false;
    }

    /// <summary>대상 월드 높이에 맞춘 묶음 배율.</summary>
    public float ScaleFor(float targetHeight) => Mathf.Clamp(targetHeight / ReferenceHeight, MinScale, MaxScale);
}
