using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace HSD.InGameDebug
{
    /// <summary>Connects the debug workspace to scene services and the actual authored catalogs.</summary>
    public sealed class UI_IngameDebugPresenter : IDisposable
    {
        private readonly UI_IngameDebugPanel _view;
        private readonly GridManager _grid;
        private readonly UnitFactory _factory;
        private readonly UnitSpawner _units;
        private readonly TotemSpawner _totems;
        private readonly LevelUpManager _choices;
        private readonly LevelUpRunState _state;
        private readonly GameManager _game;
        private readonly GameInitializer _initializer;
        private DebugTabType _tab;
        private bool _catalog = true;
        private Tier _tier;
        private CancellationTokenSource _listCts;
        private CancellationTokenSource _commandCts;
        private bool _adding;

        /// <summary>Receives the same scoped services that gameplay uses.</summary>
        public UI_IngameDebugPresenter(UI_IngameDebugPanel view, GridManager grid, UnitFactory factory,
            UnitSpawner units, TotemSpawner totems, LevelUpManager choices,
            LevelUpRunState state, GameManager game, GameInitializer initializer)
        {
            _view = view; _grid = grid; _factory = factory; _units = units; _totems = totems;
            _choices = choices; _state = state; _game = game; _initializer = initializer;
        }

        /// <summary>Opens on the last category and mode, refreshing external changes.</summary>
        public void Init() { Refresh(); _view.Notify("카탈로그에서 추가하거나 현재 배치에서 관리하세요"); }
        /// <summary>Changes category and clears a category-specific search.</summary>
        public void ChangeTab(DebugTabType tab) { if (tab < DebugTabType.Unit || tab > DebugTabType.LevelUp) return; _tab = tab; _view.ResetSearch(); _view.CloseAllInfoPopups(); Refresh(); }
        /// <summary>Switches between the available catalog and live objects/effects.</summary>
        public void SetCatalog(bool catalog) { _catalog = catalog; _view.ResetSearch(); Refresh(); }
        /// <summary>Selects a combat tier for debug unit spawning.</summary>
        public void SetTier(Tier tier) { if (tier < Tier.Normal || tier > Tier.Legend) return; _tier = tier; Refresh(); }

        /// <summary>Refreshes from current scene state and cancels any stale progressive load.</summary>
        public void Refresh()
        {
            CancelSource(ref _listCts);
            bool ready = _initializer?.IsReady == true;
            _view.BeginList(_tab, _catalog, _tier, ready && _catalog);
            if (!ready) { _view.FinishList(); _view.Notify("게임 준비 중입니다 · 잠시 후 새로고침하세요", true); return; }
            if (_catalog)
            {
                _listCts = CancellationTokenSource.CreateLinkedTokenSource(_view.GetCancellationTokenOnDestroy());
                LoadCatalogAsync(_listCts.Token).Forget();
            }
            else { RefreshCurrent(); _view.FinishList(); }
        }

        private async UniTask LoadCatalogAsync(CancellationToken token)
        {
            try
            {
                switch (_tab)
                {
                    case DebugTabType.Unit:
                        foreach (var data in _factory.UnitDataList.Where(d => d != null).OrderBy(d => d.unitName))
                        {
                            await data.LoadAssetsAsync().AttachExternalCancellation(token);
                            token.ThrowIfCancellationRequested();
                            _view.AddItem(data, data.unitName, data.GetFormattedDescription(_tier),
                                data.icon, "소환", Add, searchKeywords: data.characterId.Get(_tier).ToString());
                            await UniTask.Yield(PlayerLoopTiming.Update, token);
                        }
                        break;
                    case DebugTabType.Totem:
                        var reward = UnityEngine.Object.FindObjectsByType<TotemRewardUI>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                            .FirstOrDefault(ui => ui.gameObject.scene == _view.gameObject.scene);
                        if (reward?.TotemPool != null)
                        foreach (var data in reward.TotemPool.Where(d => d != null).OrderBy(d => d.totemId))
                        {
                            await data.LoadAssetsAsync().AttachExternalCancellation(token);
                            token.ThrowIfCancellationRequested();
                            _view.AddItem(data, TotemLabel(data), $"{(data.isRotatable ? "회전 O" : "회전 X")}\n{data.GetDisplayDescription()}",
                                data.icon, "배치", Add, searchKeywords: data.totemName + " TD" + data.totemId);
                            await UniTask.Yield(PlayerLoopTiming.Update, token);
                        }
                        break;
                    case DebugTabType.LevelUp:
                        foreach (var data in _choices.LevelUpPool.Where(d => d != null).OrderBy(d => d.chooseId))
                        {
                            bool acquired = _state.HasEverAcquired(data.chooseId);
                            _view.AddItem(data, data.chooseName, _choices.GetChoiceDescription(data), data.icon,
                                acquired ? "획득함" : "적용", Add, !acquired, data.chooseId.ToString());
                            await UniTask.Yield(PlayerLoopTiming.Update, token);
                        }
                        break;
                }
                token.ThrowIfCancellationRequested();
                _view.FinishList();
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception e)
            {
                if (!token.IsCancellationRequested) { _view.FinishList(); _view.Notify("목록을 불러오지 못했습니다 · 새로고침을 눌러주세요", true); Debug.LogException(e); }
            }
        }

        private void RefreshCurrent()
        {
            switch (_tab)
            {
                case DebugTabType.Unit:
                    foreach (var cell in _grid.GetOccupiedCells())
                    {
                        var unit = cell.OccupyingUnit;
                        if (unit?.unitData == null) continue;
                        _view.AddItem(unit, unit.unitData.unitName, $"{TierLabel(unit.currentTier)} · 칸 {cell.GridPosition}",
                            unit.unitData.icon, "제거", Remove, searchKeywords: unit.unitData.characterId.Get(unit.currentTier).ToString());
                    }
                    break;
                case DebugTabType.Totem:
                    foreach (var cell in _grid.AllCells())
                    {
                        var totem = cell.OccupyingTotem;
                        if (totem?.Data == null) continue;
                        _view.AddItem(totem, TotemLabel(totem.Data),
                            $"칸 {cell.GridPosition} · {(totem.Data.isRotatable ? "회전 O" : "회전 X")}\n{totem.Data.GetDisplayDescription()}",
                            totem.Data.icon, "제거", Remove, searchKeywords: totem.Data.totemName + " TD" + totem.Data.totemId);
                    }
                    break;
                case DebugTabType.LevelUp:
                    var pool = _choices.LevelUpPool;
                    foreach (var card in _state.Active)
                    {
                        var data = pool.FirstOrDefault(d => d != null && d.chooseId == card.CardId);
                        if (data == null) continue;
                        _view.AddItem(data, data.chooseName, _choices.GetChoiceDescription(data), data.icon, "제거", Remove,
                            searchKeywords: data.chooseId.ToString());
                    }
                    break;
            }
        }

        private bool CanCommand()
        {
            if (_initializer?.IsReady != true) { _view.Notify("게임 준비가 끝난 뒤 다시 시도하세요", true); return false; }
            if (_game.IsFinished) { _view.Notify("종료된 게임에서는 변경할 수 없습니다", true); return false; }
            return true;
        }

        private void Add(object data)
        {
            if (!CanCommand()) return;
            if (data is LevelUpData choice)
            {
                var result = _choices.TryApplyChoice(choice);
                bool success = result != SelectionApplyResult.Rejected;
                _view.Notify(success ? choice.chooseName + " 적용 요청을 처리했습니다" : "이미 획득했거나 적용할 수 없는 선택지입니다", !success);
                Refresh(); return;
            }
            if (_adding) { _view.Notify("앞선 배치를 처리하고 있습니다", true); return; }
            _commandCts = CancellationTokenSource.CreateLinkedTokenSource(_view.GetCancellationTokenOnDestroy());
            AddObjectAsync(data, _commandCts.Token).Forget();
        }

        private async UniTask AddObjectAsync(object data, CancellationToken token)
        {
            _adding = true;
            Tier requestedTier = _tier;
            try
            {
                var cells = _grid.GetEmptyCells();
                if (cells.Count == 0) { _view.Notify("빈 칸이 없습니다 · 현재 배치에서 대상을 제거하세요", true); return; }
                var cell = cells[UnityEngine.Random.Range(0, cells.Count)];
                if (data is TotemData totem)
                {
                    _view.Notify(TotemLabel(totem) + " 배치 중…");
                    bool placed = await _totems.PlaceTotemAtCellAsync(totem, cell, token);
                    token.ThrowIfCancellationRequested();
                    _view.Notify(placed ? TotemLabel(totem) + " 배치 완료 · " + cell.GridPosition : "배치하지 못했습니다 · 빈 칸을 확인하세요", !placed);
                }
                else if (data is UnitData unitData)
                {
                    await unitData.LoadAssetsAsync().AttachExternalCancellation(token);
                    token.ThrowIfCancellationRequested();
                    if (!cell.IsAvailable) { _view.Notify("선택한 칸이 사용 중입니다 · 다시 시도하세요", true); return; }
                    var unit = _factory.CreateUnitFromData(unitData, requestedTier);
                    if (unit == null) { _view.Notify("유닛을 생성하지 못했습니다", true); return; }
                    _units.PlaceUnitWithEffect(unit, cell);
                    _view.Notify(unitData.unitName + " 소환 완료 · " + TierLabel(requestedTier) + " · " + cell.GridPosition);
                }
                if (_view.isActiveAndEnabled) Refresh();
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception e) { if (!token.IsCancellationRequested) _view.Notify("배치에 실패했습니다", true); Debug.LogException(e); }
            finally { _adding = false; CancelSource(ref _commandCts); }
        }

        private void Remove(object data)
        {
            if (!CanCommand()) return;
            string name;
            if (data is UnitBase unit && unit != null)
            {
                name = unit.unitData.unitName;
                var cell = unit.currentCell;
                // A spawn animation may not have called OnPlaced yet; occupancy still owns the unit.
                if (cell == null) cell = _grid.AllCells().FirstOrDefault(c => c.OccupyingUnit == unit);
                unit.OnRemoved(); cell?.RemoveUnit(); UnityEngine.Object.Destroy(unit.gameObject);
            }
            else if (data is TotemBase totem && totem != null) { name = TotemLabel(totem.Data); _totems.SellTotem(totem); }
            else if (data is LevelUpData choice) { name = choice.chooseName; _choices.RemoveEffect(choice); }
            else return;
            _view.Notify(name + " 제거 완료"); Refresh();
        }

        private static string TierLabel(Tier tier) => tier == Tier.Legend ? "Legendary" : tier.ToString();

        // Debug-only labels for authored placeholder names; never changes TotemData or gameplay text.
        private static string TotemLabel(TotemData data)
        {
            if (!string.IsNullOrWhiteSpace(data.totemName) && !data.totemName.StartsWith("TD" + data.totemId, StringComparison.Ordinal))
                return data.totemName;
            return data.totemId switch
            {
                1001 => "보조 투사체", 1002 => "성장 뿌리", 1003 => "화염구", 1004 => "골렘 소환",
                1005 => "일시 승급", 1006 => "아머브레이크", 1007 => "그림자 공격", 1008 => "이중 강화",
                1009 => "공격 집중", 1010 => "식량 생산", 1011 => "전후방 지원", 1012 => "치명타 공명",
                1013 => "가로줄 강화", 1014 => "보스 맞이", _ => "토템"
            };
        }

        private static void CancelSource(ref CancellationTokenSource source)
        { var current = source; source = null; if (current == null) return; current.Cancel(); current.Dispose(); }
        /// <summary>Cancels pending list/placement work when the panel closes.</summary>
        public void Cancel() { CancelSource(ref _listCts); CancelSource(ref _commandCts); }
        /// <summary>Ends all operations owned by this presenter.</summary>
        public void Dispose() => Cancel();
    }
}
