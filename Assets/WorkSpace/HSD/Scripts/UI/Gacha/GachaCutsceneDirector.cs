using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Cinemachine;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 가챠 뽑기 시 재생되는 시네마틱 컷씬(물가에서 개구리 실루엣이 튀어나오는 연출)을 담당합니다.
/// MainUI를 숨기고 전용 카메라/스테이지를 활성화해 8단계 시퀀스를 재생한 뒤 원래 UI로 복귀합니다.
/// </summary>
public class GachaCutsceneDirector : MonoBehaviour
{
    [Header("Main UI")]
    [SerializeField] private GameObject mainUIRoot;

    [Header("Frog Actors")]
    [SerializeField] private RectTransform frogRight;
    [SerializeField] private RectTransform frogLeft;
    [SerializeField] private RectTransform frogCenter;

    [Header("Bubble Effect")]
    [SerializeField] private GachaBubbleEffect bubbleEffect;

    [Header("Cameras")]
    [SerializeField] private CinemachineVirtualCamera vcamFrogRight;
    [SerializeField] private CinemachineVirtualCamera vcamFrogLeft;
    [SerializeField] private CinemachineVirtualCamera vcamBubbleZoom;
    [SerializeField] private CinemachineVirtualCamera vcamReveal;

    [Header("Fade")]
    [SerializeField] private CanvasGroup whiteFadeGroup;
    [SerializeField] private float whiteFadeDuration = 0.6f;

    [Header("1. 좌우 확인 연출 (Whip Pan)")]
    [SerializeField] private float frogHopDuration = 0.35f;
    [SerializeField] private float whipPanHoldDuration = 0.25f;
    [SerializeField] private float turnDuration = 0.4f;
    [SerializeField] private float frogCamHeight = -3f;
    [SerializeField] private float defaultFov = 60f;

    [Header("2. 버블 확대 연출 (Bubble Zoom)")]
    [SerializeField] private float bubbleZoomBlendDuration = 0.5f;
    [SerializeField] private float bubbleZoomHoldDuration = 0.5f;
    [SerializeField] private float bubbleZoomCamHeight = -3f;
    [SerializeField] private float bubbleZoomFov = 40f;

    [Header("3. 점프 & 리빌 연출 (Leap & Reveal)")]
    [SerializeField] private float leapAnticipationDuration = 0.3f;
    [SerializeField] private float leapJumpPower = 300f;
    [SerializeField] private float leapDuration = 0.7f;
    [SerializeField] private float cameraReactionDelay = 0.15f;
    [SerializeField] private float revealBlendDuration = 0.7f;
    [SerializeField] private float revealDistance = -8f;
    [SerializeField] private float revealTiltAngle = -20f;
    [SerializeField] private float revealFov = 60f;
    [SerializeField] private float revealHoldDuration = 0.8f;

    [Header("Slow Motion")]
    [SerializeField] private float slowMoScale = 0.3f;

    private CinemachineVirtualCamera[] AllVcams => new[] { vcamFrogRight, vcamFrogLeft, vcamBubbleZoom, vcamReveal };

    [Button("Test Play Cutscene")]
    public void TestPlay()
    {
        PlayAsync().Forget();
    }

    public async UniTask PlayAsync()
    {
        gameObject.SetActive(true);
        mainUIRoot.SetActive(false);

        try
        {
            ResetStage();

            // 카메라는 항상 정면(중앙)에서 시작
            SetLiveCamera(vcamFrogRight);

            // 1~2. 오른쪽 개구리 등장 + 고개 돌리듯 회전
            await PopFrog(frogRight);
            await TurnCamera(vcamFrogRight, ComputeLookYAngle(vcamFrogRight.transform, frogRight));
            await UniTask.Delay(TimeSpan.FromSeconds(whipPanHoldDuration), ignoreTimeScale: true);

            // 3~4. 왼쪽 개구리 등장 + 반대쪽으로 고개 돌리듯 회전
            await PopFrog(frogLeft);
            await TurnCamera(vcamFrogRight, ComputeLookYAngle(vcamFrogRight.transform, frogLeft));
            await UniTask.Delay(TimeSpan.FromSeconds(whipPanHoldDuration), ignoreTimeScale: true);

            // 5. 부글부글 이펙트 + 중앙으로 회전 후 버블 카메라로 확대 (위치는 그대로, bubbleZoomFov로 블렌드 줌인)
            bubbleEffect?.Play();
            await TurnCamera(vcamFrogRight, ComputeLookYAngle(vcamFrogRight.transform, frogCenter));
            SetBlend(bubbleZoomBlendDuration, CinemachineBlendDefinition.Style.EaseOut);
            SetLiveCamera(vcamBubbleZoom);
            await UniTask.Delay(TimeSpan.FromSeconds(bubbleZoomHoldDuration), ignoreTimeScale: true);

            // 6. 개구리 점프(슬로우모션) 시작 - 예비동작 대기 후 실제로 뛰어오름
            Time.timeScale = slowMoScale;
            await UniTask.Delay(TimeSpan.FromSeconds(leapAnticipationDuration), ignoreTimeScale: true);
            var leapTask = LeapFrog(frogCenter);

            // 7. 개구리가 뛰어오른 뒤, cameraReactionDelay만큼 지나서 놀라 뒤로 넘어지듯 리빌 카메라로 전환
            // leapDuration과 동일하게 슬로우모션(timeScale)의 영향을 받아야 인스펙터 값만으로 점프 대비 타이밍을 가늠할 수 있음
            await UniTask.Delay(TimeSpan.FromSeconds(cameraReactionDelay), ignoreTimeScale: false);
            SetBlend(revealBlendDuration, CinemachineBlendDefinition.Style.EaseOut);
            SetLiveCamera(vcamReveal);
            await UniTask.Delay(TimeSpan.FromSeconds(revealHoldDuration), ignoreTimeScale: true);

            // 개구리가 튀어오르는 도중에 화이트아웃 시작 (착지까지 기다리지 않음)
            var fadeTask = whiteFadeGroup.DOFade(1f, whiteFadeDuration).SetUpdate(true).ToUniTask();
            await UniTask.WhenAll(leapTask, fadeTask);
        }
        finally
        {
            Time.timeScale = 1f;
            gameObject.SetActive(false);
            mainUIRoot.SetActive(true);
            whiteFadeGroup.alpha = 0f;
        }
    }

    private void ResetStage()
    {
        foreach (var vcam in AllVcams)
        {
            if (vcam != null) vcam.gameObject.SetActive(false);
        }

        SetSilhouette(frogRight);
        SetSilhouette(frogLeft);
        SetSilhouette(frogCenter);

        HideFrog(frogRight);
        HideFrog(frogLeft);
        HideFrog(frogCenter);

        bubbleEffect?.Stop();
        whiteFadeGroup.alpha = 0f;

        SetFov(vcamFrogRight, defaultFov);
        SetFov(vcamFrogLeft, defaultFov);
        SetFov(vcamBubbleZoom, bubbleZoomFov);
        SetFov(vcamReveal, revealFov);

        if (vcamFrogRight != null)
        {
            vcamFrogRight.transform.localRotation = Quaternion.identity;
            SetHeight(vcamFrogRight, frogCamHeight);
        }
        SetHeight(vcamBubbleZoom, bubbleZoomCamHeight);

        if (vcamReveal != null)
        {
            var pos = vcamReveal.transform.localPosition;
            pos.z = revealDistance;
            vcamReveal.transform.localPosition = pos;
            vcamReveal.transform.localRotation = Quaternion.Euler(revealTiltAngle, 0f, 0f);
        }
    }

    private static void SetHeight(CinemachineVirtualCamera vcam, float height)
    {
        if (vcam == null) return;
        var pos = vcam.transform.localPosition;
        pos.y = height;
        vcam.transform.localPosition = pos;
    }

    private static void SetFov(CinemachineVirtualCamera vcam, float fov)
    {
        if (vcam == null) return;
        var lens = vcam.m_Lens;
        lens.FieldOfView = fov;
        vcam.m_Lens = lens;
    }

    private static void SetSilhouette(RectTransform frog)
    {
        if (frog == null) return;
        foreach (var graphic in frog.GetComponentsInChildren<Graphic>(true))
        {
            graphic.color = Color.black;
        }
    }

    private static void HideFrog(RectTransform frog)
    {
        if (frog == null) return;
        frog.localScale = Vector3.zero;
        frog.gameObject.SetActive(false);
    }

    private async UniTask PopFrog(RectTransform frog)
    {
        if (frog == null) return;

        frog.gameObject.SetActive(true);
        frog.localScale = Vector3.zero;
        await frog.DOScale(1f, frogHopDuration).SetEase(Ease.OutCubic).ToUniTask();
    }

    private async UniTask LeapFrog(RectTransform frog)
    {
        if (frog == null) return;

        frog.gameObject.SetActive(true);
        frog.localScale = Vector3.one;
        var startPos = frog.anchoredPosition;
        await frog.DOJumpAnchorPos(startPos, leapJumpPower, 1, leapDuration)
            .SetUpdate(false)
            .ToUniTask();
    }

    private UniTask TurnCamera(CinemachineVirtualCamera vcam, float targetYAngle)
    {
        if (vcam == null) return UniTask.CompletedTask;

        return vcam.transform
            .DOLocalRotate(new Vector3(0f, targetYAngle, 0f), turnDuration)
            .SetEase(Ease.OutCubic)
            .ToUniTask();
    }

    private static float ComputeLookYAngle(Transform camTransform, Transform target)
    {
        if (target == null) return 0f;

        var direction = target.position - camTransform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return 0f;

        var angle = Quaternion.LookRotation(direction).eulerAngles.y;
        if (angle > 180f) angle -= 360f;
        return angle;
    }

    private static void SetBlend(float time, CinemachineBlendDefinition.Style style)
    {
        if (CinemachineCore.Instance.BrainCount == 0) return;

        var brain = CinemachineCore.Instance.GetActiveBrain(0);
        if (brain == null) return;
        brain.m_DefaultBlend = new CinemachineBlendDefinition(style, time);
    }

    private void SetLiveCamera(CinemachineVirtualCamera liveVcam)
    {
        foreach (var vcam in AllVcams)
        {
            if (vcam != null) vcam.gameObject.SetActive(vcam == liveVcam);
        }
    }
}
