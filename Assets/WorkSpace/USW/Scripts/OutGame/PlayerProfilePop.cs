using UnityEngine;
using DG.Tweening;

public class PlayerProfilePop : MonoBehaviour
{
    private Tween _popupTween;

    private void Start()
    {
        _popupTween = transform.DOScale(1f, 0.3f)
            .SetEase(Ease.OutBack)
            .SetAutoKill(false);
    }

    private void OnEnable()
    {
        transform.localScale = Vector3.zero;
        _popupTween?.Restart();
    }
}
