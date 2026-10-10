using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer.Unity;

/// <summary>씬 수명의 알팡 액티브. UnitBase/그리드/등급 없이 충전과 발동을 관리한다.</summary>
public sealed class AlphanActiveSkill : IChiefActiveSkill, IInitializable, ITickable, IDisposable
{
    private readonly ChieftainSelection _selector;
    private readonly DroneManager _drones;
    private readonly IChiefSelectionReader _effects;
    private readonly GameManager _game;
    private readonly AudioManager _audio;
    private AlphanSkillData _data;
    private Sprite _fallbackIcon;
    private float _elapsed;
    private bool _casting;
    private bool _disposed;
    private bool _initialized;
    private CancellationTokenSource _castCts;
    private HSD.UI.Effect.UI_ChiefSkillEffect _cutscene;

    /// <summary>씬 서비스 주입. 드론 매니저가 없는 씬에서도 등록 가능하나 사용은 비활성이다.</summary>
    public AlphanActiveSkill(ChieftainSelection selector, IEnumerable<DroneManager> drones,
        IChiefSelectionReader effects, GameManager game, AudioManager audio,
        IEnumerable<HSD.UI.Effect.UI_ChiefSkillEffect> cutscenes = null)
    {
        _selector=selector; _effects=effects; _game=game; _audio=audio;
        foreach(var manager in drones) { _drones=manager; break; }
        if (cutscenes != null)
            foreach (var cutscene in cutscenes) { _cutscene = cutscene; break; }
    }
    /// <inheritdoc />
    public Sprite Icon => _data != null && _data.Icon != null ? _data.Icon : _fallbackIcon;
    /// <inheritdoc />
    public bool IsAvailable => !_disposed && _data != null && _drones != null;
    /// <summary>알팡 선택지의 쿨타임 감소만 적용한다. 유닛/셀 스탯을 사용하지 않는다.</summary>
    public float CooldownSeconds => _data == null ? 0 : Mathf.Max(0.05f, _data.CooldownSeconds
        * (1f-(_effects?.ChiefCooldownReduction ?? 0f)));
    private bool IsPlaying => (_game == null || _game.CurrentState == GameManager.GameState.Playing) && Time.timeScale > 0;
    /// <inheritdoc />
    public float CooldownProgress => IsAvailable ? Mathf.Clamp01(_elapsed / CooldownSeconds) : 0;
    /// <inheritdoc />
    public float CooldownRemaining => IsAvailable ? Mathf.Max(0, CooldownSeconds-_elapsed) : 0;
    /// <inheritdoc />
    public bool CanActivate => IsAvailable && IsPlaying && !_casting && !_drones.IsRallying
        && _drones.RallyAvailableDroneCount > 0 && _elapsed >= CooldownSeconds;

    /// <summary>초기화는 GameInitializer의 족장 선택보다 먼저 실행된다.</summary>
    public void Initialize()
    {
        if (_disposed || _initialized || _selector == null) return;
        _initialized = true;
        _selector.OnAlphanSkillSelected += Configure;
        Configure(_selector.SelectedAlphanSkill, _selector.SelectedAlphanIcon);
    }
    /// <summary>선택된 알팡 설정으로 초기화한다. 처음에는 충전이 필요하다.</summary>
    public void Configure(AlphanSkillData data, Sprite fallbackIcon)
    {
        if (_disposed) return;
        CancelCast();
        _data=data; _fallbackIcon=fallbackIcon; _elapsed=0;
        if (_selector != null) _selector.SetActiveSkill(IsAvailable ? this : null);
    }
    /// <inheritdoc />
    public void Tick() => Advance(Time.deltaTime);
    /// <summary>플레이 중에만 충전한다. 드론 0기여도 충전은 유지한다.</summary>
    public void Advance(float seconds)
    {
        if (!IsAvailable || !IsPlaying || seconds <= 0) return;
        _elapsed=Mathf.Min(CooldownSeconds,_elapsed+seconds);
    }
    /// <inheritdoc />
    public bool TryActivate()
    {
        if (!CanActivate) return false;
        _elapsed=0; _casting=true;
        _castCts=new CancellationTokenSource();
        var cts=_castCts;
        ExecuteAsync(cts).Forget(e => { if (e is not OperationCanceledException) Debug.LogException(e); });
        ChiefActiveSkillSignals.RaiseActivated(); // 보스 예고 패턴 카운터
        return true;
    }
    private async UniTask ExecuteAsync(CancellationTokenSource cts)
    {
        try
        {
            _drones.ApplyEmergencyBuffs();
            if (!string.IsNullOrEmpty(_data.SoundAddress)) _audio?.PlaySFX(_data.SoundAddress);
            var cutscene = _cutscene != null ? _cutscene.PlayEffectAsync(Icon,cts.Token) : UniTask.CompletedTask;
            var rally = _drones.ExecuteRallyAsync(_data.DamagePerDrone,cts.Token,
                new ChiefVolleySettings(_effects?.ChiefVolleyCount ?? 0, _effects?.ChiefVolleyDelay ?? 0f, _effects?.ChiefVolleyDamageRatio ?? 0f));
            await UniTask.WhenAll(cutscene,rally);
        }
        finally
        {
            if (_castCts == cts) { _castCts=null; _casting=false; }
            if (!cts.IsCancellationRequested) cts.Cancel();
            cts.Dispose();
        }
    }
    private void CancelCast()
    {
        var cts=_castCts; _castCts=null; _casting=false;
        cts?.Cancel(); // ExecuteAsync의 finally에서 Dispose.
    }
    /// <summary>씬 종료/컨테이너 해제 시 이벤트와 실행 중 연출을 정리한다.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed=true;
        if (_selector != null) _selector.OnAlphanSkillSelected-=Configure;
        CancelCast();_data=null;
        if (_selector != null && ReferenceEquals(_selector.ActiveSkill, this))
            _selector.SetActiveSkill(null);
    }
}
