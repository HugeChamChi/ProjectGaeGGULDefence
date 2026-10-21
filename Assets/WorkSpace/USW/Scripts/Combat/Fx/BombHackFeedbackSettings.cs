using UnityEngine;

/// <summary>
/// 9122 해킹 자폭의 B 글리치 흡수 표시 설정. FxLab_BombHackReceipt 확정값을 옮겼다.
/// 표시 전용이며 스택 수치·용량·소비 규칙은 DroneHackingData와 카드 SO가 소유한다.
/// </summary>
[CreateAssetMenu(menuName = "USW/Fx/Bomb Hack Feedback Settings")]
public sealed class BombHackFeedbackSettings : ScriptableObject
{
    [Header("적중 — 짧은 보라 가로 글리치(사각 칩 없음)")]
    [Tooltip("적중점 글리치와 어두운 바탕이 사라지는 시간(초).")]
    [Min(0.01f)] public float RuptureSeconds = 0.32f;
    [Tooltip("적중점 작은 반짝임이 남는 시간(초).")]
    [Min(0.01f)] public float SparkleSeconds = 0.75f;
    [Tooltip("적중점 어두운 바탕 크기(캔버스 픽셀).")]
    [Min(0f)] public float ImpactSize = 62f;
    [Tooltip("가로 글리치 한 줄의 반쪽 길이(캔버스 픽셀).")]
    [Min(0f)] public float SliceHalfLength = 32f;
    [Tooltip("가로 글리치 줄 간격(캔버스 픽셀).")]
    [Min(0f)] public float SliceSpacing = 17f;
    [Tooltip("가로 글리치 굵기(캔버스 픽셀).")]
    [Min(0f)] public float SliceWidth = 5f;
    [Tooltip("적중점 반짝임 크기(캔버스 픽셀).")]
    [Min(0f)] public float SparkleSize = 28f;
    [Tooltip("월드 글리치 조각 수(평소 / 밀집).")]
    [Min(0)] public int GlitchBlocks = 5;
    [Min(0)] public int DenseGlitchBlocks = 3;
    [Tooltip("동시에 남아 있는 적중이 이 수 이상이면 밀집 표시로 줄인다.")]
    [Min(1)] public int DenseImpactThreshold = 4;
    [Tooltip("밀집 때 반짝임 크기 배율.")]
    [Range(0.1f, 1f)] public float DenseDetailScale = 0.65f;
    public Color RuptureColor = new Color(0.75f, 0.34f, 1f);
    public Color ImpactInkColor = new Color(0.13f, 0.06f, 0.24f, 0.85f);

    [Header("HACK 수신 — 자폭 한 기당 신호 한 개")]
    [Tooltip("적중 뒤 신호가 출발하기까지(초).")]
    [Min(0f)] public float GlitchLeadSeconds = 0.04f;
    [Tooltip("신호가 링 안으로 접혀 들어가는 시간(초). 도착 순간 HUD 숫자를 확인한다.")]
    [Min(0.01f)] public float GlitchTransferSeconds = 0.22f;
    [Tooltip("도착 뒤 충전띠 확인이 남는 시간(초).")]
    [Min(0.01f)] public float GlitchSettleSeconds = 0.1f;
    [Tooltip("짧게 몰린 수신의 시작 간격(초). 신호를 합치거나 생략하지 않는다.")]
    [Min(0.01f)] public float ReceiptSpacingSeconds = 0.07f;
    public Color DataColor = new Color(0.38f, 0.86f, 1f);
    public Color GlitchColor = new Color(0.75f, 0.34f, 1f);
    public Color ReceiptInkColor = new Color(0.05f, 0.09f, 0.16f);
    [Tooltip("충전띠 확인 호의 반지름(캔버스 픽셀).")]
    [Min(0f)] public float RingRadius = 95f;
    [Tooltip("신호 출발 때 가로 글리치 길이(캔버스 픽셀).")]
    [Min(0f)] public float GlitchWidth = 62f;
}
