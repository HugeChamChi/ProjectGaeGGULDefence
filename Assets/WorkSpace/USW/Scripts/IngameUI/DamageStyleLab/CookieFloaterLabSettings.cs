using System;
using TMPro;
using UnityEngine;

/// <summary>
/// 쿠키런 크럼블형 작은 데미지 숫자 실험실(FxLab_CookieFloater) 설정 — 후보 여러 개를 같은 타격 흐름에 띄워 비교한다.
/// 플레이 중 인스펙터에서 바꾸면 바로 반영되고 에셋에 남는다. 크기는 1080 기준 캔버스 단위, 시간은 초.
/// 실험실 전용 — 인게임 BossDamageNumbers와 무관.
/// </summary>
[CreateAssetMenu(fileName = "CookieFloaterLabSettings", menuName = "USW/UI/Cookie Floater Lab Settings")]
public class CookieFloaterLabSettings : ScriptableObject
{
    /// <summary>숫자가 움직이는 방향.</summary>
    public enum Motion { Up, Diagonal }

    /// <summary>후보 하나.</summary>
    [Serializable]
    public class Variant
    {
        public string Name = "후보";

        [Header("움직임")]
        public Motion Motion = Motion.Up;
        [Tooltip("대각선 각도 (세로 기준, 도). 무작위 방향이 아니라 보스 중심보다 왼쪽에서 뜬 숫자는 왼쪽 위로, 오른쪽은 오른쪽 위로 고정")]
        [Range(0f, 80f)] public float DiagonalAngle = 35f;
        [Tooltip("떠 있는 시간")]
        public float Lifetime = 0.6f;
        [Tooltip("수명 동안 이동하는 거리 (캔버스 단위)")]
        public float Rise = 36f;
        [Tooltip("이동 곡선. 1 = 등속, 클수록 처음에 확 튀어 오르고 끝에서 느려진다")]
        [Min(1f)] public float RiseEasePower = 2f;
        [Tooltip("이 비율부터 흐려진다 (0 = 나오자마자 흐려지기 시작)")]
        [Range(0f, 1f)] public float FadeStart = 0.5f;
        public float PopSeconds = 0.08f;
        [Tooltip("등장할 때 순간적으로 커지는 비율")]
        public float PopOvershoot = 0.3f;

        [Header("모양")]
        public float FontSize = 80f;
        public float CriticalScale = 1.5f;
        public float BurnScale = 1.5f;
        [Range(0f, 1f)] public float Alpha = 0.85f;
        [Tooltip("같은 종류 피해를 이 시간 동안 합쳐 숫자 하나로 띄운다 (쿠키런형 묶음)")]
        public float FlushInterval = 0.15f;
        [Tooltip("동시에 떠 있을 수 있는 숫자 수. 넘치면 가장 오래된 숫자를 재사용")]
        [Range(1, 32)] public int MaxPopups = 14;

        [Header("뜨는 범위")]
        [Tooltip("보스 스프라이트 영역 중 숫자가 뜨는 범위 (가로, 세로 비율)")]
        public Vector2 Scatter = new Vector2(0.7f, 0.55f);

        [Header("중앙 영역 (옅게)")]
        [Tooltip("켜면 뜨는 범위 가운데에 '중앙 영역'을 두고, 일부 숫자를 일부러 그 안에 옅게 배정한다. 나머지는 바깥 테두리에 진하게")]
        public bool UseCenterZone;
        [Tooltip("중앙 영역 크기 (뜨는 범위 대비 가로, 세로 비율)")]
        public Vector2 CenterZone = new Vector2(0.45f, 0.45f);
        [Tooltip("중앙 영역에 배정할 숫자 비율")]
        [Range(0f, 1f)] public float CenterShare = 0.4f;
        [Tooltip("중앙 영역 숫자 투명도")]
        [Range(0f, 1f)] public float CenterAlpha = 0.3f;
        [Tooltip("중앙 영역 숫자 크기 배율")]
        public float CenterScale = 0.8f;
        [Tooltip("바깥 숫자 크기 배율")]
        public float OuterScale = 1.05f;
        [Tooltip("치명타·화상은 항상 바깥에 (중앙 옅은 숫자에 묻히지 않게)")]
        public bool StrongAlwaysOuter = true;
    }

    [Header("후보 (위·아래 버튼 순서)")]
    public Variant[] Variants = Array.Empty<Variant>();

    [Header("뜨는 범위 (공통)")]
    [Tooltip("켜면 가로는 보스 폭 대신 화면 폭 전체(1080)를 쓴다 (Variant.Scatter.x 무시). 숫자가 화면 밖으로 잘리지 않게 글자 폭·대각선 이동만큼 안쪽으로 맞춘다")]
    public bool FullScreenWidth = true;
    [Tooltip("화면 양 끝 여백 (캔버스 단위)")]
    public float ScreenEdgePadding = 12f;

    [Header("글꼴·색 (인게임 BossDamageNumberSettings에서 복사)")]
    public TMP_FontAsset Font;
    public Material FontMaterial;
    public DamageStyleLabSettings.Gradient2 NormalColor = new DamageStyleLabSettings.Gradient2(new Color(1f, 0.84f, 0.62f), new Color(1f, 0.62f, 0.38f));
    public DamageStyleLabSettings.Gradient2 CriticalColor = new DamageStyleLabSettings.Gradient2(new Color(0.95f, 0.26f, 0.1f), new Color(1f, 0.62f, 0.24f));
    public DamageStyleLabSettings.Gradient2 BurnColor = new DamageStyleLabSettings.Gradient2(new Color(1f, 0.36f, 0.3f), new Color(0.84f, 0.06f, 0.1f));

    [Header("범위 표시선")]
    public Color ScatterGuideColor = new Color(1f, 1f, 1f, 0.35f);
    public Color CenterGuideColor = new Color(0.4f, 0.9f, 1f, 0.6f);
    public float GuideThickness = 3f;
}
