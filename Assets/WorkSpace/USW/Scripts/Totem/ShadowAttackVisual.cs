using System.Collections.Generic;
using UnityEngine;

/// <summary>토템 범위에 있는 동안 원본 외형을 따라가는 분신. 유닛/드론/공격 컴포넌트는 만들지 않는다.</summary>
[DefaultExecutionOrder(100)]
public sealed class ShadowAttackVisual : MonoBehaviour
{
    private readonly List<Transform> _sources = new();
    private readonly List<Transform> _copies = new();
    private readonly List<SpriteRenderer> _sourceRenderers = new();
    private readonly List<SpriteRenderer> _copyRenderers = new();
    private TotemShadowAttackSettings _settings;
    private float _elapsed;

    /// <summary>외형을 소유한 배치 유닛.</summary>
    public UnitBase Source { get; private set; }
    /// <summary>실제 외형 원본. 드론은 소환 본체 대신 실제 드론을 따른다.</summary>
    public Transform VisualSource { get; private set; }
    /// <summary>범위 효과에 연결되어 표시 중인지 여부.</summary>
    public bool IsPlaying { get; private set; }

    /// <summary>게임플레이 컴포넌트 없이 외형 계층을 한 번 생성한다.</summary>
    public static ShadowAttackVisual Create(UnitBase source, Transform owner, Transform visualSource = null)
    {
        var root = new GameObject(source.name + "_Shadow");
        root.transform.SetParent(owner, false);
        var visual = root.AddComponent<ShadowAttackVisual>();
        visual.Source = source;
        visual.VisualSource = visualSource != null ? visualSource : source.transform;
        visual.CopyHierarchy(visual.VisualSource, root.transform);
        visual.Stop();
        return visual;
    }

    private void CopyHierarchy(Transform source, Transform copy)
    {
        _sources.Add(source);
        _copies.Add(copy);
        var renderer = source.GetComponent<SpriteRenderer>();
        _sourceRenderers.Add(renderer);
        SpriteRenderer clone = null;
        if (renderer != null)
        {
            clone = copy.gameObject.AddComponent<SpriteRenderer>();
            clone.sharedMaterial = renderer.sharedMaterial;
            clone.drawMode = renderer.drawMode;
            clone.maskInteraction = renderer.maskInteraction;
        }
        _copyRenderers.Add(clone);
        foreach (Transform child in source)
        {
            if (child is RectTransform || child.GetComponent<ShadowAttackVisual>() != null) continue;
            var childCopy = new GameObject(child.name).transform;
            childCopy.SetParent(copy, false);
            CopyHierarchy(child, childCopy);
        }
    }

    /// <summary>범위 진입 시 연결한다. 이미 표시 중이면 공격마다 페이드를 다시 시작하지 않는다.</summary>
    public void Attach(TotemShadowAttackSettings settings)
    {
        _settings = settings;
        if (!IsPlaying) _elapsed = 0f;
        IsPlaying = true;
        AdvanceVisual(0f);
    }

    /// <summary>기존 공격 호출과 호환하며 외형은 끊지 않고 유지한다. 지연 피해는 토템이 별도로 처리한다.</summary>
    public void Play(BasicAttackReplay attack, TotemShadowAttackSettings settings) => Attach(settings);

    private void LateUpdate() => AdvanceVisual(Time.deltaTime);

    private void AdvanceVisual(float deltaTime)
    {
        if (!IsPlaying) return;
        if (Source == null || VisualSource == null || _settings == null) { Stop(); return; }
        _elapsed += deltaTime;
        float opacity = _settings.FadeSeconds > 0f ? Mathf.SmoothStep(0f, 1f, _elapsed / _settings.FadeSeconds) : 1f;
        for (int i = 0; i < _copies.Count; i++)
        {
            var source = _sources[i];
            var copy = _copies[i];
            if (source == null) { if (_copyRenderers[i] != null) _copyRenderers[i].enabled = false; continue; }
            if (i > 0)
            {
                copy.localPosition = source.localPosition;
                copy.localRotation = source.localRotation;
                copy.localScale = source.localScale;
                copy.gameObject.SetActive(source.gameObject.activeSelf);
            }
            var original = _sourceRenderers[i];
            var renderer = _copyRenderers[i];
            if (original == null || renderer == null) continue;
            renderer.sprite = original.sprite;
            renderer.flipX = original.flipX; renderer.flipY = original.flipY;
            renderer.sortingLayerID = original.sortingLayerID;
            renderer.sortingOrder = original.sortingOrder + 1;
            renderer.size = original.size;
            var color = _settings.Tint;
            color.a *= opacity * original.color.a;
            renderer.color = color;
            renderer.enabled = original.enabled;
            renderer.forceRenderingOff = original.forceRenderingOff || !original.gameObject.activeInHierarchy;
        }
        transform.SetPositionAndRotation(VisualSource.position + _settings.VisualOffset, VisualSource.rotation);
        var parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        var scale = VisualSource.lossyScale;
        transform.localScale = new Vector3(
            Mathf.Approximately(parentScale.x, 0f) ? scale.x : scale.x / parentScale.x,
            Mathf.Approximately(parentScale.y, 0f) ? scale.y : scale.y / parentScale.y,
            Mathf.Approximately(parentScale.z, 0f) ? scale.z : scale.z / parentScale.z);
    }

    /// <summary>범위 이탈/회수 시 외형을 숨긴다. 재진입 시 기존 복사본을 재사용한다.</summary>
    public void Stop()
    {
        IsPlaying = false;
        foreach (var renderer in _copyRenderers) if (renderer != null) renderer.enabled = false;
    }

    private void OnDisable() => Stop();
}
