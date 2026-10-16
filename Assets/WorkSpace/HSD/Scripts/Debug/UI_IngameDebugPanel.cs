using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace HSD.InGameDebug
{
    public enum DebugTabType { Unit, Totem, LevelUp, Chief }

    /// <summary>Scene-owned debug workspace, opened by the existing hidden four-tap trigger.</summary>
    public class UI_IngameDebugPanel : UI_Base
    {
        private const int DebugSortingOrder = 1100; // Existing hidden-gesture Canvas uses order 1000.
        [SerializeField] private UI_DebugItemSlot slotPrefab;
        private UI_IngameDebugPresenter _presenter;
        private DebugPanelLayout _layout;
        private IDebugInfoPopup[] _infoPopups;
        private readonly List<UI_DebugItemSlot> _slots = new List<UI_DebugItemSlot>();
        private int _used;
        private bool _catalog = true;
        private bool _loading;
        private DebugTabType _tab;
        private TimeScaleService _timeScale;

        /// <summary>Uses the scene's services and current run state without global manager searches.</summary>
        [Inject]
        public void Construct(GridManager grid, UnitFactory factory, UnitSpawner units, TotemSpawner totems,
            LevelUpManager choices, LevelUpRunState state, TimeScaleService timeScale,
            GameManager game, GameInitializer initializer)
        {
            _timeScale = timeScale;
            _presenter = new UI_IngameDebugPresenter(this, grid, factory, units, totems, choices, state, game, initializer);
        }

        protected override void Awake()
        {
            base.Awake();
            _infoPopups = GetComponentsInChildren<IDebugInfoPopup>(true);
        }

        private void EnsureBuilt()
        {
            if (_layout != null) return;
            _infoPopups ??= GetComponentsInChildren<IDebugInfoPopup>(true);
            var font = Resources.FindObjectsOfTypeAll<TMP_FontAsset>().FirstOrDefault(f => f.name == "NotoSansKR-Medium SDF");
            if (font == null) font = GetComponentInChildren<TMP_Text>(true)?.font;
            if (font == null && slotPrefab != null) font = slotPrefab.GetComponentInChildren<TMP_Text>(true)?.font;
            foreach (Transform child in transform) child.gameObject.SetActive(false);
            var rect = (RectTransform)transform;
            DebugPanelLayout.Stretch(rect, 0, 0, 0, 0); rect.localScale = Vector3.one;
            var parentCanvas = transform.parent.GetComponentInParent<Canvas>();
            _canvas = GetComponent<Canvas>();
            if (_canvas == null) _canvas = gameObject.AddComponent<Canvas>();
            if (parentCanvas != null) _canvas.sortingLayerID = parentCanvas.sortingLayerID;
            _canvas.overrideSorting = true; _canvas.sortingOrder = DebugSortingOrder;
            if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
            targetAnim = null; btn_Close = null; btn_BackgroundClose = null;
            _layout = new DebugPanelLayout(transform, font, Close, i => _presenter?.ChangeTab((DebugTabType)i),
                value => _presenter?.SetCatalog(value), i => _presenter?.SetTier((Tier)i),
                () => _presenter?.Refresh(), TogglePause, () => ShowInfoPopup(_tab));
            _layout.Search.onValueChanged.AddListener(Filter);
            foreach (var popup in _infoPopups)
            {
                var popupTransform = ((Component)popup).transform;
                popupTransform.SetParent(transform, false); popupTransform.SetAsLastSibling();
            }
        }

        /// <summary>Opens the workspace through both UI_Base and direct async callers.</summary>
        public override async UniTask OpenAsync()
        {
            gameObject.SetActive(true);
            EnsureBuilt();
            await base.OpenAsync();
            CloseAllInfoPopups();
            _layout.Pause(_timeScale?.IsRequesting(this) == true);
            if (_presenter != null) _presenter.Init();
            else Notify("서비스 연결을 기다리고 있습니다. 잠시 후 다시 열어주세요", true);
        }

        /// <summary>Cancels catalog work and releases only this panel's pause on every close path.</summary>
        public override async UniTask CloseAsync()
        {
            Cleanup();
            if (_layout != null) _layout.Search.DeactivateInputField();
            await base.CloseAsync();
            gameObject.SetActive(false);
        }

        /// <summary>Closes the retained read-only detail popups.</summary>
        public void CloseAllInfoPopups()
        {
            if (_infoPopups == null) return;
            foreach (var popup in _infoPopups) popup.ClosePopup();
        }

        /// <summary>Shows the original detail tools above the modern list.</summary>
        public void ShowInfoPopup(DebugTabType tab)
        {
            foreach (var popup in _infoPopups)
            {
                if (popup.TabType == tab) { ((Component)popup).transform.SetAsLastSibling(); popup.OpenPopup(); }
                else popup.ClosePopup();
            }
        }

        /// <summary>Starts a pooled refresh and synchronizes tab/mode state.</summary>
        public void BeginList(DebugTabType tab, bool catalog, Tier tier, bool loading)
        {
            _tab = tab; _catalog = catalog; _loading = loading; _used = 0;
            foreach (var slot in _slots) slot.gameObject.SetActive(false);
            _layout.Select(tab, catalog, tier, _infoPopups.Any(p => p.TabType == tab));
            _layout.ResetScroll(); Filter(_layout.Search.text);
        }

        /// <summary>Adds a row, reusing slots across searches and categories.</summary>
        public void AddItem(object data, string title, string description, Sprite icon, string action,
            Action<object> onClick, bool enabled = true, string searchKeywords = "")
        {
            if (_used == _slots.Count) _slots.Add(_layout.CreateSlot());
            var slot = _slots[_used++];
            slot.Init(data, title, description, icon, action, onClick, searchKeywords); slot.SetActionEnabled(enabled);
            Filter(_layout.Search.text);
        }

        /// <summary>Completes a progressive catalog load.</summary>
        public void FinishList() { _loading = false; Filter(_layout.Search.text); }

        /// <summary>Displays the command result while keeping the workspace open.</summary>
        public void Notify(string message, bool error = false) => _layout?.Message(message, error);

        /// <summary>Clears search for a category change, preserving it during command refreshes.</summary>
        public void ResetSearch() { if (_layout != null) _layout.Search.SetTextWithoutNotify(""); }

        private void Filter(string query)
        {
            int shown = 0;
            for (int i = 0; i < _slots.Count; i++)
            {
                bool visible = i < _used && _slots[i].Matches(query);
                _slots[i].gameObject.SetActive(visible);
                if (!visible) continue;
                var rect = (RectTransform)_slots[i].transform;
                rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, 1);
                rect.offsetMin = new Vector2(0, -shown * 164f - 152f); rect.offsetMax = new Vector2(0, -shown * 164f);
                shown++;
            }
            _layout.Content.sizeDelta = new Vector2(0, Mathf.Max(0, shown * 164f - 12f));
            _layout.Count(shown, _used, _catalog, _loading, !string.IsNullOrWhiteSpace(query));
        }

        private void TogglePause()
        {
            if (_timeScale == null) return;
            if (_timeScale.IsRequesting(this)) _timeScale.Release(this);
            else _timeScale.Pause(this);
            bool requested = _timeScale.IsRequesting(this);
            _layout.Pause(requested);
            Notify(requested ? "게임을 정지했습니다 · 창을 닫으면 해제됩니다" :
                _timeScale.IsPaused ? "다른 화면에서 게임을 정지하고 있습니다" : "게임을 재개했습니다");
        }

        private void Cleanup() { _presenter?.Cancel(); _timeScale?.Release(this); CloseAllInfoPopups(); }
        private void OnDisable() => Cleanup();
        private void OnDestroy()
        {
            Cleanup(); _presenter?.Dispose();
            if (_layout != null) { _layout.Search.onValueChanged.RemoveListener(Filter); _layout.Dispose(); }
        }
    }
}
