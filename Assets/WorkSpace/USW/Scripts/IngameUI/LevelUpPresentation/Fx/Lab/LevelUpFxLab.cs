using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 레벨업 이펙트 실험실(FxLab_LevelUp 씬) 드라이버 — 게임 시스템 없이 레벨업 등장 연출을 등급별로 재생한다.
/// IngameScene에서 복사해 온 레벨업 패널 + 실제 LevelUpRevealSequence를 그대로 쓰므로, 슬롯에 꽂은 이펙트가 게임과 같게 재생된다.
/// 흐름은 LevelUpUI.Show/OpenAsync와 같다: 카드 생성 → Prepare → 레이아웃 확정 → PlayAsync → 입력 허용.
/// 씬 구성은 Tools/USW/Fx/Build LevelUp Fx Lab (LevelUpFxLabBuilder)이 만든다.
/// </summary>
public class LevelUpFxLab : MonoBehaviour, IFxLabPlayable
{
    private static readonly int FillId = Shader.PropertyToID("_Fill");

    [Header("참조 (빌더가 연결)")]
    [SerializeField] private LevelUpRevealSequence _reveal;
    [Tooltip("카드를 누르면 실제 선택 연출(④ 선택 이펙트 슬롯 포함)을 재생한다")]
    [SerializeField] private LevelUpSelectSequence _select;
    [SerializeField] private GameObject _panelObject;
    [SerializeField] private Transform _cardContainer;
    [SerializeField] private LevelUpCardUI _cardPrefab;
    [Tooltip("등급별로 앞에서부터 3장을 쓴다")]
    [SerializeField] private LevelUpData[] _samples = System.Array.Empty<LevelUpData>();
    [Tooltip("소환 버튼 게이지 (머티리얼에 _Fill이 있으면 차오르는 모습을 흉내 낸다)")]
    [SerializeField] private Graphic _gaugeFill;

    [Header("이펙트 비교 (실험실 전용)")]
    [Tooltip("① 경험치 MAX 버스트 후보 (0 = A 만화풍, 1 = B 빛 번짐 — 사용자 선택). 재생할 때 복사본 LevelUpRevealSequence 슬롯 A에 꽂는다")]
    [SerializeField] private GameObject[] _gaugeBurstVariants = System.Array.Empty<GameObject>();
    [SerializeField] private int _gaugeBurstVariant = 1;
    [Tooltip("③ 카드 공개 이펙트 후보 (0 = A 일직선, 1 = B 카드 모양). 재생할 때 복사본 LevelUpRevealSequence 슬롯 B에 꽂는다")]
    [SerializeField] private GameObject[] _cardRevealVariants = System.Array.Empty<GameObject>();
    [SerializeField] private int _cardRevealVariant;
    [Tooltip("레전더리 보라 → 빨강 반전 확인용 (UPG 버튼)")]
    [SerializeField] private TierRevealFx _tierReveal;

    [Header("전환 버튼 글자 (빌더가 연결)")]
    [SerializeField] private TMPro.TMP_Text _burstLabel;
    [SerializeField] private TMPro.TMP_Text _cardLabel;
    [SerializeField] private TMPro.TMP_Text _upgradeLabel;

    [Header("재생")]
    [Tooltip("캡처(Play) 시 재생할 등급")]
    [SerializeField] private Tier _captureTier = Tier.Legend;
    [SerializeField] private bool _playOnStart;
    [SerializeField, Range(0f, 1f)] private float _gaugeStartFill = 0.6f;
    [SerializeField, Min(0.01f)] private float _gaugeFillDuration = 0.15f;
    [Tooltip("캡처(Play) 때 카드 공개 후 자동으로 고를 카드 번호 (-1이면 안 고름)")]
    [SerializeField] private int _captureSelectIndex = 1;
    [SerializeField, Min(0f)] private float _captureSelectDelay = 0.5f;

    private readonly List<LevelUpCardUI> _cards = new List<LevelUpCardUI>();
    private CanvasGroup _panel;
    private CancellationTokenSource _cts;
    private Material _gaugeMaterial;

    /// <summary>실험실은 항상 unscaled로 돈다 (실시간 캡처 전용).</summary>
    public bool UseUnscaledTime { get => true; set { } }

    private void Awake()
    {
        if (_panelObject != null)
        {
            _panel = _panelObject.GetComponent<CanvasGroup>();
            if (_panel == null) _panel = _panelObject.AddComponent<CanvasGroup>();
        }
        if (_gaugeFill != null && _gaugeFill.material != null && _gaugeFill.material.HasProperty(FillId))
        {
            _gaugeMaterial = new Material(_gaugeFill.material);
            _gaugeFill.material = _gaugeMaterial;
        }
        HidePanel();
    }

    private void Start()
    {
        RefreshLabels();
        if (_playOnStart) Play();
    }

    /// <summary>BURST 버튼: ① 경험치 MAX 버스트 A(만화풍) ↔ B(빛 번짐). 다음 재생부터 반영.</summary>
    public void ToggleGaugeBurst()
    {
        if (_gaugeBurstVariants.Length > 0) _gaugeBurstVariant = (_gaugeBurstVariant + 1) % _gaugeBurstVariants.Length;
        RefreshLabels();
    }

    /// <summary>CARD 버튼: ③ 카드 공개 A(일직선) ↔ B(카드 모양). 다음 재생부터 반영.</summary>
    public void ToggleCardReveal()
    {
        if (_cardRevealVariants.Length > 0) _cardRevealVariant = (_cardRevealVariant + 1) % _cardRevealVariants.Length;
        RefreshLabels();
    }

    /// <summary>UPG 버튼: 레전더리 반전 확률(10%) → 항상 → 없음 → 확률 순으로 바꾼다.</summary>
    public void CycleLegendUpgrade()
    {
        if (_tierReveal == null) return;
        var force = _tierReveal.DebugForceUpgrade;
        _tierReveal.DebugForceUpgrade = force == null ? true : force.Value ? false : (bool?)null;
        RefreshLabels();
    }

    /// <summary>레전더리 반전 강제 설정 (null = 확률, true = 항상, false = 없음).</summary>
    public void SetLegendUpgrade(bool? force)
    {
        if (_tierReveal != null) _tierReveal.DebugForceUpgrade = force;
        RefreshLabels();
    }

    private void RefreshLabels()
    {
        if (_burstLabel != null) _burstLabel.text = $"BURST {(char)('A' + _gaugeBurstVariant)}";
        if (_cardLabel != null) _cardLabel.text = $"CARD {(char)('A' + _cardRevealVariant)}";
        if (_upgradeLabel != null && _tierReveal != null)
        {
            var force = _tierReveal.DebugForceUpgrade;
            _upgradeLabel.text = force == null ? "UPG 10%" : force.Value ? "UPG ON" : "UPG OFF";
        }
    }

    private void OnDestroy()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        if (_gaugeMaterial != null) Destroy(_gaugeMaterial);
    }

    /// <summary>캡처용: _captureTier로 재생하고, 공개 후 _captureSelectIndex 카드를 자동으로 고른다.</summary>
    public void Play() => PlayTier(_captureTier, _captureSelectIndex);

    /// <summary>버튼용: 0 = 레어, 1 = 에픽, 2 = 레전더리. 카드는 직접 눌러 고른다.</summary>
    public void PlayTierIndex(int index) => PlayTier((Tier)((int)Tier.Rare + Mathf.Clamp(index, 0, 2)));

    /// <summary>해당 등급 카드 3장으로 게이지 레벨업 연출을 처음부터 재생한다. autoSelect ≥ 0이면 공개 후 그 카드를 고른다.</summary>
    public void PlayTier(Tier tier, int autoSelect = -1)
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        PlayAsync(tier, autoSelect, _cts.Token).Forget();
    }

    /// <summary>캡처(Play) 시 재생할 등급: 0 = 레어, 1 = 에픽, 2 = 레전더리.</summary>
    public void SetCaptureTier(int index)
    {
        _captureTier = (Tier)((int)Tier.Rare + Mathf.Clamp(index, 0, 2));
        Debug.Log($"[LevelUpFxLab] 캡처 등급: {_captureTier}");
    }

    /// <summary>① 버스트 후보를 고른다 (다음 재생부터 반영).</summary>
    public void SetGaugeBurstVariant(int index)
    {
        _gaugeBurstVariant = index;
        RefreshLabels();
        Debug.Log($"[LevelUpFxLab] 경험치 MAX 버스트: {(index == 0 ? "A 만화풍" : "B 빛 번짐")}");
    }

    /// <summary>③ 카드 공개 이펙트 후보를 고른다 (다음 재생부터 반영).</summary>
    public void SetCardRevealVariant(int index)
    {
        _cardRevealVariant = index;
        RefreshLabels();
        Debug.Log($"[LevelUpFxLab] 카드 공개 이펙트: {(index == 0 ? "A 일직선" : "B 카드 모양")}");
    }

    // 게임 코드를 고치지 않고 실험실 복사본의 슬롯만 바꾼다 (private 직렬화 필드라 리플렉션 사용 — 실험실 전용).
    private const System.Reflection.BindingFlags PrivateField =
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
    private static readonly System.Reflection.FieldInfo GaugeBurstField =
        typeof(LevelUpRevealSequence).GetField("_gaugeBurstPrefab", PrivateField);
    private static readonly System.Reflection.FieldInfo CardRevealField =
        typeof(LevelUpRevealSequence).GetField("_cardRevealPrefab", PrivateField);

    private void ApplyVariants()
    {
        ApplyVariant(GaugeBurstField, _gaugeBurstVariants, _gaugeBurstVariant);
        ApplyVariant(CardRevealField, _cardRevealVariants, _cardRevealVariant);
    }

    private void ApplyVariant(System.Reflection.FieldInfo field, GameObject[] variants, int index)
    {
        if (field == null || variants.Length == 0) return;
        field.SetValue(_reveal, variants[Mathf.Clamp(index, 0, variants.Length - 1)]);
    }

    /// <summary>패널을 닫고 카드를 지운다.</summary>
    [ContextMenu("Reset")]
    public void HidePanel()
    {
        ClearCards();
        if (_panelObject != null) _panelObject.SetActive(false);
    }

    private async UniTaskVoid PlayAsync(Tier tier, int autoSelect, CancellationToken token)
    {
        if (_panel != null) { _panel.DOKill(); _panel.alpha = 1f; }
        ClearCards();
        var layout = _cardContainer != null ? _cardContainer.GetComponent<LayoutGroup>() : null;
        if (layout != null) layout.enabled = true;

        foreach (var data in PickSamples(tier))
        {
            var card = Instantiate(_cardPrefab, _cardContainer);
            card.Setup(data, OnCardClicked);
            _cards.Add(card);
        }
        if (_cards.Count == 0) { Debug.LogWarning($"[LevelUpFxLab] {tier} 샘플 카드 없음"); return; }

        SetInteractable(false);
        ApplyVariants();
        if (_panelObject != null) _panelObject.SetActive(true);
        _reveal.Prepare(_cards, _panel, fromGauge: true);
        AnimateGauge();

        try
        {
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, token);
            if (layout != null) layout.enabled = false;
            await _reveal.PlayAsync(_cards, _panel, fromGauge: true, token);
            SetInteractable(true);
            if (autoSelect >= 0 && autoSelect < _cards.Count)
            {
                await UniTask.Delay(LevelUpUiSpace.Ms(_captureSelectDelay), DelayType.Realtime, cancellationToken: token);
                OnCardClicked(_cards[autoSelect]);
            }
        }
        catch (System.OperationCanceledException) { }
    }

    // 실제 LevelUpSelectSequence로 선택 연출(선택 카드 눌림 + ④ 선택 이펙트 + 나머지 퇴장 + 패널 페이드)을 재생한다.
    private async UniTaskVoid SelectAsync(LevelUpCardUI card, CancellationToken token)
    {
        SetInteractable(false);
        try
        {
            if (_select != null)
                await _select.PlayAsync(card, _cards, _panel, collectIcon: false, token);
            await UniTask.Delay(300, DelayType.Realtime, cancellationToken: token);
            HidePanel();
            if (_panel != null) _panel.alpha = 1f;
        }
        catch (System.OperationCanceledException) { }
    }

    // LevelUpUI.SetCardsInteractable과 같은 방식 (연출 중 입력 차단)
    private void SetInteractable(bool interactable)
    {
        foreach (var card in _cards)
        {
            if (card == null) continue;
            var group = card.GetComponent<CanvasGroup>();
            if (group == null) group = card.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = interactable;
        }
    }

    private void AnimateGauge()
    {
        if (_gaugeMaterial == null) return;
        _gaugeMaterial.DOKill();
        _gaugeMaterial.SetFloat(FillId, _gaugeStartFill);
        _gaugeMaterial.DOFloat(1f, FillId, _gaugeFillDuration).SetUpdate(true).SetLink(gameObject);
    }

    private List<LevelUpData> PickSamples(Tier tier)
    {
        var picked = new List<LevelUpData>(3);
        foreach (var d in _samples)
            if (d != null && d.tier == tier && picked.Count < 3) picked.Add(d);
        return picked;
    }

    private void OnCardClicked(LevelUpCardUI card)
    {
        Debug.Log($"[LevelUpFxLab] 선택: {card.GetData()?.chooseName}");
        if (_cts != null && !_cts.IsCancellationRequested) SelectAsync(card, _cts.Token).Forget();
    }

    private void ClearCards()
    {
        // Destroy는 프레임 끝에 처리되므로, 같은 프레임에 새 카드를 만들 때 레이아웃에 끼지 않게 먼저 떼어 낸다.
        foreach (var card in _cards)
        {
            if (card == null) continue;
            card.transform.SetParent(null, false);
            Destroy(card.gameObject);
        }
        _cards.Clear();
    }
}
