using VContainer;
using VContainer.Unity;
using UnityEngine;

public class TitleLifetimeScope : LifetimeScope
{
    [Header("Views")]
    [Tooltip("타이틀 씬의 UI 요소들을 관리하는 View 컴포넌트")]
    [SerializeField] private TitleView titleView;

    protected override void Configure(IContainerBuilder builder)
    {
        var presenterRegistration = builder.RegisterEntryPoint<TitlePresenter>();
        
        if (titleView != null)
        {
            presenterRegistration.WithParameter(titleView);
        }
    }
}
