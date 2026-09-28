using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

public class ButtonReactionHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField] private ButtonReactionData data;

    private Vector3 _originScale;
    private Tween _currentTween;

    private void Awake()
    {
        _originScale = transform.localScale;
        // 버튼별 데이터가 지정되지 않았을 때만 공용 데이터를 사용한다.
        if (data == null) data = GlobalData.ButtonReactionData;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (data == null) return;
        ExecuteScale(_originScale * data.shrinkScale, data.duration, data.pressCurve);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (data == null) return;

        ExecuteScale(_originScale, data.duration * 1.5f, data.releaseCurve);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (data == null) return;

        ExecuteScale(_originScale, data.duration * 1.5f, data.releaseCurve);
    }

    private void ExecuteScale(Vector3 targetScale, float time, AnimationCurve curve)
    {
        _currentTween?.Kill();
        _currentTween = transform.DOScale(targetScale, time)
            .SetEase(curve)
            .SetUpdate(true);
    }

    private void OnDisable()
    {
        _currentTween?.Kill();
        transform.localScale = _originScale;
    }
}