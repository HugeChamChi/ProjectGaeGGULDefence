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

    protected override void Configure(IContainerBuilder builder)
    {
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
        builder.Register<GlobalUIManager>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
        
        builder.Register<AssetLifecycleManager>(Lifetime.Singleton);
        builder.Register<SceneChangeManager>(Lifetime.Singleton);
        var debuffSettings = Resources.Load<DebuffSettings>("DebuffSettings");
        if (debuffSettings == null) throw new System.InvalidOperationException("Resources/DebuffSettings is required.");
        builder.RegisterInstance(debuffSettings);
        builder.Register<DebuffCatalog>(Lifetime.Singleton);
        builder.Register<GameDataManager>(Lifetime.Singleton);

        // 정적 주입 (UI_Base 전체가 하나의 AudioManager를 공유하도록 명시적 주입)
        builder.RegisterBuildCallback(resolver =>
        {
            var audio = resolver.Resolve<AudioManager>();
            UI_Base.Inject(audio);
        });
    }
}
