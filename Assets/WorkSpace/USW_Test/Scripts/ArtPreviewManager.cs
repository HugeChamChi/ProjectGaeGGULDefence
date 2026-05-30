using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 아트 검수 툴의 중앙 조율자.
/// Canvas 루트 오브젝트에 부착하고 Inspector에서 모든 참조를 연결하세요.
/// </summary>
public class ArtPreviewManager : MonoBehaviour
{
    [Header("Preview Display")]
    [SerializeField] private RawImage _spriteDisplay;
    [SerializeField] private GameObject _emptyPrompt;
    [SerializeField] private RawImage _backgroundImage;
    [SerializeField] private GameObject _gameUIOverlay;

    [Header("Status")]
    [SerializeField] private TextMeshProUGUI _fileNameLabel;
    [SerializeField] private TextMeshProUGUI _resolutionLabel;

    [Header("Components")]
    [SerializeField] private RuntimeSpriteLoader _loader;
    [SerializeField] private SpriteTransformController _transformController;
    [SerializeField] private BackgroundSelector _backgroundSelector;
    [SerializeField] private ScreenshotSaver _screenshotSaver;

    private void Awake()
    {
        _loader.OnTextureLoaded += HandleTextureLoaded;
    }

    private void Start()
    {
        _spriteDisplay.gameObject.SetActive(false);
        _emptyPrompt.SetActive(true);
        if (_gameUIOverlay != null) _gameUIOverlay.SetActive(false);
        _resolutionLabel.text = "기준 해상도: 1080 × 1920";
        _fileNameLabel.text = "파일을 선택하세요";
    }

    private void HandleTextureLoaded(Texture2D texture, string fileName)
    {
        _spriteDisplay.texture = texture;
        _spriteDisplay.gameObject.SetActive(true);
        _emptyPrompt.SetActive(false);
        _fileNameLabel.text = fileName;
        _transformController.ResetTransform();
    }

    /// <summary>Toggle UI의 onValueChanged에 연결하세요.</summary>
    public void SetOverlayVisible(bool visible)
    {
        if (_gameUIOverlay != null) _gameUIOverlay.SetActive(visible);
    }

    private void OnDestroy()
    {
        _loader.OnTextureLoaded -= HandleTextureLoaded;
    }
}
