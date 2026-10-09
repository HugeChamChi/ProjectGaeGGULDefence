using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>선택한 C 수면을 바탕으로 출격 버튼의 세부 연출을 비교하는 실험실.</summary>
public sealed class SortieSurfaceLab : MonoBehaviour
{
    [SerializeField] private Image[] _surfaces;
    [SerializeField] private TMP_Text _status;
    [SerializeField] private Vector2 _fillUvRange = new Vector2(0f,1f);
    [SerializeField] private bool _risingOnly;
    private float _fillClock;
    private Material[] _materials;
    private float _clock;
    private float _progress = 0.6f;
    private float _target = 0.6f;
    private bool _automatic;
    private bool _cool;
    private bool _paused;
    private static readonly int FillId = Shader.PropertyToID("_Fill");
    private static readonly int TimeId = Shader.PropertyToID("_FlowTime");
    private static readonly int CoolId = Shader.PropertyToID("_Cool");

    private void Awake()
    {
        _materials = new Material[_surfaces.Length];
        for(int i=0;i<_surfaces.Length;i++)
        {
            _materials[i] = new Material(_surfaces[i].material);
            _surfaces[i].material = _materials[i];
        }
        Apply();
    }
    private void Update()
    {
        if (!_paused)
        {
            _clock += Time.unscaledDeltaTime;
            if (_automatic)
            {
                if (_risingOnly)
                {
                    // Demonstrate accumulating charge, then hold full before a new cycle.
                    _fillClock += Time.unscaledDeltaTime;
                    if (_fillClock >= 12f) { _fillClock %= 12f; _progress = 0f; }
                    float phase = Mathf.Clamp01(_fillClock / 9f);
                    _target = Mathf.Clamp01(phase + Mathf.Sin(phase * Mathf.PI * 6f) * 0.045f);
                }
                else _target = Mathf.PingPong(_clock*0.16f,1f);
            }
            _progress = Mathf.MoveTowards(_progress,_target,Time.unscaledDeltaTime*0.45f);
        }
        Apply();
    }
    /// <summary>25/60/100% 채움으로 부드럽게 이동한다.</summary>
    public void CycleFill() { _automatic=false; _target = _target>=0.99f ? 0.25f : _target<0.5f ? 0.6f : 1f; }
    /// <summary>차오르고 비워지는 움직임을 반복한다.</summary>
    public void ToggleAutomatic() { _automatic=!_automatic; if (_automatic && _risingOnly) { _fillClock=0f; _progress=0f; _target=0f; } }
    /// <summary>모든 시안의 표면 움직임과 채움을 정지/재개한다.</summary>
    public void TogglePause() { _paused=!_paused; }
    /// <summary>모든 시안을 원색과 참고용 파란색으로 비교한다.</summary>
    public void ToggleCool() { _cool=!_cool; }
    private void Apply()
    {
        float fill = _progress <= 0.0001f ? 0f : _progress >= 0.9999f ? 1f : Mathf.Lerp(_fillUvRange.x,_fillUvRange.y,_progress);
        for(int i=0;i<_materials.Length;i++)
        {
            _materials[i].SetFloat(FillId,fill);
            _materials[i].SetFloat(TimeId,_clock);
            _materials[i].SetFloat(CoolId,_cool?1f:0f);
        }
        if(_status!=null) _status.SetText("채움 {0:0}%",_progress*100f);
    }
    private void OnDestroy()
    {
        if(_materials==null) return;
        foreach(var material in _materials) if(material!=null) Destroy(material);
    }
}
