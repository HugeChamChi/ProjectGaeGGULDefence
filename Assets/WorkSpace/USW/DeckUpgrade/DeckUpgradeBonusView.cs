using TMPro;
using UnityEngine;
using System.Text.RegularExpressions;

namespace GaeGGUL.DeckUpgrade
{
    // Presentation only. Combat bonuses are not applied by this preview UI.
    public sealed class DeckUpgradeBonusView : MonoBehaviour
    {
        private static readonly string[][] Labels =
        {
            new string[5], // Authored bonuses come from UnitData; remaining rows are legacy previews.
            new[] { "[해킹] 최대 스택 +5", "스킬 사용 시 20% 확률로 [해킹] +1", "스킬 충전 공격 횟수 -1", "스킬 사용 시 [해킹] 추가 부여 확정", "[해킹] 최대 스택 추가 +10" },
            new[] { "공격력 10% 증가", "스킬 쿨타임 20% 감소", "스킬 피해 20% 증가", "스킬 사용 시 [해킹] 추가 소모 +1", "치명타 확률/피해 10% 증가." },
            new[] { "공격력 10% 증가", "스킬 충전 공격 횟수 -1", "공격속도 10% 증가", "일반공격 시 25% 확률로 추가 공격", "일반공격 피해 20% 증가" },
            new[] { "식량 생산 쿨타임 10% 감소", "등장 시 식량 즉시 2회 생산", "보스 처치 시 식량 즉시 3회 생산", "25% 확률로 식량 2배 생산", "자체 식량 생산량 20% 증가" },
            new[] { "[액티브 스킬] 쿨타임 10% 감소", "패턴 무효화 시 남은 쿨타임 15% 감소", "전체 유닛 공격력 5% 증가", "[액티브 스킬] 사용 시 전체 유닛 공격속도 증가", "전체 유닛 스킬 쿨타임 10% 감소" }
        };
        private readonly TMP_Text[] descriptions = new TMP_Text[5];
        private readonly TermLinkTapTarget[] links = new TermLinkTapTarget[5];
        private DeckUpgradeGlossarySettings glossarySettings;
        private ResearchViewSettings viewStyle;
        private RectTransform popupParent;
        private TermPopup popup;
        private DeckUpgradeEffectTooltip effectTooltip;
        private string currentUnit;
        private const string AttackSpeedBuffId = "upgrade:chief_attack_speed";
        public TermPopup Popup => popup;
        public DeckUpgradeEffectTooltip EffectTooltip => effectTooltip;

        public void Build(ResearchViewSettings style, Transform dialog)
        {
            viewStyle = style;
            glossarySettings = Resources.Load<DeckUpgradeGlossarySettings>("DeckUpgradeGlossarySettings");
            popupParent = (RectTransform)dialog.GetComponentInParent<Canvas>().transform;
            for (int i = 0; i < descriptions.Length; i++)
            {
                var row = transform.Find("LevelBonus_" + ((i + 1) * 3));
                var text = row.Find("BonusDescription").GetComponent<TMP_Text>();
                descriptions[i] = text;
                text.fontStyle = FontStyles.Bold;
                text.enableAutoSizing = true; text.fontSizeMin = 23; text.fontSizeMax = 32;
                text.textWrappingMode = TextWrappingModes.Normal;
                text.rectTransform.sizeDelta = new Vector2(540, 72);
                text.rectTransform.anchoredPosition = new Vector2(150, -14);
                text.color = new Color32(255, 243, 228, 255);
                text.richText = true;
                links[i] = text.gameObject.AddComponent<TermLinkTapTarget>();
                links[i].Configure(null, (target, id) => ShowTerm(target, id), 10);
            }
        }

        public void Refresh(string unitName, UnitData data = null)
        {
            if (currentUnit != unitName) { popup?.HideImmediately(); effectTooltip?.Hide(); }
            currentUnit = unitName;
            int unit = unitName == "베탕" ? 0 : unitName == "델탕" ? 1 : unitName == "감망" ? 2 : unitName == "디시그망" ? 3 : unitName == "젤탕" ? 4 : 5;
            for (int i = 0; i < descriptions.Length; i++)
            {
                var presentation=UnitBonusChart.GetPresentation(data!=null?UnitBalanceRuntime.GetUnitKey(data.name):"AlphanChief",(i+1)*3);
                string description = presentation?.Text ?? data?.LevelBonuses?.Find(bonus => bonus.Level == (i + 1) * 3)?.Description
                    ?? Labels[unit][i] ?? string.Empty;
                description=description.Replace("%p","%");
                string text = presentation==null||presentation.HighlightNumbers?HighlightNumbers(description):description;
                if(presentation==null||presentation.LinkTerms)text=text.Replace("[해킹]", Mark("해킹", "hack_stack"))
                    .Replace("[자폭드론]", Mark("자폭드론", "self_destruct_drone"))
                    .Replace("[액티브 스킬]", Mark("액티브 스킬", "chief_skill"));
                if (presentation?.EffectLink=="ChiefAttackSpeed")text=text.Replace("{공격속도효과}","<link=\""+AttackSpeedBuffId+"\"><color=#91E6A6>공격속도 증가</color></link>");
                if (presentation==null && unit == 5 && i == 3)
                    text = text.Replace("공격속도 증가", "<link=\"" + AttackSpeedBuffId + "\"><color=#91E6A6>공격속도 증가</color></link>");
                if (descriptions[i].text == text) continue;
                descriptions[i].text = text; links[i].RefreshHitArea();
            }
        }
        // Apply to plain copy before inserting links/colors so numeric tag IDs stay intact.
        public static string HighlightNumbers(string text) => Regex.Replace(text,
            @"[+-]?\d+(?:\.\d+)?(?:%p?|초|회|배)?", "<color=#FFA64D>$0</color>");
        private string Mark(string name, string id)
        {
            string label = "<color=#FFDD8F>[" + name + "]</color>";
            return glossarySettings != null && glossarySettings.Glossary != null && glossarySettings.Glossary.TryFind(id, out _)
                ? "<link=\"" + TermLinker.LinkPrefix + id + "\">" + label + "</link>" : label;
        }
        private void ShowTerm(TermLinkTapTarget target, string id)
        {
            if (popup != null && popup.BlocksOwnerInput) return;
            if (id == AttackSpeedBuffId)
            {
                if (effectTooltip == null) effectTooltip = DeckUpgradeEffectTooltip.Create(popupParent, descriptions[0], viewStyle);
                effectTooltip.Show(target, id); return;
            }
            if (id.StartsWith(TermLinker.LinkPrefix)) id = id.Substring(TermLinker.LinkPrefix.Length);
            ShowEntry(id);
        }
        private void ShowEntry(string id)
        {
            if (glossarySettings == null || glossarySettings.Glossary == null) return;
            if (!glossarySettings.Glossary.TryFind(id, out var entry)) return;
            if (popup == null)
            {
                popup = TermPopup.Create(popupParent, descriptions[0], viewStyle.RoundedFill, glossarySettings.BlurShader);
                var yellow = new Color32(255, 221, 143, 255);
                popup.ConfigureLinks(new TermLinker(glossarySettings.Glossary, yellow, yellow));
                popup.ConfigureDescriptionMode(new DescriptionModeState());
                popup.SiblingTapped += ShowEntry;
            }
            popup.transform.SetAsLastSibling(); popup.Show(entry, null);
        }
        private void OnDisable() { popup?.HideImmediately(); effectTooltip?.Hide(); }
        private void OnDestroy()
        {
            if (popup != null) Destroy(popup.gameObject);
            if (effectTooltip != null) Destroy(effectTooltip.gameObject);
        }
    }
}
