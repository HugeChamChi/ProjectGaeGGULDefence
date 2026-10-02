using UnityEngine;

/// <summary>
/// 중앙 알림(CenterToast) 설정 — 연출 방식, 위치, 시간, 색.
/// 플레이 중 인스펙터에서 바꾸면 바로 반영된다 (연출 방식만 바꿀 때는 떠 있는 알림이 지워진다).
/// 시간은 timeScale을 무시하는 초 — 일시정지 중에도 알림이 뜨고 사라진다. 크기는 1080 캔버스 단위.
/// </summary>
[CreateAssetMenu(fileName = "CenterToastSettings", menuName = "USW/UI/Center Toast Settings")]
public class CenterToastSettings : ScriptableObject
{
    [Header("연출")]
    public CenterToastStyle Style = CenterToastStyle.Stack;

    [Header("배치")]
    [Tooltip("화면 가운데 기준 세로 위치 (위 +)")]
    public float AnchorY = 240f;

    [Header("시간 (초, timeScale 무시)")]
    [Tooltip("뜬 뒤 사라지기 시작할 때까지. 같은 문구를 다시 띄우면(합치는 방식) 이 시간이 다시 시작된다")]
    public float Lifetime = 1.1f;
    [Tooltip("동시에 보일 수 있는 최대 줄 수 (A 스택). 넘치면 가장 오래된 것부터 빠르게 사라진다")]
    [Range(1, 8)] public int MaxVisible = 4;

    [Header("글자 크기")]
    public float FontSize = 44f;

    [Header("모던 A·B·C (고딕 글꼴 + 유리 바탕)")]
    public Color ModernText = new Color(0.96f, 0.97f, 1f);
    [Tooltip("어두운 유리 바탕")]
    public Color ModernGlass = new Color(0.07f, 0.08f, 0.11f, 0.86f);
    public Color ModernWarning = new Color(1f, 0.33f, 0.38f);
    public Color ModernInfo = new Color(0.36f, 0.78f, 1f);
    [Tooltip("A 스택 줄 사이 간격")]
    public float StackGap = 10f;

    [Header("D 펀치 글자 (게임 글꼴)")]
    public Color TextColor = Color.white;
    public Color OutlineColor = new Color(0.08f, 0.05f, 0.1f);
    [Range(0f, 0.5f)] public float OutlineWidth = 0.22f;
    [Tooltip("글자 뒤 은은한 어두운 빛")]
    public Color Background = new Color(0.06f, 0.05f, 0.08f, 0.82f);
    [Tooltip("글자 양옆 여백 (빛 크기)")]
    public float PaddingX = 48f;
    [Tooltip("빛 높이 기준")]
    public float Height = 92f;
    public Color WarningAccent = new Color(1f, 0.36f, 0.36f);
    public Color InfoAccent = new Color(1f, 0.77f, 0.23f);

    /// <summary>D 강조색.</summary>
    public Color Accent(CenterToastKind kind) => kind == CenterToastKind.Info ? InfoAccent : WarningAccent;

    /// <summary>A·B·C 강조색.</summary>
    public Color ModernAccent(CenterToastKind kind) => kind == CenterToastKind.Info ? ModernInfo : ModernWarning;
}
