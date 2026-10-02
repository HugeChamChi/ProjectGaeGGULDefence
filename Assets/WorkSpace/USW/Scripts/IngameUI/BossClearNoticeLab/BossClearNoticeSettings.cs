using UnityEngine;

/// <summary>
/// 보스 처치 공지 실험실(FxLab_BossClearNotice) 설정 — 화면 가운데에 빠르게 떴다 빠르게 사라지는 공지 시안 4종의 공통 값.
/// 플레이 중 인스펙터에서 바꾸면 바로 반영되고 에셋에 남는다. 시간은 초(실험실 시계 = 히트스톱·슬로모션 반영), 크기는 1080 캔버스 단위.
/// 참고 연출: design/보스죽고나서연출.gif(어두운 띠 + WAVE 글자), 보스죽고나서연출2.gif(노란 띠 + 보너스 둘째 줄).
/// 실험실 전용 — 인게임 UI와 무관.
/// </summary>
[CreateAssetMenu(fileName = "BossClearNoticeSettings", menuName = "USW/UI/Boss Clear Notice Settings")]
public class BossClearNoticeSettings : ScriptableObject
{
    [Header("문구")]
    public string Title = "보스 처치!";
    [Tooltip("둘째 줄. {0} = 보너스 초 (타이머 실험실의 보너스 버튼 값)")]
    public string SubFormat = "남은 시간 +{0}초";

    [Header("박자 (초)")]
    [Tooltip("보스 처치 순간부터 공지가 뜨기 시작할 때까지")]
    public float DelayAfterKill = 0.12f;
    [Tooltip("공지가 뜨기 시작해서 사라지기 시작할 때까지 (등장 + 머무름)")]
    public float VisibleSeconds = 0.85f;
    [Tooltip("사라지는 연출 길이 배율 (1 = 시안 기본, 작을수록 더 빨리 사라짐)")]
    [Range(0.3f, 2f)] public float ExitSpeedScale = 1f;

    [Header("배치")]
    [Tooltip("화면 가운데 기준 세로 위치 (캔버스 단위, 위 +)")]
    public float NoticeY = 0f;
    [Range(0.5f, 1.5f)] public float Scale = 1f;

    [Header("색")]
    public Color DarkBand = new Color(0.02f, 0.04f, 0.05f, 0.68f);
    public Color Gold = new Color(1f, 0.773f, 0.227f);
    public Color OrangeBand = new Color(1f, 0.62f, 0.1f);
    public Color LightBand = new Color(1f, 0.86f, 0.32f);
    public Color Ink = new Color(0.24f, 0.13f, 0f);
    public Color PillColor = new Color(0.09f, 0.12f, 0.18f, 0.94f);
    public Color Cyan = new Color(0.384f, 0.894f, 1f);
    public Color Hot = new Color(1f, 0.353f, 0.47f);

    [Header("B 골드 배너 (참고 GIF 2)")]
    [Tooltip("제목·둘째 줄 글자 (크림색)")]
    public Color BannerText = new Color(1f, 0.96f, 0.86f);
    [Tooltip("글자 외곽선 (두꺼운 진갈색)")]
    public Color BannerOutline = new Color(0.3f, 0.15f, 0.05f);
    [Tooltip("둘째 줄 노란 줄")]
    public Color BannerStrip = new Color(1f, 0.86f, 0.12f);
    [Tooltip("둘째 줄 앞 글자")]
    public string BannerSubLabel = "남은 시간";
    [Tooltip("둘째 줄 값 (조금 크게). {0} = 보너스 초")]
    public string BannerSubValue = "+{0}초";

    [Header("D 크로스 스트라이크")]
    [Tooltip("글자가 박히는 순간 타이머 실험실과 같은 히트스톱·플래시·줌을 준다 (0 = 끔)")]
    [Range(0f, 2f)] public float StrikeImpact = 0.8f;
}
