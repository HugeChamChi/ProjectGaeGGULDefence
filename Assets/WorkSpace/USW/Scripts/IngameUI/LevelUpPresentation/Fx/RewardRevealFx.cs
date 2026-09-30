using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 보상 공개 연출 이펙트 (레퍼런스: design/연출예시1.mp4 — 상자 연출을 뺀 이펙트만).
/// 1단계: 화면 섬광 → 코어 글로우 → 충격파 구체(+첫 프레임 섬광 채움) → 방사형 광선 + 보케 + 불씨.
/// 광선·중심 글로우·불씨는 공개 후에도 은은하게 남고, Stop()으로 걷어낸다.
/// 요소마다 <see cref="UiFxPulse"/>(시작/상승/유지/하강/잔류 알파 + 크기 변화)로 타이밍을 인스펙터에서 조절한다.
/// 시간 0 = 공개 순간 (레퍼런스에서 상자가 착지한 프레임). 모든 트윈은 unscaled (레벨업 일시정지 중 재생).
/// </summary>
public class RewardRevealFx : MonoBehaviour, IFxLabPlayable
{
    [Serializable]
    private class Element
    {
        public Graphic Graphic;
        public UiFxPulse Pulse = new UiFxPulse();
        [NonSerialized] public Vector2 BasePosition;
    }

    [Header("Elements")]
    [SerializeField] private Element _flash = new Element();
    [SerializeField] private Element _coreGlow = new Element();
    [SerializeField] private Element _shockwave = new Element();
    [Tooltip("충격파 첫 프레임 밝은 채움 — 충격파의 자식이라 같이 커진다 (크기는 1 고정)")]
    [SerializeField] private Element _shockwaveFlash = new Element();
    [SerializeField] private Element _rays = new Element();
    [SerializeField] private Element _centerGlow = new Element();
    [SerializeField] private Element[] _bokeh = Array.Empty<Element>();

    [Header("Embers")]
    [SerializeField] private UiFxParticleEmitter _embers;
    [SerializeField] private float _embersStart = 0.17f;

    [Header("Stop")]
    [SerializeField] private float _stopFadeDuration = 0.25f;

    [Header("Time")]
    [Tooltip("true = timeScale 무시 (레벨업 일시정지 중 재생). 실험실 캡처만 false로 바꾼다")]
    [SerializeField] private bool _useUnscaledTime = true;

    [Header("Debug")]
    [SerializeField] private bool _playOnEnable;

    private Sequence _sequence;
    private bool _positionsCached;

    /// <summary>연출이 재생 중이거나 잔류 요소가 남아 있으면 true.</summary>
    public bool IsPlaying { get; private set; }

    /// <summary>timeScale 무시 여부 (불씨 파티클에도 적용). 다음 Play부터 반영.</summary>
    public bool UseUnscaledTime
    {
        get => _useUnscaledTime;
        set
        {
            _useUnscaledTime = value;
            if (_embers != null) _embers.UseUnscaledTime = value;
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
        IsPlaying = false;
    }

    /// <summary>처음부터 재생한다 (재생 중이면 되감고 다시).</summary>
    [ContextMenu("Play")]
    public void Play()
    {
        CachePositions();
        _sequence?.Kill();
        KillElementTweens();
        if (_embers != null)
        {
            _embers.UseUnscaledTime = _useUnscaledTime;
            _embers.Stop(clear: true);
        }

        _sequence = DOTween.Sequence().SetUpdate(_useUnscaledTime).SetLink(gameObject);
        foreach (var e in AllElements()) AddPulse(_sequence, e);
        if (_embers != null) _sequence.InsertCallback(_embersStart, _embers.Play);
        IsPlaying = true;
    }

    /// <summary>남아 있는 광선/글로우/불씨를 걷어낸다.</summary>
    [ContextMenu("Stop")]
    public void Stop()
    {
        _sequence?.Kill();
        _sequence = null;
        _embers?.Stop();
        var fade = DOTween.Sequence().SetUpdate(_useUnscaledTime).SetLink(gameObject);
        foreach (var e in AllElements())
        {
            if (e.Graphic == null || !e.Graphic.gameObject.activeSelf) continue;
            var g = e.Graphic;
            fade.Join(g.DOFade(0f, _stopFadeDuration).SetEase(Ease.OutQuad));
        }
        fade.OnComplete(() =>
        {
            foreach (var e in AllElements()) if (e.Graphic != null) e.Graphic.gameObject.SetActive(false);
            IsPlaying = false;
        });
    }

    private static void AddPulse(Sequence seq, Element e) => e.Pulse.AppendTo(seq, e.Graphic, e.BasePosition);

    private void CachePositions()
    {
        if (_positionsCached) return;
        foreach (var e in AllElements())
            if (e.Graphic != null) e.BasePosition = e.Graphic.rectTransform.anchoredPosition;
        _positionsCached = true;
    }

    private void KillElementTweens()
    {
        foreach (var e in AllElements())
        {
            if (e.Graphic == null) continue;
            e.Graphic.DOKill();
            e.Graphic.rectTransform.DOKill();
        }
    }

    private IEnumerable<Element> AllElements()
    {
        yield return _flash;
        yield return _coreGlow;
        yield return _shockwave;
        yield return _shockwaveFlash;
        yield return _rays;
        yield return _centerGlow;
        foreach (var b in _bokeh) yield return b;
    }
}
