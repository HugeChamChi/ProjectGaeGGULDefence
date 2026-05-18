using AssetKits.ParticleImage;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// ════════════════════════════════════════════════════════
// UIManager — InGameSingleton 교체 + Manager 접근 통일
// ════════════════════════════════════════════════════════
public class UIManager : InGameSingleton<UIManager>
{
    [Header("Buttons")]
    [SerializeField] private Button summonButton;
    [SerializeField] private Button startButton;

    [Header("Display")]
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text currencyText;
    [SerializeField] private TMP_Text spawnCostText;
    [SerializeField] private TMP_Text bossHpText;
    [SerializeField] private Slider   bossHpSlider;

    [Header("Population")]
    [SerializeField] private TMP_Text populationText;

    [Header("Slider Tween")]
    [SerializeField] private float sliderTweenDuration = 0.4f;
    [SerializeField] private float bossBarShakePower = 15f;
    [SerializeField] private int   bossBarShakeVibrato = 25;

    [Header("Currency Tween")]
    [SerializeField] private float currencyTweenDuration = 0.18f;
    [SerializeField] private float currencyPunchScale = 0.12f;
    [SerializeField] private Color currencyFlashColor = new Color(1f, 0.86f, 0.35f);

    [Header("Effect")]
    [SerializeField] private ParticleImage hitEffectPrefab;   // 보스 피격 시 생성될 ParticleImage 프리팹
    [SerializeField] private Transform particleTarget;

    private int _displayedHp;
    private int _displayedHpMax;
    private int _displayedCurrency;
    private RectTransform _currencyTextRect;
    private Vector3 _currencyTextBaseScale = Vector3.one;
    private Color _currencyTextBaseColor = Color.white;

    [Header("Panels")]
    [SerializeField] private GameObject startPanel;

    [Header("Result")]
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private Button     retryButton;
    [SerializeField] private Button     homeButton;

    protected override void Awake()
    {
        base.Awake();
    }

    private void Start()
    {
        Time.timeScale = 1f;

        if (summonButton != null)
            summonButton.onClick.AddListener(Manager.Spawner.OnSpawnButtonPressed);

        startButton.onClick.AddListener(Manager.Game.OnStartButtonPressed);

        Manager.Timer.OnTimerTick += t => timerText.text = $"{Mathf.CeilToInt(t)}";
        if (currencyText != null)
        {
            _currencyTextRect = currencyText.rectTransform;
            _currencyTextBaseScale = _currencyTextRect.localScale;
            _currencyTextBaseColor = currencyText.color;
            _displayedCurrency = Mathf.FloorToInt(Manager.Currency.Currency);
            currencyText.text = $"식량: {_displayedCurrency}";
            Manager.Currency.OnCurrencyChanged += UpdateCurrencyDisplay;
        }

        // 소환 비용 텍스트 초기값 + 변경 구독
        if (spawnCostText != null)
        {
            spawnCostText.text = $"소환 {(int)Manager.Spawner.CurrentCost}";
            Manager.Spawner.OnCostChanged += cost => spawnCostText.text = $"소환 {(int)cost}";
        }

        if (bossHpSlider != null)
        {
            bossHpSlider.interactable = false;
            bossHpSlider.minValue     = 0f;
            bossHpSlider.maxValue     = 1f;
            bossHpSlider.value        = 1f;
        }

        if (retryButton != null)
            retryButton.onClick.AddListener(OnRetryButtonPressed);

        if (homeButton != null)
            homeButton.onClick.AddListener(OnHomeButtonPressed);

        if (populationText != null && Manager.Population != null)
        {
            populationText.text = $"0 / {Manager.Population.Max}";
            Manager.Population.OnPopulationChanged += (cur, max) =>
                populationText.text = $"{cur} / {max}";
        }

        resultPanel.SetActive(false);
    }

    public void UpdateBossHp(int current, int max)
    {
        if (max <= 0) return;

        _displayedHpMax = max;

        // 슬라이더 부드럽게
        if (bossHpSlider != null)
        {
            // 데미지를 입었을 때만 이펙트 재생 (현재 HP가 이전 HP보다 작을 때)
            if (current < _displayedHp && hitEffectPrefab != null && particleTarget != null)
            {
                var particle = RM.Instantiate(hitEffectPrefab, particleTarget.position, hitEffectPrefab.transform.rotation, particleTarget, true);
                if (particle != null)
                {
                    particle.Play();
                    RM.Destroy(particle, particle.duration + 0.5f);
                }
            }

            bossHpSlider.DOKill();
            bossHpSlider.DOValue((float)current / max, sliderTweenDuration)
                        .SetEase(Ease.OutCubic);
            
            BossHpShakeAnimation();
        }

        // HP 텍스트 숫자 부드럽게
        if (bossHpText != null)
        {
            int from = _displayedHp;
            DOTween.To(() => from, x =>
            {
                from            = x;
                _displayedHp    = x;
                bossHpText.text = $"{x} / {_displayedHpMax}";
            }, current, sliderTweenDuration).SetEase(Ease.OutCubic);
        }

        _displayedHp = current;
    }

    private void UpdateCurrencyDisplay(float current)
    {
        if (currencyText == null) return;

        int target = Mathf.FloorToInt(current);
        if (target == _displayedCurrency)
        {
            currencyText.text = $"식량: {target}";
            return;
        }

        DOTween.Kill(currencyText);
        DOTween.To(() => _displayedCurrency, value =>
        {
            _displayedCurrency = value;
            currencyText.text = $"식량: {value}";
        }, target, currencyTweenDuration).SetEase(Ease.OutCubic).SetTarget(currencyText);

        if (_currencyTextRect == null) return;

        _currencyTextRect.DOKill(true);
        _currencyTextRect.localScale = _currencyTextBaseScale;
        currencyText.color = _currencyTextBaseColor;

        _currencyTextRect
            .DOPunchScale(Vector3.one * currencyPunchScale, currencyTweenDuration, 6, 0.6f)
            .SetEase(Ease.OutCubic);

        currencyText
            .DOColor(currencyFlashColor, currencyTweenDuration * 0.45f)
            .SetLoops(2, LoopType.Yoyo)
            .OnComplete(() => currencyText.color = _currencyTextBaseColor);
    }

    private void BossHpShakeAnimation()
    {
        if (bossHpSlider == null) return;

        RectTransform target = bossHpSlider.transform as RectTransform;
        if (target == null) return;
        
        // 이전 위치 트윈 제거 및 위치 초기화
        target.DOKill(true);

        // UI 요소이므로 DOShakeAnchorPos 사용
        target.DOShakeAnchorPos(0.2f, bossBarShakePower, bossBarShakeVibrato, 90, false, true);
    }

    public void ShowResult(bool isWin)
    {
        resultPanel.SetActive(true);
        Time.timeScale = 0f;
    }

    public void HideStartButton()
    {
        if (startButton != null) startButton.gameObject.SetActive(false);
        if (startPanel  != null) startPanel.SetActive(false);
    }

    private void OnRetryButtonPressed()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void OnHomeButtonPressed()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("LobbyScene");
    }
}
