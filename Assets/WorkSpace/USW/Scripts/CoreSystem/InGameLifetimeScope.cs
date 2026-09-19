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
        builder.RegisterComponentInHierarchy<ExpEffectController>();
        builder.RegisterComponentInHierarchy<ExpBarUI>();

        builder.RegisterComponentInHierarchy<GridManager>();
        builder.RegisterComponentInHierarchy<UnitFactory>();
        builder.RegisterComponentInHierarchy<UnitSpawner>();

        builder.RegisterComponentInHierarchy<BossManager>();
        builder.RegisterComponentInHierarchy<BossPatternController>();
        builder.RegisterComponentInHierarchy<TotemSpawner>();
        var totemInteractionSettings = Resources.Load<TotemInteractionSettings>("TotemInteractionSettings");
        if (totemInteractionSettings == null) throw new System.InvalidOperationException("TotemInteractionSettings is required.");
        builder.RegisterInstance(totemInteractionSettings);
        builder.Register<TotemInventory>(Lifetime.Scoped);
        builder.RegisterComponentInHierarchy<TotemRotationUI>();
        builder.RegisterComponentInHierarchy<TotemInventoryUI>();
        builder.RegisterComponentInHierarchy<TotemBuffManager>();
        builder.RegisterComponentInHierarchy<BuffManager>();

        builder.RegisterComponentInHierarchy<ChieftainSpawner>();

        builder.RegisterComponentInHierarchy<MergeManager>();
        builder.RegisterComponentInHierarchy<LevelUpManager>();

        builder.RegisterComponentInHierarchy<UIManager>();
        builder.RegisterComponentInHierarchy<DamageFloaterManager>();
        builder.RegisterComponentInHierarchy<CurrencyFloaterManager>();

        builder.RegisterComponentInHierarchy<ProjectilePool>();
        builder.RegisterComponentInHierarchy<AudioManager>();
        var upgradeKeyOverrides = Resources.Load<UpgradeKeyOverrides>("UpgradeKeyOverrides");
        if (upgradeKeyOverrides == null) throw new System.InvalidOperationException("UpgradeKeyOverrides is required.");
        builder.RegisterInstance(upgradeKeyOverrides);
        var upgradeSettings = Resources.Load<UpgradeSettings>("UpgradeSettings");
        if (upgradeSettings == null) throw new System.InvalidOperationException("UpgradeSettings is required.");
        builder.RegisterInstance(upgradeSettings);
        builder.RegisterComponentInHierarchy<UpgradeManager>();
        builder.RegisterComponentInHierarchy<InputManager>();

        if (isDron)
        {
            builder.RegisterComponentInHierarchy<DroneManager>();
        }

        builder.RegisterEntryPoint<AlphanActiveSkill>(Lifetime.Scoped).AsSelf();
        builder.RegisterEntryPoint<GameInitializer>();

        builder.RegisterBuildCallback(resolver =>
        {
            GlobalResolver = resolver;
        });
    }
}
