using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

/// <summary>지정한 씬에 존재하는 선택적 컴포넌트를 비활성 오브젝트까지 DI에 등록한다.</summary>
public static class SceneComponentRegistration
{
    /// <summary>다른 씬의 컴포넌트를 가져오지 않고 기존 배치와 직렬화 참조를 유지한다.</summary>
    public static void RegisterOptional<T>(IContainerBuilder builder, Scene scene) where T : Component
    {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var component in root.GetComponentsInChildren<T>(true))
            {
                // 기존 씬 객체의 수명은 씬이 소유한다. 같은 타입 여러 개는 Scoped로
                // 등록해야 VContainer의 Singleton 구현 타입 중복 검사를 피할 수 있다.
                builder.Register(_ => component, Lifetime.Scoped);
                // 같은 타입의 버튼이 여러 개여도 모든 인스턴스를 주입한다.
                builder.RegisterBuildCallback(resolver => resolver.Inject(component));
            }
    }
}
