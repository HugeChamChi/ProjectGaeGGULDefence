using UnityEngine;
using VContainer;

/// <summary>Keeps direct Editor Play isolated; lobby entry uses real research, stamina and rewards.</summary>
[DefaultExecutionOrder(-7900)]
public sealed class HardIngameEntry : MonoBehaviour
{
    [SerializeField] private InGameLifetimeScope _scope;
    private void Awake()
    {
#if UNITY_EDITOR
        if (!_scope.Container.Resolve<SceneChangeManager>().IsTransitioning)
        {
            gameObject.AddComponent<IngameEditorPreview>().Configure(_scope.Container.Resolve<WaveManager>());
            _scope.Container.Resolve<ResearchRunBonuses>().PrepareEditorPreview();
            _scope.Container.Resolve<GameManager>().EnableEditorPreview();
        }
#endif
    }
}
