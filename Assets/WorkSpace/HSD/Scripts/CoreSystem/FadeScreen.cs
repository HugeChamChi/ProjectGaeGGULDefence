using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Cysharp.Threading.Tasks;

public class FadeScreen : MonoBehaviour, IFadeScreen
{
    [Header("Fade Settings")]
    [Tooltip("페이드에 사용할 머티리얼이 들어있는 이미지 (ex. Custom_CircleTransition.mat)")]
    [SerializeField] private Image fadeImage;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Material Settings")]
    [Tooltip("머티리얼을 사용해 애니메이션할 경우 체크하세요")]
    [SerializeField] private bool useMaterialTransition = false;
    [Tooltip("머티리얼에서 조절할 파라미터 이름")]
    [SerializeField] private string materialPropertyName = "_Progress";

    private Material _fadeMaterial;

    private void Awake()
    {
        if (fadeImage != null && useMaterialTransition && fadeImage.material != null)
        {
            // 원본 머티리얼 에셋이 변조되지 않도록 인스턴스로 복제
            _fadeMaterial = Instantiate(fadeImage.material);
            fadeImage.material = _fadeMaterial;
        }

        // 초기 상태: 화면이 보이는 상태 (Clear) -> _Progress = 1.0f
        SetClearState();
    }

    private void SetClearState()
    {
        if (useMaterialTransition && _fadeMaterial != null)
        {
            _fadeMaterial.SetFloat(materialPropertyName, 1f);
            if (fadeImage != null) fadeImage.raycastTarget = false;
        }
        else if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
        }
    }

    [Button("Fade In (Test)")]
    public async UniTask FadeInAsync(float duration = 0.5f)
    {
        // 화면을 가리는 상태 (Obscured) -> _Progress를 0에서 0.5로 이동
        if (useMaterialTransition && _fadeMaterial != null)
        {
            if (fadeImage != null) fadeImage.raycastTarget = true;
            // 0부터 시작해서 0.5까지
            _fadeMaterial.SetFloat(materialPropertyName, 0f);
            await _fadeMaterial.DOFloat(0.5f, materialPropertyName, duration).ToUniTask();
        }
        else if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
            await canvasGroup.DOFade(1f, duration).ToUniTask();
        }
    }

    [Button("Fade Out (Test)")]
    public async UniTask FadeOutAsync(float duration = 0.5f)
    {
        // 화면이 보이게 되는 상태 (Clear) -> _Progress를 0.5에서 1.0으로 이동
        if (useMaterialTransition && _fadeMaterial != null)
        {
            if (fadeImage != null) fadeImage.raycastTarget = true; // 페이드 아웃 중 터치 방지
            // 0.5부터 시작해서 1까지
            _fadeMaterial.SetFloat(materialPropertyName, 0.5f);
            await _fadeMaterial.DOFloat(1f, materialPropertyName, duration).ToUniTask();
            if (fadeImage != null) fadeImage.raycastTarget = false;
        }
        else if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true; // 페이드 아웃 중 터치 방지
            await canvasGroup.DOFade(0f, duration).ToUniTask();
            canvasGroup.blocksRaycasts = false;
        }
    }
}
