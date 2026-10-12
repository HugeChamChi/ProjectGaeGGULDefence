using UnityEngine;

/// <summary>
/// 버프 연출 한 가지 모양새(시안) — StatBuffFx.Play에 넘긴다. 모든 크기는 월드 단위, 발밑 = 0.
/// 실험실 시안 13종은 StatBuffFxLabBuilder가 Data/Fx/StatBuff/에 만든다. 분위기 기준: design/버프형식.gif, design/버프예시2.gif
/// (납작한 만화풍 조각이 떠오르고, 발밑에 빛 줄기가 돈다).
/// </summary>
[CreateAssetMenu(menuName = "USW/Fx/Stat Buff Style", fileName = "StatBuffStyle")]
public sealed class StatBuffFxStyle : ScriptableObject
{
    [Header("이름 (실험실 표시용)")]
    public string DisplayName = "버프";
    [TextArea(1, 3)] public string Usage = "";

    [Header("색")]
    [Tooltip("조각·발밑 오라·빛기둥·눈금")]
    public Color Primary = new Color(0.36f, 0.92f, 0.68f, 1f);
    [Tooltip("두 번째 조각·속도선·발밑 소용돌이")]
    public Color Secondary = new Color(0.7f, 1f, 0.85f, 1f);
    [Tooltip("머리 위 배지")]
    public Color BadgeColor = new Color(0.36f, 0.92f, 0.68f, 1f);
    [Tooltip("끄면 OutlineColor, 켜면 조각 색을 어둡게 해서 외곽선으로 쓴다")]
    public bool AutoOutlineColor = true;
    public Color OutlineColor = new Color(0.1f, 0.1f, 0.15f, 0.9f);

    [Header("조각 색 변화")]
    [Tooltip("조각의 수명에 따라 색을 바꾼다. 꺼두면 기존 Primary/Secondary를 유지한다")]
    public bool AnimateRiserColor;
    [Tooltip("0 = 생성, 1 = 소멸. 알파는 기존 등장/퇴장 연출과 함께 적용된다")]
    public Gradient RiserColorOverLife = new Gradient();

    [Header("떠오르는 조각")]
    public StatBuffShape Shape = StatBuffShape.Triangle;
    public bool Outline;
    [Tooltip("아래로 가라앉는다 (디버프)")]
    public bool Downward;
    public StatBuffSpawnPattern Pattern = StatBuffSpawnPattern.Scatter;
    [Min(0.02f)] public float Interval = 0.12f;
    [Min(0.1f)] public float Life = 0.85f;
    [Min(0f)] public float Rise = 0.75f;
    [Tooltip("최소 / 최대 크기")]
    public Vector2 Size = new Vector2(0.22f, 0.4f);
    [Tooltip("생기는 범위 — 가로 반폭 / 세로")]
    public Vector2 Area = new Vector2(0.65f, 1.1f);
    [Tooltip("두 개씩 겹쳐 올린다 (공격속도 구분용)")]
    public bool Stacked;
    [Min(0f)] public float StackGap = 0.62f;
    [Tooltip("초당 회전 각도 범위")]
    public Vector2 SpinRange = Vector2.zero;
    [Tooltip("처음에 무작위로 기울인다")]
    public bool RandomTilt;
    [Tooltip("좌우로 흔들리는 폭")]
    [Min(0f)] public float Sway;
    [Min(0f)] public float SwayFrequency = 6f;
    [Tooltip("끝날 때 크기 배율 (1보다 크면 커지며 사라진다 — 연기)")]
    [Min(0f)] public float EndScale = 0.55f;

    [Header("두 번째 조각 (섞기)")]
    [Range(0f, 1f)] public float SecondaryShare;
    [Tooltip("None이면 Shape와 같은 모양")]
    public StatBuffShape SecondaryShape = StatBuffShape.None;
    public bool SecondaryDownward;

    [Header("반짝이")]
    [Min(0f)] public float SparkleInterval = 0.2f;
    [Min(0f)] public float SparkleSize = 0.16f;
    public Color SparkleColor = Color.white;

    [Header("속도선 (빠르게 솟는 가는 줄)")]
    public bool SpeedLines;
    [Min(0.02f)] public float SpeedLineInterval = 0.07f;
    [Min(0f)] public float SpeedLineLength = 0.45f;
    [Min(0f)] public float SpeedLineSpeed = 3.2f;

    [Header("발밑")]
    public bool Swoosh;
    [Range(1, 4)] public int SwooshArcs = 2;
    public float SwooshSpeed = 320f;
    [Min(0f)] public float SwooshRadius = 0.55f;
    [Tooltip("발밑에 은은한 색 원판")]
    public bool GroundAura;
    [Min(0f)] public float AuraRadius = 0.6f;
    [Tooltip("남은 시간 눈금 — 지속시간이 있을 때 시간 따라 하나씩 꺼진다")]
    public bool TimerPips;
    [Range(4, 16)] public int PipCount = 10;

    [Header("빛기둥 (유닛 뒤)")]
    public bool Pillar;
    [Min(0f)] public float PillarHeight = 1.9f;
    [Min(0f)] public float PillarWidth = 0.95f;

    [Header("머리 위 배지")]
    public StatBuffShape Badge = StatBuffShape.None;
    public float BadgeY = 1.55f;
    [Min(0f)] public float BadgeSize = 0.5f;
    [Min(0f)] public float BadgeHold = 0.8f;
    [Tooltip("다시 띄우는 간격 (0 = 걸릴 때 한 번)")]
    [Min(0f)] public float BadgeRepeat;

    [Header("시작")]
    public bool BurstRing = true;
    [Min(0)] public int BurstCount = 4;

    [Header("시간")]
    [Min(0.05f)] public float Intro = 0.35f;
    [Min(0.05f)] public float Outro = 0.35f;

    /// <summary>조각 색에 맞는 외곽선 색.</summary>
    public Color ResolveOutline(Color fill)
    {
        if (!AutoOutlineColor) return OutlineColor;
        return new Color(fill.r * 0.35f, fill.g * 0.35f, fill.b * 0.35f, 0.9f);
    }
}
