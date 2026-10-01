using UnityEngine;

public class UnitDependencies
{
    /// <summary>현재 전투에 고정된 영구 강화 수치.</summary>
    public ResearchRunBonuses Research { get; set; }
    /// <summary>Scene terminal state, shared by food production and combat callbacks.</summary>
    public GameManager GameManager { get; set; }
    public GameDataManager GameDataManager { get; set; }
    public GridManager GridManager { get; set; }
    public UpgradeManager UpgradeManager { get; set; }
    public LevelUpManager LevelUpManager { get; set; }
    public TotemBuffManager TotemBuffManager { get; set; }
    public CurrencyFloaterManager CurrencyFloaterManager { get; set; }
    public ChieftainSpawner ChieftainManager { get; set; }
    public BossManager BossManager { get; set; }
    public ProjectilePool ProjectileManager { get; set; }
    public AudioManager AudioManager { get; set; }
    public CurrencyManager CurrencyManager { get; set; }
    public BuffManager BuffManager { get; set; }
    /// <summary>씬 선택 화면의 공통 공격 보류 상태.</summary>
    public FieldPauseVisuals FieldPause { get; set; }
    /// <summary>Scene run values shared by combat, new units and live information UI.</summary>
    public IRunStatModifiers RunStatModifiers { get; set; }
    /// <summary>Reports unrepresentable run stats to the progression owner.</summary>
    public System.Action<string> ReportRunStatFailure { get; set; }
}
