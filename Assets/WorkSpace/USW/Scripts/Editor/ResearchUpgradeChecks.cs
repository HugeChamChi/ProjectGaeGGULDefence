using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>실제 트리와 메모리 진행만 사용한다. 계정 저장/골드/열린 씬은 변경하지 않는다.</summary>
public static class ResearchUpgradeChecks
{
    private const string Data = "Assets/WorkSpace/USW/Data/OutgameUpgrade/";

    [MenuItem("Tools/USW/Research/Validate Upgrade Changes")]
    public static void Run()
    {
        var tree = AssetDatabase.LoadAssetAtPath<ResearchTreeData>(Data + "UpgradeTree_Basic.asset");
        var settings = AssetDatabase.LoadAssetAtPath<ResearchViewSettings>(Data + "UpgradeViewSettings.asset");
        var merge = tree.Find("4-1");
        Assert(merge.Parents.Count == 2, "Real basic merge fixture changed.");
        var left = tree.Find(merge.Parents[0]);
        var right = tree.Find(merge.Parents[1]);
        var levels = new Dictionary<string, int>();
        ResearchProgress Progress() => new ResearchProgress(tree, levels);

        Assert(!Progress().TryUpgrade(merge), "0/0 must stay locked.");
        levels[left.Id] = left.MaxLevel;
        Assert(!Progress().TryUpgrade(merge), "MAX/0 must stay locked.");
        levels.Clear(); levels[right.Id] = right.MaxLevel;
        Assert(!Progress().TryUpgrade(merge), "0/MAX must stay locked.");
        levels[left.Id] = 1; levels[right.Id] = 1;
        var both = Progress();
        Assert(both.CanUpgrade(merge) && both.TryUpgrade(merge), "1/1 must allow the merge purchase.");
        Assert(both.GetRecommended() == tree.Find("1-1"), "Recommendation still follows the authored tree order.");

        levels.Clear(); levels["1-1"] = tree.Find("1-1").MaxLevel - 1;
        Assert(!Progress().IsUnlocked(tree.Find("2-1")), "Single-parent paths still require MAX.");
        Assert(Progress().HighestMaxedRow == -1, "Partial upgrades must not reveal the background.");
        levels["1-1"]++;
        Assert(Progress().IsUnlocked(tree.Find("2-1")), "Single-parent MAX must unlock.");
        Assert(Progress().HighestMaxedRow == 0, "First MAX must reveal row zero.");
        levels[left.Id] = left.MaxLevel;
        var progress = Progress();
        Assert(progress.HighestMaxedRow == left.Row, "Highest MAX determines the frontier.");
        var restored = new ResearchProgress(tree, progress.Capture().GetLevels(tree.TreeKey));
        Assert(restored.HighestMaxedRow == progress.HighestMaxedRow, "Reload must restore the same frontier.");
        progress.ResetAll();
        Assert(progress.HighestMaxedRow == -1, "Reset must hide the background.");

        CheckChoice();
        CheckGate();
        CheckBackground(tree, settings);
        CheckCompletedPresentation(tree, settings);
        CheckReferenceBands(tree);
        Debug.Log("[ResearchUpgradeChecks] PASS: merge 0/0, MAX/0, 0/MAX, 1/1; single-parent MAX; choice and total gates; background reset/reload/scroll/preview; four patterns.");
    }

    private static void CheckChoice()
    {
        var tree = AssetDatabase.LoadAssetAtPath<ResearchTreeData>(Data + "UpgradeTree_Choice.asset");
        var merge = tree.Nodes.Find(n => n.Parents.Count == 2 && n.RequireAnyParent);
        var parent = tree.Find(merge.Parents[0]);
        var levels = new Dictionary<string, int> { [parent.Id] = 1 };
        var progress = new ResearchProgress(tree, levels);
        Assert(!progress.RequiresBothParentsStarted(merge) && !progress.IsUnlocked(merge), "Exclusive choice must not require both parents or unlock at one point.");
        levels[parent.Id] = parent.MaxLevel;
        progress = new ResearchProgress(tree, levels);
        Assert(progress.IsUnlocked(merge), "One MAX must still unlock an exclusive merge.");
        progress.ResetChoice(parent);
        Assert(progress.HighestMaxedRow == -1 && !progress.IsUnlocked(merge), "Changing choice must retract progression.");
    }

    private static void CheckGate()
    {
        var tree = ScriptableObject.CreateInstance<ResearchTreeData>();
        try
        {
            var a = new ResearchNodeData { Id = "a", MaxLevel = 3 };
            var b = new ResearchNodeData { Id = "b", MaxLevel = 3 };
            var merge = new ResearchNodeData { Id = "merge", Parents = new List<string> { "a", "b" }, RequireAnyParent = true, RequiredTotalLevel = 4 };
            tree.Nodes.AddRange(new[] { a, b, merge });
            var levels = new Dictionary<string, int> { ["a"] = 1, ["b"] = 1 };
            Assert(!new ResearchProgress(tree, levels).IsUnlocked(merge), "Total-level gate must still apply.");
            levels["a"] = 3;
            Assert(new ResearchProgress(tree, levels).IsUnlocked(merge), "3/1 with total gate met must unlock.");
            levels["b"] = 0; merge.RequiredTotalLevel = 0;
            Assert(!new ResearchProgress(tree, levels).IsUnlocked(merge), "Any-parent flag must not bypass a nonexclusive two-parent minimum.");
        }
        finally { UnityEngine.Object.DestroyImmediate(tree); }
    }

    private static void CheckBackground(ResearchTreeData tree, ResearchViewSettings settings)
    {
        var root = new GameObject("ResearchBackgroundCheck", typeof(RectTransform));
        try
        {
            var viewport = (RectTransform)root.transform;
            viewport.sizeDelta = new Vector2(1080f, 1200f);
            var content = ResearchUi.NewRect(viewport, "Content");
            content.anchorMin = content.anchorMax = new Vector2(0.5f, 0f);
            content.pivot = new Vector2(0.5f, 0f);
            content.sizeDelta = new Vector2(1080f, 4000f);
            var progress = new ResearchProgress(tree, null);
            var layer = ResearchUi.Stretch(ResearchUi.NewRect(viewport, "Background"));
            var bg = layer.gameObject.AddComponent<ResearchBackground>();
            bg.Initialize(viewport, content, settings, progress);
            var surface = layer.Find("MaxReveal/Art").GetComponent<Image>();
            Assert(settings.BackgroundDesign == ResearchBackgroundDesign.Original && surface.sprite == settings.PanelBackground,
                "Original settings must restore the original background artwork.");
            bg.RefreshVisuals();
            Assert(bg.RevealHeight == 0f, "No MAX means no visible artwork.");
            Assert(surface.type == Image.Type.Tiled && surface.rectTransform.rect.height == content.rect.height,
                "Artwork must repeat throughout the tree height.");
            var first = tree.Find("1-1");
            for (int i = 0; i < first.MaxLevel; i++) Assert(progress.TryUpgrade(first), "Fixture upgrade failed.");
            bg.RefreshVisuals();
            Assert(Mathf.Approximately(bg.RevealHeight, settings.ContentPadding), "Fill must end at the MAX node center.");
            float artY = surface.rectTransform.anchoredPosition.y;
            content.anchoredPosition = new Vector2(0f, -100f);
            bg.RefreshVisuals();
            Assert(Mathf.Approximately(surface.rectTransform.anchoredPosition.y, artY - 100f),
                "Artwork must scroll together with the tree.");
            Assert(Mathf.Approximately(bg.RevealHeight, settings.ContentPadding - 100f), "Reveal must track scrolled content.");
            content.anchoredPosition = new Vector2(0f, -1000f);
            bg.RefreshVisuals();
            Assert(bg.RevealHeight == 0f, "Scrolling above the frontier must be dark.");
            int total = progress.TotalLevel;
            bg.Preview = true;
            foreach (ResearchBackgroundPattern pattern in Enum.GetValues(typeof(ResearchBackgroundPattern)))
            {
                bg.SetPattern(pattern); bg.RefreshVisuals();
                Assert(bg.RevealHeight == viewport.rect.height && progress.TotalLevel == total, "Preview must fill without changing saved progress.");
                foreach (var image in layer.GetComponentsInChildren<Image>(true)) Assert(!image.raycastTarget, "Background must not block node or scroll input.");
            }
            bg.Preview = false; progress.ResetAll(); bg.RefreshVisuals();
            Assert(bg.RevealHeight == 0f, "Leaving preview after reset must be dark.");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("[ResearchUpgradeChecks] " + message);
    }

    private static void CheckReferenceBands(ResearchTreeData tree)
    {
        var names = new[] { "02_CyanDiagonal" };
        foreach (string name in names)
        {
            var settings = AssetDatabase.LoadAssetAtPath<ResearchViewSettings>("Assets/WorkSpace/USW/Data/OutgameUpgradeBackgrounds/Background_" + name + ".asset");
            var root = new GameObject("ReferenceBandCheck", typeof(RectTransform));
            try
            {
                var viewport = (RectTransform)root.transform;
                viewport.sizeDelta = new Vector2(1080f, 1200f);
                var content = ResearchUi.NewRect(viewport, "Content");
                content.anchorMin = content.anchorMax = new Vector2(0.5f, 0f);
                content.pivot = new Vector2(0.5f, 0f);
                content.sizeDelta = new Vector2(1080f, 4000f);
                var levels = new Dictionary<string, int> { ["1-1"] = tree.Find("1-1").MaxLevel };
                var progress = new ResearchProgress(tree, levels);
                var layer = ResearchUi.Stretch(ResearchUi.NewRect(viewport, "Background"));
                var bg = layer.gameObject.AddComponent<ResearchBackground>();
                bg.Initialize(viewport, content, settings, progress);
                bg.RefreshVisuals();
                Assert(Mathf.Approximately(bg.RevealHeight, settings.ContentPadding), "Reference band must end at the MAX row.");
                var frontier = layer.Find("MaxReveal/MaxFrontierLine").GetComponent<Image>();
                Assert(frontier.rectTransform.anchorMin.x == 0f && frontier.rectTransform.anchorMax.x == 1f && frontier.rectTransform.sizeDelta.y == 4f,
                    "Bright MAX boundary must span the full width as a thin line.");
                var fade = viewport.Find("BlackBottomFade").GetComponent<ResearchGradientGraphic>();
                Assert(Mathf.Approximately(fade.color.a, 0.45f), "Bottom gradient must use the softer opacity.");
                Assert(fade.transform.GetSiblingIndex() > content.GetSiblingIndex() && !fade.raycastTarget,
                    "Bottom gradient must shade upgrade nodes without blocking input.");
                Assert(layer.Find("MaxReveal/Art").GetComponent<Image>().sprite == settings.PanelBackground && settings.PanelBackground != null,
                    "Research band must retain the laboratory artwork.");
                Assert(layer.Find("Unfinished").GetComponent<Image>().sprite == null,
                    "Unfinished rows must not show the laboratory artwork.");
                var icons = Array.FindAll(layer.GetComponentsInChildren<Image>(), image => image.name == "FloatingFrog");
                Assert(icons.Length > 2, "Floating frog pattern must be present.");
                foreach (var icon in icons)
                    Assert(icon.transform.IsChildOf(layer.Find("MaxReveal")) && (icon.sprite == settings.BackgroundIconA || icon.sprite == settings.BackgroundIconB),
                        "Background sprites must be clipped to the completed band.");
                Assert(icons[0].rectTransform.sizeDelta != icons[1].rectTransform.sizeDelta,
                    "Floating frogs must have varied sizes.");
                Assert(icons[0].rectTransform.localRotation != icons[1].rectTransform.localRotation,
                    "Floating frogs must have varied angles.");
                content.anchoredPosition = new Vector2(0f, -100f);
                bg.RefreshVisuals();
                Assert(Mathf.Approximately(bg.RevealHeight, settings.ContentPadding - 100f), "Reference band must follow scrolling immediately.");
                progress.ResetAll(); bg.RefreshVisuals();
                Assert(bg.RevealHeight == 0f, "Reset must hide the completed band immediately.");
                foreach (var graphic in layer.GetComponentsInChildren<Graphic>(true)) Assert(!graphic.raycastTarget, "Reference decoration must not block input.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }

    private static void CheckCompletedPresentation(ResearchTreeData tree, ResearchViewSettings settings)
    {
        var root = new GameObject("CompletedPresentationCheck", typeof(RectTransform));
        try
        {
            var levels = new Dictionary<string, int> { ["1-1"] = tree.Find("1-1").MaxLevel };
            var progress = new ResearchProgress(tree, levels);
            var panel = new ResearchInfoPanel((RectTransform)root.transform, settings);
            panel.SetProgress(progress);
            panel.Show(tree.Find("1-1"));
            var main = root.transform.Find("ActionRow/MainButton0").GetComponent<Button>();
            var recommend = root.transform.Find("ActionRow/RecommendButton").GetComponent<Button>();
            Assert(!main.interactable && main.GetComponentInChildren<TMPro.TMP_Text>().text == settings.MaxLabel,
                "Completed selected node must retain the blue button with a noninteractive MAX label.");
            Assert(recommend.gameObject.activeSelf && recommend.interactable,
                "Recommendation remains available beside MAX while other nodes can be upgraded.");
            Assert(root.transform.Find("FollowToggle") == null && root.transform.Find("Dot0") == null,
                "Fixed panel must not create mode controls or page dots.");
            panel.Show(tree.Find("2-1"));
            Assert(main.gameObject.activeSelf, "Selecting an unfinished node keeps the fixed button row.");
            var view = ResearchNodeView.Create((RectTransform)root.transform, tree.Find("1-1"), settings, settings.NodeSize, null);
            view.Refresh(tree.Find("1-1").MaxLevel, ResearchNodeState.Maxed);
            Assert(view.transform.Find("Node_Max") == null, "Completed nodes must not add MAX labels.");
            foreach (var node in tree.Nodes) levels[node.Id] = node.MaxLevel;
            panel.SetProgress(new ResearchProgress(tree, levels));
            panel.Refresh();
            Assert(!main.interactable && !recommend.interactable && main.gameObject.activeSelf && recommend.gameObject.activeSelf,
                "Fully completed tree must retain both authored buttons without accepting upgrades.");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
}
