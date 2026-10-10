using UnityEngine;

public class UnitDependencies
{
    /// <summary>Scene countdown used by time-recovery skills.</summary>
    public TimerController Timer { get; set; }
    /// <summary>현재 전투에 고정된 영구 강화 수치.</summary>
    public ResearchRunBonuses Research { get; set; }
    /// <summary>Scene terminal state, shared by food production and combat callbacks.</summary>
    public GameManager GameManager { get; set; }
    public GameDataManager GameDataManager { get; set; }
    public GridManager GridManager { get; set; }
    public UpgradeManager UpgradeManager { get; set; }
    /// <summary>선택지의 읽기 전용 전투 보정.</summary>
    public ISelectionCombatReader SelectionCombat { get; set; }
    /// <summary>드론 기능별 설정.</summary>
    public IDroneEffectReader DroneEffects { get; set; }
    /// <summary>지속 효과가 확정된 후의 변경 알림.</summary>
    public ISelectionEffectChanges SelectionChanges { get; set; }
    /// <summary>씬의 기본 전투 설정.</summary>
    public CombatSettings CombatSettings { get; set; }
    public TotemBuffManager TotemBuffManager { get; set; }
    public CurrencyFloaterManager CurrencyFloaterManager { get; set; }
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
