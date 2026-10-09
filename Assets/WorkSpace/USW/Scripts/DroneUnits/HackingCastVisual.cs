using UnityEngine;

/// <summary>GammanTeleport의 점멸/접촉을 재생한다. 실제 드론/그리드/집결 위치는 바꾸지 않는다.</summary>
public sealed class HackingCastVisual : MonoBehaviour
{
    private SpriteRenderer _copy;
    private LineRenderer _beam;
    private BossBase _target;
    private Vector3 _origin;
    private DroneHackingData _data;
    private bool _consumer;
    /// <summary>시각 사본과 공유 머티리얼 선을 만들고 재사용한다.</summary>
    public void Begin(SpriteRenderer source, BossBase target, DroneHackingData data, bool consumer)
    {
        _target = target; _data = data; _consumer = consumer;
        _origin = source != null ? source.transform.position : transform.position;
        if (_copy == null)
        {
            var go = new GameObject("HackTeleportDrone");
            go.transform.SetParent(transform, false);
            _copy = go.AddComponent<SpriteRenderer>();
            _copy.sortingLayerName = "FX"; _copy.sortingOrder = 40;
            _beam = go.AddComponent<LineRenderer>();
            _beam.positionCount = 2; _beam.useWorldSpace = true;
            _beam.sortingLayerName = "FX"; _beam.sortingOrder = 39;
        }
        _copy.sprite = source != null ? source.sprite : null;
        Vector3 worldScale = source != null ? source.transform.lossyScale : Vector3.one;
        Vector3 parentScale = transform.lossyScale;
        _copy.transform.localScale = new Vector3(worldScale.x / Mathf.Max(.001f, Mathf.Abs(parentScale.x)),
            worldScale.y / Mathf.Max(.001f, Mathf.Abs(parentScale.y)), 1f);
        _beam.sharedMaterial = data.LineMaterial;
        _beam.startColor = _beam.endColor = data.HackColor;
        _beam.startWidth = .08f; _beam.endWidth = .03f;
        Draw(0);
    }
    /// <summary>전투 시간 진행률로 움직이며 피해를 만들지 않는다.</summary>
    public void Draw(float progress)
    {
        if (_target == null) { Clear(); return; }
        Vector3 contact = _target.transform.position;
        Vector3 point = _consumer ? contact + _data.TeleportOffset : Vector3.Lerp(_origin, contact, progress);
        _copy.transform.position = point;
        _copy.enabled = _consumer && progress > .15f;
        _copy.color = new Color(1f, 1f, 1f, progress < .3f ? .45f : 1f);
        _beam.enabled = _data.LineMaterial != null && (!_consumer || progress >= .8f);
        _beam.SetPosition(0, _consumer ? point : Vector3.Lerp(_origin, contact, Mathf.Max(0, progress - .18f)));
        _beam.SetPosition(1, _consumer ? contact : point);
    }
    /// <summary>취소/복귀 시 모든 외형을 숨긴다.</summary>
    public void Clear() { if (_copy != null) _copy.enabled = false; if (_beam != null) _beam.enabled = false; }
}
