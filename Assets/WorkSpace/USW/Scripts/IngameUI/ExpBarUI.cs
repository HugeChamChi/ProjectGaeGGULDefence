using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using GaeGGUL.Animation;

/// <summary>
/// 플레이어 경험치 바 UI
/// </summary>
public class ExpBarUI : MonoBehaviour
{
    [SerializeField] private Slider expSlider;
    [SerializeField] private float  tweenDuration = 0.35f;
    [SerializeField] private float  animationSpeed = 1f;
    [SerializeField] private Anim_InOutBase scaleAnim;

    private void Start()
    {
        if (expSlider != null)
        {
            expSlider.minValue     = 0f;
            expSlider.maxValue     = 1f;
            expSlider.value        = 0f;
            expSlider.interactable = false;
        }

        Manager.Exp.OnExpChanged += OnExpChanged;
        Manager.Exp.OnLevelUp   += OnLevelUp;

        Refresh();
    }

    private void OnDestroy()
    {
        Manager.Exp.OnExpChanged -= OnExpChanged;
        Manager.Exp.OnLevelUp   -= OnLevelUp;
    }

    private void OnExpChanged(float _)
    {
        Refresh(levelUp: false);
        ScaleAnimation();
    }

    private void OnLevelUp()
    {
        Refresh(levelUp: true);
    }

    private void Refresh(bool levelUp = false)
    {
        if (expSlider == null) return;

        float expToLevelUp = Manager.Exp.ExpToLevelUp;
        float target       = expToLevelUp > 0f ? Manager.Exp.CurrentExp / expToLevelUp : 0f;

        expSlider.DOKill();

        if (levelUp)
        {
            // 꽉 채운 뒤 새 값으로 리셋
            expSlider.DOValue(1f, tweenDuration * 0.4f)
                     .SetEase(Ease.OutCubic)
                     .OnComplete(() =>
                     {
                         expSlider.value = 0f;
                         expSlider.DOValue(target, tweenDuration)
                                  .SetEase(Ease.OutCubic);
                     });
        }
        else
        {
            expSlider.DOValue(target, tweenDuration)
                     .SetEase(Ease.OutCubic);
        }
    }

    private void ScaleAnimation()
    {
        // 이미 진행 중인 트윈이 있다면 중단하고 현재 상태에서 이어서 시작하도록 함
        transform.DOKill();

        // 퉁 튀는 느낌을 위해 살짝 커졌다가 원래대로 돌아오는 시퀀스
        // duration 대신 speed를 사용하여 상태에 상관없이 일정한 속도로 움직이게 함
        transform.DOScale(1.1f, animationSpeed)
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
