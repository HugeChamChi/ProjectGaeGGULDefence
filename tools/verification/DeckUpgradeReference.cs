using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class DeckUpgradeReference
{
    public static string Show()
    {
        foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) canvas.gameObject.SetActive(false);
        var s = AssetDatabase.LoadAssetAtPath<ResearchViewSettings>("Assets/WorkSpace/USW/Data/OutgameUpgrade/UpgradeViewSettings.asset");
        var tree = AssetDatabase.LoadAssetAtPath<ResearchTreeData>("Assets/WorkSpace/USW/Data/OutgameUpgrade/UpgradeTree_Basic.asset");
        var go = new GameObject("ExistingUpgradeReference", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920); scaler.matchWidthOrHeight = 0;
        var root = (RectTransform)go.transform;
        var bg = ResearchUi.NewImage(root, "Background", null, Vector2.zero, Vector2.zero);
        ResearchUi.Stretch(bg.rectTransform); bg.color = s.UnfinishedBackground;
        var treeArea = ResearchUi.Stretch(ResearchUi.NewRect(root, "TreeArea"), 0, 440, 0, 130);
        var progress = new ResearchProgress(tree, new Dictionary<string, int>());
        var view = new ResearchTreeView(treeArea, tree, s, progress);
        var panel = ResearchUi.NewImage(root, "InfoPanel", s.CardBackground, Vector2.zero, Vector2.zero);
        panel.rectTransform.anchorMin = Vector2.zero; panel.rectTransform.anchorMax = new Vector2(1, 0);
        panel.rectTransform.pivot = new Vector2(.5f, 0); panel.rectTransform.sizeDelta = new Vector2(0, 440);
        var info = new ResearchInfoPanel(panel.rectTransform, s); info.SetProgress(progress);
        var node = progress.GetRecommended(); if (node != null) { info.Show(node); view.Select(node); view.ScrollTo(node, false); }
        var bar = ResearchUi.NewImage(root, "TopBar", null, Vector2.zero, Vector2.zero);
        bar.color = new Color32(0, 80, 96, 255); bar.rectTransform.anchorMin = new Vector2(0, 1); bar.rectTransform.anchorMax = Vector2.one;
        bar.rectTransform.pivot = new Vector2(.5f, 1); bar.rectTransform.sizeDelta = new Vector2(0, 130);
        ResearchUi.NewText(bar.transform, "Title", "영구 강화", 42, s.FontBold, TMPro.TextAlignmentOptions.Center);
        return "Rendered existing ResearchTreeView + ResearchInfoPanel with authored UpgradeViewSettings. " +
            "Font=" + AssetDatabase.GetAssetPath(s.FontBold) + "; nodeArt=" + s.HasNodeArt + "; fill=" + (s.RoundedFill != null);
    }
}
