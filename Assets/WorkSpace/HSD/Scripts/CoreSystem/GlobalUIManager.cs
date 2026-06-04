using Cysharp.Threading.Tasks;
using VContainer;
using VContainer.Unity;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class GlobalUIManager 
{
    private readonly IFadeScreen _fadeScreen;

    [Inject]
    public GlobalUIManager(IFadeScreen fadeScreen)
    {
        _fadeScreen = fadeScreen;
    }

    public async UniTask FadeInAsync(float duration = 0.8f)
    {
        if (_fadeScreen != null)
        {
            await _fadeScreen.FadeInAsync(duration);
        }
    }

    public async UniTask FadeOutAsync(float duration = 0.8f)
    {
        if (_fadeScreen != null)
        {
            await _fadeScreen.FadeOutAsync(duration);
        }
    }
}

// 프리팹을 지정하지 않았을 때 자동으로 생성되는 기본 검은색 페이드 화면 구현체
public class DefaultFadeScreen : IFadeScreen, IInitializable
{
    private Image _fallbackFadeImage;

    public void Initialize()
    {
        var canvasGO = new GameObject("GlobalUICanvas");
        Object.DontDestroyOnLoad(canvasGO);
        
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;
        
        var canvasScaler = canvasGO.AddComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1920, 1080);
        
        canvasGO.AddComponent<GraphicRaycaster>();

        var fadeGO = new GameObject("FadeImage");
        fadeGO.transform.SetParent(canvasGO.transform, false);
        
        _fallbackFadeImage = fadeGO.AddComponent<Image>();
        _fallbackFadeImage.color = new Color(0, 0, 0, 0);
        _fallbackFadeImage.raycastTarget = false;
        
        var rectTransform = _fallbackFadeImage.rectTransform;
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.sizeDelta = Vector2.zero;
    }

    public async UniTask FadeInAsync(float duration = 0.5f)
    {
        if (_fallbackFadeImage != null)
        {
            _fallbackFadeImage.raycastTarget = true;
            await _fallbackFadeImage.DOFade(1f, duration).ToUniTask();
        }
    }

    public async UniTask FadeOutAsync(float duration = 0.5f)
    {
        if (_fallbackFadeImage != null)
        {
            await _fallbackFadeImage.DOFade(0f, duration).ToUniTask();
            _fallbackFadeImage.raycastTarget = false;
        }
    }
}
