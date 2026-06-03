using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace HSD.InGameDebug
{
    public class UI_IngameDebugPresenter
    {
        private UI_IngameDebugPanel _view;
        private DebugTabType _currentTab;
        private CancellationTokenSource _addViewCts;

        private TotemData[] _allTotemData;

        public UI_IngameDebugPresenter(UI_IngameDebugPanel view)
        {
            _view = view;
        }

        public void Dispose()
        {
            _addViewCts?.Cancel();
            _addViewCts?.Dispose();
            _addViewCts = null;
            _view = null;
        }

        public void Init()
        {
            ChangeTab(0);
        }

        public void ChangeTab(DebugTabType tab)
        {
            _currentTab = tab;
            _view.HideAddView();
            _view.CloseAllInfoPopups();
            
            _view.UpdateInfoButtonVisibility(tab);
            
            RefreshList();
        }

        public void OpenInfoView()
        {
            _view.ShowInfoPopup(_currentTab);
        }

        private void RefreshList()
        {
            _view.ClearList();
            
            switch (_currentTab)
            {
                case DebugTabType.Totem:
                    _view.ShowAddButton(true);
                    RefreshTotemList();
                    break;
                case DebugTabType.LevelUp:
                    _view.ShowAddButton(true);
                    RefreshLevelUpList();
                    break;
                case DebugTabType.Chief:
                    _view.ShowAddButton(false);
                    RefreshChiefList();
                    break;
                case DebugTabType.Unit:
                    _view.ShowAddButton(true);
                    RefreshUnitList();
                    break;
            }
        }

        public async void OpenAddView()
        {
            _addViewCts?.Cancel();
            _addViewCts?.Dispose();
            _addViewCts = new CancellationTokenSource();
            var token = _addViewCts.Token;

            if (_view == null) return;

            _view.ClearAddList();
            _view.ShowAddView();

            if (_currentTab == DebugTabType.Totem)
            {
                var pool = UnityEngine.Object.FindObjectOfType<TotemSelectUI>(true)?.TotemPool;
                if (pool != null)
                {
                    foreach (var data in pool)
                    {
                        if (data == null) continue;
                        if (token.IsCancellationRequested || _view == null || _view.gameObject == null) break;
                        _view.AddAddItem(data, data.totemName, data.description, data.icon, "+", OnAddTotem);
                        // 최적화: 매 프레임마다 일정 개수만 생성하여 렉 방지
                        bool canceled = await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
                        if (canceled) break;
                    }
                }
            }
            else if (_currentTab == DebugTabType.LevelUp)
            {
                var levelUpManager = UnityEngine.Object.FindObjectOfType<LevelUpManager>(true);
                var pool = levelUpManager?.LevelUpPool;
                if (pool != null)
                {
                    foreach (var data in pool)
                    {
                        if (data == null) continue;
                        if (token.IsCancellationRequested || _view == null || _view.gameObject == null) break;
                        
                        string statDesc = $"{data.description}\n";
                        if (data.primaryValue != 0) statDesc += $"[{data.primaryEffect}] {data.primaryValue:+#;-#;0} ";
                        if (data.secondaryValue != 0) statDesc += $"[{data.secondaryEffect}] {data.secondaryValue:+#;-#;0} ";
                        if (data.specialValue != 0) statDesc += $"[{data.specialEffect}] {data.specialValue:+#;-#;0}";

                        _view.AddAddItem(data, data.chooseName, statDesc.Trim(), data.icon, "+", OnAddLevelUp);
                        // 최적화: 매 프레임마다 일정 개수만 생성하여 렉 방지
                        bool canceled = await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
                        if (canceled) break;
                    }
                }
            }
            else if (_currentTab == DebugTabType.Unit)
            {
                var pool = UnityEngine.Object.FindObjectOfType<UnitFactory>(true)?.UnitDataList;
                if (pool != null)
                {
                    foreach (var data in pool)
                    {
                        if (data == null) continue;
                        if (token.IsCancellationRequested || _view == null || _view.gameObject == null) break;
                        _view.AddAddItem(data, data.unitName, data.description, data.icon, "+", OnAddUnit);
                        bool canceled = await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
                        if (canceled) break;
                    }
                }
            }
        }

        // --- Totem Logic ---
        private void RefreshTotemList()
        {
            var activeTotems = Object.FindObjectsOfType<TotemBase>();
            foreach (var totem in activeTotems)
            {
                var data = totem.Data;
                
                string name = totem.name;
                string desc = "배치된 토템";
                Sprite icon = null;
                
                if (data != null)
                {
                    name = data.totemName;
                    desc = data.description;
                    icon = data.icon;
                }

                _view.AddListItem(totem, name, desc, icon, "X", OnRemoveTotem);
            }
        }

        private void OnRemoveTotem(object obj)
        {
            if (obj is TotemBase totem)
            {
                var spawner = UnityEngine.Object.FindObjectOfType<TotemSpawner>(true);
                spawner?.SellTotem(totem);
                RefreshList();
            }
        }

        private void OnAddTotem(object obj)
        {
            if (obj is TotemData data)
            {
                var spawner = UnityEngine.Object.FindObjectOfType<TotemSpawner>(true);
                spawner?.SpawnTotemByData(data);
                _view.HideAddView();
                RefreshList();
            }
        }

        // --- LevelUp Logic ---
        private void RefreshLevelUpList()
        {
            var levelUpManager = UnityEngine.Object.FindObjectOfType<LevelUpManager>(true);
            if (levelUpManager == null) return;

            var chosenIds = levelUpManager.ChosenIds.ToList();
            var pool = levelUpManager.LevelUpPool;

            foreach (var id in chosenIds)
            {
                var data = pool?.FirstOrDefault(d => d != null && d.chooseId == id);
                if (data != null)
                {
                    string statDesc = $"{data.description}\n";
                    if (data.primaryValue != 0) statDesc += $"[{data.primaryEffect}] {data.primaryValue:+#;-#;0} ";
                    if (data.secondaryValue != 0) statDesc += $"[{data.secondaryEffect}] {data.secondaryValue:+#;-#;0} ";
                    if (data.specialValue != 0) statDesc += $"[{data.specialEffect}] {data.specialValue:+#;-#;0}";

                    _view.AddListItem(data, data.chooseName, statDesc.Trim(), data.icon, "X", OnRemoveLevelUp);
                }
            }
        }

        private void OnRemoveLevelUp(object obj)
        {
            if (obj is LevelUpData data)
            {
                UnityEngine.Object.FindObjectOfType<LevelUpManager>(true)?.RemoveEffect(data);
                RefreshList();
            }
        }

        private void OnAddLevelUp(object obj)
        {
            if (obj is LevelUpData data)
            {
                UnityEngine.Object.FindObjectOfType<LevelUpManager>(true)?.ApplyEffect(data);
                _view.HideAddView();
                RefreshList();
            }
        }

        // --- Chief Logic ---
        private void RefreshChiefList()
        {
            if (Player.Chief == null) return;
            
            int currentChiefId = Player.Chief.SelectedChiefId;
            var chiefs = Table.Character.Chief.Chiefs;

            foreach (var chief in chiefs)
            {
                bool isCurrent = chief.Id == currentChiefId;
                string btnText = isCurrent ? "선택됨" : "교체";
                
                _view.AddListItem(chief, chief.Name, $"ID: {chief.Id}", chief.Icon, btnText, OnChangeChief);
            }
        }

        private void OnChangeChief(object obj)
        {
            if (obj is ChiefData data)
            {
                if (Player.Chief != null && Player.Chief.SelectedChiefId == data.Id) return;

                Player.Chief?.SetSelectedChief(data.Id);
                UnityEngine.Object.FindObjectOfType<ChieftainSpawner>(true)?.ChangeChieftain(data.Id);
                RefreshList();
            }
        }

        // --- Unit Logic ---
        private void RefreshUnitList()
        {
            var cells = UnityEngine.Object.FindObjectOfType<GridManager>(true)?.GetOccupiedCells();
            if (cells == null) return;

            foreach (var cell in cells)
            {
                var unit = cell.OccupyingUnit;
                if (unit == null || unit.unitData == null) continue;

                // 족장은 제외 (족장 탭에서 관리)
                var chieftainSpawner = UnityEngine.Object.FindObjectOfType<ChieftainSpawner>(true);
                if (chieftainSpawner != null && chieftainSpawner.ChieftainUnit == unit) continue;

                _view.AddListItem(unit, unit.unitData.unitName, $"", unit.unitData.icon, "X", OnRemoveUnit);
            }
        }

        private void OnRemoveUnit(object obj)
        {
            if (obj is UnitBase unit)
            {
                var cell = unit.currentCell;
                if (cell != null) cell.RemoveUnit();
                Object.Destroy(unit.gameObject);
                RefreshList();
            }
        }

        private void OnAddUnit(object obj)
        {
            if (obj is UnitData data)
            {
                var emptyCells = UnityEngine.Object.FindObjectOfType<GridManager>(true)?.GetEmptyCells();
                if (emptyCells == null || emptyCells.Count == 0)
                {
                    Debug.LogWarning("[Debug] 빈 셀 없음 — 유닛 생성 취소");
                    return;
                }

                var cell = emptyCells[Random.Range(0, emptyCells.Count)];
                var factory = UnityEngine.Object.FindObjectOfType<UnitFactory>(true);
                var spawner = UnityEngine.Object.FindObjectOfType<UnitSpawner>(true);
                
                if (factory != null && spawner != null)
                {
                    var unit = factory.CreateUnit(data.unitType);
                    if (unit != null)
                    {
                        spawner.PlaceUnitWithEffect(unit, cell);
                    }
                }

                _view.HideAddView();
                RefreshList();
            }
        }
    }
}