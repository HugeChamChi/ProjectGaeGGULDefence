using System;
using VContainer;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 디버그 콘솔은 다른 시스템에서 절대 참조하지 않아야 합니다. (완전한 독립성)
public class UI_DebugConsole : UI_Base
{
    [Inject] private CurrencyManager _currencyManager;

    [Header("Debug Console specific")]
    [SerializeField] private Transform contentRoot;
    [SerializeField] private UI_DebugItem itemPrefab;
    
    [Header("Categories")]
    [SerializeField] private Button btn_Units;
    [SerializeField] private Button btn_Currency;
    
    [Header("Modes")]
    [SerializeField] private Button btn_ToggleMode;
    [SerializeField] private Text txt_CurrentMode;

    private enum DebugMode { Possessed, All }
    private enum DebugCategory { Units, Currency }

    private DebugMode _currentMode = DebugMode.Possessed;
    private DebugCategory _currentCategory = DebugCategory.Units;

    private List<UI_DebugItem> _spawnedItems = new List<UI_DebugItem>();

    protected override void Awake()
    {
        base.Awake();
        BindButtons();
    }

    private void BindButtons()
    {
        if (btn_Units != null) btn_Units.onClick.AddListener(() => ChangeCategory(DebugCategory.Units));
        if (btn_Currency != null) btn_Currency.onClick.AddListener(() => ChangeCategory(DebugCategory.Currency));
        if (btn_ToggleMode != null) btn_ToggleMode.onClick.AddListener(ToggleMode);
    }

    private void ToggleMode()
    {
        _currentMode = _currentMode == DebugMode.Possessed ? DebugMode.All : DebugMode.Possessed;
        if (txt_CurrentMode != null)
        {
            txt_CurrentMode.text = _currentMode == DebugMode.Possessed ? "Mode: Possessed (Can Delete/Reduce)" : "Mode: All (Can Add)";
        }
        RefreshList();
    }

    private void ChangeCategory(DebugCategory category)
    {
        _currentCategory = category;
        RefreshList();
    }

    public void RefreshList()
    {
        ClearList();

        if (_currentCategory == DebugCategory.Units)
        {
            RefreshUnits();
        }
        else if (_currentCategory == DebugCategory.Currency)
        {
            RefreshCurrency();
        }
    }

    private void RefreshUnits()
    {
        // Table이 초기화되어 있는지 확인 (게임 실행 후 Table 데이터가 있어야 함)
        if (Table.Character == null || Table.Character.Count == 0)
        {
            Debug.LogWarning("[DebugConsole] Table.Character is not initialized or empty.");
            return;
        }

        foreach (var charData in Table.Character.Characters)
        {
            int ownedCount = Player.Character != null ? Player.Character.GetCount(charData.Id) : 0;
            
            // 보유 모드일 때는 가진 유닛만 표시
            if (_currentMode == DebugMode.Possessed && ownedCount <= 0) continue;

            string itemName = $"{charData.Name} (ID:{charData.Id}) [Count:{ownedCount}]";
            string actionName = _currentMode == DebugMode.Possessed ? "-1" : "+1";

            CreateItem(itemName, actionName, () =>
            {
                if (_currentMode == DebugMode.Possessed)
                {
                    if (ownedCount > 0)
                    {
                        Player.Character?.AddCharacter(charData.Id, -1);
                        Debug.Log($"[DebugConsole] Removed 1 {charData.Name}");
                    }
                }
                else
                {
                    Player.Character?.AddCharacter(charData.Id, 1);
                    Debug.Log($"[DebugConsole] Added 1 {charData.Name}");
                }
                RefreshList(); // 화면 갱신
            });
        }
    }

    private void RefreshCurrency()
    {
        // 1. Out-game Currency (Gold, Diamond)
        var playerData = Player.PlayerData?.Data;
        if (playerData != null)
        {
            // Gold
            string goldName = $"Gold [{playerData.Gold}]";
            string goldAction = _currentMode == DebugMode.Possessed ? "-1000" : "+1000";
            CreateItem(goldName, goldAction, () =>
            {
                int amount = _currentMode == DebugMode.Possessed ? -1000 : 1000;
                playerData.Gold += amount;
                Player.PlayerData.RefreshUI(playerData); // UI 갱신 이벤트 호출
                RefreshList();
            });

            // Diamond
            string diaName = $"Diamond [{playerData.Diamond}]";
            string diaAction = _currentMode == DebugMode.Possessed ? "-100" : "+100";
            CreateItem(diaName, diaAction, () =>
            {
                int amount = _currentMode == DebugMode.Possessed ? -100 : 100;
                playerData.Diamond += amount;
                Player.PlayerData.RefreshUI(playerData);
                RefreshList();
            });
        }
        else
        {
            Debug.LogWarning("[DebugConsole] PlayerData is null. Out-game currency not loaded.");
        }

        // 2. In-game Currency
        if (_currencyManager != null)
        {
            string inGameName = $"InGame Currency [{_currencyManager.Currency}]";
            string inGameAction = _currentMode == DebugMode.Possessed ? "-100" : "+100";
            CreateItem(inGameName, inGameAction, () =>
            {
                if (_currentMode == DebugMode.Possessed)
                    _currencyManager.Spend(100);
                else
                    _currencyManager.AddCurrency(100);
                
                RefreshList();
            });
        }
    }

    private void CreateItem(string itemName, string actionName, Action onActionClicked)
    {
        if (itemPrefab == null || contentRoot == null) return;

        var itemGo = Instantiate(itemPrefab, contentRoot);
        itemGo.gameObject.SetActive(true);
        itemGo.Setup(itemName, actionName, onActionClicked);
        _spawnedItems.Add(itemGo);
    }

    private void ClearList()
    {
        foreach (var item in _spawnedItems)
        {
            if (item != null)
            {
                Destroy(item.gameObject);
            }
        }
        _spawnedItems.Clear();
    }

    public override void Open()
    {
        base.Open();
        if (txt_CurrentMode != null)
            txt_CurrentMode.text = _currentMode == DebugMode.Possessed ? "Mode: Possessed (Can Delete/Reduce)" : "Mode: All (Can Add)";
        RefreshList();
    }
}
