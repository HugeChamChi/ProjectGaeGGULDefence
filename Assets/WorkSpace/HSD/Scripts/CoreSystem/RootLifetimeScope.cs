using UnityEngine;
using VContainer;
using VContainer.Unity;

public class RootLifetimeScope : LifetimeScope
{
    [Header("UI Settings")]
    [Tooltip("IFadeScreen을 상속받은 페이드 화면 프리팹 (지정하지 않으면 런타임에 기본 검은 화면을 자동 생성합니다)")]
    [SerializeField] private FadeScreen fadeScreenPrefab;

    [Header("Managers")]
    [SerializeField] private AudioManager audioManager;
    [SerializeField] private ResearchTreeData _researchTree;

    [Header("Tutorial")]
    [UnityEngine.Serialization.FormerlySerializedAs("tutorialManagerPrefab")]
    [SerializeField] private GaeGGUL.Tutorial.TutorialManager _tutorialManagerPrefab;

    protected override void Configure(IContainerBuilder builder)
    {
        if (_tutorialManagerPrefab != null)
            builder.RegisterComponentInNewPrefab(_tutorialManagerPrefab, Lifetime.Singleton).UnderTransform(transform);
        else
            builder.RegisterComponentOnNewGameObject<GaeGGUL.Tutorial.TutorialManager>(Lifetime.Singleton, "TutorialManager").UnderTransform(transform);
        if (fadeScreenPrefab != null)
        {
            builder.RegisterComponentInNewPrefab(fadeScreenPrefab, Lifetime.Singleton).As<IFadeScreen>();
        }
        else
        {
            builder.Register<DefaultFadeScreen>(Lifetime.Singleton).As<IFadeScreen>();
        }

        if (audioManager != null)
        {
            builder.RegisterComponent(audioManager);
        }

        builder.Register<BackendGameData>(Lifetime.Singleton).AsSelf();
        builder.Register<BackendSession>(Lifetime.Singleton);
        builder.Register<AppInitialization>(Lifetime.Singleton);
        builder.Register<GaeGGUL.Tutorial.IngameTutorialProgress>(Lifetime.Singleton);
        if (_researchTree == null) throw new System.InvalidOperationException("Root research tree is required.");
        builder.RegisterInstance(_researchTree);
        builder.RegisterInstance(Player.PlayerData);
        builder.Register<GamePresentationSettings>(Lifetime.Singleton);
        builder.Register<PlayerPrefsResearchSaveStore>(Lifetime.Singleton).As<IResearchSaveStore>();
        builder.Register<ResearchAccountContext>(Lifetime.Singleton);
        builder.Register<ResearchSaveService>(Lifetime.Singleton);
        builder.Register<GlobalUIManager>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
        
        builder.Register<AssetLifecycleManager>(Lifetime.Singleton);
        builder.Register<SceneChangeManager>(Lifetime.Singleton);
        var debuffSettings = Resources.Load<DebuffSettings>("DebuffSettings");
        if (debuffSettings == null) throw new System.InvalidOperationException("Resources/DebuffSettings is required.");
        builder.RegisterInstance(debuffSettings);
        builder.Register<DebuffCatalog>(Lifetime.Singleton);
        builder.Register<DebuffInfoPresenter>(Lifetime.Singleton);
        builder.Register<GameDataManager>(Lifetime.Singleton);

        // 정적 주입 (UI_Base 전체가 하나의 AudioManager를 공유하도록 명시적 주입)
        builder.RegisterBuildCallback(resolver =>
        {
            var audio = resolver.Resolve<AudioManager>();
            UI_Base.Inject(audio);
            resolver.Inject(Player.Mail);
            resolver.Inject(Table.Coupon);
            Player.Inject(resolver.Resolve<BackendGameData>());
        });
    }
}
