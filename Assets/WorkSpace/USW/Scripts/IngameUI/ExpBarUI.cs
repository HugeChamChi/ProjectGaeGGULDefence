using UnityEngine;
using VContainer;
using UnityEngine.UI;
using DG.Tweening;
using GaeGGUL.Animation;

/// <summary>
/// 플레이어 경험치 바 UI. SummonButton 위에 시각 오버레이로 얹는 용도로도 쓸 수 있다
/// (마스크/Fill 이미지의 Raycast Target을 꺼두면 버튼 클릭과 기능적으로 겹치지 않는다).
///
/// Slider가 아니라 fillImage에 적용된 LiquidWaveFill 셰이더의 _Fill 프로퍼티로 채워진다:
/// 셰이더가 진행도 주변을 사인파로 흔들어 물결처럼 차오르는 수면 경계선을 그린다.
/// maskRect(RectMask2D)는 더 이상 채움 경계를 담당하지 않고 항상 완전히 열어 둔다
/// (물결 파고가 경계에서 잘리지 않도록 하기 위함).
///
/// Scene 구성:
///   (SummonButton 등 아무 부모)
///     Background (Image, 빈 상태 배경 스프라이트, 풀 사이즈, Raycast Target 꺼짐)
///     Mask (RectTransform + RectMask2D, anchor/pivot = (0.5, 0) 아래쪽 정렬)
///       Fill (Image + LiquidWaveFill 머티리얼, 높이 = 바 전체 높이 고정, Raycast Target 꺼짐)
/// </summary>
public class ExpBarUI : MonoBehaviour
{
    [Inject] private ExpManager _expManager;
    [Inject] private AudioManager _audioManager;

    [Header("마스킹 게이지 (아래→위)")]
    [Tooltip("아래쪽 정렬된 RectMask2D 컨테이너. 물결 셰이더가 채움을 담당하므로 항상 완전히 개방해 둔다.")]
    [SerializeField] private RectTransform maskRect;
    [Tooltip("진행도 1일 때 maskRect의 목표 높이. 0이면 부모 RectTransform의 현재 높이를 사용.")]
    [SerializeField] private float fullHeight = 0f;
    [Tooltip("LiquidWaveFill 셰이더가 적용된 Fill 이미지. 진행도를 _Fill 프로퍼티로 흘려준다.")]
    [SerializeField] private Image fillImage;

    [SerializeField] private float tweenDuration  = 0.35f;
    [SerializeField] private float animationSpeed = 1f;
    [SerializeField] private Anim_InOutBase scaleAnim;

    private static readonly int FillId = Shader.PropertyToID("_Fill");

    private float _currentProgress;
    private float _lastSoundTime;
    private Material _fillMaterial;

    private void Start()
    {
        if (maskRect != null && fullHeight <= 0f)
        {
            fullHeight = maskRect.parent is RectTransform parentRect
                ? parentRect.rect.height
                : maskRect.rect.height;
        }

        if (maskRect != null)
        {
            // 물결 셰이더가 실제 채움 경계를 그리므로 마스크는 항상 완전히 열어 둔다.
            Vector2 size = maskRect.sizeDelta;
            size.y = fullHeight;
            maskRect.sizeDelta = size;
        }

        _fillMaterial = fillImage != null ? fillImage.material : null;

        SetProgressImmediate(0f);

        _expManager.OnExpChanged += OnExpChanged;
        _expManager.OnLevelUp   += OnLevelUp;

        Refresh();
    }

    private void OnDestroy()
    {
        if (_expManager != null)
        {
            _expManager.OnExpChanged -= OnExpChanged;
            _expManager.OnLevelUp   -= OnLevelUp;
        }
    }

    private void OnExpChanged(float _)
    {
        if (Time.unscaledTime - _lastSoundTime > 0.05f)
        {
            _audioManager.PlaySFX("02.Expup");
            _lastSoundTime = Time.unscaledTime;
        }

        Refresh(levelUp: false);
        ScaleAnimation();
    }

    private void OnLevelUp()
    {
        _audioManager.PlaySFX("02.Levelup");
        Refresh(levelUp: true);
    }

    private void Refresh(bool levelUp = false)
    {
        if (maskRect == null) return;

        float expToLevelUp = _expManager.ExpToLevelUp;
        float target        = expToLevelUp > 0f ? _expManager.CurrentExp / expToLevelUp : 0f;

        maskRect.DOKill();

        if (levelUp)
        {
            // 꽉 채운 뒤 새 값으로 리셋
            DOTween.To(() => _currentProgress, SetProgress, 1f, tweenDuration * 0.4f)
                   .SetTarget(maskRect)
                   .SetEase(Ease.OutCubic)
                   .OnComplete(() =>
                   {
                       SetProgressImmediate(0f);
                       DOTween.To(() => _currentProgress, SetProgress, target, tweenDuration)
                              .SetTarget(maskRect)
                              .SetEase(Ease.OutCubic);
                   });
        }
        else
        {
            DOTween.To(() => _currentProgress, SetProgress, target, tweenDuration)
                   .SetTarget(maskRect)
                   .SetEase(Ease.OutCubic);
        }
    }

    private void SetProgress(float t)
    {
        _currentProgress = t;
        if (_fillMaterial == null) return;
        _fillMaterial.SetFloat(FillId, Mathf.Clamp01(t));
    }

    private void SetProgressImmediate(float t)
    {
        maskRect?.DOKill();
        SetProgress(t);
    }

    private void ScaleAnimation()
    {
        // 이미 진행 중인 트윈이 있다면 중단하고 현재 상태에서 이어서 시작하도록 함
        transform.DOKill();

        // 퉁 튀는 느낌을 위해 살짝 커졌다가 원래대로 돌아오는 시퀀스
        // duration 대신 speed를 사용하여 상태에 상관없이 일정한 속도로 움직이게 함
        transform.DOScale(1.04f, animationSpeed)
                 .SetSpeedBased()
                 .SetEase(Ease.OutQuad)
                 .OnComplete(() =>
                 {
                     transform.DOScale(1f, animationSpeed)
                              .SetSpeedBased()
                              .SetEase(Ease.OutBack);
                 });
    }
}
