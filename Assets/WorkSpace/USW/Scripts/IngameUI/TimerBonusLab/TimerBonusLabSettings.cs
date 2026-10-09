using System;
using UnityEngine;

/// <summary>
/// 보스 처치 시간 보너스 연출 실험실(FxLab_TimerBonus) 설정 — 시안 5종(합체·슬롯 릴·질주·스탬프·해킹)과 공통 텐션 값.
/// 플레이 중 인스펙터에서 바꾸면 바로 반영되고 에셋에 남는다.
/// 시간은 초, 거리·속도는 HTML 시안 px 기준 (× PxScale = 1080 캔버스 단위).
/// 실험실 전용 — 인게임 TimerController/UIManager와 무관.
/// </summary>
[CreateAssetMenu(fileName = "TimerBonusLabSettings", menuName = "USW/UI/Timer Bonus Lab Settings")]
public class TimerBonusLabSettings : ScriptableObject
{
    /// <summary>A 합체: 보너스 칩이 날아와 타이머에 박히며 합쳐진다.</summary>
    [Serializable]
    public class FusionTuning
    {
        [Tooltip("칩이 보스 자리에서 타이머까지 날아가는 시간")]
        public float FlySeconds = 0.34f;
        [Tooltip("충돌 후 숫자가 합산값까지 올라가는 시간")]
        public float CountSeconds = 0.3f;
        [Tooltip("충돌 순간 가로로 퍼지는 비율")]
        public float SquashX = 0.38f;
        [Tooltip("충돌 순간 세로로 눌리는 비율")]
        public float SquashY = 0.32f;
    }

    /// <summary>B 슬롯 릴: 자릿수가 돌다가 왼쪽부터 멈춘다.</summary>
    [Serializable]
    public class SlotReelTuning
    {
        [Tooltip("처치 후 릴이 돌기 시작할 때까지 (칩이 빨려 들어가는 시간)")]
        public float StartDelay = 0.28f;
        [Tooltip("첫 자릿수가 멈출 때까지 도는 시간")]
        public float SpinSeconds = 0.42f;
        [Tooltip("다음 자릿수가 멈추는 간격")]
        public float Stagger = 0.095f;
        [Tooltip("멈출 때 지나쳤다 돌아오는 정도 (OutBack)")]
        public float Overshoot = 1.5f;
    }

    /// <summary>C 질주: 속도선과 함께 기울며 순식간에 카운트업.</summary>
    [Serializable]
    public class DashTuning
    {
        [Tooltip("반대로 살짝 젖히는 준비 동작")]
        public float WindUpSeconds = 0.07f;
        [Tooltip("숫자가 합산값까지 올라가는 시간")]
        public float CountUpSeconds = 0.43f;
        [Tooltip("돌진할 때 기울기 (도)")]
        public float LeanDegrees = 20f;
        [Tooltip("RGB 잔상 좌우 간격 (px)")]
        public float RgbOffset = 8f;
        [Tooltip("게이지가 꽉 찼을 때의 시간 (초)")]
        public float BarMaxSeconds = 45f;
        [Tooltip("돌진 중 초당 속도선 수")]
        public float StreaksPerSecond = 240f;
    }

    /// <summary>D 스탬프: 거대한 +N이 찍히고 조각이 타이머로 흡수된다.</summary>
    [Serializable]
    public class StampTuning
    {
        [Tooltip("떨어지기 시작할 때 크기 배율")]
        public float StartScale = 3.4f;
        public float FallStart = 0.04f;
        [Tooltip("바닥에 쾅 찍히는 시점 (처치 기준 초)")]
        public float HitTime = 0.2f;
        [Tooltip("조각으로 부서지는 시점")]
        public float BreakTime = 0.43f;
        [Range(4, 40)] public int ShardCount = 16;
        [Tooltip("첫 조각이 타이머에 닿는 시점")]
        public float ArriveTime = 0.78f;
        [Tooltip("화면 흔들림 세기 (px)")]
        public float ShakeAmplitude = 11f;
    }

    /// <summary>
    /// E 해킹: 타이머가 가로 띠로 찢어지고 숫자가 뒤섞이다가 왼쪽부터 새 값으로 고정된다.
    /// 2026-10-09 사용자 "조금 더 빨리" — 처치~정상화 약 0.94초 → 0.47초.
    /// 레퍼런스 design/글리치모드.gif — 금 간 크리스탈의 하늘색 스캔라인·행 어긋남 글리치.
    /// </summary>
    [Serializable]
    public class HackTuning
    {
        [Tooltip("처치 직후 깜빡깜빡 감염되는 구간")]
        public float InfectSeconds = 0.05f;
        [Tooltip("숫자가 뒤섞이며 진행 막대가 차는 시간 (첫 자릿수 고정까지)")]
        public float HackSeconds = 0.28f;
        [Tooltip("다음 자릿수가 고정되는 간격")]
        public float LockStagger = 0.045f;
        [Tooltip("완료 후 글리치가 가라앉는 시간")]
        public float SettleSeconds = 0.18f;
        [Tooltip("글리치 무늬가 바뀌는 간격 (짧을수록 정신없음)")]
        public float TickSeconds = 0.04f;
        [Tooltip("타이머를 자르는 가로 띠 수")]
        [Range(2, 12)] public int SliceCount = 7;
        [Tooltip("가로 띠가 어긋나는 최대 거리 (px)")]
        public float MaxSliceShift = 24f;
        [Tooltip("RGB 잔상 최대 간격 (px)")]
        public float RgbOffset = 4f;
        [Tooltip("하늘색 스캔라인 최대 수")]
        [Range(0, 16)] public int ScanlineCount = 9;
        [Tooltip("픽셀 노이즈 조각 최대 수")]
        [Range(0, 24)] public int NoiseBlockCount = 12;
        [Tooltip("해킹 중 타이머가 하늘색으로 물드는 정도")]
        [Range(0f, 1f)] public float CyanTint = 0.7f;
        [Tooltip("완료 후 잔진동 글리치 시점 (완료 기준 초)")]
        public float[] AftershockTimes = { 0.3f };
        public float AftershockSeconds = 0.08f;

        [Header("중첩 시안 G~J (여러 캐릭터가 연달아 발동)")]
        [Tooltip("발동 시점 묶음 — '발동 타이밍' 버튼이 순환. 시점은 처치 기준 초, 발동마다 보너스 1번씩")]
        public StackTriggerSet[] StackTriggerSets =
        {
            new StackTriggerSet { Name = "몰아서 3 + 늦게 1", Times = new[] { 0f, 0.1f, 0.22f, 0.75f } },
            new StackTriggerSet { Name = "끝날 때쯤", Times = new[] { 0f, 0.4f, 0.52f } },
            new StackTriggerSet { Name = "연타 6", Times = new[] { 0f, 0.15f, 0.3f, 0.45f, 0.6f, 0.75f } },
        };
        [Tooltip("합산: 진행 중에 합쳐질 때마다 고정이 늦춰지는 시간")]
        public float MergeExtendSeconds = 0.08f;
        [Tooltip("J: 앞 해킹이 끝난 뒤 이 시간 안의 발동은 결과에 이어 붙어 숫자가 누적된다 (넘으면 단독 짧은 버전)")]
        public float ChainWindowSeconds = 0.6f;
        [Tooltip("J: 이어 붙는 짧은 버전끼리 최소 간격 (한꺼번에 몰려도 하나씩 툭툭 보이게)")]
        public float ChainGapSeconds = 0.12f;
        [Tooltip("발동한 드론에서 타이머까지 해킹 신호가 날아가는 시간")]
        public float SignalSeconds = 0.18f;

        [Header("인게임 (디시그망 시간 회복)")]
        [Tooltip("인게임 해킹 완료 순간 화면 전체 흰 플래시·줌. 꺼짐 = 타이머만 터지고 연출 시계 히트스톱만 (자주 발동해도 눈이 덜 피곤하게)")]
        public bool RuntimeScreenFlash;
        [Tooltip("인게임: TIME HACK 막대·결과 글자를 이만큼(px) 더 내린다 — 타이머 바로 아래 보스 HP바를 가리지 않게")]
        public float RuntimeBarDropPx = 30f;
    }

    /// <summary>해킹 중첩 시안의 발동 시점 묶음 하나.</summary>
    [Serializable]
    public class StackTriggerSet
    {
        public string Name;
        public float[] Times;
    }

    [Header("흐름 (초)")]
    [Tooltip("보스 등장~처치까지 (긴장 구간)")]
    public float PreKillSeconds = 1f;
    [Tooltip("처치 후 연출 구간")]
    public float AnimSeconds = 1.6f;
    [Tooltip("연출 후 카운트다운을 보여 주는 구간")]
    public float PostSeconds = 1.1f;
    [Tooltip("반복 시작 시 남은 시간 (처치 순간 약 10.8초)")]
    public float BaseRemaining = 11.8f;
    [Tooltip("보너스 버튼이 순환하는 값")]
    public float[] Bonuses = { 5f, 10f, 15f, 30f };
    public int DefaultBonusIndex = 2;

    [Header("텐션 (공통)")]
    [Tooltip("처치 전 심장박동 시점 (반복 시작 기준 초) — 점점 촘촘하게")]
    public float[] HeartbeatTimes = { 0.08f, 0.4f, 0.65f, 0.83f, 0.95f };
    [Tooltip("처치 직전 타이머가 빨갛게 물드는 최소 정도")]
    [Range(0f, 1f)] public float DangerBase = 0.35f;
    [Tooltip("처치 순간 멈춤 (히트스톱, 초)")]
    public float KillHitStop = 0.095f;
    [Range(0f, 1f)] public float KillFlash = 0.65f;
    public float KillZoom = 0.07f;
    [Tooltip("시안별 충돌 순간 멈춤 (세기 1 기준, 초)")]
    public float ImpactHitStop = 0.07f;
    [Range(0f, 1f)] public float ImpactFlash = 0.45f;
    public float ImpactZoom = 0.06f;

    [Header("처치 전 드론 공격 (플레이 중 바로 반영·저장)")]
    [Tooltip("드론 자리 — 보스 중심 기준 px (위 +, 아래 -). 순서: Normal(민트) / Buffer(노랑) / Debuffer(빨강)")]
    public Vector2[] DroneSlots = { new Vector2(-120f, -150f), new Vector2(120f, -150f), new Vector2(0f, -180f) };
    [Tooltip("드론·레이저 크기 배율 (인게임 원본 1은 실험실 보스에 비해 작다)")]
    public float DroneScale = 2f;
    [Tooltip("드론이 번갈아 쏘는 간격 (초) — 한 마리 기준으로는 이 값 × 드론 수")]
    public float DroneFireInterval = 0.23f;

    [Header("크기")]
    [Tooltip("HTML 시안 px → 캔버스 단위 배율")]
    public float PxScale = 2.2f;
    public float TimerFontSize = 150f;

    [Header("색")]
    public Color TimerColor = new Color(1f, 0.965f, 0.9f);
    public Color GoldColor = new Color(1f, 0.773f, 0.227f);
    public Color DangerColor = new Color(1f, 0.31f, 0.43f);
    public Color CyanColor = new Color(0.384f, 0.894f, 1f);
    public Color HotColor = new Color(1f, 0.353f, 0.47f);

    [Header("시안별")]
    public FusionTuning Fusion = new FusionTuning();
    public SlotReelTuning SlotReel = new SlotReelTuning();
    public DashTuning Dash = new DashTuning();
    public StampTuning Stamp = new StampTuning();
    public HackTuning Hack = new HackTuning();
}
