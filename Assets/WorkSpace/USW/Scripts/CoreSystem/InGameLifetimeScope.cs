using UnityEngine;
using VContainer;
using VContainer.Unity;

[DefaultExecutionOrder(-8000)]
public class InGameLifetimeScope : LifetimeScope
{
    public static IObjectResolver GlobalResolver { get; private set; }
    [SerializeField] private bool isDron;

    protected override void Configure(IContainerBuilder builder)
    {
        // 씬에 이미 배치되어 있는 매니저(MonoBehaviour)들을 찾아서 모두 등록합니다.
        builder.RegisterComponentInHierarchy<GameManager>();
        builder.RegisterComponentInHierarchy<WaveManager>();
        builder.RegisterComponentInHierarchy<TimerController>();

        builder.RegisterComponentInHierarchy<CurrencyManager>();
        builder.RegisterComponentInHierarchy<ExpManager>();

        builder.RegisterComponentInHierarchy<GridManager>();
        builder.RegisterComponentInHierarchy<UnitFactory>();
        builder.RegisterComponentInHierarchy<UnitSpawner>();
        builder.RegisterComponentInHierarchy<PopulationManager>();

        builder.RegisterComponentInHierarchy<BossManager>();
        builder.RegisterComponentInHierarchy<TotemSpawner>();
        builder.RegisterComponentInHierarchy<TotemBuffManager>();

        builder.RegisterComponentInHierarchy<ChieftainSpawner>();

        builder.RegisterComponentInHierarchy<MergeManager>();
        builder.RegisterComponentInHierarchy<LevelUpManager>();

        builder.RegisterComponentInHierarchy<UIManager>();
        builder.RegisterComponentInHierarchy<DamageFloaterManager>();
        builder.RegisterComponentInHierarchy<CurrencyFloaterManager>();

        builder.RegisterComponentInHierarchy<ProjectilePool>();
        builder.RegisterComponentInHierarchy<AudioManager>();
        builder.RegisterComponentInHierarchy<UpgradeManager>();

        if (isDron)
        {
            builder.RegisterComponentInHierarchy<DroneManager>();
        }

        builder.RegisterEntryPoint<GameInitializer>();

        builder.RegisterBuildCallback(resolver =>
        {
            GlobalResolver = resolver;
        });
    }
}
