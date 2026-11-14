using Spine.Unity;
using TMPro;
using UnityEngine;

/// <summary>Authored Deertree pattern values and installed Spine references. The in-game asset (Data/BossData/Deertree) takes its
/// fight numbers from the BossPatterns chart (value list: BossPatterns.DeertreeValues); the lab asset keeps bundled values.</summary>
[CreateAssetMenu(menuName = "Game/Deertree Boss Settings")]
public sealed class DeertreeBossSettings : ScriptableObject
{
    public SkeletonDataAsset Boss;
    public SkeletonDataAsset Root;
    public SkeletonDataAsset Branch;
    public SkeletonDataAsset Curse;
    [Min(.1f)] public float RootCooldown = 5f;
    [Range(0, 1)] public float FruitThreshold = .1f;
    [Range(0, 1)] public float CurseThreshold = .5f;
    [Min(.1f)] public float FruitDuration = 5f;
    [Min(.1f)] public float CurseDuration = 5f;
    [Range(0, 1)] public float HealPerApple = .1f;
    [Min(1)] public int AppleTaps = 3;
    [Min(1)] public int CurseTaps = 5;
    [Min(1)] public int CurseTargets = 3;
    [Header("Tutorial practice only (disabled on production settings)")]
    public bool CurseOnlyPractice;
    [Tooltip("Offline preview timing estimate, measured from skill start to effect appearance.")]
    [Min(0)] public float RootWindup = 1.2f;
    [Min(0)] public float FruitWindup = 1.2f;
    [Min(0)] public float CurseWindup = 1.2f;
    [Min(0)] public float BranchReturnDuration = .6f;
    [Header("Spine pose timing")]
    [Min(.01f)] public float BranchGrowDuration = 2.5f;
    [Min(0)] public float BranchHoldAt = 2.5f;
    [Min(0)] public float BranchReturnAt = 4f;
    [Min(0)] public float RootHoldAt = 1.2f;
    [Min(0)] public float VineHoldAt = 1.2f;
    [Min(0)] public float FruitBossHoldAt = 1.5f;
    [Min(0)] public float FruitBossLoopEnd = 1.9f;
    [Min(.1f)] public float FruitBossLoopPeriod = 1.8f;
    [Min(.01f)] public float FruitBossReturnBlend = .16f;
    [Min(0)] public float FruitBossReturnAt = 3.2f;
    [Min(0)] public float CurseBossHoldAt = 1.2f;
    [Min(0)] public float CurseBossLoopEnd = 1.6f;
    [Min(.1f)] public float CurseBossLoopPeriod = 1.8f;
    [Min(.01f)] public float CurseBossReturnBlend = .16f;
    [Min(0)] public float CurseBossReturnAt = 2.6f;
    [Min(.01f)] public float CurseRecoveryDuration = .4f;
    [Header("Touch target material feedback (apples and curse bindings)")]
    public Shader AppleShader;
    public Color AppleHighlight = new Color(1f, .96f, .86f, 1f);
    [Range(0, 1)] public float AppleGlow = .18f;
    [Range(0, 1)] public float BindingGlow = .22f;
    [Min(.1f)] public float AppleScale = 1.35f;
    [Header("In-game layout (BossDeertree, world units relative to the boss root)")]
    public Vector2 BossCenter = new Vector2(0f, 0f);
    [Min(.1f)] public float BossWidth = 9f;
    public Vector2 BranchCenter = new Vector2(0f, -2.2f);
    [Min(.1f)] public float BranchWidth = 9f;
    [Tooltip("Root and binding width as a fraction of one grid tile width.")]
    [Min(.1f)] public float RootWidthPerCell = .8f;
    [Min(.1f)] public float VineWidthPerCell = .82f;
    [Tooltip("Apple touch radius in pixels at a 1920 pixel tall screen.")]
    [Min(1f)] public float AppleTouchPixels = 80f;
    [Header("Root sale badge (shown over a selected root)")]
    public TMP_FontAsset SellFont;
    public string SellLabel = "\uD310\uB9E4 0";
    public Color SellBackground = new Color(.1f, .16f, .15f, .92f);
}
