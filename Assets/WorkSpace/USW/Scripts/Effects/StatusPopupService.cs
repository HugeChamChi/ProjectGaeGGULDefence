using System.Collections.Generic;
using UnityEngine;
using VContainer;

/// <summary>
/// 상태 팝업 관리자 (사용자 요청 2026-10-23). 토템 버프·선택지·보스 디버프 등 출처와 상관없이
/// "이 대상에 이 효과"만 받아 StatusPopupCatalog의 문구·색으로 대상 몸통 위에 띄운다.
/// 대상 그림 크기로 글자 배율과 몸통 중심을 정하고, 효과별 재표시 대기 시간(Cooldown)을 지킨다.
/// 팝업 컨테이너는 처음 쓸 때 데미지 숫자 캔버스 안 맨 아래 층에 코드로 만든다(씬 수정 없음).
/// DamageFloaterManager는 BossManager를 주입받으므로 생성자에서 받지 않고 처음 띄울 때 꺼낸다(순환 의존 방지).
/// </summary>
public sealed class StatusPopupService
{
    private const int PruneThreshold = 64;

    private readonly StatusPopupCatalog _catalog;
    private readonly IObjectResolver _resolver;
    private readonly Dictionary<(Transform Target, StatusPopupKind Kind), float> _lastShown = new();
    private readonly List<(Transform Target, StatusPopupKind Kind)> _prune = new();
    private readonly List<Renderer> _renderers = new();
    private readonly List<StatusPopupKind> _selectionKinds = new();
    private StatusPopupSpawner _spawner;
    private bool _warnedMissingContainer;

    /// <summary>인게임 스코프에서 카탈로그와 리졸버(컨테이너 위치를 늦게 찾기 위함)를 받는다.</summary>
    public StatusPopupService(StatusPopupCatalog catalog, IObjectResolver resolver)
    {
        _catalog = catalog;
        _resolver = resolver;
    }

    /// <summary>대상 몸통 위에 효과 한 줄을 띄운다. 같은 대상에 이어서 부르면 한 묶음으로 쌓인다.</summary>
    public void Show(Transform target, StatusPopupKind kind)
    {
        if (target == null || _catalog == null || !_catalog.TryGet(kind, out var entry)) return;
        if (!PassCooldown(target, kind, entry.Cooldown)) return;
        var spawner = EnsureSpawner();
        if (spawner == null) return;

        MeasureBody(target, out var bodyOffset, out float height);
        spawner.Show(target, entry.Text, entry.ArrowUp, entry.Color, _catalog.ScaleFor(height), bodyOffset);
    }

    /// <summary>보스 등 대상에 새로 걸린 디버프를 띄운다.</summary>
    public void ShowDebuff(Transform target, DebuffKind kind)
    {
        switch (kind)
        {
            case DebuffKind.Burn: Show(target, StatusPopupKind.Burn); break;
            case DebuffKind.ArmorBreak: Show(target, StatusPopupKind.ArmorBreak); break;
            case DebuffKind.DamageTakenIncrease: Show(target, StatusPopupKind.DamageTakenUp); break;
        }
    }

    /// <summary>
    /// 선택지로 얻은 능력치 효과를 배치된 모든 유닛에 띄운다. 능력치가 아닌 효과(추가 공격·폭탄 등)는 띄우지 않는다.
    /// </summary>
    public void ShowSelection(IReadOnlyList<SelectionEffectDefinition> effects, IEnumerable<GridCell> cells)
    {
        if (effects == null || cells == null) return;
        _selectionKinds.Clear();
        foreach (var effect in effects)
            if (TryMapSelection(effect, out var kind) && !_selectionKinds.Contains(kind)) _selectionKinds.Add(kind);
        if (_selectionKinds.Count == 0) return;

        foreach (var cell in cells)
        {
            var unit = cell != null ? cell.OccupyingUnit : null;
            if (unit == null) continue;
            foreach (var kind in _selectionKinds) Show(unit.transform, kind);
        }
    }

    private static bool TryMapSelection(SelectionEffectDefinition effect, out StatusPopupKind kind)
    {
        kind = default;
        switch (effect)
        {
            case DroneAttackSpeedDefinition drone when drone.BonusRatio > 0f:
                kind = StatusPopupKind.SpeedUp;
                return true;
            case StatModifierDefinition stat when !Mathf.Approximately(stat.Ratio, 0f):
                bool up = stat.Ratio > 0f;
                switch (stat.Stat)
                {
                    case SelectionStat.Attack: kind = up ? StatusPopupKind.AttackUp : StatusPopupKind.AttackDown; return true;
                    case SelectionStat.AttackSpeed: kind = up ? StatusPopupKind.SpeedUp : StatusPopupKind.SpeedDown; return true;
                    case SelectionStat.CritChance when up: kind = StatusPopupKind.CritChanceUp; return true;
                    case SelectionStat.CritDamage when up: kind = StatusPopupKind.CritDamageUp; return true;
                    case SelectionStat.GaugeSpeed when up: kind = StatusPopupKind.SkillChargeUp; return true;
                    case SelectionStat.FoodSpeed when up: kind = StatusPopupKind.FoodUp; return true;
                }
                return false;
        }
        return false;
    }

    private bool PassCooldown(Transform target, StatusPopupKind kind, float cooldown)
    {
        if (cooldown <= 0f) return true;
        var key = (target, kind);
        float now = Time.time;
        if (_lastShown.TryGetValue(key, out float last) && now - last < cooldown) return false;
        _lastShown[key] = now;
        if (_lastShown.Count > PruneThreshold) PruneDestroyedTargets();
        return true;
    }

    private void PruneDestroyedTargets()
    {
        _prune.Clear();
        foreach (var key in _lastShown.Keys) if (key.Target == null) _prune.Add(key);
        foreach (var key in _prune) _lastShown.Remove(key);
    }

    // 대상 그림(스프라이트·Spine 메시)의 범위로 몸통 중심과 높이를 잰다. 버프 이펙트·파티클은 뺀다.
    private void MeasureBody(Transform target, out Vector3 bodyOffset, out float height)
    {
        target.GetComponentsInChildren(false, _renderers);
        bool any = false;
        var bounds = default(Bounds);
        foreach (var renderer in _renderers)
        {
            if (!renderer.enabled || renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer) continue;
            if (renderer.GetComponentInParent<StatBuffFx>() != null) continue;
            if (!any) { bounds = renderer.bounds; any = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        _renderers.Clear();
        if (!any)
        {
            bodyOffset = Vector3.zero;
            height = _catalog.ReferenceHeight;
            return;
        }
        bodyOffset = bounds.center - target.position;
        bodyOffset.z = 0f;
        height = bounds.size.y;
    }

    private StatusPopupSpawner EnsureSpawner()
    {
        if (_spawner != null) return _spawner;
        var floaters = _resolver != null && _resolver.TryResolve<DamageFloaterManager>(out var resolved) ? resolved : null;
        var anchor = floaters != null ? floaters.damageTextContainer as RectTransform : null;
        if (anchor == null)
        {
            if (!_warnedMissingContainer) Debug.LogWarning("[StatusPopupService] 데미지 숫자 컨테이너가 없어 상태 팝업을 띄우지 않는다.");
            _warnedMissingContainer = true;
            return null;
        }

        // 데미지 숫자 캔버스 안, 숫자들보다 아래 층에 화면 전체 크기 컨테이너를 둔다.
        var container = new GameObject("StatusPopups", typeof(RectTransform)).GetComponent<RectTransform>();
        container.SetParent(anchor, false);
        container.anchorMin = Vector2.zero;
        container.anchorMax = Vector2.one;
        container.pivot = new Vector2(0.5f, 0.5f);
        container.sizeDelta = Vector2.zero;
        container.anchoredPosition = Vector2.zero;
        container.SetAsFirstSibling();

        _spawner = container.gameObject.AddComponent<StatusPopupSpawner>();
        _spawner.Configure(container, _catalog.Style);
        return _spawner;
    }
}
