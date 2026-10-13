using Spine.Unity;
using UnityEngine;
using VContainer;

/// <summary>토템 Spine 외형에 기존 스프라이트의 색·깊이 정렬과 선택 화면의 대기 시간을 적용한다.</summary>
[DefaultExecutionOrder(200)]
public sealed class TotemSpineVisual : MonoBehaviour
{
    [Inject] private FieldPauseVisuals _fieldPause;
    [SerializeField] private SkeletonAnimation _skeleton;
    [SerializeField] private SpriteRenderer _source;
    private MeshRenderer _renderer;

    private void Awake()
    {
        if (_skeleton != null) _renderer = _skeleton.GetComponent<MeshRenderer>();
    }

    private void LateUpdate()
    {
        if (_skeleton == null || _source == null || _renderer == null) return;
        _renderer.sortingLayerID = _source.sortingLayerID;
        _renderer.sortingOrder = _source.sortingOrder;
        var color = _source.color;
        _skeleton.Skeleton?.SetColor(color.r, color.g, color.b, color.a);
        _skeleton.UnscaledTime = _fieldPause != null && _fieldPause.IsActive;
    }

    private void OnDisable()
    {
        if (_skeleton != null) _skeleton.UnscaledTime = false;
    }
}
