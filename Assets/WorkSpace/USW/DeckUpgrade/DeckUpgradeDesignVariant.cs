using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GaeGGUL.DeckUpgrade
{
    [DefaultExecutionOrder(1000)]
    public sealed class DeckUpgradeDesignVariant : MonoBehaviour
    {
        [Range(1, 3)] public int variant = 1;
        public GameObject lobbyTopTemplate;
        private Transform page, grid;
        private bool lastOwnedOnly;
        private IEnumerator Start() { yield return null; yield return null; Apply(); }
        public void Apply()
        {
            page = transform.Find("DroneUpgradeCanvas/SafeArea/Page");
            page.Find("Header").gameObject.SetActive(false);
            var banner = page.Find("CollectionBanner").GetComponent<Image>();
            page.GetComponent<Image>().color = banner.color;
            transform.Find("DroneUpgradeCanvas").GetComponent<Image>().color = banner.color;
            banner.sprite = null;
            var divider = (RectTransform)page.Find("CollectionBanner/BannerEdge");
            divider.anchoredPosition = new Vector2(0, -390); divider.sizeDelta = new Vector2(1080, 32);
            divider.GetComponent<Image>().sprite = null;
            if (lobbyTopTemplate != null)
            {
                var top = (RectTransform)Instantiate(lobbyTopTemplate, page, false).transform;
                top.name = "LobbyTopPreview"; top.anchorMin = top.anchorMax = new Vector2(.5f, 1); top.pivot = new Vector2(.5f, 1);
                top.sizeDelta = new Vector2(1080, 213); top.anchoredPosition = new Vector2(0, 12); top.localScale = Vector3.one;
                top.SetAsLastSibling(); page.Find("UnitDetailsPopup").SetAsLastSibling();
            }
            var summary = (RectTransform)page.Find("DeckCollection/DeckSummaryStrip");
            summary.GetComponent<Image>().sprite = null;
            summary.anchoredPosition = new Vector2(-24, 0); summary.sizeDelta = new Vector2(1080, 64);
            foreach (RectTransform item in summary) item.anchoredPosition += new Vector2(24, 0);
            grid = page.Find("DeckCollection/UnitListPanel/UnitViewport/UnitGrid");
            foreach (var image in page.GetComponentsInChildren<Image>(true))
            {
                if (image.name == "DroneFrameTrim" || image.name == "SlotNamePlate") image.gameObject.SetActive(false);
                if (image.name == "ChiefUnit" || image.name.StartsWith("Unit_") || image.name.StartsWith("LockedUnit_")) image.color = Color.clear;
                if (variant == 3 && image.name == "CardShadow") image.gameObject.SetActive(false);
                if (image.name == "PortraitInset" && image.transform.parent.name == "ColoredEdge")
                {
                    var rect = image.rectTransform;
                    float inset = variant == 2 ? 12 : variant == 3 ? 3 : 7;
                    rect.anchoredPosition = new Vector2(inset, -inset);
                    rect.sizeDelta = new Vector2(222 - inset * 2, 282 - inset * 2);
                }
            }
            foreach (var label in page.GetComponentsInChildren<TMP_Text>(true)) label.fontStyle |= FontStyles.Bold;
            foreach (var trim in page.GetComponentsInChildren<Transform>(true))
                if (trim.name == "DroneFrameTrim" && trim.Find("ChiefCrestBase") != null) AddChiefEffects(trim);
            Arrange();
            RemoveUnusedViews();
            var dialog = page.Find("UnitDetailsPopup/UnitInfoDialog");
            dialog.gameObject.AddComponent<DeckUpgradeStatsTabs>().Build(GetComponent<DeckSelectorConcept>());
        }
        private void RemoveUnusedViews()
        {
            var bindings = new GameObject("RuntimeBindings", typeof(RectTransform)).transform;
            bindings.SetParent(page, false); bindings.gameObject.SetActive(false);
            foreach (string path in new[] { "Header/Wallet/Amount", "DeckScroll/Viewport/DeckContent/UnfoldingUnitList/ChiefBonus", "DeckCollection/UnitListPanel/UnitViewport/UnitGrid/ChiefUnit/ChiefLevel" })
            {
                var value = page.Find(path); if (value != null) value.SetParent(bindings, false);
            }
            foreach (string path in new[] { "Header", "DeckScroll" })
            {
                var old = page.Find(path); if (old != null) RemoveView(old.gameObject);
            }
            foreach (var old in page.GetComponentsInChildren<Transform>(true))
                if (old != null && (old.name == "DroneFrameTrim" || old.name == "SlotNamePlate")) RemoveView(old.gameObject);
        }
        private static void RemoveView(GameObject old)
        {
            old.name = "RemovedUnusedView"; old.SetActive(false);
            if (Application.isPlaying) Destroy(old); else DestroyImmediate(old);
        }
        private void AddChiefEffects(Transform originalTrim)
        {
            var frame = originalTrim.parent;
            var crown = (RectTransform)new GameObject("FloatingGoldCrown", typeof(RectTransform)).transform;
            crown.SetParent(frame, false); crown.anchorMin = crown.anchorMax = new Vector2(0, 1); crown.pivot = new Vector2(0, 1);
            crown.sizeDelta = new Vector2(232, 292); crown.anchoredPosition = Vector2.zero;
            foreach (Transform part in originalTrim)
                if (part.name.StartsWith("ChiefCrest")) Instantiate(part.gameObject, crown, false).name = part.name;
            crown.gameObject.AddComponent<DeckUpgradeCrownFloat>();
            var motes = (RectTransform)new GameObject("ChiefGoldMotes", typeof(RectTransform)).transform;
            motes.SetParent(frame, false); motes.anchorMin = motes.anchorMax = new Vector2(0, 1); motes.pivot = new Vector2(.5f, .5f);
            motes.sizeDelta = new Vector2(232, 292); motes.anchoredPosition = new Vector2(116, -146); motes.localScale = Vector3.one * .45f;
            motes.gameObject.AddComponent<RisingUpgradeMotes>().Build(GetComponent<DeckSelectorConcept>().screen.style.CircleFill);
        }
        private void LateUpdate()
        {
            if (grid != null && lastOwnedOnly != !grid.Find("UnownedHeader").gameObject.activeSelf) Arrange();
        }
        private void Arrange()
        {
            var divider = (RectTransform)grid.Find("UnownedHeader"); lastOwnedOnly = !divider.gameObject.activeSelf;
            float scale = variant == 2 ? 1.16f : variant == 3 ? 1.12f : 1.2f;
            float rowStep = variant == 2 ? 510 : variant == 3 ? 455 : 490;
            int ownedCount = GetComponent<DeckSelectorConcept>().screen.units.Length + 1;
            float dividerY = 40 + Mathf.CeilToInt(ownedCount / 3f) * rowStep;
            divider.anchoredPosition = new Vector2(25, -dividerY);
            int count = 0;
            foreach (Transform child in grid)
            {
                if (child == divider) continue;
                bool owned = count < ownedCount; int index = owned ? count : count - ownedCount;
                var card = (RectTransform)child;
                card.localScale = Vector3.one * scale;
                card.anchoredPosition = new Vector2((1032 - (232 * scale + 660)) * .5f + index % 3 * 330, -(owned ? 40 : dividerY + 104) - index / 3 * rowStep);
                count++;
            }
            ((RectTransform)grid).sizeDelta = new Vector2(1032, lastOwnedOnly ? dividerY + 20 : dividerY + 104 + Mathf.CeilToInt((count - ownedCount) / 3f) * rowStep + 20);
        }
    }
}
