using System.Collections.Generic;
using UnityEngine;
using VContainer;
using VContainer.Unity;

/// <summary>Scene-owned, injected prefab instances shared by destination buttons.</summary>
public sealed class LobbyPopupHost : MonoBehaviour
{
    [SerializeField] private Canvas _canvas;
    [Inject] private IObjectResolver _resolver;
    private readonly Dictionary<Component, Component> _instances = new();
    /// <summary>Creates each prefab once under the lobby canvas, preserving its authored sorting order.</summary>
    public T GetOrCreate<T>(T prefab) where T : Component
    {
        if (prefab == null || _canvas == null || _resolver == null) return null;
        if (_instances.TryGetValue(prefab, out var existing) && existing != null) return (T)existing;
        var instance = _resolver.Instantiate(prefab, _canvas.rootCanvas.transform, false);
        var canvas = instance.GetComponent<Canvas>();
        if (canvas != null) { canvas.overrideSorting = true; canvas.sortingLayerID = _canvas.sortingLayerID; }
        _instances[prefab] = instance;
        return instance;
    }
    /// <summary>Opens a cached prefab through its existing UI_Base contract.</summary>
    public UI_Base OpenPopup(UI_Base prefab)
    {
        var instance = GetOrCreate(prefab);
        instance?.Open();
        // Enabling a previously disabled Canvas can reset its inherited sorting state.
        var canvas = instance != null ? instance.GetComponent<Canvas>() : null;
        var authoredCanvas = prefab != null ? prefab.GetComponent<Canvas>() : null;
        if (canvas != null)
        {
            canvas.overrideSorting = true;
            canvas.sortingLayerID = _canvas.sortingLayerID;
            if (authoredCanvas != null) canvas.sortingOrder = authoredCanvas.sortingOrder;
        }
        return instance;
    }
    private void OnDestroy()
    {
        foreach (var instance in _instances.Values) if (instance != null) Destroy(instance.gameObject);
        _instances.Clear();
    }
}
