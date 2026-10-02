using System;
using UnityEngine;

/// <summary>
/// 보스 처치 시간 보너스 연출 실험실(FxLab_TimerBonus) 설정 — 시안 4종(합체·슬롯 릴·질주·스탬프)과 공통 텐션 값.
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
}
