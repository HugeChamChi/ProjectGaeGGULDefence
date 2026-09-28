using VContainer;
using VContainer.Unity;

/// <summary>단독 강화 씬도 프로젝트 RootLifetimeScope의 저장 서비스를 주입받는다.</summary>
public sealed class ResearchLifetimeScope : LifetimeScope
{
    /// <inheritdoc />
    protected override void Configure(IContainerBuilder builder)
    {
        builder.RegisterComponentInHierarchy<ResearchScreen>();
    }
}
