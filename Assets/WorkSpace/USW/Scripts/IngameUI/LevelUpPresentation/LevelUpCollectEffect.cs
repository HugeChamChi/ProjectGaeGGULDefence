using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

/// <summary>
/// 레벨업 획득 연출 (LevelUpUI와 같은 GameObject에 부착). HUD Canvas에서 재생되어 패널이 닫힌 뒤에도 이어진다.
///   1. 선택지 아이콘이 화면 중앙에 팝업 [이펙트 슬롯 C]
///   2. 아이콘이 빛 구슬로 바뀜 [슬롯 E: 빛 구슬]
///   3. 구슬이 빨려 들어감 [이펙트 슬롯 D: 도착]
///      - 갈래 빛: 선택지 대상에게 작은 구슬이 갈라져 꽂힘 — 대상 유닛들 / 족장 스킬 버튼(알팡)
///        (대상 판별: LevelUpFeedbackTargets)
///      - 메인 빛: 아이콘 본체였던 큰 구슬은 우측 하단 수집 지점(_collectTarget, 메인 루트)으로 들어감
/// </summary>
public class LevelUpCollectEffect : MonoBehaviour
{
    [Inject] private GridManager _gridManager;
    [Inject] private ChieftainSpawner _chieftainSpawner;

    [Header("참조")]
    [Tooltip("획득 연출을 그릴 HUD Canvas. 비우면 수집 지점/족장 버튼이 속한 최상위 Canvas.")]
    [SerializeField] private RectTransform _hudRoot;
    [Tooltip("대상이 없는 선택지의 빛 구슬 도착 지점. 씬에서 위치/크기를 조정한다. 비우면 _collectFallbackViewport.")]
    [SerializeField] private RectTransform _collectTarget;
    [Tooltip("_collectTarget이 없을 때 쓰는 화면 비율 좌표 (0,0=좌하단, 1,1=우상단).")]
    [SerializeField] private Vector2 _collectFallbackViewport = new Vector2(0.88f, 0.1f);
    [Tooltip("알팡 등 그리드 밖 족장 스킬 선택지의 빛 구슬 도착 지점 (족장 스킬 버튼).")]
    [SerializeField] private RectTransform _chiefSkillTarget;

    [Header("이펙트 슬롯 (비우면 임시 이펙트)")]
    [Tooltip("C: 선택지 아이콘이 화면 중앙에 등장할 때 터지는 이펙트.")]
    [SerializeField] private GameObject _iconAppearPrefab;
    [Tooltip("D: 빛 구슬이 도착했을 때 터지는 이펙트 (유닛 몸통에서도 재생).")]
    [SerializeField] private GameObject _arrivePrefab;
    [Tooltip("E: 아이콘 대신 날아가는 빛 구슬 (루프 파티클 등). 비우면 임시 빛 구슬 + 잔상.")]
    [SerializeField] private GameObject _orbPrefab;
    [Tooltip("슬롯 프리팹 인스턴스를 자동 제거할 시간(초, unscaled).")]
    [SerializeField, Min(0.1f)] private float _effectLifetime = 1.5f;

    [Header("1. 획득 아이콘")]
    [SerializeField, Min(1f)] private float _iconSize = 200f;
    [Tooltip("중앙 팝업 시간 (0 → 오버슈트 → 1).")]
    [SerializeField, Min(0.01f)] private float _iconPopDuration = 0.3f;
    [SerializeField, Min(1f)] private float _iconPopOvershoot = 1.15f;
    [Tooltip("중앙에서 머무는 시간.")]
    [SerializeField, Min(0f)] private float _iconHold = 0.15f;
    [Tooltip("아이콘이 빛 구슬로 바뀌며 사라지는 시간.")]
    [SerializeField, Min(0.01f)] private float _iconVanishDuration = 0.12f;

    [Header("2. 빛 구슬 흡수")]
    [SerializeField, Min(1f)] private float _orbSize = 150f;
    [Tooltip("빛 구슬이 중앙에서 커지며 나타나는 시간.")]
    [SerializeField, Min(0.01f)] private float _orbAppearDuration = 0.1f;
    [Tooltip("목적지까지 빨려 들어가는 시간.")]
    [SerializeField, Min(0.01f)] private float _orbFlyDuration = 0.35f;
    [SerializeField] private Ease _orbFlyEase = Ease.InCubic;
    [Tooltip("도착 시 빛 구슬 배율 — 들어가면서 이만큼 작아진다.")]
    [SerializeField, Min(0f)] private float _orbEndScale = 0.25f;
    [SerializeField] private Color _orbColor = new Color(1f, 0.85f, 0.35f, 1f);
    [Tooltip("임시 빛 구슬의 잔상 생성 간격(초). 0이면 잔상 없음.")]
    [SerializeField, Min(0f)] private float _orbTrailInterval = 0.025f;
    [SerializeField, Min(0.01f)] private float _orbTrailFade = 0.2f;

    [Header("3. 갈래 빛 / 메인 루트")]
    [Tooltip("켜면 대상이 있어도 메인 빛(아이콘 본체)은 항상 수집 지점으로 간다. 끄면 대상이 없을 때만.")]
    [SerializeField] private bool _mainRouteAlways = true;
    [Tooltip("갈래 빛이 출발한 뒤 메인 빛이 수집 지점으로 출발하기까지의 간격.")]
    [SerializeField, Min(0f)] private float _mainRouteDelay = 0.12f;
    [Tooltip("갈래 빛(대상에게 가는 작은 구슬)의 크기 — 메인 빛 대비 배율. 비행 중 축소도 같은 비율로 적용된다.")]
    [SerializeField, Min(0.05f)] private float _branchOrbScale = 0.4f;
    [Tooltip("대상 유닛이 여럿일 때 갈래 빛이 하나씩 출발하는 간격.")]
    [SerializeField, Min(0f)] private float _orbSplitStagger = 0.05f;
    [Tooltip("한 번에 꽂히는 최대 유닛 수 (그 이상은 생략).")]
    [SerializeField, Min(1)] private int _maxSplitTargets = 12;
    [Tooltip("유닛 발밑(피벗) 기준 구슬이 꽂히는 높이 (월드 단위).")]
    [SerializeField] private float _unitHitOffsetY = 0.6f;

    [Header("임시 이펙트 (슬롯이 비었을 때)")]
    [SerializeField] private Color _placeholderColor = new Color(1f, 0.85f, 0.4f, 1f);
    [SerializeField, Min(1f)] private float _iconAppearBurstSize = 480f;
    [SerializeField, Min(1f)] private float _arriveBurstSize = 200f;
    [SerializeField, Min(1f)] private float _unitHitBurstSize = 220f;

    private LevelUpFxKit _fx;
    private readonly List<UnitBase> _targetUnits = new List<UnitBase>();

    private LevelUpFxKit Fx => _fx ??= new LevelUpFxKit(_effectLifetime, _placeholderColor);

    private RectTransform HudRoot =>
        _hudRoot != null ? _hudRoot
        : LevelUpUiSpace.RootCanvasOf(_collectTarget) ?? LevelUpUiSpace.RootCanvasOf(_chiefSkillTarget);

    /// <summary>획득 연출을 시작한다 (fire-and-forget). 이 오브젝트가 비활성화돼도 끝까지 재생된다.</summary>
    public void Play(LevelUpData data, Sprite icon) => PlayAsync(data, icon).Forget();

    private async UniTaskVoid PlayAsync(LevelUpData data, Sprite sprite)
    {
        var root = HudRoot;
        if (root == null || sprite == null) return;

        var icon = LevelUpFxKit.CreateImage(root, sprite, _iconSize, Color.white, "LevelUpCollectIcon");
        icon.preserveAspect = true;
        var iconRt = icon.rectTransform;
        iconRt.anchorMin = iconRt.anchorMax = new Vector2(0.5f, 0.5f);
        iconRt.anchoredPosition = Vector2.zero;
        iconRt.SetAsLastSibling();
        iconRt.localScale = Vector3.zero;
        Fx.Track(icon.gameObject);
        GameObject orb = null;

        try
        {
            // 1) 아이콘 팝업
            Fx.Spawn(_iconAppearPrefab, root, iconRt.position, _iconAppearBurstSize);
            var iconToken = icon.GetCancellationTokenOnDestroy();
            await DOTween.Sequence().SetUpdate(true).SetLink(icon.gameObject)
                .Append(iconRt.DOScale(_iconPopOvershoot, _iconPopDuration * 0.6f).SetEase(Ease.OutQuad))
                .Append(iconRt.DOScale(1f, _iconPopDuration * 0.4f).SetEase(Ease.InOutSine))
                .ToUniTask(cancellationToken: iconToken);
            await UniTask.Delay(LevelUpUiSpace.Ms(_iconHold), DelayType.Realtime, cancellationToken: iconToken);

            // 2) 아이콘이 빛 구슬로 바뀜 — 아이콘은 빠르게 수축/소멸, 구슬은 같은 자리에서 커진다.
            orb = CreateOrb(root, iconRt.position);
            var orbRt = (RectTransform)orb.transform;
            orbRt.localScale = Vector3.zero;
            _ = DOTween.Sequence().SetUpdate(true).SetLink(icon.gameObject)
                .Join(iconRt.DOScale(0.3f, _iconVanishDuration).SetEase(Ease.InQuad))
                .Join(icon.DOFade(0f, _iconVanishDuration).SetEase(Ease.InQuad));
            var orbToken = orb.GetCancellationTokenOnDestroy();
            await orbRt.DOScale(1f, _orbAppearDuration).SetEase(Ease.OutBack).SetUpdate(true).SetLink(orb)
                       .ToUniTask(cancellationToken: orbToken);
            Fx.Release(icon.gameObject);

            // 3) 갈래 빛은 대상에게, 메인 빛은 수집 지점(메인 루트)으로
            var destination = LevelUpFeedbackTargets.Resolve(data, _gridManager, _chieftainSpawner, _targetUnits);
            Vector3 from = orbRt.position;
            var flights = new List<UniTask>();
            if (destination == LevelUpFeedbackDestination.Units && _targetUnits.Count > 0)
                flights.Add(FlyToUnitsAsync(root, from));
            else if (destination == LevelUpFeedbackDestination.ChiefSkill && _chiefSkillTarget != null)
                flights.Add(FlyBranchAsync(root, from, LevelUpUiSpace.WorldPointIn(root, _chiefSkillTarget),
                                           _chiefSkillTarget, _arriveBurstSize));

            if (_mainRouteAlways || flights.Count == 0)
            {
                if (flights.Count > 0 && _mainRouteDelay > 0f)
                    await UniTask.Delay(LevelUpUiSpace.Ms(_mainRouteDelay), DelayType.Realtime, cancellationToken: orbToken);
                flights.Add(FlyMainRouteAsync(root, orbRt, orbToken));
            }
            else
            {
                Fx.Release(orb); orb = null; // 메인 빛 없이 갈래 빛만
            }
            await UniTask.WhenAll(flights);
        }
        catch (System.OperationCanceledException) { }
        finally
        {
            if (icon != null) Fx.Release(icon.gameObject);
            if (orb != null) Fx.Release(orb);
        }
    }

    /// <summary>메인 빛: 아이콘 본체였던 큰 구슬이 수집 지점으로 들어가고 수집 지점이 한 번 튄다.</summary>
    private async UniTask FlyMainRouteAsync(RectTransform root, RectTransform orbRt, CancellationToken token)
    {
        Vector3 target = CollectPoint(root);
        await FlyAsync(root, orbRt, target, token);
        Fx.Spawn(_arrivePrefab, root, target, _arriveBurstSize);
        Punch(_collectTarget);
    }

    /// <summary>갈래 빛이 대상 유닛 수만큼 나뉘어 각 유닛에 꽂힌다.</summary>
    private async UniTask FlyToUnitsAsync(RectTransform root, Vector3 from)
    {
        int count = Mathf.Min(_targetUnits.Count, _maxSplitTargets);
        var flights = new List<UniTask>(count);
        var token = this.GetCancellationTokenOnDestroy();
        for (int i = 0; i < count; i++)
        {
            var unit = _targetUnits[i];
            if (i > 0 && _orbSplitStagger > 0f)
                await UniTask.Delay(LevelUpUiSpace.Ms(_orbSplitStagger), DelayType.Realtime, cancellationToken: token);
            if (unit == null) continue;
            Vector3 hit = LevelUpUiSpace.UnitCanvasPoint(root, unit, _unitHitOffsetY);
            flights.Add(FlyBranchAsync(root, from, hit, null, _unitHitBurstSize));
        }
        await UniTask.WhenAll(flights);
    }

    /// <summary>갈래 빛 하나: 작은 구슬이 from → to로 날아가 터지고, punch가 있으면 한 번 튀게 한다.</summary>
    private async UniTask FlyBranchAsync(RectTransform root, Vector3 from, Vector3 to, RectTransform punch, float burstSize)
    {
        var orb = CreateOrb(root, from);
        var orbRt = (RectTransform)orb.transform;
        try
        {
            orbRt.localScale = Vector3.one * _branchOrbScale;
            await FlyAsync(root, orbRt, to, orb.GetCancellationTokenOnDestroy(), _orbEndScale * _branchOrbScale);
            Fx.Spawn(_arrivePrefab, root, to, burstSize);
            Punch(punch);
        }
        catch (System.OperationCanceledException) { }
        finally { Fx.Release(orb); }
    }

    private static void Punch(RectTransform target)
    {
        if (target == null) return;
        target.DOKill(true);
        _ = target.DOPunchScale(Vector3.one * 0.2f, 0.25f, 6, 0.6f).SetUpdate(true).SetLink(target.gameObject);
    }

    /// <summary>구슬이 작아지며 target으로 가속 비행한다 (+ 임시 잔상).</summary>
    private UniTask FlyAsync(RectTransform root, RectTransform orbRt, Vector3 target, CancellationToken token, float endScale = -1f)
    {
        var fly = DOTween.Sequence().SetUpdate(true).SetLink(orbRt.gameObject)
            .Join(orbRt.DOMove(target, _orbFlyDuration).SetEase(_orbFlyEase))
            .Join(orbRt.DOScale(endScale >= 0f ? endScale : _orbEndScale, _orbFlyDuration).SetEase(Ease.InQuad));
        if (_orbPrefab == null && _orbTrailInterval > 0f)
            SpawnTrailAsync(root, orbRt, _orbFlyDuration, token).Forget();
        return fly.ToUniTask(cancellationToken: token);
    }

    /// <summary>빛 구슬 — 슬롯 E 프리팹이 있으면 그것을, 없으면 임시(바깥 글로우 + 흰 코어)를 만든다.</summary>
    private GameObject CreateOrb(RectTransform root, Vector3 worldPos)
    {
        GameObject orb;
        if (_orbPrefab != null)
        {
            orb = Instantiate(_orbPrefab, root);
        }
        else
        {
            orb = new GameObject("LevelUpCollectOrb", typeof(RectTransform));
            var orbRt = (RectTransform)orb.transform;
            orbRt.SetParent(root, false);
            LevelUpFxKit.CreateImage(orbRt, LevelUpFxKit.SoftCircle, _orbSize * 1.8f, _orbColor);
            LevelUpFxKit.CreateImage(orbRt, LevelUpFxKit.SoftCircle, _orbSize * 0.7f, Color.white);
        }
        orb.transform.position = worldPos;
        orb.transform.SetAsLastSibling();
        Fx.Track(orb);
        return orb;
    }

    /// <summary>임시 빛 구슬 잔상 — 비행 중 일정 간격으로 흐려지는 원을 남긴다.</summary>
    private async UniTaskVoid SpawnTrailAsync(RectTransform root, RectTransform orb, float duration, CancellationToken token)
    {
        float elapsed = 0f;
        while (elapsed < duration && orb != null && !token.IsCancellationRequested)
        {
            var ghost = LevelUpFxKit.CreateImage(root, LevelUpFxKit.SoftCircle, _orbSize * 1.2f, _orbColor);
            var ghostRt = ghost.rectTransform;
            ghostRt.position = orb.position;
            ghostRt.localScale = orb.localScale;
            ghostRt.SetSiblingIndex(orb.GetSiblingIndex());
            _ = DOTween.Sequence().SetUpdate(true).SetLink(ghost.gameObject)
                .Join(ghost.DOFade(0f, _orbTrailFade).SetEase(Ease.OutQuad))
                .Join(ghostRt.DOScale(ghostRt.localScale * 0.5f, _orbTrailFade))
                .OnComplete(() => { if (ghost != null) Destroy(ghost.gameObject); });
            if (await UniTask.Delay(LevelUpUiSpace.Ms(_orbTrailInterval), DelayType.Realtime, cancellationToken: token)
                            .SuppressCancellationThrow()) return;
            elapsed += _orbTrailInterval;
        }
    }

    private Vector3 CollectPoint(RectTransform root)
    {
        if (_collectTarget != null) return LevelUpUiSpace.WorldPointIn(root, _collectTarget);
        var rect = root.rect;
        return root.TransformPoint(new Vector3(rect.xMin + rect.width * _collectFallbackViewport.x,
                                               rect.yMin + rect.height * _collectFallbackViewport.y, 0f));
    }

    private void OnDestroy() => _fx?.DestroyAll();
}
