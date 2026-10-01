using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>씬 수명 서비스에 해당 씬의 현재 컴포넌트를 제공한다. 동적으로 생성된 이펙트도 포함한다.</summary>
public sealed class SceneComponentCollection
{
    private readonly Scene _scene;
    /// <summary>LifetimeScope가 소유한 씬을 명시적으로 전달한다.</summary>
    public SceneComponentCollection(Scene scene) => _scene = scene;

    /// <summary>전역 검색 없이 지정된 씬의 컴포넌트를 열거한다.</summary>
    public IEnumerable<T> Enumerate<T>(bool includeInactive = true) where T : Component
    {
        if (!_scene.IsValid() || !_scene.isLoaded) yield break;
        foreach (var root in _scene.GetRootGameObjects())
            foreach (var component in root.GetComponentsInChildren<T>(includeInactive))
                if (includeInactive || component.gameObject.activeInHierarchy) yield return component;
    }
}
