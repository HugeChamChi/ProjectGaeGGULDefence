using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 결과 화면 실험실(FxLab_Result) 드라이버. 담백 톤과 빌드 표시를 고정한 가짜 기록으로 재생한다.
/// 결과 화면의 다시하기/로비로 버튼도 실험실에서는 다시 재생한다.
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

    [Header("마지막 보스 HP 비교 (실험실 예시)")]
    [SerializeField, Range(0f, 1f)] private float _lastBossDamageRatio = 0.75f;
    [SerializeField] private ResultBossProgressStyle _bossProgressStyle;
    [SerializeField] private Button[] _progressButtons;
    [SerializeField] private Button[] _damageButtons;

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
    }

    private ResultBuildChoice[] CreateBuildChoices()
    {
        if (_buildChoices == null) return Array.Empty<ResultBuildChoice>();
        var choices = new ResultBuildChoice[_buildChoices.Length];
        for (int i = 0; i < choices.Length; i++)
        {
            var choice = _buildChoices[i];
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
