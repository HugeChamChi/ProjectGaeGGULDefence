using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>최종 토템 출처의 획득/해제를 비교한다. 연출 종료는 실제 버프를 제거하지 않는다.</summary>
public sealed class UnitBuffVisualController : MonoBehaviour
{
    private UnitBase _unit;
    private UnitDependencies _deps;
    private readonly Dictionary<TotemBuffVisualState.Key, float> _permissions = new();
    private readonly HashSet<TotemBuffVisualState.Key> _current = new();
    private readonly List<TotemBuffVisualState.Key> _removed = new();
    private readonly Dictionary<StatBuffFxStyle, StatBuffFx> _active = new();
    private readonly List<StatBuffFxStyle> _release = new();
    private Renderer[] _renderers;
    private SortingGroup _sortingGroup;
    private float _lifetimeClock;
    private bool _subscribed, _relocating;
    private bool _stateDirty = true;
    private GridCellModel _lastModel;
    private IReadOnlyList<TotemBuffVisualState.Contribution> _contributions;

    /// <summary>기존 유닛 의존성 경로로 씬 공급자·풀·설정을 받는다.</summary>
    public void Init(UnitBase unit, UnitDependencies deps)
    {
        _unit = unit; _deps = deps;
        _renderers = unit.GetComponentsInChildren<Renderer>(true);
        _sortingGroup = unit.GetComponentInChildren<SortingGroup>(true);
    }
    /// <summary>같은 출처 안 이동에서는 기존 표시 권한을 유지한다.</summary>
    public void Placed()
    {
        _relocating = false;
        _stateDirty = true;
        if (!_subscribed && _deps?.TotemBuffManager != null)
        {
            _deps.TotemBuffManager.VisualState.OnCompleted += OnStateCompleted;
            _subscribed = true;
        }
    }
    private void OnStateCompleted() => _stateDirty = true;
    /// <summary>실제 제거는 즉시 반환하며, 이동 중 임시 제거는 획득 이력을 보존한다.</summary>
    public void Removed(bool relocating)
    {
        _relocating = relocating;
        if (!relocating) Clear();
    }
    private void LateUpdate()
    {
        if (_deps?.BuffVisualSettings == null || _unit == null) return;
        if (_unit.currentCell == null) { if (!_relocating) Clear(); return; }
        bool running = _deps.GameManager?.CurrentState == GameManager.GameState.Playing
            && Time.timeScale > 0f && _deps.FieldPause?.AttacksHeld != true;
        if (running) _lifetimeClock += Time.deltaTime;
        Refresh();
    }
    private void Refresh()
    {
        var model = _unit.currentCell.Model;
        var settings = _deps.BuffVisualSettings;
        if (_stateDirty || _lastModel != model)
        {
            _contributions = _deps.TotemBuffManager.VisualState.Read(model);
            _lastModel = model; _stateDirty = false;
        }
        var contributions = _contributions;
        _current.Clear();
        float attackPermission = 0f, speedPermission = 0f, slowPermission = 0f, disabledPermission = 0f;
        bool newAttack = false, newSpeed = false, newSlow = false, newDisabled = false;
        foreach (var contribution in contributions)
        {
            _current.Add(contribution.Source);
            bool added = !_permissions.TryGetValue(contribution.Source, out float end);
            if (added)
                _permissions.Add(contribution.Source, end = _lifetimeClock + settings.PermanentSeconds);
            float remaining = contribution.EndEncounterSeconds >= 0f
                ? Mathf.Max(0f, contribution.EndEncounterSeconds - _deps.TotemBuffManager.BossEncounterSeconds)
                : Mathf.Max(0f, end - _lifetimeClock);
            if (contribution.Disabled) { disabledPermission = Mathf.Max(disabledPermission, remaining); newDisabled |= added; }
            else if (contribution.Kind == StatKind.AttackPercent && contribution.Amount > 0f) { attackPermission = Mathf.Max(attackPermission, remaining); newAttack |= added; }
            else if (contribution.Kind == StatKind.Speed && contribution.Amount > 0f) { speedPermission = Mathf.Max(speedPermission, remaining); newSpeed |= added; }
            else if (contribution.Kind == StatKind.Speed && contribution.Amount < 0f) { slowPermission = Mathf.Max(slowPermission, remaining); newSlow |= added; }
        }
        _removed.Clear();
        foreach (var key in _permissions.Keys) if (!_current.Contains(key)) _removed.Add(key);
        foreach (var key in _removed) _permissions.Remove(key);

        var state = _deps.TotemBuffManager.VisualState;
        float attack = model.TotemAttackModifier * (1f + model.GetTotemCellBonus(StatKind.AttackPercent) + state.GlobalAttackBonus) - 1f;
        float speed = (1f + model.GetTotemCellBonus(StatKind.Speed) + state.GlobalSpeedBonus) / model.TotemSpeedModifier - 1f;
        var first = (StatBuffFxStyle)null; var second = (StatBuffFxStyle)null;
        float firstTime = 0f, secondTime = 0f;
        if (!model.IsSealed)
        {
            if (model.TotemAttackDisabled)
            { first = settings.Disabled; firstTime = disabledPermission; }
            else if (attack > 0f && speed < 0f)
            { first = settings.Mixed; firstTime = Mathf.Max(attackPermission, slowPermission); }
            else
            {
                if (attack > 0f) { first = settings.Attack; firstTime = attackPermission; }
                if (speed > 0f) { second = settings.Speed; secondTime = speedPermission; }
            }
        }
        _release.Clear();
        foreach (var pair in _active)
            if (!(pair.Key == first && firstTime > 0f) && !(pair.Key == second && secondTime > 0f)) _release.Add(pair.Key);
        foreach (var style in _release) { _deps.BuffFxPool.Return(_active[style]); _active.Remove(style); }
        bool firstIntro = first == settings.Disabled ? newDisabled : first == settings.Mixed ? newAttack || newSlow : newAttack;
        Show(first, firstTime, firstIntro); Show(second, secondTime, newSpeed);
        if (!model.IsSealed) ShowNewPopups(model.TotemAttackDisabled, attack, speed, newAttack, newSpeed, newSlow, newDisabled);
    }
    // 이펙트 시작 고리가 뜨는 순간(새로 얻은 효과)에만 같은 색 문구를 띄운다. 이동·수치 갱신에서는 띄우지 않는다.
    private void ShowNewPopups(bool disabled, float attack, float speed, bool newAttack, bool newSpeed, bool newSlow, bool newDisabled)
    {
        var popups = _deps.StatusPopups;
        if (popups == null) return;
        var target = _unit.transform;
        if (disabled)
        {
            if (newDisabled) popups.Show(target, StatusPopupKind.AttackDisabled);
            return;
        }
        if (newAttack && attack > 0f) popups.Show(target, StatusPopupKind.AttackUp);
        if (newSpeed && speed > 0f) popups.Show(target, StatusPopupKind.SpeedUp);
        if (newSlow && speed < 0f) popups.Show(target, StatusPopupKind.SpeedDown);
    }
    private void Show(StatBuffFxStyle style, float remaining, bool playIntro)
    {
        if (style == null || remaining <= 0f) return;
        if (!_active.TryGetValue(style, out var fx))
        {
            fx = _deps.BuffFxPool.Rent(style); _active.Add(style, fx);
            fx.Play(_unit.transform, style, 0f, _deps.BuffVisualSettings.Scale, playIntro);
        }
        // 실제 수명은 위 권한/보스 시계가 소유하며 FX는 모션만 진행한다.
        fx.UseUnscaledTime = _deps.FieldPause?.IsActive == true;
        int layer = 0, order = Mathf.RoundToInt(-_unit.transform.position.y * 100f);
        if (_sortingGroup != null) { layer = _sortingGroup.sortingLayerID; order = _sortingGroup.sortingOrder; }
        else foreach (var renderer in _renderers)
            if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy)
            { layer = renderer.sortingLayerID; order = renderer.sortingOrder; break; }
        fx.SetSorting(layer, order + _deps.BuffVisualSettings.FrontOffset, order + _deps.BuffVisualSettings.BehindOffset);
        fx.SetRemaining(remaining);
    }
    private void Clear()
    {
        if (_subscribed && _deps?.TotemBuffManager != null)
            _deps.TotemBuffManager.VisualState.OnCompleted -= OnStateCompleted;
        _subscribed = false;
        foreach (var fx in _active.Values) _deps?.BuffFxPool?.Return(fx);
        _active.Clear(); _permissions.Clear(); _current.Clear();
        _lastModel = null; _contributions = null; _stateDirty = true;
    }
    private void OnDisable() => Clear();
    private void OnDestroy() => Clear();
}
