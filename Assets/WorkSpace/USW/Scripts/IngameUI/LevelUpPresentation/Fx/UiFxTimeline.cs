using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI 이펙트 프리팹 한 개의 재생기 — 요소(Graphic)마다 UiFxPulse 타임라인 + 선택적으로 회전/머티리얼 값 트윈,
/// 파티클(UiFxParticleEmitter)은 정해진 시각에 방출한다.
/// 레벨업 이펙트 슬롯(LevelUpRevealSequence A/B 등)은 프리팹을 생성만 하므로 OnEnable에서 자동 재생한다.
/// 모든 트윈은 기본 unscaled (레벨업 일시정지 중 재생). 머티리얼 값을 트윈하는 요소는 인스턴스 머티리얼을 만들어 쓰고 파괴 시 정리한다.
/// </summary>
public class UiFxTimeline : MonoBehaviour, IFxLabPlayable
{
    [Serializable]
    public class Track
    {
        public Graphic Graphic;
        public UiFxPulse Pulse = new UiFxPulse();
        [Tooltip("재생 동안 회전량(도). 0이면 회전 없음")] public float Rotate;
        [Tooltip("트윈할 머티리얼 float 이름 (비우면 없음) — 예: 링 두께 _Thickness")] public string FloatName;
        public float FloatFrom;
        public float FloatTo;
        public Ease FloatEase = Ease.OutQuad;
        [NonSerialized] public Vector2 BasePosition;
        [NonSerialized] public Material Instance;
    }

    [Serializable]
    public class EmitterCue
    {
        public UiFxParticleEmitter Emitter;
        [Tooltip("방출 시각(초)")] public float Start;
    }

    [SerializeField] private Track[] _tracks = Array.Empty<Track>();
    [SerializeField] private EmitterCue[] _emitters = Array.Empty<EmitterCue>();
    [SerializeField] private bool _playOnEnable = true;
    [SerializeField] private bool _useUnscaledTime = true;

    private Sequence _sequence;
    private bool _cached;

    /// <summary>timeScale 무시 여부 (파티클에도 적용). 다음 Play부터 반영.</summary>
    public bool UseUnscaledTime { get => _useUnscaledTime; set => _useUnscaledTime = value; }

    /// <summary>모든 요소가 끝나는 시각(초) — 잔류 요소는 하강 끝 기준.</summary>
    public float Duration
    {
        get
        {
            float end = 0f;
            foreach (var t in _tracks) if (t.Graphic != null) end = Mathf.Max(end, t.Pulse.End);
            foreach (var e in _emitters) if (e.Emitter != null) end = Mathf.Max(end, e.Start);
            return end;
        }
    }

    private void OnEnable()
    {
        if (_playOnEnable && Application.isPlaying) Play();
    }

    private void OnDisable()
    {
        _sequence?.Kill();
        _sequence = null;
    }

    private void OnDestroy()
    {
        foreach (var t in _tracks)
            if (t.Instance != null) Destroy(t.Instance);
    }

    /// <summary>처음부터 재생한다.</summary>
    [ContextMenu("Play")]
    public void Play()
    {
        Cache();
        _sequence?.Kill();
        _sequence = DOTween.Sequence().SetUpdate(_useUnscaledTime).SetLink(gameObject);

        foreach (var t in _tracks)
        {
            if (t.Graphic == null) continue;
            var rt = t.Graphic.rectTransform;
            t.Graphic.DOKill();
            rt.DOKill();
            rt.localRotation = Quaternion.identity;
            t.Pulse.AppendTo(_sequence, t.Graphic, t.BasePosition);
            float span = Mathf.Max(t.Pulse.End - t.Pulse.Start, 0.0001f);
            if (!Mathf.Approximately(t.Rotate, 0f))
                _sequence.Insert(t.Pulse.Start, rt.DOLocalRotate(new Vector3(0f, 0f, t.Rotate), span, RotateMode.FastBeyond360).SetEase(Ease.OutQuad));
            if (t.Instance != null)
            {
                t.Instance.DOKill();
                t.Instance.SetFloat(t.FloatName, t.FloatFrom);
                _sequence.Insert(t.Pulse.Start, t.Instance.DOFloat(t.FloatTo, t.FloatName, span).SetEase(t.FloatEase));
            }
        }

        foreach (var e in _emitters)
        {
            if (e.Emitter == null) continue;
            var emitter = e.Emitter;
            emitter.UseUnscaledTime = _useUnscaledTime;
            emitter.Stop(clear: true);
            _sequence.InsertCallback(e.Start, emitter.Play);
        }
    }

    private void Cache()
    {
        if (_cached) return;
        foreach (var t in _tracks)
        {
            if (t.Graphic == null) continue;
            t.BasePosition = t.Graphic.rectTransform.anchoredPosition;
            if (!string.IsNullOrEmpty(t.FloatName) && t.Graphic.material != null && t.Graphic.material.HasProperty(t.FloatName))
            {
                t.Instance = new Material(t.Graphic.material);
                t.Graphic.material = t.Instance;
            }
        }
        _cached = true;
    }
}
