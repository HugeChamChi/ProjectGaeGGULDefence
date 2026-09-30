using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

/// <summary>
/// 레벨업 선택지 등장 연출 (LevelUpUI와 같은 GameObject에 부착). 레퍼런스: design/연출예시1.mp4
///   1. 게이지 풀 — 패널을 숨긴 채 소환 버튼 펀치 + [이펙트 슬롯 A: 게이지 버스트] → 잠시 뒤 패널 페이드 인
///   2. 카드 1→2→3 — 소환 버튼 위치에서 빛덩어리(노랑→흰색 덮개)로 튀어나와 제자리로 비행
///   3. 도착 — 덮개가 걷히며 내용 공개 + [이펙트 슬롯 B: 카드 공개 버스트]
///   (선택) 등급 연출 — 패널이 나타날 때 카드 뒤 TierRevealFx를 카드 최고 등급으로 재생하고, 끝날 때까지 카드 등장을 늦춘다.
/// 선택 후 연출은 LevelUpSelectSequence, 획득 연출은 LevelUpCollectEffect가 담당한다.
/// 레벨업 중 Time.timeScale = 0 이므로 모든 트윈은 unscaled로 돈다.
/// </summary>
public class LevelUpRevealSequence : MonoBehaviour
{
    [Inject] private ExpManager _expManager;

    [Header("참조")]
    [Tooltip("카드가 튀어나오는 기준점 (소환 버튼 / 경험치 바).")]
    [SerializeField] private RectTransform _origin;
    [Tooltip("카드 이펙트를 생성할 부모. 비우면 카드 컨테이너의 부모(레벨업 패널)를 쓴다.")]
    [SerializeField] private RectTransform _effectRoot;

    [Header("이펙트 슬롯 (비우면 임시 이펙트)")]
    [Tooltip("A: 게이지가 꽉 찼을 때 소환 버튼 위치에서 터지는 이펙트.")]
    [SerializeField] private GameObject _gaugeBurstPrefab;
    [Tooltip("B: 카드 내용이 공개될 때 카드 중심에서 터지는 이펙트.")]
    [SerializeField] private GameObject _cardRevealPrefab;
    [Tooltip("등급 연출: 패널 안 카드 뒤에 둔 TierRevealFx. 카드 중 가장 높은 등급으로 재생하고, 끝날 때까지 카드 등장을 늦춘다. 비우면 없음.")]
    [SerializeField] private TierRevealFx _tierReveal;
    [Tooltip("슬롯 프리팹 인스턴스를 자동 제거할 시간(초, unscaled).")]
    [SerializeField, Min(0.1f)] private float _effectLifetime = 1.5f;

    [Header("1. 게이지 풀")]
    [Tooltip("경험치 바가 꽉 차는 트윈을 기다리는 시간.")]
    [SerializeField, Min(0f)] private float _gaugeFillWait = 0.15f;
    [SerializeField] private float _originPunch = 0.25f;
    [SerializeField, Min(0.01f)] private float _originPunchDuration = 0.3f;
    [Tooltip("버스트 후 레벨업 패널이 나타나기까지의 딜레이 — 이 동안 게이지/섬광이 필드 위에 보인다.")]
    [SerializeField, Min(0f)] private float _panelDelay = 0.45f;
    [SerializeField, Min(0.01f)] private float _panelFadeInDuration = 0.2f;
    [Tooltip("패널이 나타난 뒤 첫 카드가 나오기까지의 간격.")]
    [SerializeField, Min(0f)] private float _afterPanelDelay = 0.05f;

    [Header("2. 카드 비행")]
    [SerializeField, Min(0f)] private float _cardInterval = 0.1f;
    [SerializeField, Min(0.01f)] private float _cardFlyDuration = 0.28f;
    [SerializeField] private Ease _cardFlyEase = Ease.OutCubic;
    [SerializeField, Min(0f)] private float _cardStartScale = 0.25f;
    [SerializeField, Min(1f)] private float _cardOvershootScale = 1.12f;
    [SerializeField] private Color _slabColor = new Color(1f, 0.88f, 0.35f, 1f);

    [Header("3. 공개")]
    [SerializeField, Min(0.01f)] private float _flashToWhiteDuration = 0.06f;
    [SerializeField, Min(0.01f)] private float _revealFadeDuration = 0.2f;
    [SerializeField, Min(0.01f)] private float _settleDuration = 0.18f;

    [Header("임시 이펙트 (슬롯이 비었을 때)")]
    [SerializeField] private Color _placeholderColor = new Color(1f, 0.85f, 0.4f, 1f);
    [SerializeField, Min(1f)] private float _gaugeBurstSize = 420f;
    [SerializeField, Min(1f)] private float _cardBurstSize = 360f;

    private LevelUpFxKit _fx;
    private readonly List<Image> _slabs = new List<Image>();
    private readonly List<Tween> _running = new List<Tween>();

    private LevelUpFxKit Fx => _fx ??= new LevelUpFxKit(_effectLifetime, _placeholderColor);

    /// <summary>카드를 만든 직후(레이아웃 계산 전) 호출 — 카드를 숨기고, 게이지 연출이면 패널도 숨긴다.</summary>
    public void Prepare(IReadOnlyList<LevelUpCardUI> cards, CanvasGroup panel, bool fromGauge)
    {
        foreach (var card in cards)
            if (card != null) card.transform.localScale = Vector3.zero;
        if (panel != null)
        {
            panel.DOKill();
            panel.alpha = fromGauge ? 0f : 1f;
        }
    }

    /// <summary>레이아웃이 확정된 뒤 호출 — 등장 연출이 끝나면 반환한다.</summary>
    public async UniTask PlayAsync(IReadOnlyList<LevelUpCardUI> cards, CanvasGroup panel, bool fromGauge, CancellationToken token)
    {
        var root = ResolveEffectRoot(cards);
        float tierStart = 0f;
        float cardsAt = 0f;
        try
        {
            // 1. 게이지 풀 — 패널이 숨겨진 상태라 필드 위 소환 버튼에서 섬광이 보인다.
            if (fromGauge)
            {
                await UniTask.Delay(LevelUpUiSpace.Ms(_gaugeFillWait), DelayType.Realtime, cancellationToken: token);
                if (_origin != null)
                {
                    _origin.DOKill(true);
                    _ = _origin.DOPunchScale(Vector3.one * _originPunch, _originPunchDuration, 6, 0.6f)
                           .SetUpdate(true).SetLink(_origin.gameObject);
                    var originRoot = LevelUpUiSpace.RootCanvasOf(_origin) ?? root;
                    Fx.Spawn(_gaugeBurstPrefab, originRoot, LevelUpUiSpace.WorldPointIn(originRoot, _origin), _gaugeBurstSize);
                }
                await UniTask.Delay(LevelUpUiSpace.Ms(_panelDelay), DelayType.Realtime, cancellationToken: token);
                tierStart = Time.realtimeSinceStartup;
                cardsAt = PlayTierReveal(cards);
                if (panel != null)
                    await panel.DOFade(1f, _panelFadeInDuration).SetUpdate(true).SetLink(panel.gameObject)
                               .ToUniTask(TweenCancelBehaviour.CancelAwait, token); // 취소 시 트윈은 finally에서 정리
                await UniTask.Delay(LevelUpUiSpace.Ms(_afterPanelDelay), DelayType.Realtime, cancellationToken: token);
            }
            else
            {
                tierStart = Time.realtimeSinceStartup;
                cardsAt = PlayTierReveal(cards);
            }

            // 등급 연출이 카드 등장 시각을 늦추면 그만큼 더 기다린다 (패널 페이드 등으로 이미 흐른 시간은 뺀다)
            float remain = cardsAt - (Time.realtimeSinceStartup - tierStart);
            if (remain > 0f)
                await UniTask.Delay(LevelUpUiSpace.Ms(remain), DelayType.Realtime, cancellationToken: token);

            // 2~3. 카드 순차 등장
            var tasks = new List<UniTask>(cards.Count);
            for (int i = 0; i < cards.Count; i++)
            {
                if (i > 0) await UniTask.Delay(LevelUpUiSpace.Ms(_cardInterval), DelayType.Realtime, cancellationToken: token);
                if (cards[i] != null) tasks.Add(PlayCardAsync(cards[i], root, token));
            }
            await UniTask.WhenAll(tasks);
        }
        finally
        {
            // 취소/완료 어느 쪽이든 카드가 정상 상태로 남도록 정리한다.
            // 카드 비행/공개 시퀀스는 취소 시 대기만 끝내고(CancelAwait) 여기서 한꺼번에 종료한다 —
            // 트윈 Kill 콜백 안에서 다른 트윈을 Kill하면 DOTween 내부 목록이 꼬여 IndexOutOfRange가 난다.
            foreach (var seq in _running) if (seq.IsActive()) seq.Kill();
            _running.Clear();
            foreach (var card in cards)
                if (card != null) { card.transform.DOKill(); card.transform.localScale = Vector3.one; }
            ClearSlabs();
            if (panel != null) { panel.DOKill(); panel.alpha = 1f; }
        }
    }

    private async UniTask PlayCardAsync(LevelUpCardUI card, RectTransform root, CancellationToken token)
    {
        var rt = (RectTransform)card.transform;
        Vector3 target = rt.position;
        rt.localScale = Vector3.one; // Prepare에서 0으로 숨긴 상태 — 덮개 크기를 재기 전에 복원
        var slab = CreateSlab(rt, card.RevealShape);

        rt.position = _origin != null ? LevelUpUiSpace.WorldPointIn((RectTransform)rt.parent, _origin) : target;
        rt.localScale = Vector3.one * _cardStartScale;

        var fly = DOTween.Sequence().SetUpdate(true).SetLink(card.gameObject)
            .Join(rt.DOMove(target, _cardFlyDuration).SetEase(_cardFlyEase))
            .Join(rt.DOScale(_cardOvershootScale, _cardFlyDuration).SetEase(_cardFlyEase))
            .Join(slab.DOColor(Color.white, _cardFlyDuration).SetEase(Ease.InQuad));
        _running.Add(fly);
        await fly.ToUniTask(TweenCancelBehaviour.CancelAwait, token);

        // 도착 순간: 흰 섬광 → 덮개가 걷히며 내용 공개 + 버스트
        slab.color = Color.white;
        await UniTask.Delay(LevelUpUiSpace.Ms(_flashToWhiteDuration), DelayType.Realtime, cancellationToken: token);
        var fx = Fx.Spawn(_cardRevealPrefab, root, LevelUpUiSpace.WorldPointIn(root, rt), _cardBurstSize);
        if (fx != null && fx.TryGetComponent<CardRevealFx>(out var cardFx)) cardFx.PlayOnCard(card);

        var reveal = DOTween.Sequence().SetUpdate(true).SetLink(card.gameObject)
            .Join(slab.DOFade(0f, _revealFadeDuration).SetEase(Ease.OutQuad))
            .Join(rt.DOScale(1f, _settleDuration).SetEase(Ease.OutBack));
        _running.Add(reveal);
        await reveal.ToUniTask(TweenCancelBehaviour.CancelAwait, token);
    }

    /// <summary>카드 최고 등급으로 등급 연출을 재생하고, 카드가 날아오기 시작할 시각(초)을 반환한다. 슬롯이 비면 0.</summary>
    private float PlayTierReveal(IReadOnlyList<LevelUpCardUI> cards)
    {
        if (_tierReveal == null) return 0f;
        var top = Tier.Rare;
        foreach (var card in cards)
        {
            var data = card != null ? card.GetData() : null;
            if (data != null && data.tier > top) top = data.tier;
        }
        return _tierReveal.Play(top);
    }

    // ── 덮개(빛덩어리) ─────────────────────────────────────────

    /// <summary>카드 위에 빛 덮개를 씌운다. shape가 있으면 그 스프라이트/영역을 따라 카드 외곽 모양으로 덮는다.</summary>
    private Image CreateSlab(RectTransform card, Image shape)
    {
        var go = new GameObject("RevealSlab", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(card, false);
        var image = go.GetComponent<Image>();
        if (shape != null && shape.sprite != null)
        {
            var src = shape.rectTransform;
            rt.position = src.TransformPoint(src.rect.center);
            rt.rotation = src.rotation;
            rt.localScale = Vector3.one;
            rt.sizeDelta = src.rect.size * (src.lossyScale.x / card.lossyScale.x);
            image.sprite = shape.sprite;
            image.type = shape.type;
            image.preserveAspect = shape.preserveAspect;
            image.pixelsPerUnitMultiplier = shape.pixelsPerUnitMultiplier;
        }
        else
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
        rt.SetAsLastSibling();
        image.color = _slabColor;
        image.raycastTarget = false;
        _slabs.Add(image);
        return image;
    }

    private void ClearSlabs()
    {
        foreach (var slab in _slabs)
            if (slab != null) { slab.DOKill(); Destroy(slab.gameObject); }
        _slabs.Clear();
    }

    private RectTransform ResolveEffectRoot(IReadOnlyList<LevelUpCardUI> cards)
    {
        if (_effectRoot != null) return _effectRoot;
        foreach (var card in cards)
            if (card != null && card.transform.parent != null)
                return card.transform.parent.parent as RectTransform ?? card.transform.parent as RectTransform;
        return null;
    }

    // LevelUpUI.Hide가 이 오브젝트를 비활성화해도 이펙트는 각자 수명으로 사라진다. 덮개만 즉시 정리한다.
    private void OnDisable() => ClearSlabs();

    private void OnDestroy() => _fx?.DestroyAll();

    // ── 디버그 ────────────────────────────────────────────────

    /// <summary>디버그: 남은 경험치를 채워 실제 레벨업 흐름(연출 포함)을 발동한다. (씬 디버그 버튼이 연결됨)</summary>
    [ContextMenu("디버그: 즉시 레벨업 (연출 테스트)")]
    public void DebugTriggerLevelUp()
    {
        if (!Application.isPlaying || _expManager == null || _expManager.IsMaxLevel) return;
        _expManager.AddExp(Mathf.Max(_expManager.ExpToLevelUp - _expManager.CurrentExp, 0.01f));
    }
}
