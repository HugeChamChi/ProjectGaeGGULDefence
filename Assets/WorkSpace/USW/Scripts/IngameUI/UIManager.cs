using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using DG.Tweening;

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

    [Header("Wave")]
    [SerializeField] private TMP_Text waveText;

    [Header("Slider Tween")]
    [SerializeField] private float sliderTweenDuration = 0.4f;

    private int _displayedHp;
    private int _displayedHpMax;

    [Header("Panels")]
    [SerializeField] private GameObject startPanel;

    [Header("Result")]
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private TMP_Text   resultText;
    [SerializeField] private Button     retryButton;

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
        Manager.Currency.OnCurrencyChanged += c => currencyText.text = $"식량: {(int)c}";

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

        resultPanel.SetActive(false);
    }

    public void UpdateWaveText(int current, int total) =>
        waveText.text = $"WAVE {current} / {total}";

    public void UpdateBossHp(int current, int max)
    {
        if (max <= 0) return;

        _displayedHpMax = max;

        // 슬라이더 부드럽게
        if (bossHpSlider != null)
        {
            bossHpSlider.DOKill();
            bossHpSlider.DOValue((float)current / max, sliderTweenDuration)
                        .SetEase(Ease.OutCubic);
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

    public void ShowResult(bool isWin)
    {
        resultPanel.SetActive(true);
        resultText.text = isWin ? "승리!" : "게임 오버";
        Time.timeScale  = 0f;
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
}
