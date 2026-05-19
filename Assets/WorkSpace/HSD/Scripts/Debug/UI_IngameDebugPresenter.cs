using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace HSD.InGameDebug
{
    public class UI_IngameDebugPresenter
    {
        private UI_IngameDebugPanel _view;
        private DebugTabType _currentTab;

        private TotemData[] _allTotemData;

        public UI_IngameDebugPresenter(UI_IngameDebugPanel view)
        {
            _view = view;
        }

        public void Init()
        {
            ChangeTab(0);
        }

        public void ChangeTab(DebugTabType tab)
        {
            _currentTab = tab;
            _view.HideAddView();
            RefreshList();
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
            _view.ClearAddList();
            _view.ShowAddView();

            if (_currentTab == DebugTabType.Totem)
            {
                var pool = TotemSelectUI.Instance?.TotemPool;
                if (pool != null)
                {
                    foreach (var data in pool)
                    {
                        if (data == null) continue;
                        _view.AddAddItem(data, data.totemName, data.description, data.icon, "+", OnAddTotem);
                        // 최적화: 매 프레임마다 일정 개수만 생성하여 렉 방지
                        await Cysharp.Threading.Tasks.UniTask.Yield();
                    }
                }
            }
            else if (_currentTab == DebugTabType.LevelUp)
            {
                var pool = LevelUpManager.Instance?.LevelUpPool;
                if (pool != null)
                {
                    foreach (var data in pool)
                    {
                        if (data == null) continue;
                        _view.AddAddItem(data, data.chooseName, data.description, data.icon, "+", OnAddLevelUp);
                        // 최적화: 매 프레임마다 일정 개수만 생성하여 렉 방지
                        await Cysharp.Threading.Tasks.UniTask.Yield();
                    }
                }
            }
            else if (_currentTab == DebugTabType.Unit)
            {
                var pool = UnitFactory.Instance?.UnitDataList;
                if (pool != null)
                {
                    foreach (var data in pool)
                    {
                        if (data == null) continue;
                        _view.AddAddItem(data, data.unitName, data.description, data.icon, "+", OnAddUnit);
                        await Cysharp.Threading.Tasks.UniTask.Yield();
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
                TotemSpawner.Instance.SellTotem(totem);
                RefreshList();
            }
        }

        private void OnAddTotem(object obj)
        {
            if (obj is TotemData data)
            {
                TotemSpawner.Instance.SpawnTotemByData(data);
                _view.HideAddView();
                RefreshList();
            }
        }

        // --- LevelUp Logic ---
        private void RefreshLevelUpList()
        {
            if (LevelUpManager.Instance == null) return;

            var chosenIds = LevelUpManager.Instance.ChosenIds.ToList();
            var pool = LevelUpManager.Instance.LevelUpPool;

            foreach (var id in chosenIds)
            {
                var data = pool?.FirstOrDefault(d => d != null && d.chooseId == id);
                if (data != null)
                {
                    _view.AddListItem(data, data.chooseName, data.description, data.icon, "X", OnRemoveLevelUp);
                }
            }
        }

        private void OnRemoveLevelUp(object obj)
        {
            if (obj is LevelUpData data)
            {
                LevelUpManager.Instance.RemoveEffect(data);
                RefreshList();
            }
        }

        private void OnAddLevelUp(object obj)
        {
            if (obj is LevelUpData data)
            {
                LevelUpManager.Instance.ApplyEffect(data);
                _view.HideAddView();
                RefreshList();
            }
        }

        // --- Chief Logic ---
        private void RefreshChiefList()
        {
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
                if (Player.Chief.SelectedChiefId == data.Id) return;

                Player.Chief.SetSelectedChief(data.Id);
                ChieftainSpawner.Instance?.ChangeChieftain(data.Id);
                RefreshList();
            }
        }

        // --- Unit Logic ---
        private void RefreshUnitList()
        {
            var cells = Manager.Grid?.GetOccupiedCells();
            if (cells == null) return;

            foreach (var cell in cells)
            {
                var unit = cell.OccupyingUnit;
                if (unit == null || unit.unitData == null) continue;

                // 족장은 제외 (족장 탭에서 관리)
                if (ChieftainSpawner.Instance != null && ChieftainSpawner.Instance.ChieftainUnit == unit) continue;

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
                var emptyCells = Manager.Grid?.GetEmptyCells();
                if (emptyCells == null || emptyCells.Count == 0)
                {
                    Debug.LogWarning("[Debug] 빈 셀 없음 — 유닛 생성 취소");
                    return;
                }

                var cell = emptyCells[Random.Range(0, emptyCells.Count)];
                var unit = UnitFactory.Instance.CreateUnit(data.unitType);
                if (unit != null)
                {
                    Manager.Spawner.PlaceUnitWithEffect(unit, cell);
                }

                _view.HideAddView();
                RefreshList();
            }
        }
    }
}