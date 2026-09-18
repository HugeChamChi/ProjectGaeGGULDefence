using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Animator/Controller 없이 스프라이트 교체만으로 유닛의 상태 연출(Idle 호흡, 스킬 액션 플래시 등)을
/// 표현하는 경량 모핑 컴포넌트. 상태는 이름으로 구분되며 프레임 1장(정지) 또는 여러 장(순환)을 지원한다.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class UnitMorph : MonoBehaviour
{
    [Serializable]
    public class MorphState
    {
        public string name = "Idle";
        public Sprite[] frames;
        [Min(0.02f)] public float frameInterval = 0.35f;
        [Tooltip("켜두면 frames를 끝까지 순환 재생. holdSeconds가 지정되면 그 시간 뒤 자동으로 Idle로 복귀.")]
        public bool loop = true;
    }

    [SerializeField] private SpriteRenderer _renderer;
    [SerializeField] private List<MorphState> _states = new();
    [SerializeField] private string _idleStateName = "Idle";
    [Tooltip("여러 유닛이 같은 타이밍에 동시에 호흡하지 않도록 Idle 시작 프레임을 랜덤화")]
    [SerializeField] private bool _randomizeIdleStart = true;

    private CancellationTokenSource _playCts;
    private string _currentStateName;

    private void Awake()
    {
        if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
    }

    private void OnEnable() => PlayIdle();

    private void OnDisable()
    {
        _playCts?.Cancel();
        _playCts?.Dispose();
        _playCts = null;
    }

    /// <summary>현재 재생 중인 상태 이름.</summary>
    public string CurrentState => _currentStateName;

    public void PlayIdle() => Play(_idleStateName);

    /// <summary>
    /// 지정한 상태를 재생한다. <paramref name="holdSeconds"/>를 주면 루프 상태여도 그 시간 뒤 Idle로 자동 복귀한다
    /// (예: 스킬 발동 시 "Action" 상태를 0.15초만 보여주고 되돌리는 용도).
    /// </summary>
    public void Play(string stateName, float? holdSeconds = null)
    {
        var state = _states.Find(s => s.name == stateName);
        if (state == null || state.frames == null || state.frames.Length == 0 || _renderer == null) return;

        _playCts?.Cancel();
        _playCts?.Dispose();
        _playCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        _currentStateName = stateName;

        int startFrame = (_randomizeIdleStart && stateName == _idleStateName)
            ? UnityEngine.Random.Range(0, state.frames.Length)
            : 0;

        PlayStateAsync(state, startFrame, holdSeconds, _playCts.Token).Forget();
    }

    private async UniTaskVoid PlayStateAsync(MorphState state, int startFrame, float? holdSeconds, CancellationToken token)
    {
        int   frame   = startFrame;
        float elapsed = 0f;

        while (!token.IsCancellationRequested)
        {
            _renderer.sprite = state.frames[frame % state.frames.Length];

            if (await UniTask.Delay(Mathf.RoundToInt(state.frameInterval * 1000f), cancellationToken: token).SuppressCancellationThrow())
                return;

            frame++;
            elapsed += state.frameInterval;

            if (!state.loop && frame >= state.frames.Length)
                break;

            if (holdSeconds.HasValue && elapsed >= holdSeconds.Value)
                break;
        }

        if (token.IsCancellationRequested) return;

        // 원샷(loop=false) 또는 holdSeconds로 끊긴 재생은 끝나면 Idle로 복귀한다.
        if (_currentStateName != _idleStateName && (!state.loop || holdSeconds.HasValue))
            PlayIdle();
    }
}
