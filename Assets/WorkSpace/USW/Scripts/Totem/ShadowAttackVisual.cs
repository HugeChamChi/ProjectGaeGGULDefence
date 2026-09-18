using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>게임플레이 컴포넌트 없이 스프라이트 계층만 복제하는 재사용 가능한 공격 분신.</summary>
public sealed class ShadowAttackVisual : MonoBehaviour
{
    private readonly List<SpriteRenderer> _renderers = new();
    private readonly List<Transform> _sources = new();
    private readonly List<Transform> _copies = new();
    private Animator _sourceAnimator;
    private AnimationClip _clip;
    private BasicAttackReplay _attack;
    private TotemShadowAttackSettings _settings;
    private float _elapsed;
    private float _attackSeconds;
    private Vector3 _position;
    private Quaternion _rotation;
    private Vector3 _scale;

    /// <summary>분신의 외형 원본. 판매로 파괴되면 null이 된다.</summary>
    public UnitBase Source { get; private set; }
    /// <summary>실제 외형 원본. 드론 공격은 드론 Transform을 사용한다.</summary>
    public Transform VisualSource { get; private set; }
    /// <summary>지연/재생 중인지 여부.</summary>
    public bool IsPlaying { get; private set; }

    /// <summary>원본과 같은 이름/Transform/SpriteRenderer 계층만 생성한다.</summary>
    public static ShadowAttackVisual Create(UnitBase source, Transform owner, Transform visualSource = null)
    {
        var visualRoot = visualSource != null ? visualSource : source.transform;
        var root = new GameObject(source.name + "_Shadow");
        root.transform.SetParent(owner, false);
        var visual = root.AddComponent<ShadowAttackVisual>();
        visual.Source = source;
        visual.VisualSource = visualRoot;
        visual._sourceAnimator = visualRoot == source.transform
            ? (source.animator != null ? source.animator.GetComponent<Animator>() : null)
            : visualRoot.GetComponentInChildren<Animator>();
        visual.CopyHierarchy(visualRoot, root.transform);
        visual.SetVisible(false);
        return visual;
    }

    private void CopyHierarchy(Transform source, Transform copy)
    {
        _sources.Add(source);
        _copies.Add(copy);
        var renderer = source.GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            var clone = copy.gameObject.AddComponent<SpriteRenderer>();
            clone.sprite = renderer.sprite;
            clone.sharedMaterial = renderer.sharedMaterial;
            clone.sortingLayerID = renderer.sortingLayerID;
            clone.sortingOrder = renderer.sortingOrder + 1;
            clone.flipX = renderer.flipX;
            clone.flipY = renderer.flipY;
            _renderers.Add(clone);
        }
        foreach (Transform child in source)
        {
            // UI와 물리/게임플레이 컴포넌트는 복제하지 않는다.
            if (child is RectTransform) continue;
            var childCopy = new GameObject(child.name).transform;
            childCopy.SetParent(copy, false);
            CopyHierarchy(child, childCopy);
        }
    }

    /// <summary>원본 공격 시작에 호출한다. 지연 중 원본 Animator가 선택한 실제 Attack 클립을 포착한다.</summary>
    public void Play(BasicAttackReplay attack, TotemShadowAttackSettings settings)
    {
        _attack = attack;
        _settings = settings;
        _elapsed = 0f;
        _clip = null;
        _attackSeconds = Mathf.Max(0.01f, Source.GetCurrentAttackInterval());
        for (int i = 0; i < _copies.Count; i++)
        {
            if (_sources[i] == null) continue;
            _copies[i].localPosition = _sources[i].localPosition;
            _copies[i].localRotation = _sources[i].localRotation;
            _copies[i].localScale = _sources[i].localScale;
            var original = _sources[i].GetComponent<SpriteRenderer>();
            var copy = _copies[i].GetComponent<SpriteRenderer>();
            if (original != null && copy != null)
            {
                copy.sprite = original.sprite;
                copy.flipX = original.flipX;
                copy.flipY = original.flipY;
            }
        }
        _position = attack.Origin + settings.VisualOffset;
        _rotation = VisualSource.rotation;
        var parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        var sourceScale = VisualSource.lossyScale;
        _scale = new Vector3(
            Mathf.Approximately(parentScale.x, 0) ? sourceScale.x : sourceScale.x / parentScale.x,
            Mathf.Approximately(parentScale.y, 0) ? sourceScale.y : sourceScale.y / parentScale.y,
            Mathf.Approximately(parentScale.z, 0) ? sourceScale.z : sourceScale.z / parentScale.z);
        transform.SetPositionAndRotation(_position, _rotation);
        transform.localScale = _scale;
        IsPlaying = true;
        SetVisible(false);
    }

    private void LateUpdate()
    {
        if (!IsPlaying) return;
        if (!_attack.CanReplay) { Stop(); return; }
        if (_clip == null && _sourceAnimator != null && _sourceAnimator.runtimeAnimatorController != null &&
            _sourceAnimator.GetCurrentAnimatorStateInfo(0).IsName("Attack"))
        {
            var clips = _sourceAnimator.GetCurrentAnimatorClipInfo(0);
            if (clips.Length > 0) _clip = clips[0].clip;
        }
        _elapsed += Time.deltaTime;
        float time = _elapsed - _settings.DelaySeconds;
        if (time < 0f) return;
        if (time >= Mathf.Max(_settings.VisualSeconds, _attackSeconds)) { Stop(); return; }
        if (_clip != null)
        {
            var animationRoot = transform;
            if (_sourceAnimator != null)
            {
                int index = _sources.IndexOf(_sourceAnimator.transform);
                if (index >= 0) animationRoot = _copies[index];
            }
            _clip.SampleAnimation(animationRoot.gameObject, Mathf.Clamp01(time / _attackSeconds) * _clip.length);
        }
        transform.SetPositionAndRotation(_position, _rotation);
        transform.localScale = _scale;
        SetVisible(true);
    }

    private void SetVisible(bool visible)
    {
        foreach (var renderer in _renderers)
        {
            renderer.enabled = visible;
            if (_settings != null) renderer.color = _settings.Tint;
        }
    }

    /// <summary>풀에서 재사용할 수 있도록 표시만 종료한다.</summary>
    public void Stop()
    {
        IsPlaying = false;
        SetVisible(false);
    }
}
