using TMPro;
using UnityEngine;

/// <summary>
/// 9105 긴급 교신: 족장 스킬이 보스의 해킹 스택을 전량 소모하는 순간의 연출 설정.
/// 시안 세 가지를 같은 설정에서 고른다(실험실에서 비교 후 하나만 남긴다).
/// </summary>
[CreateAssetMenu(fileName = "ChiefStackBurstSettings", menuName = "Game/Fx/Chief Stack Burst Settings")]
public sealed class ChiefStackBurstSettings : ScriptableObject
{
    /// <summary>표식이 사라지는 방식.</summary>
    public enum BurstStyle
    {
        /// <summary>A: 표식이 빔 착탄점으로 빨려 들어가 모인 뒤 한 번에 터진다.</summary>
        Siphon = 0,
        /// <summary>B: 바깥 표식부터 번개로 이어지며 차례로 터지고 마지막에 착탄점이 터진다.</summary>
        Chain = 1,
        /// <summary>C: 모든 표식이 동시에 크게 터지고 빛줄기가 착탄점으로 꽂힌다.</summary>
        Volley = 2,
    }

    [Header("시안")]
    public BurstStyle Style = BurstStyle.Siphon;

    [Header("재질 (해킹 표식과 같은 것)")]
    public Material GlowMaterial;
    public Material RingMaterial;
    public Material LineMaterial;

    [Header("색")]
    public Color Tint = new Color(.38f, .86f, 1f, 1f);
    public Color HotTint = new Color(.62f, .5f, 1f, 1f);
    public Color CoreColor = Color.white;

    [Header("표식")]
    [Min(0f)] public float MarkGlowSize = .75f;
    [Min(0f)] public float MarkCoreSize = .22f;
    [Tooltip("표식 위치를 받지 못했을 때 착탄점 둘레에 표식을 대신 놓는 반지름")]
    [Min(0f)] public float FallbackRadius = .9f;
    [Tooltip("표식 1개가 대표하는 스택 수 (해킹 데이터 StacksPerMark와 같게)")]
    [Min(1)] public int StacksPerMark = 5;

    [Header("A 흡수")]
    [Min(0f)] public float SiphonStagger = .035f;
    [Min(.01f)] public float SiphonTravel = .24f;
    [Tooltip("이동 경로가 옆으로 휘는 정도 (거리 비율)")]
    public float SiphonCurve = .35f;

    [Header("B 연쇄")]
    [Min(.01f)] public float ChainSpacing = .055f;
    [Min(0f)] public float ChainLinkSeconds = .12f;

    [Header("C 일제")]
    [Min(0f)] public float VolleyDelay = .14f;

    [Header("표식 하나 터짐")]
    [Min(.01f)] public float PopSeconds = .3f;
    [Min(0f)] public float PopRing = 1.1f;

    [Header("큰 폭발")]
    [Tooltip("끄면 착탄점의 모임 빛, 번개, 큰 폭발을 그리지 않는다(표식 폭발과 숫자만). 효과음 시점은 마지막 표식 직후")]
    public bool ImpactBurst = true;
    [Min(.01f)] public float BurstSeconds = .55f;
    [Min(0f)] public float BurstRing = 3.4f;
    [Min(0f)] public float BurstFlash = 4.2f;
    [Range(0, 32)] public int SparkCount = 18;
    [Min(0f)] public float SparkReach = 2.2f;

    [Header("숫자")]
    [Tooltip("끄면 소모 스택 숫자(HACK 50)를 띄우지 않는다")]
    public bool ShowCount = true;
    public TMP_FontAsset Font;
    [Tooltip("숫자 위에 작게 붙는 이름. 비우면 숫자만")]
    public string Label = "HACK";
    [Min(.1f)] public float TextSize = 9f;
    public Vector3 TextOffset = new Vector3(0f, 1.6f, 0f);
    [Min(0f)] public float TextHoldSeconds = .55f;

    [Header("정렬")]
    public string SortingLayer = "FX";
    public int SortingOrder = 60;
}
