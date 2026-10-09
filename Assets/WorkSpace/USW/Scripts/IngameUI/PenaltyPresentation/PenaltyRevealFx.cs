using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 무한모드 패널티 부여 연출 (10라운드 보스 처치 후 등 EndlessRunService.OnPenaltyReserved 시점에 재생 예정 — 게임 연결 전, 실험실 FxLab_Penalty에서 확인).
/// 레퍼런스: design/패널티연출.gif — 배너 등장 → 문구 슬롯 회전(세로 번짐) → 회색 배너에 중간 문구 → 노란 섬광·가로 빛·충격파·조각 튀김
///   → 결과 두 줄 + 위쪽 노란 문구 + 불씨 → 잠시 후 사라짐. 레퍼런스의 주사위 조각 자리는 삼각형 조각으로 바꿨다.
/// 배너 바탕은 USW/UI/FxPenaltyBanner — SPR_UI2100_CardFrame_Rare 머리띠처럼 비스듬한 삼각형 무늬(SPR_UI1000_ButtonArrow_Off)가 줄 방향으로 계속 흐른다.
/// 문구는 RunPenaltyData(이름·대상·단위·증감값)로 만든다. 모든 트윈은 기본 unscaled (보상 화면 일시정지 중 재생).
/// </summary>
public class PenaltyRevealFx : MonoBehaviour, IFxLabPlayable
{
    private static readonly int QuadSizeId = Shader.PropertyToID("_QuadSize");
    private static readonly int BoxHalfId = Shader.PropertyToID("_BoxHalf");
    private static readonly int GrayId = Shader.PropertyToID("_Gray");
    private static readonly int FlashId = Shader.PropertyToID("_Flash");

    [Header("요소 (빌더가 연결)")]
    [SerializeField] private CanvasGroup _group;
    [Tooltip("배너·문구·빛을 묶는 부모 — 등장 시 세로로 펼쳐지고 섬광 때 튄다")]
    [SerializeField] private RectTransform _bannerRoot;
    [SerializeField] private Image _banner;
    [Tooltip("슬롯 문구 줄들의 부모 (RectMask2D 뷰포트 안)")]
    [SerializeField] private RectTransform _rollContent;
    [Tooltip("슬롯 문구 한 줄 원본 — 나머지 줄은 Awake에서 복제")]
    [SerializeField] private TextMeshProUGUI _rollRowTemplate;
    [SerializeField] private TextMeshProUGUI _result;
    [SerializeField] private TextMeshProUGUI _tag;
    [SerializeField] private Image _flare;
    [SerializeField] private Image _ring;
    [SerializeField] private UiFxParticleEmitter _sparks;
    [SerializeField] private UiFxParticleEmitter _debris;

    [Header("배너")]
    [SerializeField] private Vector2 _bannerSize = new Vector2(920f, 150f);
    [Tooltip("섬광 바깥 번짐이 그려질 여백(px)")]
    [SerializeField] private float _glowPadding = 90f;
    [SerializeField] private float _flareWidth = 1500f;
    [SerializeField] private float _flareHeight = 120f;

    [Header("문구")]
    [Tooltip("슬롯이 도는 동안 스쳐 지나가는 문구 후보 (무작위 순서, 연속 중복 없음)")]
    [SerializeField] private string[] _rollCandidates =
    {
        "패널티1패널티1패널티1", "패널티2패널티2패널티2", "패널티3패널티3패널티3", "패널티4패널티4패널티4",
    };
    [Tooltip("슬롯이 멈추며 보여 주는 중간 문구 (회색 배너)")]
    [SerializeField] private string _landingText = "불길한 기운이 감돕니다";
    [Tooltip("결과와 함께 배너 위에 튀어나오는 노란 문구")]
    [SerializeField] private string _tagText = "패널티!";
    [Tooltip("슬롯 줄 수 (마지막 줄 = 중간 문구). 많을수록 빨리 돈다")]
    [SerializeField] private int _rollRows = 18;
    [Tooltip("슬롯이 가장 빠를 때 문구를 세로로 늘이는 양 (1 = 두 배)")]
    [SerializeField] private float _rollBlur = 1.4f;

    [Header("타이밍 (초)")]
    [SerializeField] private float _popIn = 0.16f;
    [SerializeField] private float _rollDuration = 0.9f;
    [Tooltip("중간 문구에서 멈춰 있는 시간")]
    [SerializeField] private float _landHold = 0.25f;
    [SerializeField] private float _flashRise = 0.05f;
    [SerializeField] private float _flashFall = 0.35f;
    [Tooltip("결과를 보여 주는 시간")]
    [SerializeField] private float _hold = 1.6f;
    [SerializeField] private float _fadeOut = 0.3f;
    [SerializeField] private bool _useUnscaledTime = true;

    [Header("실험실")]
    [Tooltip("실험실 캡처(Play())용 패널티")]
    [SerializeField] private RunPenaltyData _labPenalty;

    private readonly List<TextMeshProUGUI> _rows = new List<TextMeshProUGUI>();
    private Material _bannerSource;
    private Material _bannerMat;
    private Sequence _sequence;
    private CancellationTokenSource _playCts;

    /// <inheritdoc />
    public bool UseUnscaledTime { get => _useUnscaledTime; set => _useUnscaledTime = value; }

    /// <summary>슬롯 문구 후보를 바꾼다 (다음 재생부터).</summary>
    public void SetRollCandidates(string[] candidates)
    {
        if (candidates != null && candidates.Length > 0) _rollCandidates = candidates;
    }

    private void Awake()
    {
        if (_banner != null && _banner.material != null)
        {
            _bannerSource = _banner.material;
            _bannerMat = new Material(_bannerSource);
            _banner.material = _bannerMat;
        }
        BuildRows();
        HideImmediate();
    }

    private void OnDisable()
    {
        CancelPlay();
        _sequence?.Kill();
        _sequence = null;
        HideImmediate();
    }

    private void OnDestroy()
    {
        CancelPlay();
        if (_bannerMat != null) Destroy(_bannerMat);
    }

    /// <summary>실험실용: _labPenalty로 재생.</summary>
    [ContextMenu("Play")]
    public void Play() => Play(_labPenalty, 1);

    /// <summary>재생하고 기다리지 않는다. 재생 중이면 처음부터 다시 재생한다.</summary>
    public void Play(RunPenaltyData penalty, int stackCount)
    {
        CancelPlay();
        _playCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        PlayAsync(penalty, stackCount, _playCts.Token).Forget();
    }

    /// <summary>
    /// 연출을 처음부터 끝(사라짐)까지 재생한다. 취소되면 즉시 숨긴다.
    /// </summary>
    /// <param name="penalty">표시할 패널티 (null이면 이름만 비운 채 재생)</param>
    /// <param name="stackCount">이 패널티의 누적 횟수 (2 이상이면 제목에 ×N 표시)</param>
    public async UniTask PlayAsync(RunPenaltyData penalty, int stackCount, CancellationToken token)
    {
        _sequence?.Kill();
        var sequence = BuildSequence(penalty, stackCount);
        _sequence = sequence;
        try
        {
            await sequence.ToUniTask(TweenCancelBehaviour.CancelAwait, token);
        }
        finally
        {
            if (sequence.IsActive()) sequence.Kill();
            if (token.IsCancellationRequested) HideImmediate();
        }
    }

    private void CancelPlay()
    {
        if (_playCts == null) return;
        _playCts.Cancel();
        _playCts.Dispose();
        _playCts = null;
    }

    private void BuildRows()
    {
        if (_rollRowTemplate == null || _rows.Count > 0) return;
        _rows.Add(_rollRowTemplate);
        for (int i = 1; i < Mathf.Max(2, _rollRows); i++)
        {
            var row = Instantiate(_rollRowTemplate, _rollRowTemplate.transform.parent);
            row.name = $"Row_{i}";
            _rows.Add(row);
        }
    }

    private Sequence BuildSequence(RunPenaltyData penalty, int stackCount)
    {
        BuildRows();
        HideImmediate();
        ApplyBannerSize();
        FillRows();
        if (_result != null) _result.text = ResultText(penalty, stackCount);
        if (_tag != null) _tag.text = _tagText;

        var seq = DOTween.Sequence().SetUpdate(_useUnscaledTime).SetLink(gameObject);

        // 1. 등장: 배너가 세로로 펼쳐지며 나타남
        _group.alpha = 0f;
        _bannerRoot.localScale = new Vector3(1f, 0.2f, 1f);
        seq.Insert(0f, _group.DOFade(1f, _popIn));
        seq.Insert(0f, _bannerRoot.DOScaleY(1f, _popIn).SetEase(Ease.OutBack));

        // 2. 슬롯: 문구 줄들이 위로 빠르게 지나가다 감속해 마지막 줄(중간 문구)에서 멈춘다. 빠를수록 세로로 번짐.
        float rollStart = _popIn * 0.5f;
        float step = _bannerSize.y;
        if (_rollContent != null && _rows.Count > 0)
        {
            _rollContent.gameObject.SetActive(true);
            _rollContent.anchoredPosition = Vector2.zero;
            float distance = step * (_rows.Count - 1);
            // 진행률 p(선형)로 위치 = OutQuart(1-(1-p)^4), 속도 ∝ (1-p)^3 → 번짐
            seq.Insert(rollStart, DOTween.To(() => 0f, p =>
            {
                float rest = 1f - p;
                _rollContent.anchoredPosition = new Vector2(0f, distance * (1f - rest * rest * rest * rest));
                ApplyRollBlur(rest * rest * rest);
            }, 1f, _rollDuration).SetEase(Ease.Linear));
        }
        float land = rollStart + _rollDuration;
        if (_bannerMat != null) seq.Insert(land - 0.1f, _bannerMat.DOFloat(1f, GrayId, 0.12f));

        // 3. 섬광: 배너가 노랗게 차오르고 가로 빛·충격파·삼각형 조각
        float burst = land + _landHold;
        if (_bannerMat != null) seq.Insert(burst, _bannerMat.DOFloat(1f, FlashId, _flashRise));
        seq.Insert(burst, _bannerRoot.DOPunchScale(new Vector3(0.06f, 0.12f, 0f), 0.3f, 6, 0.6f));
        if (_flare != null)
        {
            var rt = _flare.rectTransform;
            seq.InsertCallback(burst, () =>
            {
                rt.sizeDelta = new Vector2(_flareWidth, _flareHeight);
                rt.localScale = new Vector3(0.2f, 1f, 1f);
                _flare.color = WithAlpha(_flare.color, 0f);
                _flare.gameObject.SetActive(true);
            });
            seq.Insert(burst, _flare.DOFade(1f, 0.05f));
            seq.Insert(burst, rt.DOScaleX(1f, 0.22f).SetEase(Ease.OutCubic));
            seq.Insert(burst + 0.12f, _flare.DOFade(0f, 0.4f).SetEase(Ease.OutQuad));
        }
        if (_ring != null)
        {
            var rt = _ring.rectTransform;
            seq.InsertCallback(burst, () =>
            {
                rt.localScale = Vector3.one * 0.2f;
                _ring.color = WithAlpha(_ring.color, 1f);
                _ring.gameObject.SetActive(true);
            });
            seq.Insert(burst, rt.DOScale(1f, 0.45f).SetEase(Ease.OutQuad));
            seq.Insert(burst + 0.05f, _ring.DOFade(0f, 0.4f).SetEase(Ease.OutCubic));
        }
        if (_debris != null) seq.InsertCallback(burst, () => { _debris.UseUnscaledTime = _useUnscaledTime; _debris.Play(); });

        // 4. 결과: 섬광이 가장 밝을 때 문구를 바꾸고, 섬광이 걷히며 결과·노란 문구·불씨
        float swap = burst + _flashRise + 0.03f;
        seq.InsertCallback(swap, () =>
        {
            if (_rollContent != null) _rollContent.gameObject.SetActive(false);
            if (_result != null) _result.alpha = 1f;
            if (_bannerMat != null) _bannerMat.SetFloat(GrayId, 0f);
            if (_sparks != null) { _sparks.UseUnscaledTime = _useUnscaledTime; _sparks.Play(); }
        });
        if (_bannerMat != null) seq.Insert(swap, _bannerMat.DOFloat(0f, FlashId, _flashFall).SetEase(Ease.OutQuad));
        if (_tag != null)
        {
            var rt = _tag.rectTransform;
            seq.Insert(swap + 0.1f, _tag.DOFade(1f, 0.06f));
            seq.Insert(swap + 0.1f, rt.DOScale(1f, 0.32f).SetEase(Ease.OutBack, 2.2f));
        }

        // 5. 사라짐
        float end = swap + _flashFall + _hold;
        seq.InsertCallback(end, () => { if (_sparks != null) _sparks.Stop(); });
        seq.Insert(end, _group.DOFade(0f, _fadeOut));
        seq.InsertCallback(end + _fadeOut, HideImmediate);
        return seq;
    }

    // 배너 쿼드 = 배너 + 섬광 번짐 여백. 셰이더는 px 크기를 받아 둥근 사각형을 그린다.
    private void ApplyBannerSize()
    {
        if (_banner == null) return;
        var quad = _bannerSize + Vector2.one * (_glowPadding * 2f);
        _banner.rectTransform.sizeDelta = quad;
        if (_bannerMat != null)
        {
            // 재생마다 원본 머티리얼 값을 다시 읽는다 — 플레이 중 LUP_Banner(삼각형 각도·색 등)를 고치면 다음 재생에 바로 반영
            if (_bannerSource != null) _bannerMat.CopyPropertiesFromMaterial(_bannerSource);
            _bannerMat.SetVector(QuadSizeId, quad);
            _bannerMat.SetVector(BoxHalfId, _bannerSize * 0.5f);
            _bannerMat.SetFloat(GrayId, 0f);
            _bannerMat.SetFloat(FlashId, 0f);
        }
        if (_rollContent != null && _rollContent.parent is RectTransform viewport) viewport.sizeDelta = _bannerSize;
    }

    // 줄 i는 y = -i × 배너 높이. 앞줄들은 후보 문구(연속 중복 없이 무작위), 마지막 줄은 중간 문구.
    private void FillRows()
    {
        string last = null;
        for (int i = 0; i < _rows.Count; i++)
        {
            var row = _rows[i];
            row.rectTransform.anchoredPosition = new Vector2(0f, -i * _bannerSize.y);
            row.rectTransform.sizeDelta = new Vector2(_bannerSize.x - 40f, _bannerSize.y);
            row.rectTransform.localScale = Vector3.one;
            row.alpha = 1f;
            if (i == _rows.Count - 1) { row.text = _landingText; continue; }
            string pick = PickCandidate(last);
            row.text = pick;
            last = pick;
        }
    }

    private string PickCandidate(string avoid)
    {
        if (_rollCandidates == null || _rollCandidates.Length == 0) return string.Empty;
        for (int tries = 0; tries < 4; tries++)
        {
            string pick = _rollCandidates[UnityEngine.Random.Range(0, _rollCandidates.Length)];
            if (pick != avoid) return pick;
        }
        return _rollCandidates[0];
    }

    // blur01: 1 = 가장 빠름. 줄을 세로로 늘이고 살짝 흐리게 — 레퍼런스의 세로 잔상 느낌.
    private void ApplyRollBlur(float blur01)
    {
        var scale = new Vector3(1f, 1f + _rollBlur * blur01, 1f);
        float alpha = 1f - 0.45f * blur01;
        foreach (var row in _rows)
        {
            row.rectTransform.localScale = scale;
            row.alpha = alpha;
        }
    }

    private void HideImmediate()
    {
        if (_group != null) _group.alpha = 0f;
        if (_bannerRoot != null) _bannerRoot.localScale = Vector3.one;
        if (_result != null) _result.alpha = 0f;
        if (_tag != null) { _tag.alpha = 0f; _tag.rectTransform.localScale = Vector3.zero; }
        if (_flare != null) _flare.gameObject.SetActive(false);
        if (_ring != null) _ring.gameObject.SetActive(false);
        if (_sparks != null) _sparks.Stop(clear: true);
        if (_debris != null) _debris.Stop(clear: true);
        if (_rollContent != null) _rollContent.gameObject.SetActive(true);
        if (_bannerMat != null) { _bannerMat.SetFloat(GrayId, 0f); _bannerMat.SetFloat(FlashId, 0f); }
    }

    /// <summary>결과 두 줄: 패널티 이름(누적 2회 이상이면 ×N) / 효과 설명.</summary>
    public static string ResultText(RunPenaltyData penalty, int stackCount)
    {
        if (penalty == null) return string.Empty;
        string title = stackCount >= 2 ? $"{penalty.DisplayName} ×{stackCount}" : penalty.DisplayName;
        return $"{title}\n<size=78%>{Describe(penalty)}</size>";
    }

    /// <summary>대상·단위·증감값으로 효과 설명을 만든다. 예: 유닛 공격력 10% 감소.</summary>
    public static string Describe(RunPenaltyData penalty)
    {
        if (penalty.Target == RunPenaltyTarget.ChoiceReduction) return $"레벨업·토템 보상 후보 {System.Math.Abs(penalty.Delta):0}개 감소";
        if (penalty.Target == RunPenaltyTarget.PermanentCellSeal) return $"무작위 {penalty.Delta:0}칸 영구 봉인 · 점유물 작동 중단";
        string target = penalty.Target switch
        {
            RunPenaltyTarget.UnitAttack => "유닛 공격력",
            RunPenaltyTarget.UnitAttackFrequency => "유닛 공격속도",
            RunPenaltyTarget.ExperienceGain => "EXP 획득",
            RunPenaltyTarget.BossHp => "보스 HP",
            RunPenaltyTarget.BossDefense => "보스 방어력",
            _ => penalty.DisplayName,
        };
        if (!penalty.IsConfigured || penalty.Unit == RunPenaltyUnit.Unspecified || Math.Abs(penalty.Delta) < 1e-9)
            return $"{target} 변화 (수치 미정)";
        string amount = Math.Abs(penalty.Delta).ToString("0.#");
        string sign = penalty.Delta < 0 ? "감소" : "증가";
        return penalty.Unit == RunPenaltyUnit.Percent ? $"{target} {amount}% {sign}" : $"{target} {amount} {sign}";
    }

    private static Color WithAlpha(Color c, float a) { c.a = a; return c; }
}
