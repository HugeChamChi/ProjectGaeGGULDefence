using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI 이펙트 요소 하나의 알파/크기/이동 타임라인 (RewardRevealFx, UiFxTimeline 공용).
/// 0 → Peak(상승) → 유지 → Rest(하강). Rest가 0이면 끝에서 비활성화, >0이면 그 알파로 남는다.
/// </summary>
[Serializable]
public class UiFxPulse
{
    [Tooltip("시작 시각(초)")] public float Start;
    [Tooltip("0 → Peak 알파까지 걸리는 시간")] public float Rise = 0.05f;
    public float Peak = 1f;
    [Tooltip("Peak 유지 시간")] public float Hold;
    [Tooltip("Peak → Rest 알파까지 걸리는 시간")] public float Fall = 0.1f;
    [Tooltip("하강 후 남는 알파 (0이면 사라짐, >0이면 계속 유지)")] public float Rest;
    public Ease FallEase = Ease.InQuad;
    public float ScaleFrom = 1f;
    public float ScaleTo = 1f;
    [Tooltip("크기 변화 시간 (0이면 즉시 ScaleTo)")] public float ScaleDuration;
    public Ease ScaleEase = Ease.OutQuad;
    [Tooltip("시작 위치 기준 이동량 (캔버스 단위, 전체 수명 동안)")] public Vector2 Drift;

    /// <summary>하강이 끝나는 시각.</summary>
    public float End => Start + Rise + Hold + Fall;

    /// <summary>
    /// graphic을 초기 상태(알파 0, ScaleFrom, basePosition, 비활성)로 되돌리고 이 타임라인을 seq에 삽입한다.
    /// </summary>
    public void AppendTo(Sequence seq, Graphic graphic, Vector2 basePosition)
    {
        if (graphic == null) return;
        var rt = graphic.rectTransform;
        var c = graphic.color;
        c.a = 0f;
        graphic.color = c;
        rt.localScale = Vector3.one * ScaleFrom;
        rt.anchoredPosition = basePosition;
        graphic.gameObject.SetActive(false);

        seq.InsertCallback(Start, () => graphic.gameObject.SetActive(true));
        seq.Insert(Start, graphic.DOFade(Peak, Mathf.Max(Rise, 0.0001f)).SetEase(Ease.OutQuad));
        seq.Insert(Start + Rise + Hold, graphic.DOFade(Rest, Mathf.Max(Fall, 0.0001f)).SetEase(FallEase));
        if (ScaleDuration > 0f)
            seq.Insert(Start, rt.DOScale(ScaleTo, ScaleDuration).SetEase(ScaleEase));
        else
            seq.InsertCallback(Start, () => rt.localScale = Vector3.one * ScaleTo);
        if (Drift != Vector2.zero)
            seq.Insert(Start, rt.DOAnchorPos(basePosition + Drift, End - Start).SetEase(Ease.OutSine));
        if (Rest <= 0f)
            seq.InsertCallback(End, () => graphic.gameObject.SetActive(false));
    }
}
