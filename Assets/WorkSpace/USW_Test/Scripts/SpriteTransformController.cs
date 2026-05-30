using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 미리보기 스프라이트(RawImage)의 위치·크기·좌우반전을 슬라이더/버튼으로 제어합니다.
/// SpriteDisplay RawImage의 부모 오브젝트에 부착하거나,
/// _spriteRect에 SpriteDisplay의 RectTransform을 직접 연결하세요.
/// </summary>
public class SpriteTransformController : MonoBehaviour
{
    [Header("조작 대상")]
    [SerializeField] private RectTransform _spriteRect;

    [Header("위치 슬라이더")]
    [SerializeField] private Slider _posXSlider;
    [SerializeField] private Slider _posYSlider;
    [SerializeField] private TextMeshProUGUI _posXLabel;
    [SerializeField] private TextMeshProUGUI _posYLabel;

    [Header("크기 슬라이더")]
    [SerializeField] private Slider _scaleSlider;
    [SerializeField] private TextMeshProUGUI _scaleLabel;

    [Header("버튼")]
    [SerializeField] private Button _flipButton;
    [SerializeField] private Button _resetButton;
    [SerializeField] private TextMeshProUGUI _flipLabel;

    [Header("범위 설정")]
    [SerializeField] private float _posRange     = 500f;
    [SerializeField] private float _scaleMin     = 0.1f;
    [SerializeField] private float _scaleMax     = 3f;
    [SerializeField] private float _defaultScale = 1f;

    private bool  _flipped;
    private float _currentScale;

    private void Awake()
    {
        _posXSlider.minValue  = -_posRange;
        _posXSlider.maxValue  =  _posRange;
        _posYSlider.minValue  = -_posRange;
        _posYSlider.maxValue  =  _posRange;
        _scaleSlider.minValue = _scaleMin;
        _scaleSlider.maxValue = _scaleMax;

        _posXSlider.onValueChanged.AddListener(OnPosXChanged);
        _posYSlider.onValueChanged.AddListener(OnPosYChanged);
        _scaleSlider.onValueChanged.AddListener(OnScaleChanged);
        _flipButton.onClick.AddListener(ToggleFlip);
        _resetButton.onClick.AddListener(ResetTransform);
    }

    /// <summary>새 텍스처 로드 시 ArtPreviewManager에서 호출됩니다.</summary>
    public void ResetTransform()
    {
        _flipped      = false;
        _currentScale = _defaultScale;
        _spriteRect.anchoredPosition = Vector2.zero;
        ApplyScale();

        _posXSlider.SetValueWithoutNotify(0f);
        _posYSlider.SetValueWithoutNotify(0f);
        _scaleSlider.SetValueWithoutNotify(_defaultScale);
        RefreshLabels();
    }

    private void OnPosXChanged(float v)
    {
        _spriteRect.anchoredPosition = new Vector2(v, _spriteRect.anchoredPosition.y);
        RefreshLabels();
    }

    private void OnPosYChanged(float v)
    {
        _spriteRect.anchoredPosition = new Vector2(_spriteRect.anchoredPosition.x, v);
        RefreshLabels();
    }

    private void OnScaleChanged(float v)
    {
        _currentScale = v;
        ApplyScale();
        RefreshLabels();
    }

    private void ToggleFlip()
    {
        _flipped = !_flipped;
        ApplyScale();
        if (_flipLabel) _flipLabel.text = _flipped ? "반전 ON" : "반전 OFF";
    }

    private void ApplyScale()
    {
        float xSign = _flipped ? -1f : 1f;
        _spriteRect.localScale = new Vector3(xSign * _currentScale, _currentScale, 1f);
    }

    private void RefreshLabels()
    {
        Vector2 pos = _spriteRect.anchoredPosition;
        if (_posXLabel)  _posXLabel.text  = $"X: {pos.x:F0}";
        if (_posYLabel)  _posYLabel.text  = $"Y: {pos.y:F0}";
        if (_scaleLabel) _scaleLabel.text = $"배율: {_currentScale:F2}x";
    }
}
