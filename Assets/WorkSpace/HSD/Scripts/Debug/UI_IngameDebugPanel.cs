using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
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

        private UI_IngameDebugPresenter _presenter;
        private List<UI_DebugItemSlot> _activeSlots = new List<UI_DebugItemSlot>();
        private List<UI_DebugItemSlot> _activeAddSlots = new List<UI_DebugItemSlot>();

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

            btn_TabTotem?.onClick.AddListener(() => _presenter.ChangeTab(DebugTabType.Totem));
            btn_TabLevelUp?.onClick.AddListener(() => _presenter.ChangeTab(DebugTabType.LevelUp));
            btn_TabChief?.onClick.AddListener(() => _presenter.ChangeTab(DebugTabType.Chief));
            btn_TabUnit?.onClick.AddListener(() => _presenter.ChangeTab(DebugTabType.Unit));

            btn_ShowAddView?.onClick.AddListener(() => _presenter.OpenAddView());
            btn_CloseAddView?.onClick.AddListener(() => HideAddView());
        }

        public override void Open()
        {
            base.Open();
            _presenter.Init();
        }

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
    }
}