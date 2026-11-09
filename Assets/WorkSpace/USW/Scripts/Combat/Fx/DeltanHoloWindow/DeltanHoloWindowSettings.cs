using System;
using UnityEngine;

/// <summary>
/// 델탕 skill 홀로그램 창 연출 값. 창마다 위치·크기·등장 시각을 두고, 모두 같은 시각에 닫힌다.
/// 길이 단위는 이펙트 Transform의 로컬 단위(유닛 외형 기준), 시간 단위는 초.
/// </summary>
[CreateAssetMenu(menuName = "USW/Fx/Deltan Holo Window Settings", fileName = "DeltanHoloWindowSettings")]
public class DeltanHoloWindowSettings : ScriptableObject
{
    /// <summary>창 하나의 배치와 등장 시각.</summary>
    [Serializable]
    public class Window
    {
        /// <summary>이펙트 기준 로컬 위치.</summary>
        public Vector2 Offset;
        /// <summary>창 본문 가로 길이(빛 번짐 제외).</summary>
        public float Width = 0.3f;
        /// <summary>기울기(도).</summary>
        public float Rotation;
        /// <summary>Play 후 튀어나오는 시각.</summary>
        public float Delay;
    }

    [Header("Art")]
    public Sprite FrameSprite;
    public Sprite LineSprite;
    [Tooltip("창 그림 전체 대비 본문 상자 비율 (가로, 세로). 나머지는 빛 번짐 여백")]
    public Vector2 ContentRatio = new Vector2(240f / 288f, 170f / 218f);

    [Header("Windows")]
    public Window[] Windows = Array.Empty<Window>();

    [Header("Timing")]
    public float PopDuration = 0.18f;
    [Tooltip("튀어나올 때 넘치는 정도 (back ease 계수)")]
    public float PopOvershoot = 2.2f;
    [Tooltip("Play 후 모든 창이 닫히기 시작하는 시각")]
    public float CloseTime = 1.45f;
    public float CloseDuration = 0.18f;

    [Header("Text lines")]
    public int LinesPerWindow = 3;
    [Tooltip("본문 높이 대비 제목 줄 높이")]
    public float TitleBarRatio = 0.2f;
    [Tooltip("본문 높이 대비 글줄 두께")]
    public float LineThicknessRatio = 0.075f;
    [Tooltip("본문 높이 대비 글줄 간격")]
    public float LineSpacingRatio = 0.17f;
    [Tooltip("본문 가로 대비 왼쪽 여백")]
    public float LineLeftPadRatio = 0.08f;
    [Tooltip("본문 가로 대비 글줄 길이 범위")]
    public Vector2 LineWidthRange = new Vector2(0.35f, 0.82f);
    [Tooltip("글줄 길이가 바뀌는 속도 (타자 치는 느낌)")]
    public float LineJitterSpeed = 3f;
    public Color LineColor = new Color(1f, 1f, 1f, 0.9f);

    [Header("Sorting")]
    public string SortingLayer = "FX";
    public int SortingOrder = 40;
}
