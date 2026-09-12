using UnityEngine;
using UnityEngine.UI;

/// <summary>조커 소환 토템의 충전 비율을 선택적으로 표시하는 프리팹 UI.</summary>
public sealed class TotemSpawnGaugeUI : MonoBehaviour
{
    [SerializeField] private TotemWildcardSpawner _source;
    [SerializeField] private Image _fill;

    private void OnEnable()
    {
        if (_source == null) _source = GetComponentInParent<TotemWildcardSpawner>();
        if (_source == null) return;
        _source.OnGaugeChanged += SetProgress;
        SetProgress(_source.GaugeProgress);
    }

    private void OnDisable()
    {
        if (_source != null) _source.OnGaugeChanged -= SetProgress;
    }

    private void SetProgress(float progress)
    {
        if (_fill != null) _fill.fillAmount = progress;
    }
}
