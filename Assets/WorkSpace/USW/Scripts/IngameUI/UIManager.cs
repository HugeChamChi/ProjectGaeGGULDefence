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
    [SerializeField] private HealthBarSettingsSO healthBarSettings;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text currencyText;
    [SerializeField] private TMP_Text spawnCostText;
    [SerializeField] private TMP_Text bossHpText;
    [SerializeField] private TextMeshProUGUI bossHpLineText;
    [SerializeField] private Slider   currentLineSlider;
    [SerializeField] private Slider   nextLineSlider;

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

    private Image _currentLineImage;
    private Image _nextLineImage;

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

        if (startButton != null)
            startButton.onClick.AddListener(Manager.Game.OnStartButtonPressed);

        if (timerText != null)
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

        if (currentLineSlider != null)
        {
            currentLineSlider.interactable = false;
            currentLineSlider.minValue     = 0f;
            currentLineSlider.maxValue     = 1f;
            currentLineSlider.value        = 1f;
            if (currentLineSlider.fillRect != null)
                _currentLineImage = currentLineSlider.fillRect.GetComponent<Image>();
        }
        
        if (nextLineSlider != null)
        {
            nextLineSlider.interactable = false;
            nextLineSlider.minValue     = 0f;
            nextLineSlider.maxValue     = 1f;
            nextLineSlider.value        = 1f;
            if (nextLineSlider.fillRect != null)
                _nextLineImage = nextLineSlider.fillRect.GetComponent<Image>();
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

        if (resultPanel != null)
            resultPanel.SetActive(false);
    }

    public void UpdateBossHp(int current, int max)
    {
        if (max <= 0) return;

        _displayedHpMax = max;

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

        if (currentLineSlider != null)
        {
            currentLineSlider.DOKill();
            BossHpShakeAnimation();
        }

        if (bossHpText != null)
        {
            DOTween.Kill(bossHpText);
        }

        if (nextLineSlider != null)
        {
            nextLineSlider.value = 1f; // 다음 줄은 꽉 차있는 상태
        }

        int hpPerLine = healthBarSettings != null && healthBarSettings.healthPerLine > 0 ? healthBarSettings.healthPerLine : max;
        int from = _displayedHp;

        DOTween.To(() => from, x =>
        {
            from = x;
            _displayedHp = x;

            int currentLine = Mathf.CeilToInt((float)x / hpPerLine);
            if (x <= 0) currentLine = 0;

            int currentLineHp = x - (currentLine - 1) * hpPerLine;
            if (x <= 0) currentLineHp = 0;

            if (currentLineSlider != null)
            {
                currentLineSlider.value = (float)currentLineHp / hpPerLine;
            }

            if (healthBarSettings != null && healthBarSettings.lineColors != null && healthBarSettings.lineColors.Length > 0)
            {
                int colorLen = healthBarSettings.lineColors.Length;
                
                int currentColorIndex = currentLine > 0 ? (currentLine - 1) % colorLen : 0;
                int nextColorIndex = currentLine > 1 ? (currentLine - 2) % colorLen : -1;

                Color currentColor = currentLine > 0 ? healthBarSettings.lineColors[currentColorIndex] : Color.clear;
                Color nextColor = nextColorIndex >= 0 ? healthBarSettings.lineColors[nextColorIndex] : Color.clear;

                if (_currentLineImage != null)
                {
                    _currentLineImage.color = currentColor;
                }

                if (_nextLineImage != null)
                {
                    _nextLineImage.color = nextColor;
                }
            }

            if (bossHpText != null)
            {
                bossHpText.text = $"{x} / {_displayedHpMax}";
            }

            if (bossHpLineText != null)
            {
                bossHpLineText.text = $"x{currentLine}";
            }

        }, current, sliderTweenDuration)
        .SetEase(Ease.OutCubic)
        .SetTarget(bossHpText);

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
        if (currentLineSlider == null) return;

        RectTransform target = currentLineSlider.transform as RectTransform;
        if (target == null) return;
        
        // 이전 위치 트윈 제거 및 위치 초기화
        target.DOKill(true);

        // UI 요소이므로 DOShakeAnchorPos 사용
        target.DOShakeAnchorPos(0.2f, bossBarShakePower, bossBarShakeVibrato, 90, false, true);
    }

    public void ShowResult(bool isWin)
    {
        if (resultPanel != null)
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
