using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [테스트 전용] 레벨업 등장 흐름 시험대 (LevelUpFlowTest 씬, DI 없음). 레퍼런스: design/levelup-reveal-reference-frames.png
///   1. 전환 — 주황 속도선(UI_DiagonalFlowBackground)이 우상단에서 쏟아져 화면을 덮었다가(WipeIn) 좌하단으로 빠져나감(WipeOut)
///   2. 검은 화면 — 이펙트 슬롯(_burstPrefab)이 터짐 (비우면 생략)
///   3. 메인 — 카드 1→2→3 "빵빵빵" (0 → 오버슈트 → 1, 기울기 복원, 흰 덮개가 잠깐 머물다 걷힘, 카드 영역 흔들림)
/// 샘플 LevelUpData로 실제 카드 프리팹을 채운다. 화면 좌상단 버튼으로 다시 재생.
/// 실제 인게임 연출은 LevelUpRevealSequence — 여기서 확정된 값만 옮긴다.
/// </summary>
public class LevelUpPopTest : MonoBehaviour
{
    private static readonly int WipeInId = Shader.PropertyToID("_WipeIn");
    private static readonly int WipeOutId = Shader.PropertyToID("_WipeOut");

    [Header("참조")]
    [Tooltip("메인 화면(검은 배경 + 카드 영역 + 텍스트).")]
    [SerializeField] private CanvasGroup _panel;
    [SerializeField] private RectTransform _cardArea;
    [SerializeField] private LevelUpCardUI _cardPrefab;
    [SerializeField] private LevelUpData[] _sampleData;
    [Tooltip("전환 오버레이 (UI_DiagonalFlowBackground 머티리얼, 최상단). 비우면 전환 생략.")]
    [SerializeField] private Image _transition;

    [Header("1. 전환")]
    [SerializeField, Min(0.01f)] private float _wipeInDuration = 0.4f;
    [SerializeField] private Ease _wipeInEase = Ease.OutQuad;
    [Tooltip("화면을 완전히 덮은 채 머무는 시간.")]
    [SerializeField, Min(0f)] private float _coverHold = 0.15f;
    [SerializeField, Min(0.01f)] private float _wipeOutDuration = 0.4f;
    [SerializeField] private Ease _wipeOutEase = Ease.InQuad;

    [Header("2. 검은 화면 이펙트")]
    [Tooltip("검은 화면에서 터질 이펙트 프리팹 (직접 제작해 연결). 비우면 생략.")]
    [SerializeField] private GameObject _burstPrefab;
    [Tooltip("이펙트 생성 위치. 비우면 메인 패널 중앙.")]
    [SerializeField] private RectTransform _burstPoint;
    [SerializeField, Min(0.1f)] private float _burstLifetime = 2f;
    [Tooltip("검은 화면이 드러난 뒤 이펙트가 터지기까지.")]
    [SerializeField, Min(0f)] private float _burstDelay = 0.05f;
    [Tooltip("이펙트가 터진 뒤 첫 카드가 나오기까지.")]
    [SerializeField, Min(0f)] private float _burstToCards = 0.35f;

    [Header("3. 카드 팝")]
    [SerializeField, Min(0f)] private float _cardInterval = 0.12f;
    [SerializeField, Min(0.01f)] private float _popDuration = 0.32f;
    [SerializeField, Min(0f)] private float _popOvershoot = 2.2f;
    [Tooltip("카드가 기울어진 채 튀어나오는 각도 (카드마다 좌우 번갈아).")]
    [SerializeField] private float _popTilt = 10f;
    [SerializeField] private Color _flashColor = new Color(1f, 0.97f, 0.8f, 1f);
    [Tooltip("흰 덮개가 걷히기 전 머무는 시간.")]
    [SerializeField, Min(0f)] private float _flashHold = 0.1f;
    [SerializeField, Min(0.01f)] private float _flashDuration = 0.2f;
    [Tooltip("카드가 튀어나올 때 카드 영역이 흔들리는 세기 (Canvas 단위).")]
    [SerializeField] private float _areaPunch = 14f;
    [SerializeField, Min(0.01f)] private float _areaPunchDuration = 0.18f;

    [Header("테스트")]
    [SerializeField] private bool _playOnStart = true;
    [SerializeField] private bool _showButtons = true;

    private readonly List<LevelUpCardUI> _cards = new List<LevelUpCardUI>();
    private readonly List<GameObject> _bursts = new List<GameObject>();
    private CancellationTokenSource _cts;
    private Vector2 _areaPos;
    private Material _transitionMat;

    private void Start()
    {
        if (_cardArea != null) _areaPos = _cardArea.anchoredPosition;
        if (_transition != null && _transition.material != null)
        {
            _transitionMat = new Material(_transition.material); // 에셋 머티리얼을 건드리지 않도록 인스턴스
            _transition.material = _transitionMat;
        }
        if (_playOnStart) Replay();
    }

    /// <summary>카드를 새로 만들고 전체 흐름을 처음부터 재생한다.</summary>
    [ContextMenu("다시 재생")]
    public void Replay()
    {
        StopPlay();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        PlayAsync(_cts.Token).Forget();
    }

    private async UniTaskVoid PlayAsync(CancellationToken token)
    {
        BuildCards();
        ClearBursts();
        if (_panel != null) { _panel.DOKill(); _panel.alpha = 0f; }

        // 1. 전환 — 덮는 동안 뒤에서 메인 패널(검은 화면)을 켠다.
        if (_transitionMat != null)
        {
            _transition.gameObject.SetActive(true);
            _transitionMat.SetFloat(WipeInId, 0f);
            _transitionMat.SetFloat(WipeOutId, 0f);
            await TweenFloat(WipeInId, _wipeInDuration, _wipeInEase, token);
            if (_panel != null) _panel.alpha = 1f;
            await UniTask.Delay(Ms(_coverHold), DelayType.Realtime, cancellationToken: token);
            await TweenFloat(WipeOutId, _wipeOutDuration, _wipeOutEase, token);
            _transition.gameObject.SetActive(false);
        }
        else if (_panel != null) _panel.alpha = 1f;

        // 레이아웃이 카드 위치를 확정할 때까지 한 프레임 대기 (스케일은 레이아웃과 무관하므로 0으로 숨겨둠)
        await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, token);

        // 2. 검은 화면 이펙트
        await UniTask.Delay(Ms(_burstDelay), DelayType.Realtime, cancellationToken: token);
        SpawnBurst();
        await UniTask.Delay(Ms(_burstToCards), DelayType.Realtime, cancellationToken: token);

        // 3. 카드 팝
        for (int i = 0; i < _cards.Count; i++)
        {
            if (i > 0) await UniTask.Delay(Ms(_cardInterval), DelayType.Realtime, cancellationToken: token);
            Pop(_cards[i], i);
        }
    }

    private UniTask TweenFloat(int id, float duration, Ease ease, CancellationToken token)
    {
        var mat = _transitionMat;
        mat.SetFloat(id, 0f);
        return DOTween.To(() => mat.GetFloat(id), x => mat.SetFloat(id, x), 1f, duration)
            .SetEase(ease).SetUpdate(true).SetLink(_transition.gameObject)
            .ToUniTask(cancellationToken: token);
    }

    private void SpawnBurst()
    {
        if (_burstPrefab == null) return;
        var parent = _burstPoint != null ? _burstPoint : _panel != null ? (RectTransform)_panel.transform : null;
        if (parent == null) return;
        var burst = Instantiate(_burstPrefab, parent, false);
        _bursts.Add(burst);
        Destroy(burst, _burstLifetime);
    }

    private void Pop(LevelUpCardUI card, int index)
    {
        if (card == null) return;
        var rt = (RectTransform)card.transform;
        float tilt = index % 2 == 0 ? _popTilt : -_popTilt;
        rt.localScale = Vector3.zero;
        rt.localRotation = Quaternion.Euler(0f, 0f, tilt);

        _ = DOTween.Sequence().SetUpdate(true).SetLink(card.gameObject)
            .Join(rt.DOScale(1f, _popDuration).SetEase(Ease.OutBack, _popOvershoot))
            .Join(rt.DOLocalRotate(Vector3.zero, _popDuration).SetEase(Ease.OutBack));

        var flash = CreateFlash(rt);
        _ = flash.DOFade(0f, _flashDuration).SetDelay(_flashHold).SetEase(Ease.OutQuad).SetUpdate(true)
                 .SetLink(flash.gameObject).OnComplete(() => Destroy(flash.gameObject));

        if (_cardArea != null)
        {
            _cardArea.DOKill(true);
            _cardArea.anchoredPosition = _areaPos;
            _ = _cardArea.DOPunchAnchorPos(new Vector2(0f, -_areaPunch), _areaPunchDuration, 8, 0.6f)
                         .SetUpdate(true).SetLink(_cardArea.gameObject);
        }
    }

    // 카드 외곽(테두리 스프라이트)을 따라 흰 덮개를 씌운다.
    private Image CreateFlash(RectTransform card)
    {
        var go = new GameObject("PopFlash", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(card, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var image = go.GetComponent<Image>();
        var shape = card.GetComponent<LevelUpCardUI>()?.RevealShape;
        if (shape != null && shape.sprite != null)
        {
            image.sprite = shape.sprite;
            image.type = shape.type;
            image.pixelsPerUnitMultiplier = shape.pixelsPerUnitMultiplier;
        }
        image.color = _flashColor;
        image.raycastTarget = false;
        rt.SetAsLastSibling();
        return image;
    }

    private void BuildCards()
    {
        ClearCards();
        if (_cardArea == null || _cardPrefab == null || _sampleData == null) return;
        foreach (var data in _sampleData)
        {
            if (data == null) continue;
            var card = Instantiate(_cardPrefab, _cardArea);
            card.Setup(data, null);
            card.transform.localScale = Vector3.zero;
            _cards.Add(card);
        }
    }

    private void ClearCards()
    {
        foreach (var card in _cards)
            if (card != null) { card.transform.DOKill(); Destroy(card.gameObject); }
        _cards.Clear();
    }

    private void ClearBursts()
    {
        foreach (var burst in _bursts)
            if (burst != null) Destroy(burst);
        _bursts.Clear();
    }

    private void StopPlay()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    private static int Ms(float seconds) => Mathf.RoundToInt(seconds * 1000f);

    private void OnGUI()
    {
        if (!_showButtons) return;
        GUI.skin.button.fontSize = 32;
        if (GUI.Button(new Rect(20, 20, 260, 80), "다시 재생")) Replay();
        if (GUI.Button(new Rect(20, 110, 260, 80), "버튼 숨기기")) _showButtons = false;
    }

    private void OnDestroy()
    {
        StopPlay();
        if (_transitionMat != null) Destroy(_transitionMat);
    }
}
