using UnityEngine;

public class UnitDependencies
{
    public GameDataManager GameDataManager { get; set; }
    public PopulationManager PopulationManager { get; set; }
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
}
