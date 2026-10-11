using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Shared information-window switch and foreground canvas placement.</summary>
public static class InfoPanelPresentation
{
    private const int InformationSortingOrder = 30;
    private const float ToggleScale = .75f;

    /// <summary>Keeps information above combat numbers while retaining the parent's sorting layer.</summary>
    public static Canvas EnsureCanvas(GameObject panel)
    {
        var parent = panel.transform.parent != null ? panel.transform.parent.GetComponentInParent<Canvas>() : null;
        var canvas = panel.GetComponent<Canvas>();
        if (canvas == null) canvas = panel.AddComponent<Canvas>();
        canvas.renderMode = parent != null ? parent.rootCanvas.renderMode : RenderMode.ScreenSpaceOverlay;
        if (parent != null) canvas.worldCamera = parent.rootCanvas.worldCamera;
        canvas.overrideSorting = true;
        if (parent != null) canvas.sortingLayerID = parent.sortingLayerID;
        canvas.sortingOrder = InformationSortingOrder;
        if (panel.GetComponent<GraphicRaycaster>() == null) panel.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    /// <summary>Creates the common switch at the upper right of a compact information window.</summary>
    public static Toggle CreateToggle(Transform panel, TMP_Text source, System.Action<bool> changed)
    {
        var toggle = DescriptionToggleView.Create(panel, source, changed);
        var rect = (RectTransform)toggle.transform;
        rect.anchoredPosition = new Vector2(-20f, -16f);
        rect.localScale = Vector3.one * ToggleScale;
        return toggle;
    }
}