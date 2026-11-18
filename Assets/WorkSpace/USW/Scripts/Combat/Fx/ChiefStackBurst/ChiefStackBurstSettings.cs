using UnityEngine;

/// <summary>
/// 9105 긴급 교신: 족장 스킬이 보스의 해킹 스택을 전량 소모하는 순간의 연출 설정.
/// </summary>
[CreateAssetMenu(fileName = "ChiefStackBurstSettings", menuName = "Game/Fx/Chief Stack Burst Settings")]
public sealed class ChiefStackBurstSettings : ScriptableObject
{
    [Header("재질 (해킹 표식과 같은 것)")]
    public Material GlowMaterial;
    public Material RingMaterial;
    public Material LineMaterial;

    [Header("색")]
    public Color Tint = new Color(.38f, .86f, 1f, 1f);
    public Color CoreColor = Color.white;

    [Header("표식")]
    [Min(0f)] public float MarkGlowSize = .75f;
    [Min(0f)] public float MarkCoreSize = .22f;
    [Tooltip("표식 위치를 받지 못했을 때 착탄점 둘레에 표식을 대신 놓는 반지름")]
    [Min(0f)] public float FallbackRadius = .9f;
    [Tooltip("표식 1개가 대표하는 스택 수 (해킹 데이터 StacksPerMark와 같게)")]
    [Min(1)] public int StacksPerMark = 5;

    [Header("연쇄")]
    [Min(.01f)] public float ChainSpacing = .055f;
    [Min(0f)] public float ChainLinkSeconds = .12f;

    [Header("표식 하나 터짐")]
    [Min(.01f)] public float PopSeconds = .3f;
    [Min(0f)] public float PopRing = 1.1f;

    [Header("정렬")]
    public string SortingLayer = "FX";
    public int SortingOrder = 60;
}
