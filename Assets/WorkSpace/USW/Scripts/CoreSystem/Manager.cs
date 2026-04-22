using UnityEngine;

/// <summary>
/// 모든 매니저의 전역 접근 통로 (정적 클래스)
/// 
/// 사용법:
///   Manager.UI.UpdateBossHp(cur, max);
///   Manager.Currency.AddCurrency(100f);
///   Manager.Wave.OnAllBossesDefeated();
/// 
/// 직접 싱글톤 호출(UIManager.Instance.xxx) 대신 반드시 이 클래스를 통해 접근
/// → 나중에 DontDestroyOnLoad / Addressable 방식으로 바꿔도 호출부 수정 없음
/// </summary>
public static class Manager
{
    // ── 게임 흐름 ──────────────────────────────────────────────
    public static GameManager      Game       => GameManager.Instance;
    public static WaveManager      Wave       => WaveManager.Instance;
    public static TimerController  Timer      => TimerController.Instance;

    // ── 경제 / 경험치 ──────────────────────────────────────────
    public static CurrencyManager  Currency   => CurrencyManager.Instance;
    public static ExpManager       Exp        => ExpManager.Instance;

    // ── 그리드 / 유닛 ──────────────────────────────────────────
    public static GridManager      Grid       => GridManager.Instance;
    public static UnitFactory      UnitFactory => UnitFactory.Instance;
    public static UnitSpawner      Spawner    => UnitSpawner.Instance;

    // ── 보스 / 토템 ────────────────────────────────────────────
    public static BossManager      Boss       => BossManager.Instance;
    public static TotemSpawner     Totem      => TotemSpawner.Instance;
    public static TotemBuffManager Buff       => TotemBuffManager.Instance;

    // ── 합성 / 레벨업 ──────────────────────────────────────────
    public static MergeManager     Merge      => MergeManager.Instance;
    public static LevelUpManager   LevelUp    => LevelUpManager.Instance;

    // ── UI ─────────────────────────────────────────────────────
    public static UIManager        UI         => UIManager.Instance;
    public static LevelUpUI        LevelUpUI  => LevelUpUI.Instance;

    // ── 투사체 ─────────────────────────────────────────────────
    public static ProjectilePool   Projectile => ProjectilePool.Instance;
}
