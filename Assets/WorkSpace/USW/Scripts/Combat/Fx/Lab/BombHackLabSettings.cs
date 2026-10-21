using UnityEngine;

/// <summary>9122 전장 시안의 표시 설정. 실제 카드·스택 런타임을 수정하지 않는다.</summary>
[CreateAssetMenu(menuName = "USW/Fx/Bomb Hack Lab Settings")]
public sealed class BombHackLabSettings : ScriptableObject
{
    [Header("비교 조건 — 원본 자폭 프리팹에서 복사")]
    public float ChargeSeconds = 0.35f;
    public float FlightSeconds = 0.4f;
    public float FlightBend = 0.8f;
    [Header("표시 시간")]
    public float FirstLaunch = 1.1f;
    public float RepeatSeconds = 3.2f;
    public float RuptureSeconds = 0.32f;
    public float UploadSeconds = 0.65f;
    public float CompressionSeconds = 0.42f;
    public float BrandSeconds = 0.75f;
    public float HudPulseSeconds = 0.28f;
    [Header("색 — 해킹/폭발 부위만 소유, 기본 탄색을 변경하지 않음")]
    public Color RuptureColor = new Color(0.75f, 0.34f, 1f);
    public Color DataColor = new Color(0.38f, 0.86f, 1f);
    public Color InkColor = new Color(0.13f, 0.06f, 0.24f, 0.85f);
    [Header("크기 (캔버스 기준)")]
    public float ImpactSize = 62f;
    public float PacketSize = 30f;
    public float ArcPixels = 150f;
    public float CircuitWidth = 5f;
    [Header("표시 풀/밀집")]
    public int ShotCapacity = 24;
    public int MaxHudRoutes = 3;
    public int DenseOwners = 12;
    public int DronesPerOwner = 4;
    public int AmbientBeamCount = 8;
    public float DenseDetailScale = 0.65f;
    public int StartStacks = 12;
    [Header("연속 발사 비교 — 실제 공격 주기 아님")]
    public float PressureShotInterval = 0.1f;
}
