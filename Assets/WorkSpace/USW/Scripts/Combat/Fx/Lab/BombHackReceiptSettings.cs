using UnityEngine;

/// <summary>수신 시안의 모양과 표시 시간. 실제 카드/공격/해킹 설정은 보유하지 않는다.</summary>
[CreateAssetMenu(menuName = "USW/Fx/Bomb Hack Receipt Settings")]
public sealed class BombHackReceiptSettings : ScriptableObject
{
    [Header("확정 B 글리치 흡수 — 개별 수신, 짧은 이동")]
    public float GlitchLeadSeconds = 0.04f;
    public float GlitchTransferSeconds = 0.22f;
    public float GlitchSettleSeconds = 0.1f;
    public float ReceiptSpacingSeconds = 0.07f;
    [Header("데이터/글리치 — 기존 낙인 색은 변경하지 않음")]
    public Color DataColor = new Color(0.38f, 0.86f, 1f);
    public Color GlitchColor = new Color(0.75f, 0.34f, 1f);
    public Color InkColor = new Color(0.05f, 0.09f, 0.16f);
    [Header("HUD 주변 크기 (캔버스 기준)")]
    public float RingRadius = 95f;
    public float GlitchWidth = 62f;
}
