using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Read-only content validation, pure rendering/RNG checks, and live production input checks.</summary>
public static class ChoiceDescriptionChecks
{
    [MenuItem("Tools/Descriptions/Validate Content and Core")]
    public static void RunEditorChecks()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Run content/core checks outside Play mode.");
        var catalog = Resources.Load<DescriptionTermCatalog>("DescriptionTermCatalog");
        var resolver = CreateResolver(catalog);
        var errors = new List<string>();
        var ids = new HashSet<string>();
        foreach (var entry in catalog.Entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.Id) || !Regex.IsMatch(entry.Id, @"^[a-z0-9:-]+$") || !ids.Add(entry.Id))
                errors.Add("Missing, unsafe or duplicate catalog ID.");
            else if (string.IsNullOrWhiteSpace(entry.DisplayName) || string.IsNullOrWhiteSpace(entry.Body)) errors.Add(entry.Id + ": missing name/body.");
        }
        var allowed = new HashSet<string> { "primaryValue", "secondaryValue", "specialValue", "Interval", "Value", "ValuePercent", "MaxValuePercent", "Count", "value", "count", "seconds", "max", "refund" };
        int count = 0;
        var glossary = AssetDatabase.LoadAssetAtPath<TermGlossary>("Assets/WorkSpace/USW/Data/UI/TermGlossary.asset");
        for (int i = 1; i <= 24; i++)
        {
            var card = AssetDatabase.LoadAssetAtPath<LevelUpData>("Assets/WorkSpace/USW/Data/SelectionData/DroneSelection" + i + ".asset");
            if (card == null) { errors.Add("Missing card " + i); continue; }
            count++;
            if (string.IsNullOrWhiteSpace(card.simpleDescription)) errors.Add(card.name + ": missing simple description.");
            CheckTemplate(card.description, card.name + " detailed", card);
            CheckTemplate(card.simpleDescription, card.name + " simple", card);
            SelectionDescriptionFormatter.ValidateTemplate(card.description, card.Composition);
            SelectionDescriptionFormatter.ValidateTemplate(card.simpleDescription, card.Composition);
            foreach (var id in card.DescriptionTerms)
                if (glossary == null || !glossary.TryFind(id, out var term) || string.IsNullOrWhiteSpace(term.BriefBody) || string.IsNullOrWhiteSpace(term.DetailedBody))
                    errors.Add(card.name + ": missing pointed term " + id);
        }
        Assert(count == 24, "All 24 authored cards must be validated.");
        if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
        CheckFormatter();
        CheckTermLinks(glossary);
        CheckRandomBoundary(resolver);
        Debug.Log("[Descriptions] PASS: 24 cards, glossary pointers, catalog/debuff terms, composition tokens, formatter fallbacks/markup and random snapshot boundary. Input/layout require Play checks and visual QA.");

        void CheckTemplate(string text, string label, LevelUpData card)
        {
            text = text ?? string.Empty;
            if (Regex.Replace(text, @"\{[^{}]+\}", string.Empty).Contains("{") || text.Count(c => c == '{') != text.Count(c => c == '}'))
                errors.Add(label + ": malformed token.");
            foreach (Match match in Regex.Matches(text, @"\{([^{}]+)\}"))
            {
                string token = match.Groups[1].Value;
                if (token.StartsWith("term:"))
                {
                    if (!resolver.TryResolve(token.Substring(5), out var term) || string.IsNullOrWhiteSpace(term.Body)) errors.Add(label + ": unresolved/empty term " + token);
                }
                else if (!allowed.Contains(token)) errors.Add(label + ": unknown numeric token " + token);
            }
            if (Regex.IsMatch(text, @"\{(?:ValuePercent|MaxValuePercent)\}%(?!p)") && card.droneEffect?.Kind == DroneSelectionKind.GammanFrequency)
                errors.Add(label + ": additive Gamman bonus must use %p.");
            if (text.Contains("<link")) errors.Add(label + ": authored links must use explicit term tokens.");
        }
    }

    private static IDescriptionTermResolver CreateResolver(DescriptionTermCatalog catalog)
    {
        var settings = Resources.Load<DebuffSettings>("DebuffSettings");
        return new DescriptionTermResolver(catalog, new DebuffInfoPresenter(settings, new DebuffCatalog(settings)));
    }
    private static void CheckTermLinks(TermGlossary glossary)
    {
        var linker = new TermLinker(glossary, Color.cyan, Color.yellow);
        var card = AssetDatabase.LoadAssetAtPath<LevelUpData>("Assets/WorkSpace/USW/Data/SelectionData/DroneSelection9.asset");
        foreach (var text in new[] { card.simpleDescription, card.description })
        {
            string linked = linker.AddLinks(text, card.DescriptionTerms);
            Assert(TermLinker.LinkedIds(linked).Distinct().SequenceEqual(new[] { "detonation" }),
                "9109 residue text must open detonation in both modes, never mana.");
            Assert(linked.Contains("기폭</color>"), "The detonation word must be interactive.");
            Assert(!linked.Contains("<u>") && !linked.Contains("▶"), "Terms use only colors without underline or video markers.");
        }
        Assert(TermLinker.LinkedIds(linker.AddLinks("해킹 스킬, 마나")).SequenceEqual(new[] { "hack_stack", "mana" }),
            "Hacking skill and mana must retain distinct glossary meanings.");
        foreach (var id in new[] { "self_destruct_drone", "chief_skill", "hack_stack", "time_manipulation" })
            Assert(glossary.TryFind(id, out var entry) && entry.Clip != null, "Video term missing its clip: " + id);
    }
    private static void CheckFormatter()
    {
        var catalog = ScriptableObject.CreateInstance<DescriptionTermCatalog>();
        catalog.Entries.Add(new DescriptionTermCatalog.Entry { Id = "valid", DisplayName = "<term>", Body = "Definition" });
        catalog.Entries.Add(new DescriptionTermCatalog.Entry { Id = "empty", DisplayName = "Empty", Body = "" });
        var resolver = new DescriptionTermResolver(catalog);
        var values = new Dictionary<string, string> { ["number"] = "20" };
        var snapshot = new DescriptionSnapshot("<color=red>{number}%</color> {term:valid} {term:missing} {term:empty} {unknown} [1~10%] {open", "", values);
        values["number"] = "999";
        string output = DescriptionFormatter.Format(snapshot, false, resolver, true);
        Assert(output.Contains("<color=red>20%</color>"), "Snapshot must copy values and preserve TMP markup; empty simple falls back.");
        Assert(output.Contains("<link=\"valid\">") && output.Contains("「\uFF1Cterm\uFF1E」"), "Known terms link and names stay plain text.");
        Assert(output.Contains("「missing」") && output.Contains("「Empty」") && !output.Contains("<link=\"empty\">"), "Missing and empty terms stay noninteractive.");
        Assert(output.Contains("{unknown}") && output.Contains("{open") && output.Contains("[1~10%]"), "Unknown/malformed tokens and ordinary brackets are retained.");
        Assert(!DescriptionFormatter.Format(snapshot, true, resolver, false).Contains("<link"), "Result descriptions contain no new links.");
        var nested = new DescriptionSnapshot("<link=\"existing\">{term:valid}</link> <color=\"{term:valid}\">x</color>", "short");
        string retained = DescriptionFormatter.Format(nested, true, resolver, true);
        Assert(Regex.Matches(retained, "<link").Count == 1 && retained.Contains("<color=\"{term:valid}\">"), "Existing links and tag interiors are not decorated again.");
        Assert(DescriptionFormatter.Format(nested, false, resolver, true) == "short", "Authored simple text is selected.");
        UnityEngine.Object.DestroyImmediate(catalog);
    }
    private static void CheckRandomBoundary(IDescriptionTermResolver resolver)
    {
        var savedRandom = UnityEngine.Random.state;
        var root = new GameObject("DescriptionSnapshotCheck");
        var manager = root.AddComponent<LevelUpManager>();
        var state = new LevelUpRunState();
        typeof(LevelUpManager).GetField("_runState", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .SetValue(manager, state);
        LevelUpData card = null;
        try
        {
            // 2026-10-21부터 실제 카드에 범위 추첨이 없으므로 추첨 경계는 메모리 전용 카드로 검사한다.
            card = ScriptableObject.CreateInstance<LevelUpData>();
            card.chooseId = 99108; card.EffectSchemaVersion = 1;
            card.description = "해킹 생산 +{value}%"; card.simpleDescription = "해킹 생산 증가";
            card.Composition = new SelectionCardDefinition();
            card.Composition.Effects.Add(new HackingProductionRollDefinition { MinimumRatio = .01f, MaximumRatio = .1f });
            UnityEngine.Random.InitState(9876);
            var before = UnityEngine.Random.state;
            var snapshot = new ChoiceDescriptionAdapter(card, manager).Capture();
            var captured = UnityEngine.Random.state;
            Assert(!before.Equals(captured), "First presented random card must capture its roll even in simple mode.");
            for (int i = 0; i < 10; i++) DescriptionFormatter.Format(snapshot, i % 2 == 0, resolver, true);
            resolver.TryResolve("debuff:1003", out _);
            Assert(captured.Equals(UnityEngine.Random.state), "Toggling/rendering/tooltip lookup must not consume RNG.");
            var repeated = new ChoiceDescriptionAdapter(card, manager).Capture();
            manager.ApplyEffect(card);
            Assert(repeated.Values["value"] == snapshot.Values["value"] && captured.Equals(UnityEngine.Random.state), "Existing run cache must preserve the displayed roll on selection.");
            Assert(Mathf.RoundToInt(manager.CurrentEffects.HackingProductionBonus * 100).ToString() == snapshot.Values["value"], "Applied composition value must match the snapshot.");
        }
        finally { UnityEngine.Random.state = savedRandom; UnityEngine.Object.DestroyImmediate(root); if (card != null) UnityEngine.Object.DestroyImmediate(card); }
    }

    [MenuItem("Tools/Descriptions/Run Standalone Play Checks")]
    public static void RunPlayChecks()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Open ChoiceDescriptionTest and enter Play mode first.");
        RunPlayChecksAsync().Forget(exception => Debug.LogException(exception));
    }
    private static async UniTask RunPlayChecksAsync()
    {
        var harness = UnityEngine.Object.FindFirstObjectByType<ChoiceDescriptionTestHarness>();
        Assert(harness != null, "Standalone description harness must be active.");
        harness.ShowChoices();
        await UniTask.WaitUntil(() => harness.IsReady);
        Canvas.ForceUpdateCanvases();
        var card = harness.Snapshots.Keys.First();
        var pointer = TermPointer(card, -1);
        int selections = harness.SelectionCount, opens = harness.TermOpenCount;
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        Assert(hits.Count > 0, "The displayed term must receive a UI raycast.");
        pointer.pointerCurrentRaycast = pointer.pointerPressRaycast = hits[0];
        var target = ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerDownHandler);
        Assert(target == card.gameObject, "The term raycast must route input to its production card.");
        ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerClickHandler);
        Assert(harness.TermOpenCount == opens + 1 && harness.SelectionCount == selections && harness.BlocksInput, "Term tap opens only the tooltip.");
        float remaining = harness.Remaining;
        await UniTask.Delay(250, DelayType.Realtime);
        Assert(Mathf.Approximately(remaining, harness.Remaining), "Reading a tooltip pauses remaining selection time.");
        var popup = UnityEngine.Object.FindFirstObjectByType<DebuffInfoPopup>();
        popup.Close();
        Assert(harness.BlocksInput, "Dismissal frame keeps the release shield.");
        await UniTask.Delay(100, DelayType.Realtime);
        Assert(harness.BlocksInput, "Exit animation keeps input blocked until it finishes.");
        await UniTask.Delay(250, DelayType.Realtime);
        Assert(!harness.BlocksInput, "Release shield clears after all pointers are released.");
        float resumed = harness.Remaining;
        await UniTask.Delay(150, DelayType.Realtime);
        Assert(harness.Remaining < resumed, "Timer resumes from remaining time.");
        opens = harness.TermOpenCount;
        pointer = TermPointer(card, -1);
        card.OnPointerDown(pointer);
        await UniTask.Delay(450, DelayType.Realtime);
        card.OnPointerUp(pointer); card.OnPointerClick(pointer);
        Assert(harness.TermOpenCount == opens && harness.SelectionCount == selections, "Long term press becomes neither a tooltip nor a selection.");
        card.OnPointerDown(pointer);
        var dragged = new PointerEventData(EventSystem.current) { pointerId = -1, position = pointer.position + new Vector2(100, 0), button = PointerEventData.InputButton.Left };
        card.OnBeginDrag(dragged); card.OnDrag(dragged);
        card.OnPointerUp(pointer); card.OnPointerClick(pointer);
        Assert(harness.TermOpenCount == opens && harness.SelectionCount == selections, "Dragging away and returning cancels the term gesture.");
        card.OnPointerDown(pointer);
        var second = TermPointer(card, -2); card.OnPointerDown(second);
        card.OnPointerUp(pointer); card.OnPointerClick(pointer);
        Assert(harness.TermOpenCount == opens && harness.SelectionCount == selections, "A second pointer cancels the original term tap.");
        card.SetTutorialPreviewOnly(true);
        card.OnPointerDown(pointer);
        await UniTask.Delay(450, DelayType.Realtime);
        var peek = harness.GetComponentInChildren<UI_Peekthrough>();
        Assert(peek.OwnsPeek(card), "PreviewOnly term area follows real field hold input.");
        card.OnPointerUp(pointer); card.OnPointerClick(pointer);
        Assert(harness.SelectionCount == selections && harness.TermOpenCount == opens, "PreviewOnly release never selects or opens terms.");
        card.SetTutorialPreviewOnly(false);
        await UniTask.Delay(300, DelayType.Realtime);
        Debug.Log("[Descriptions] PASS standalone Play checks: term tap/long press/drag/second pointer, tooltip timer/shield/resume, and PreviewOnly field hold. Physical touch and small-screen layout remain visual QA.");
    }
    private static PointerEventData TermPointer(LevelUpCardUI card, int pointerId)
    {
        var text = card.DescriptionText; text.ForceMeshUpdate();
        Assert(text.textInfo.linkCount > 0, "The card must display real linked terms.");
        var info = text.textInfo.characterInfo[text.textInfo.linkInfo[0].linkTextfirstCharacterIndex];
        Vector3 local = (info.bottomLeft + info.topRight) * 0.5f;
        var canvas = text.GetComponentInParent<Canvas>();
        var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        return new PointerEventData(EventSystem.current) { pointerId = pointerId,
            position = RectTransformUtility.WorldToScreenPoint(camera, text.transform.TransformPoint(local)), button = PointerEventData.InputButton.Left };
    }
    private static void Assert(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("[Descriptions] " + message); }
}
