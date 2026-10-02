using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 결과 화면 실험실(FxLab_Result) 드라이버. 담백 톤과 빌드 표시를 고정한 가짜 기록으로 재생한다.
/// 결과 화면의 다시하기/로비로 버튼도 실험실에서는 다시 재생한다.
/// 빌드(이번 판 고른 선택지) 예시 개수 버튼(3 / 12 / 20개)은 실행 시 HP 예시 버튼을 복제해 한 줄 더 만든다 —
/// 12개 초과면 6열×2줄 영역에서 세로 스크롤이 생기는지 확인용 (사용자 요청 2026-10-02). 씬·프리팹은 건드리지 않는다.
/// </summary>
public class ResultScreenLab : MonoBehaviour, IFxLabPlayable
{
    [SerializeField] private ResultScreenView _view;

    [Header("비교 설정")]
    [SerializeField] private bool _newRecord = true;

    [Header("가짜 기록")]
    [SerializeField] private int _round = 23;
    [Tooltip("신기록일 때 이전 최고 = 라운드 - 값, 아닐 때 = 라운드 + 값")]
    [SerializeField] private int _recordGap = 2;
    [SerializeField] private int _bossKills = 22;
    [SerializeField] private float _survivalSeconds = 754f;
    [SerializeField] private LevelUpData[] _buildChoices;
    [Tooltip("빌드 예시 개수. 선택지 목록보다 많으면 처음부터 반복해 채운다 (12 초과 = 세로 스크롤 예시)")]
    [SerializeField] private int _buildCount = 20;
    [SerializeField] private int[] _buildCountSamples = { 3, 12, 20 };

    [Header("마지막 보스 HP 비교 (실험실 예시)")]
    [SerializeField, Range(0f, 1f)] private float _lastBossDamageRatio = 0.75f;
    [SerializeField] private ResultBossProgressStyle _bossProgressStyle;
    [SerializeField] private Button[] _progressButtons;
    [SerializeField] private Button[] _damageButtons;

    private Button[] _buildCountButtons = Array.Empty<Button>();
    private const float BuildRowGap = 60f;       // HP 예시 줄 바로 아래 한 줄

    [Header("라운드 비례 보상 (임시 규칙: 라운드 × 값)")]
    [SerializeField] private Sprite _goldIcon;
    [SerializeField] private int _goldPerRound = 50;
    [SerializeField] private Sprite _expIcon;
    [SerializeField] private int _expPerRound = 12;

    /// <inheritdoc />
    public bool UseUnscaledTime
    {
        get => _view.UseUnscaledTime;
        set => _view.UseUnscaledTime = value;
    }

    private void Start()
    {
        CreateBuildCountButtons();
        _view.RetryRequested += Play;
        _view.HomeRequested += Play;
        Play();
    }

    private void OnDestroy()
    {
        if (_view == null) return;
        _view.RetryRequested -= Play;
        _view.HomeRequested -= Play;
    }

    /// <inheritdoc />
    public void Play()
    {
        RefreshControls();
        var data = new ResultScreenData
        {
            Round = _round,
            BestRound = _newRecord ? _round - _recordGap : _round + _recordGap,
            BossKills = _bossKills,
            SurvivalSeconds = _survivalSeconds,
            LastBossDamageRatio = _lastBossDamageRatio,
            BossProgressStyle = _bossProgressStyle,
            BuildChoices = CreateBuildChoices(),
            Rewards = new[]
            {
                new ResultReward(_goldIcon, _round * _goldPerRound),
                new ResultReward(_expIcon, _round * _expPerRound),
            },
        };
        _view.Show(data, ResultScreenTone.Plain, true);
    }

    /// <summary>담백 톤을 유지하며 HP 연출 후보를 선택해 재생한다.</summary>
    public void SetBossProgressStyle(int style)
    {
        _bossProgressStyle = (ResultBossProgressStyle)Mathf.Clamp(style, 0, 2);
        Play();
    }

    /// <summary>빌드 예시 개수를 바꿔 다시 재생한다 (12 초과면 세로 스크롤).</summary>
    public void SetBuildCount(int count)
    {
        _buildCount = Mathf.Max(0, count);
        Play();
    }

    /// <summary>같은 라운드에서 HP 감소율만 바꿔 연출을 비교한다.</summary>
    public void SetBossDamagePercent(int percent)
    {
        _lastBossDamageRatio = Mathf.Clamp(percent, 0, 100) / 100f;
        Play();
    }

    private void RefreshControls()
    {
        var selected = new Color(0.14f, 0.38f, 0.31f, 1f);
        var normal = new Color(0.08f, 0.09f, 0.10f, 1f);
        if (_progressButtons != null)
            for (int i = 0; i < _progressButtons.Length; i++)
                if (_progressButtons[i] != null) _progressButtons[i].image.color = i == (int)_bossProgressStyle ? selected : normal;
        int[] sampleDamage = { 15, 75, 95 };
        if (_damageButtons != null)
            for (int i = 0; i < _damageButtons.Length && i < sampleDamage.Length; i++)
                if (_damageButtons[i] != null) _damageButtons[i].image.color = Mathf.RoundToInt(_lastBossDamageRatio * 100f) == sampleDamage[i] ? selected : normal;
        for (int i = 0; i < _buildCountButtons.Length && i < _buildCountSamples.Length; i++)
            if (_buildCountButtons[i] != null) _buildCountButtons[i].image.color = _buildCount == _buildCountSamples[i] ? selected : normal;
    }

    // HP 예시 버튼 줄을 복제해 그 아래에 빌드 개수 버튼 줄을 만든다 (실험실 씬을 다시 빌드하지 않아도 되게).
    private void CreateBuildCountButtons()
    {
        if (_damageButtons == null || _damageButtons.Length == 0 || _damageButtons[0] == null) return;
        var sampleRow = (RectTransform)_damageButtons[0].transform.parent;
        var row = Instantiate(sampleRow.gameObject, sampleRow.parent).GetComponent<RectTransform>();
        row.name = "BuildCountSamples";
        row.anchoredPosition = sampleRow.anchoredPosition + new Vector2(0f, -BuildRowGap);
        // 복제된 HP 버튼은 지우고 템플릿 하나로 새 버튼을 만든다
        for (int i = row.childCount - 1; i >= 0; i--) Destroy(row.GetChild(i).gameObject);

        _buildCountButtons = new Button[_buildCountSamples.Length];
        for (int i = 0; i < _buildCountSamples.Length; i++)
        {
            int count = _buildCountSamples[i];
            var source = _damageButtons[Mathf.Min(i, _damageButtons.Length - 1)];
            var button = Instantiate(source.gameObject, row).GetComponent<Button>();
            button.name = "BuildCount" + count;
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() => SetBuildCount(count));
            var label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.text = count > 12 ? $"빌드 {count}개 (스크롤)" : $"빌드 {count}개";
            ((RectTransform)button.transform).anchoredPosition = ((RectTransform)source.transform).anchoredPosition;
            _buildCountButtons[i] = button;
        }
    }

    // 고른 순서대로. 예시 개수가 목록보다 많으면 처음부터 반복해 채운다.
    private ResultBuildChoice[] CreateBuildChoices()
    {
        if (_buildChoices == null || _buildChoices.Length == 0 || _buildCount <= 0) return Array.Empty<ResultBuildChoice>();
        var choices = new ResultBuildChoice[_buildCount];
        for (int i = 0; i < choices.Length; i++)
        {
            var choice = _buildChoices[i % _buildChoices.Length];
            choices[i] = new ResultBuildChoice
            {
                Icon = choice != null ? choice.icon : null,
                Name = choice != null ? choice.chooseName : "선택지",
                Description = choice != null ? choice.description : string.Empty,
            };
        }
        return choices;
    }

}
