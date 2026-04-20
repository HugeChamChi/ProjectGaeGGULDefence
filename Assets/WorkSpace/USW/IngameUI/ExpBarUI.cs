using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// 플레이어 경험치 바 UI
/// </summary>
public class ExpBarUI : MonoBehaviour
{
    [SerializeField] private Slider expSlider;
    [SerializeField] private float  tweenDuration = 0.35f;

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

    private void OnExpChanged(float _) => Refresh(levelUp: false);
    private void OnLevelUp()           => Refresh(levelUp: true);

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
}
