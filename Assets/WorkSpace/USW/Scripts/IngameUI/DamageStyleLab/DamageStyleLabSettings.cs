using System;
using TMPro;
using UnityEngine;

/// <summary>
/// 데미지 표시 비교 테스트(TotemSelectTest 씬) 설정. 폰트 후보, 종류별 색, 모비노기형/쿠키런형 연출 수치.
/// 크기는 기준 해상도 1080x1920 캔버스 단위, 시간은 실제 초(unscaled).
/// </summary>
[CreateAssetMenu(fileName = "DamageStyleLabSettings", menuName = "USW/UI/Damage Style Lab Settings")]
public class DamageStyleLabSettings : ScriptableObject
{
    /// <summary>폰트 후보 하나. 머티리얼은 외곽선·그림자를 넣은 전용 머티리얼.</summary>
    [Serializable]
    public class FontEntry
    {
        public string DisplayName;
        public TMP_FontAsset Font;
        public Material Material;
    }

    /// <summary>모비노기형 숫자 등장 방식.</summary>
    public enum MobiEntryStyle { Pop, Slam }

    /// <summary>위→아래 그라데이션 두 색.</summary>
    [Serializable]
    public struct Gradient2
    {
        public Color Top;
        public Color Bottom;
        public Gradient2(Color top, Color bottom) { Top = top; Bottom = bottom; }
        public VertexGradient ToVertexGradient() => new VertexGradient(Top, Top, Bottom, Bottom);
    }

    [Header("폰트 후보 (버튼을 누를 때마다 순서대로)")]
    public FontEntry[] Fonts = Array.Empty<FontEntry>();

    [Header("색 (위 → 아래 그라데이션)")]
    [Tooltip("일반 피해 — 쿠키런 크럼블의 연한 주황")]
    public Gradient2 NormalColor = new Gradient2(new Color(1f, 0.84f, 0.62f), new Color(1f, 0.62f, 0.38f));
    [Tooltip("치명타 — 모비노기처럼 위 빨강에서 아래 주황으로 (GIF 측정 #EB441E → #EC9339). 외곽선·그림자와 섞이며 채도가 빠져서 입력값은 더 진하게 둔다")]
    public Gradient2 CriticalColor = new Gradient2(new Color(1f, 0.07f, 0f), new Color(1f, 0.68f, 0.14f));
    [Tooltip("화상 — 모비노기 '강타' 같은 빨강")]
    public Gradient2 BurnColor = new Gradient2(new Color(1f, 0.36f, 0.3f), new Color(0.84f, 0.06f, 0.1f));

    [Header("공통")]
    [Tooltip("숫자 캔버스 정렬 순서. HUD보다 위, 팝업(레벨업·보상)보다 아래")]
    public int SortingOrder = 20;
    [Tooltip("숫자가 올라가지 않을 화면 위쪽 비율. 타이머·보스 HP바 등 상단 HUD 영역")]
    [Range(0f, 0.5f)] public float TopHudRatio = 0.16f;

    /// <summary>기존 에셋 직렬화 호환용. 모비노기형은 타격별 즉시 표시하므로 이 값은 사용하지 않는다.</summary>
    [HideInInspector] public float MobiFlushInterval = 0.25f;

    [Header("모비노기형 — 보스 옆에 크게 쌓기")]
    public float MobiFontSize = 76f;
    [Tooltip("치명타 숫자 크기 배율 (일반 = 1)")]
    public float MobiCriticalScale = 1.5f;
    [Tooltip("화상 숫자 크기 배율 (일반 = 1)")]
    public float MobiBurnScale = 1.5f;
    [Tooltip("한 줄이 떠 있는 시간")]
    public float MobiLifetime = 1.1f;
    [Tooltip("이 비율 이후부터 흐려진다 (0~1)")]
    [Range(0f, 1f)] public float MobiFadeStart = 0.6f;
    [Tooltip("줄 간격 = 글자 크기 × 이 값")]
    public float MobiLineSpacing = 0.92f;
    [Tooltip("동시에 표시할 타격 수. 가득 차면 가장 오래된 숫자를 새 타격으로 즉시 교체한다")]
    [Min(1)] public int MobiMaxLines = 7;
    [Tooltip("새 줄이 들어오면 기존 줄이 밀려 올라가는 속도 (클수록 빠름)")]
    public float MobiFollowSpeed = 18f;
    public float MobiPopSeconds = 0.1f;
    [Tooltip("등장할 때 순간적으로 커지는 비율")]
    public float MobiPopOvershoot = 0.5f;
    [Tooltip("치명타 등장 흔들림 (캔버스 단위)")]
    public float MobiCriticalShake = 8f;
    public float MobiShakeSeconds = 0.18f;
    [Tooltip("등장 방식. Pop = 0에서 커졌다 줄어듦(기존), Slam = 크게 찍힌 뒤 줄어들며 자리 잡음(모비노기 GIF 분석, FxLab_DamageFloater)")]
    public MobiEntryStyle MobiEntry = MobiEntryStyle.Pop;
    [Tooltip("Slam: 크게 찍힌 상태에서 제자리 크기로 줄어드는 시간")]
    public float MobiSlamSeconds = 0.1f;
    [Tooltip("Slam: 등장 첫 프레임 크기 배율")]
    public float MobiSlamStartScale = 1.7f;
    [Tooltip("Slam: 등장 첫 프레임 가로·세로 추가 배율 (가로로 늘고 세로로 눌린 모양 → 1,1로 복귀)")]
    public Vector2 MobiSlamStretch = new Vector2(1.25f, 0.8f);
    [Tooltip("Slam: 등장 첫 프레임 위치 = 자리 + 이 값 (캔버스 단위). 레퍼런스는 왼쪽 아래에서 튀어 들어온다")]
    public Vector2 MobiSlamFrom = new Vector2(-60f, -40f);
    [Tooltip("Slam: 자리 잡을 때 살짝 작아졌다 돌아오는 정도 (0.06 = 94%까지). 쫀득한 반동")]
    [Range(0f, 0.3f)] public float MobiSlamUndershoot = 0.06f;
    [Tooltip("사라지는 동안 위로 뜨는 거리 (캔버스 단위, 0 = 제자리에서 사라짐)")]
    public float MobiFadeRise;
    [Tooltip("피해량 비례 크기: (이번 피해 / 같은 종류 최근 평균)^이 값. 큰 타격은 크게, 작은 타격은 작게. 0 = 끔(모두 같은 크기)")]
    [Range(0f, 2f)] public float MobiRelativeSizePower;
    [Tooltip("피해량 비례 크기의 최소·최대 배율")]
    public Vector2 MobiRelativeSizeRange = new Vector2(0.8f, 1.35f);
    [Tooltip("최근 평균에 쓰는 타격 수 (클수록 평균이 천천히 바뀜)")]
    [Min(1)] public int MobiRelativeSizeWindow = 20;
    [Tooltip("숫자 묶음 위치 = 보스 중심 + 이 값 (캔버스 단위, 1080 기준). X+ 오른쪽, Y+ 위. "
             + "가장 아래(새) 줄 위치이고 이전 줄은 위로 쌓인다. 플레이 중에 바꾸면 바로 반영된다. "
             + "쌓인 줄이 상단 HUD(TopHudRatio)에 닿으면 흐려진다.")]
    public Vector2 MobiOffset = new Vector2(166f, -215f);

    [Header("쿠키런형 — 작은 숫자")]
    public float CookieFlushInterval = 0.15f;
    public float CookieFontSize = 40f;
    [Tooltip("치명타 숫자 크기 배율 (일반 = 1)")]
    public float CookieCriticalScale = 1.5f;
    [Tooltip("화상 숫자 크기 배율 (일반 = 1)")]
    public float CookieBurnScale = 1.5f;
    [Range(0f, 1f)] public float CookieAlpha = 0.85f;
    public float CookieLifetime = 0.6f;
    [Tooltip("떠오르는 거리 (캔버스 단위)")]
    public float CookieRise = 36f;
    public float CookiePopSeconds = 0.08f;
    public float CookiePopOvershoot = 0.3f;
    [Tooltip("보스 스프라이트 안에서 흩어지는 범위 (가로, 세로 비율)")]
    public Vector2 CookieScatter = new Vector2(0.7f, 0.55f);
    [Min(1)] public int CookieMaxPopups = 14;

    [Header("샘플 재생 (보스를 기다리지 않고 비교용)")]
    public float SampleSeconds = 3f;
    [Tooltip("에디터 전용: 켜 두면 샘플 재생을 누른 뒤 멈추지 않고 계속 재생한다 (위치·크기 조정용). 빌드에서는 무시")]
    public bool SampleLoopInEditor;
    public float SampleHitInterval = 0.07f;
    public float SampleNormalDamage = 1200f;
    public float SampleCriticalMultiplier = 2.5f;
    public float SampleBurnDamage = 300f;
    [Range(0f, 1f)] public float SampleCriticalChance = 0.2f;
    [Range(0f, 1f)] public float SampleBurnChance = 0.12f;
    [Range(0f, 1f)] public float SampleDamageSpread = 0.3f;
}
