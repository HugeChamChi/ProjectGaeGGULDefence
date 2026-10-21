using UnityEngine;
using VContainer;

/// <summary>Starts the standalone hard-boss scene without requiring a signed-in research save in Editor.</summary>
[DefaultExecutionOrder(-7900)]
public sealed class HardIngameEntry : MonoBehaviour
{
    [SerializeField] private InGameLifetimeScope _scope;
    private void Awake()
    {
#if UNITY_EDITOR
        _scope.Container.Resolve<ResearchRunBonuses>().PrepareEditorPreview();
#endif
    }
}
