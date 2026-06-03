using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using System;

namespace HSD.InGameDebug
{
    public enum DebugTabType
    {
        Unit,
        Totem,
        LevelUp,
        Chief,
    }

    public class UI_IngameDebugPanel : UI_Base
    {
        [Header("Tabs")]
        [SerializeField] private Button btn_TabUnit;
        [SerializeField] private Button btn_TabTotem;
        [SerializeField] private Button btn_TabLevelUp;
        [SerializeField] private Button btn_TabChief;

        [Header("List View")]
        [SerializeField] private Transform listContentParent;
        [SerializeField] private UI_DebugItemSlot slotPrefab;

        [Header("Add View (All Items)")]
        [SerializeField] private GameObject addViewGroup;
        [SerializeField] private Transform addListContentParent;
        [SerializeField] private Button btn_ShowAddView;
        [SerializeField] private Button btn_CloseAddView;

        [Header("Info View")]
        [SerializeField] private Button btn_ShowInfoView;

        private UI_IngameDebugPresenter _presenter;
        private List<UI_DebugItemSlot> _activeSlots = new List<UI_DebugItemSlot>();
        private List<UI_DebugItemSlot> _activeAddSlots = new List<UI_DebugItemSlot>();

        private IDebugInfoPopup[] _infoPopups;

        public static UI_IngameDebugPanel Instance { get; private set; }

        protected override void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            base.Awake();
            _presenter = new UI_IngameDebugPresenter(this);
            _infoPopups = GetComponentsInChildren<IDebugInfoPopup>(true);

            btn_TabTotem?.onClick.AddListener(() => _presenter.ChangeTab(DebugTabType.Totem));
            btn_TabLevelUp?.onClick.AddListener(() => _presenter.ChangeTab(DebugTabType.LevelUp));
            btn_TabChief?.onClick.AddListener(() => _presenter.ChangeTab(DebugTabType.Chief));
            btn_TabUnit?.onClick.AddListener(() => _presenter.ChangeTab(DebugTabType.Unit));

            btn_ShowAddView?.onClick.AddListener(() => _presenter.OpenAddView());
            btn_CloseAddView?.onClick.AddListener(() => HideAddView());
            btn_ShowInfoView?.onClick.AddListener(() => _presenter.OpenInfoView());
        }

        public override void Open()
        {
            base.Open();
            _presenter.Init();
        }

        public Transform GetListContentParent() => listContentParent;

        public void ClearList()
        {
            foreach (var slot in _activeSlots)
            {
                if (slot != null) Destroy(slot.gameObject);
            }
            _activeSlots.Clear();
        }

        public void ClearAddList()
        {
            foreach (var slot in _activeAddSlots)
            {
                if (slot != null) Destroy(slot.gameObject);
            }
            _activeAddSlots.Clear();
        }

        public void AddListItem(object data, string name, string desc, Sprite icon, string btnText, Action<object> onClickAction)
        {
            var slot = Instantiate(slotPrefab, listContentParent);
            slot.Init(data, name, desc, icon, btnText, onClickAction);
            slot.gameObject.SetActive(true);
            _activeSlots.Add(slot);
        }

        public void AddAddItem(object data, string name, string desc, Sprite icon, string btnText, Action<object> onClickAction)
        {
            var slot = Instantiate(slotPrefab, addListContentParent);
            slot.Init(data, name, desc, icon, btnText, onClickAction);
            slot.gameObject.SetActive(true);
            _activeAddSlots.Add(slot);
        }

        public void ShowAddView()
        {
            if (addViewGroup != null) addViewGroup.SetActive(true);
        }

        public void HideAddView()
        {
            if (addViewGroup != null) addViewGroup.SetActive(false);
        }

        public void ShowAddButton(bool show)
        {
            if (btn_ShowAddView != null) btn_ShowAddView.gameObject.SetActive(show);
        }

        public void ShowInfoPopup(DebugTabType tabType)
        {
            foreach (var popup in _infoPopups)
            {
                if (popup.TabType == tabType)
                    popup.OpenPopup();
                else
                    popup.ClosePopup();
            }
        }

        public void UpdateInfoButtonVisibility(DebugTabType tabType)
        {
            if (btn_ShowInfoView != null)
            {
                bool hasPopup = _infoPopups.Any(p => p.TabType == tabType);
                btn_ShowInfoView.gameObject.SetActive(hasPopup);
            }
        }

        protected virtual void OnDestroy()
        {
            btn_TabTotem?.onClick.RemoveAllListeners();
            btn_TabLevelUp?.onClick.RemoveAllListeners();
            btn_TabChief?.onClick.RemoveAllListeners();
            btn_TabUnit?.onClick.RemoveAllListeners();
            btn_ShowAddView?.onClick.RemoveAllListeners();
            btn_CloseAddView?.onClick.RemoveAllListeners();
            btn_ShowInfoView?.onClick.RemoveAllListeners();

            _presenter?.Dispose();
        }
    }
}