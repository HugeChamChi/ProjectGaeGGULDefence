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
        builder.RegisterInstance(new SceneComponentCollection(gameObject.scene));
        builder.Register<ResearchRunBonuses>(Lifetime.Scoped);
        builder.RegisterEntryPoint<GaeGGUL.Tutorial.TutorialSceneBinding>();
        SceneComponentRegistration.RegisterOptional<GaeGGUL.Tutorial.TutorialActor>(builder, gameObject.scene);
        builder.Register<GaeGGUL.Extension.GridCellExtension>(Lifetime.Scoped);
        // 씬에 이미 배치되어 있는 매니저(MonoBehaviour)들을 찾아서 모두 등록합니다.
        builder.Register<TimeScaleService>(Lifetime.Scoped); // 게임 속도 단일 소유자 — Time.timeScale 직접 쓰기 금지
        builder.Register<DefeatPresentation>(Lifetime.Scoped);
        builder.Register<FieldPauseVisuals>(Lifetime.Scoped); // 선택 화면 정지 중 대기 모션·이펙트만 실제 시간으로
        builder.Register<EndlessRandom>(Lifetime.Scoped).As<IEndlessRandom>();
        builder.Register<EndlessRunService>(Lifetime.Scoped).AsSelf().As<IRunStatModifiers>();
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
        builder.RegisterEntryPoint<TotemHoldFeedback>(Lifetime.Scoped).AsSelf(); // 토템 홀드 게이지 + 슬로우
        builder.Register<TotemDragRangePreview>(Lifetime.Scoped); // 토템 이동 중 범위 미리보기
        builder.RegisterComponentInHierarchy<TotemInventoryUI>();
        builder.RegisterComponentInHierarchy<TotemBuffManager>();
        builder.RegisterComponentInHierarchy<BuffManager>();

        builder.RegisterComponentInHierarchy<ChieftainSelection>();

        builder.RegisterComponentInHierarchy<MergeManager>();
        var mergeEffectSettings = Resources.Load<MergeEffectSettings>("MergeEffectSettings");
        if (mergeEffectSettings == null) throw new System.InvalidOperationException("MergeEffectSettings is required.");
        builder.RegisterInstance(mergeEffectSettings);
        builder.Register<MergeEffectPlayer>(Lifetime.Scoped); // 합성 연출 (잔상 흡입 + 먼지구름)
        builder.Register<DragSellService>(Lifetime.Scoped); // 드래그 판매 (판매 띠가 있는 씬에서만 동작)
        builder.RegisterComponentInHierarchy<LevelUpManager>();

        builder.RegisterComponentInHierarchy<UIManager>();
        SceneComponentRegistration.RegisterOptional<CenterToast>(builder, gameObject.scene);
        builder.RegisterComponentInHierarchy<DamageFloaterManager>();
        SceneComponentRegistration.RegisterOptional<BossDamageNumbers>(builder, gameObject.scene);
        SceneComponentRegistration.RegisterOptional<HSD.UI.Effect.UI_ChiefSkillEffect>(builder, gameObject.scene);
        SceneComponentRegistration.RegisterOptional<HSD.InGameDebug.UI_IngameDebugPanel>(builder, gameObject.scene);
        SceneComponentRegistration.RegisterOptional<HSD.InGameDebug.UI_OpenDebugButton>(builder, gameObject.scene);
        SceneComponentRegistration.RegisterOptional<HSD.UI.Upgrade.UI_UpgradePanel>(builder, gameObject.scene);
        SceneComponentRegistration.RegisterOptional<EnchantButtonOpener>(builder, gameObject.scene);
        builder.RegisterComponentInHierarchy<CurrencyFloaterManager>();

        builder.RegisterComponentInHierarchy<ProjectilePool>();
        // 앱 오디오는 부모 RootLifetimeScope의 등록을 사용한다.
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
        builder.RegisterEntryPoint<GameInitializer>().AsSelf();

        builder.RegisterBuildCallback(resolver =>
        {
            GlobalResolver = resolver;
        });
    }
}
