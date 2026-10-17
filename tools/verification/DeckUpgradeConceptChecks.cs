using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using GaeGGUL.DeckUpgrade;
using TMPro;
using Spine.Unity;
using UnityEngine;
using UnityEngine.UI;

public static class DeckUpgradeConceptChecks
{
    public static string Economy()
    {
        var data = new DeckUpgradeProgress();
        if (!data.TryUpgrade("A") || data.money != 11850 || data.Level("A") != 2 || data.Level("B") != 1)
            throw new Exception("Debit or unit isolation failed");
        var copy = JsonUtility.FromJson<DeckUpgradeProgress>(JsonUtility.ToJson(data));
        if (copy.Level("A") != 2 || copy.money != 11850) throw new Exception("Save roundtrip failed");
        copy.money = 0;
        if (copy.TryUpgrade("A") || copy.Level("A") != 2) throw new Exception("Insufficient-money boundary failed");
        copy.money = 1000000;
        for (int i = 0; i < 10; i++) copy.TryUpgrade("A");
        int money = copy.money;
        if (copy.Level("A") != 20 || copy.TryUpgrade("A") || copy.money != money) throw new Exception("Maximum-level boundary failed");
        return "PASS: debit, unit independence, serialization, insufficient money, max level.";
    }

    public static string SetDesign(int index)
    {
        var screen = UnityEngine.Object.FindFirstObjectByType<DroneUpgradeConceptScreen>();
        screen.StopAllCoroutines(); screen.BuildView();
        return "Concept " + (index + 1);
    }

    public static async Task<string> Check()
    {
        var screen = UnityEngine.Object.FindFirstObjectByType<DroneUpgradeConceptScreen>();
        if (!Application.isPlaying || screen == null) throw new Exception("Open a concept in Play mode.");
        for (int i = 0; i < 1; i++)
        {
            SetDesign(i); await Task.Delay(80);
            if (screen.GetComponentsInChildren<TMP_Text>(true).Any(x => x.name == "Instruction"))
                throw new Exception("Bottom instruction must be removed");
            if (screen.ChiefPreview.Graphic == null || screen.ChiefPreview.Animation.AnimationState.GetTrack(0).Animation.Name != screen.chiefIdle)
                throw new Exception("Chief showcase must play Spine idle");
            if (screen.GetComponentsInChildren<RectTransform>(true).Any(x => x.name == "HeroStage" || x.name == "PreviewLight"))
                throw new Exception("Removed showcase/backplate must not exist");
            var gauge = screen.GetComponentsInChildren<UpgradeCostGauge>(true).First();
            gauge.SetAmount(75, 150, true);
            if (gauge.Ready || gauge.Ratio != .5f || gauge.Arrow.gameObject.activeSelf)
                throw new Exception("Half-money gauge readiness failed");
            gauge.SetAmount(150, 150, true);
            if (!gauge.Ready || gauge.Ratio != 1 || !gauge.Arrow.gameObject.activeSelf)
                throw new Exception("Full-money arrow failed");
            if (gauge.Arrow.anchorMin != new Vector2(1, .5f) || gauge.Arrow.sizeDelta.y < ((RectTransform)gauge.transform).sizeDelta.y * 1.9f ||
                gauge.Arrow.GetComponent<Image>().color.g <= gauge.Arrow.GetComponent<Image>().color.r)
                throw new Exception("Ready arrow must be large, green and attached at the right-center edge");
            float arrowY = gauge.Arrow.localScale.y;
            bool arrowMoved = false;
            for (int sample = 0; sample < 4; sample++)
            {
                await Task.Delay(50);
                arrowMoved |= Mathf.Abs(gauge.Arrow.localScale.y - arrowY) > .005f;
            }
            if (!arrowMoved) throw new Exception("Arrow does not stretch");
            if (screen.GetComponentsInChildren<RectTransform>(true).Any(x => x.name == "WhiteReflection" || x.name == "SoftReflection"))
                throw new Exception("Gauge shimmer must be removed");
            var edges = screen.GetComponentsInChildren<Image>(true).Where(x => x.name == "ColoredEdge").ToArray();
            if (edges.Select(x => x.color).Distinct().Count() != 1) throw new Exception("Unit frame colors differ");
            foreach (var coin in screen.GetComponentsInChildren<RectTransform>(true).Where(x => x.name == "GoldIcon"))
                if (coin.anchorMin != new Vector2(0, .5f) || coin.anchoredPosition != new Vector2(-7, 0))
                    throw new Exception("Gold icon must sit at the left edge");
            var buttons = screen.GetComponentsInChildren<Button>();
            var slots = buttons.Where(x => x.name.StartsWith("Unit_")).Select(x => (RectTransform)x.transform).ToArray();
            var resting = slots.Select(x => x.anchoredPosition).ToArray();
            var traveler = screen.GetComponentsInChildren<RectTransform>().First(x => x.name == "DroneTabFrame");
            var line = screen.GetComponentsInChildren<RectTransform>().First(x => x.name == "DroneSectionOutline");
            if (((RectTransform)line.parent).anchoredPosition.y != -200) throw new Exception("Section line must sit below chief circle center");
            screen.SetReveal(0);
            if (traveler.anchoredPosition != new Vector2(506, -120)) throw new Exception("Chief must rest at tab center when closed");
            var circle = screen.GetComponentsInChildren<RectTransform>(true).First(x => x.name == "ChiefGrowingCircle");
            if (circle.gameObject.activeSelf || circle.localScale != Vector3.zero || traveler.sizeDelta != new Vector2(240, 240))
                throw new Exception("Closed chief must be larger without a backing circle");
            foreach (var strip in screen.GetComponentsInChildren<RectTransform>(true).Where(x => x.name == "CostStrip"))
            {
                float visualCenter = strip.anchoredPosition.x + (180 - (7 + 42 * 1.15f / 2) + (8 + 34 * 1.84f / 2)) / 2;
                if (Mathf.Abs(visualCenter - 116) > 1) throw new Exception("Gold strip with coin/arrow is not centered");
            }
            var draw = screen.GetComponentsInChildren<RectTransform>().First(x => x.name == "SectionDrawClip");
            if (draw.sizeDelta.y != 0) throw new Exception("Section must start undrawn");
            screen.SetReveal(.15f);
            if (traveler.anchoredPosition.y >= -120 || traveler.anchoredPosition.y <= -480)
                throw new Exception("Chief and divider must reveal progressively");
            screen.SetReveal(.5f);
            if (draw.sizeDelta.y <= 0 || draw.sizeDelta.y >= 1210) throw new Exception("Divider must flow after chief arrival");
            screen.SetReveal(1);
            if (traveler.anchoredPosition != new Vector2(506, -480)) throw new Exception("Chief must land at upgrade slot");
            var tabRect = screen.GetComponentsInChildren<RectTransform>().First(x => x.name == "DroneDeckTab");
            if (tabRect.sizeDelta.y != 300) throw new Exception("Deck tab must be tall");
            var tabDrones = screen.GetComponentsInChildren<RectTransform>().Where(x => x.name.StartsWith("TabDrone_")).ToArray();
            if (tabDrones.Count(x => x.anchoredPosition.x < 506) != 2 || tabDrones.Count(x => x.anchoredPosition.x > 506) != 2)
                throw new Exception("Tab drones must fly on both sides");
            if (screen.GetComponentsInChildren<Image>().Any(x => x.name.StartsWith("DroneBeam_")))
                throw new Exception("Beams must be removed");
            if (tabDrones.Any(x => !screen.combatDroneSprites.Contains(x.GetComponent<Image>().sprite)))
                throw new Exception("Tab must use actual combat drone sprites");
            foreach (float sample in new[] { .01f, .2f, .43f, .7f, 1f })
            {
                screen.SetReveal(sample);
                for (int slot = 0; slot < slots.Length; slot++)
                    if (slots[slot].anchoredPosition != resting[slot] || slots[slot].localRotation != Quaternion.identity)
                        throw new Exception("Slots must scale in place without sliding or rotating");
            }
            if (screen.GetComponentsInChildren<RectTransform>(true).Count(x => x.name.StartsWith("DummyDeckTab_")) != 0)
                throw new Exception("Only drone deck must remain");
            if (screen.GetComponentsInChildren<RectTransform>().Any(x => x.name == "UnitDetailsPopup")) throw new Exception("Details must start hidden");
            if (screen.units.Length != 5 || buttons.Count(x => x.name.StartsWith("Unit_")) != 5 ||
                buttons.Count(x => x.name == "DroneDeckTab") != 1) throw new Exception("Not drone-only");
            buttons.First(x => x.name == "DroneDeckTab").onClick.Invoke(); await Task.Delay(900);
            if (screen.Expanded || screen.IsAnimating) throw new Exception("Collapse failed");
            buttons.First(x => x.name == "DroneDeckTab").onClick.Invoke(); await Task.Delay(900);
            if (!screen.Expanded || screen.IsAnimating) throw new Exception("Expand failed");
            foreach (var card in buttons.Where(x => x.name.StartsWith("Unit_")))
                if (card.GetComponent<CanvasGroup>().alpha != 1 || !card.GetComponent<CanvasGroup>().blocksRaycasts)
                    throw new Exception("Cards did not become interactive");
            buttons.First(x => x.name == "Unit_2").onClick.Invoke(); await Task.Delay(280);
            if (!screen.GetComponentsInChildren<TMP_Text>().Any(x => x.text.StartsWith("식량 생산"))) throw new Exception("Producer stat missing");
            if (!screen.GetComponentsInChildren<RectTransform>().Any(x => x.name == "UnitDetailsPopup")) throw new Exception("Touch did not open popup");
            if (screen.PopupPreview.Graphic == null || screen.PopupPreview.Animation.AnimationState.GetTrack(0) == null)
                throw new Exception("Selected Spine preview missing");
            if (screen.GetComponentsInChildren<TMP_Text>(true).Any(x => x.name == "BonusState" || x.text.Contains("\uC131\uACF5\uB960")))
                throw new Exception("Success-rate and bonus sublabels must be removed");
            var abilities = screen.GetComponentsInChildren<ScrollRect>().First(x => x.name == "SpecialAbilityScroll");
            var skillPanel = abilities.content.Find("SkillNamePanel");
            if (skillPanel == null || skillPanel.GetComponent<Image>().color == abilities.GetComponent<Image>().color ||
                string.IsNullOrWhiteSpace(skillPanel.GetComponentInChildren<TMP_Text>().text))
                throw new Exception("Skill name must be in a distinct colored panel");
            var motes = screen.GetComponentsInChildren<RectTransform>().Where(x => x.name.StartsWith("RisingMote_")).ToArray();
            if (motes.Length != 9) throw new Exception("Missing rising motes");
            var motePosition = motes[0].anchoredPosition;
            await Task.Delay(90);
            if (motes[0].anchoredPosition == motePosition) throw new Exception("Motes are not moving");
            if (!abilities.vertical || abilities.horizontal || abilities.viewport.GetComponent<RectMask2D>() == null)
                throw new Exception("Special abilities must be a masked vertical scroll view");
            if (!screen.GetComponentsInChildren<TMP_Text>().First(x => x.name == "SpeedStat").text.Contains("#91E6A6"))
                throw new Exception("Next attack speed must be green");
            await Task.Delay(80);
            if (screen.IsPopupAnimating || Mathf.Abs(screen.PopupScale - 1) > .001f) throw new Exception("Popup did not settle");
            screen.SamplePopup(0);
            if (screen.PopupScale > .2f) throw new Exception("Popup must start small");
            screen.SamplePopup(.72f);
            if (Mathf.Abs(screen.PopupScale - 1.06f) > .001f) throw new Exception("Popup must have one subtle overshoot");
            float previous = screen.PopupScale;
            foreach (float t in new[] { .78f, .86f, .94f, 1f })
            {
                screen.SamplePopup(t);
                if (screen.PopupScale > previous || screen.PopupScale < .999f) throw new Exception("Popup must settle once without rebounds");
                previous = screen.PopupScale;
            }
            screen.SamplePopup(1);
            screen.GetComponentsInChildren<Button>().First(x => x.name == "CloseDetails").onClick.Invoke();
            if (screen.GetComponentsInChildren<RectTransform>().Any(x => x.name == "UnitDetailsPopup")) throw new Exception("Popup close failed");
            buttons.First(x => x.name == "Unit_4").onClick.Invoke(); await Task.Delay(280);
            if (screen.Selected != 4) throw new Exception("Card selection failed");
            if (screen.PopupPreview.Graphic != null) throw new Exception("Sprite-only source unexpectedly created Spine");
            screen.CloseDetails();
            screen.ToggleDeck(); await Task.Delay(90); screen.ToggleDeck(); await Task.Delay(900);
            if (!screen.Expanded || screen.IsAnimating) throw new Exception("Interrupted toggle failed");
        }
        SetDesign(0);
        MilestoneTabs();
        string upgradeResult = await UpgradeFeedback();
        return "PASS navy: chief travels tab-to-slot, flowing divider, round combat drones without beams, fixed slots, skill/bonus tab unlock boundaries, popup/gauge. " + upgradeResult;
    }

    public static string MilestoneTabs()
    {
        var screen = UnityEngine.Object.FindFirstObjectByType<DroneUpgradeConceptScreen>();
        var field = typeof(DroneUpgradeConceptScreen).GetField("progress", BindingFlags.Instance | BindingFlags.NonPublic);
        var original = field.GetValue(screen);
        try
        {
            var sample = new DeckUpgradeProgress();
            sample.units.Add(new DeckUpgradeProgress.Entry { id = screen.units[0].name, level = 1 });
            field.SetValue(screen, sample);
            foreach (int level in new[] { 2, 3, 5, 6, 8, 9, 11, 12, 14, 15 })
            {
                sample.units[0].level = level; screen.SelectUnit(0); screen.SetAbilityTab(1);
                var scroll = screen.GetComponentsInChildren<ScrollRect>().First(x => x.name == "SpecialAbilityScroll");
                if (scroll.content.name != "LevelBonusContent") throw new Exception("Bonus tab did not switch scroll content");
                foreach (int threshold in new[] { 3, 6, 9, 12, 15 })
                {
                    var row = scroll.content.Find("LevelBonus_" + threshold);
                    bool locked = level < threshold;
                    if (row.Find("LockedCover").gameObject.activeSelf != locked || row.Find("BonusLock").gameObject.activeSelf != locked)
                        throw new Exception("Bonus lock/cover threshold mismatch at " + level + "/" + threshold);
                }
                screen.SetAbilityTab(0);
                if (scroll.content.name != "AbilityContent") throw new Exception("Skill tab did not restore description");
            }
            return "PASS: both scroll tabs and all bonus unlock boundaries.";
        }
        finally { field.SetValue(screen, original); screen.BuildView(); }
    }

    public static async Task<string> UpgradeFeedback()
    {
        var screen = UnityEngine.Object.FindFirstObjectByType<DroneUpgradeConceptScreen>();
        var field = typeof(DroneUpgradeConceptScreen).GetField("progress", BindingFlags.Instance | BindingFlags.NonPublic);
        var original = (DeckUpgradeProgress)field.GetValue(screen);
        bool hadSave = PlayerPrefs.HasKey(DeckUpgradeProgress.SaveKey);
        string saved = PlayerPrefs.GetString(DeckUpgradeProgress.SaveKey, "");
        try
        {
            field.SetValue(screen, new DeckUpgradeProgress()); screen.BuildView(); screen.SelectUnit(0);
            await Task.Delay(360);
            var dialog = screen.GetComponentsInChildren<RectTransform>().First(x => x.name == "UnitInfoDialog");
            var button = screen.GetComponentsInChildren<Button>().First(x => x.name == "UpgradeButton");
            screen.UpgradeSelected();
            if (screen.GetComponentsInChildren<TMP_Text>(true).Any(x => x.name == "Notice"))
                throw new Exception("Upgrade success notice must be removed");
            if (!screen.PopupPreview.Animation.AnimationState.GetTrack(0).Animation.Name.StartsWith("attack"))
                throw new Exception("Upgrade did not start actual attack animation");
            var output = Path.Combine(Directory.GetCurrentDirectory(), "outputs/drone-concepts/upgradeframes"); Directory.CreateDirectory(output);
            float maxButton = 1;
            for (int frame = 0; frame < 26; frame++)
            {
                if (dialog.localScale != Vector3.one) throw new Exception("Upgrade must not punch the dialog");
                maxButton = Mathf.Max(maxButton, button.transform.localScale.x);
                ScreenCapture.CaptureScreenshot(Path.Combine(output, frame.ToString("00") + ".png"));
                await Task.Delay(33);
            }
            if (maxButton < 1.02f || button.transform.localScale != Vector3.one) throw new Exception("Button punch failed");
            var state = DeckUpgradeProgress.Load();
            if (state.Level(screen.units[0].name) != 2 || state.money != 11850) throw new Exception("Live upgrade debit/save failed");
            await Task.Delay(300);
            if (screen.PopupPreview.Animation.AnimationState.GetTrack(0).Animation.Name != screen.idleAnimations[0])
                throw new Exception("Attack did not return to idle");
            float before = state.UnitMultiplier(screen.units[0].name);
            screen.SelectUnit(-1); await Task.Delay(360); screen.UpgradeSelected();
            var chiefLabels = screen.GetComponentsInChildren<TMP_Text>();
            if (chiefLabels.First(x => x.name == "SkillName").text != "집결 사격" ||
                !chiefLabels.First(x => x.name == "Description").text.Contains(screen.chief.AlphanSkill.CooldownSeconds.ToString("0.#")))
                throw new Exception("Chief skill panel must describe actual rally skill data");
            var chiefState = DeckUpgradeProgress.Load();
            if (screen.PopupPreview.Animation.AnimationState.GetTrack(0).Animation.Name != screen.chiefIdle ||
                screen.ChiefPreview.Animation.AnimationState.GetTrack(0).Animation.Name != screen.chiefIdle)
                throw new Exception("Chief must stay idle after upgrading");
            if (chiefState.Level(DeckUpgradeProgress.ChiefId) != 2 || chiefState.money != 11700 ||
                Mathf.Abs(chiefState.ChiefBonus - .02f) > .0001f || chiefState.UnitMultiplier(screen.units[0].name) <= before ||
                chiefState.Level(screen.units[0].name) != 2)
                throw new Exception("Chief deck-wide bonus/debit/independence failed");
            if (!screen.GetComponentsInChildren<Button>(true).Any(x => x.name == "ChiefUpgradeSlot"))
                throw new Exception("Missing circular chief slot");
            if (screen.GetComponentsInChildren<TMP_Text>(true).Any(x => x.name == "Cost" && x.text.EndsWith(" G")))
                throw new Exception("Gold must use icon instead of letter G");
            return "PASS: unit and chief debit/save, 2% deck-wide bonus, fixed dialog, button punch, attack then idle. Save restored.";
        }
        finally
        {
            if (hadSave) PlayerPrefs.SetString(DeckUpgradeProgress.SaveKey, saved); else PlayerPrefs.DeleteKey(DeckUpgradeProgress.SaveKey);
            PlayerPrefs.Save(); field.SetValue(screen, original); screen.BuildView();
        }
    }

    public static async Task<string> Export()
    {
        var output = Path.Combine(Directory.GetCurrentDirectory(), "outputs/drone-concepts"); Directory.CreateDirectory(output);
        var screen = UnityEngine.Object.FindFirstObjectByType<DroneUpgradeConceptScreen>();
        for (int i = 0; i < 1; i++)
        {
            SetDesign(i); await Task.Delay(300);
            var dir = Path.Combine(output, "frames" + (i + 1)); Directory.CreateDirectory(dir);
            // Fast bounce needs 30 fps sampling; these are real running-game captures.
            screen.ToggleDeck();
            var closeDir = Path.Combine(output, "closeframes1"); Directory.CreateDirectory(closeDir);
            for (int frame = 0; frame < 10; frame++)
            {
                ScreenCapture.CaptureScreenshot(Path.Combine(closeDir, frame.ToString("00") + ".png"));
                await Task.Delay(33);
            }
            await Task.Delay(300);
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "closed.png")); await Task.Delay(150);
            screen.ToggleDeck();
            for (int frame = 0; frame < 26; frame++)
            {
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, frame.ToString("00") + ".png"));
                await Task.Delay(33);
            }
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "concept" + (i + 1) + ".png"));
            await Task.Delay(200);
            var popupDir = Path.Combine(output, "popupframes" + (i + 1)); Directory.CreateDirectory(popupDir);
            screen.SelectUnit(0);
            for (int frame = 0; frame < 19; frame++)
            {
                ScreenCapture.CaptureScreenshot(Path.Combine(popupDir, frame.ToString("00") + ".png"));
                await Task.Delay(33);
            }
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "popup" + (i + 1) + ".png"));
            await Task.Delay(150); // ScreenCapture keeps only the last request in a rendered frame.
            screen.SetAbilityTab(1); await Task.Delay(150);
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "bonuses.png")); await Task.Delay(150);
            var bonusScroll = screen.GetComponentsInChildren<ScrollRect>().First(x => x.name == "SpecialAbilityScroll");
            bonusScroll.verticalNormalizedPosition = 0; await Task.Delay(150);
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "bonuses-bottom.png")); await Task.Delay(150);
            screen.SetAbilityTab(0);
            if (i == 0)
            {
                var readyDir = Path.Combine(output, "readyframes"); Directory.CreateDirectory(readyDir);
                for (int frame = 0; frame < 30; frame++)
                {
                    ScreenCapture.CaptureScreenshot(Path.Combine(readyDir, frame.ToString("00") + ".png"));
                    await Task.Delay(50);
                }
            }
            await Task.Delay(200); screen.CloseDetails();
        }
        SetDesign(0);
        screen.SelectUnit(-1); await Task.Delay(400);
        ScreenCapture.CaptureScreenshot(Path.Combine(output, "chief.png")); await Task.Delay(150);
        screen.CloseDetails();
        return "Exported navy list, Alphan detail and unit upgrade motion to " + output;
    }
}
