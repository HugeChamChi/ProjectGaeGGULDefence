using Spine.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>UI-only drone silhouettes and stage progress on the persistent transition canvas.</summary>
public sealed class TransitionLoadingView : MonoBehaviour
{
    [SerializeField] private CanvasGroup group;
    [SerializeField] private Slider progressBar;
    [SerializeField] private TMP_Text status;
    [SerializeField] private RectTransform[] droneHosts;
    [SerializeField] private SkeletonDataAsset[] droneData;
    [SerializeField] private Material silhouetteMaterial;
    private Vector2[] _origins;
    private float _target;
    private bool _loading;

    private void Awake()
    {
        group.alpha = 0f;
        group.blocksRaycasts = false;
        _origins = new Vector2[droneHosts.Length];
        for (int i = 0; i < droneHosts.Length; i++)
        {
            var host = droneHosts[i];
            _origins[i] = host.anchoredPosition;
            var components = SkeletonGraphic.NewSkeletonGraphicGameObject(droneData[i], host, silhouetteMaterial);
            var graphic = components.skeletonRenderer;
            var animation = components.skeletonAnimation;
            graphic.name = "LoadingDrone";
            graphic.allowMultipleCanvasRenderers = true;
            graphic.additiveMaterial = graphic.multiplyMaterial = graphic.screenMaterial = silhouetteMaterial;
            graphic.raycastTarget = false;
            graphic.maskable = false;
            animation.unscaledTime = true;
            var track = animation.AnimationState.SetAnimation(0, "idle", true);
            track.TrackTime = i * 0.27f;
            animation.UpdateComplete += HideGroundEffects;
            animation.Update(0);
            HideGroundEffects(graphic);
            graphic.MatchRectTransformWithBounds();
            var bounds = graphic.rectTransform.rect.size;
            float scale = Mathf.Min(host.rect.width / bounds.x, host.rect.height / bounds.y) * 0.9f;
            var rect = graphic.rectTransform;
            rect.anchorMin = rect.anchorMax = Vector2.one * 0.5f;
            rect.localScale = Vector3.one * scale;
            rect.anchoredPosition = Vector2.Scale(rect.pivot - Vector2.one * 0.5f, bounds) * scale;
        }
    }

    public void Begin()
    {
        gameObject.SetActive(true);
        _loading = true;
        progressBar.value = _target = 0f;
        status.text = "출격 준비 중";
    }

    public void Report(float progress, string message)
    {
        if (!_loading || progress < _target) return;
        _target = Mathf.Clamp01(progress);
        status.text = message;
        if (_target >= 1f) progressBar.value = 1f;
    }

    public void SetVisibility(float amount)
    {
        bool becomingVisible = group.alpha == 0f && amount > 0f;
        group.alpha = Mathf.SmoothStep(0f, 1f, amount);
        if (!becomingVisible) return;
        progressBar.GetComponent<Graphic>().SetAllDirty();
        progressBar.targetGraphic.SetAllDirty();
        status.SetAllDirty();
        Canvas.ForceUpdateCanvases();
    }
    public void Hide() { _loading = false; group.alpha = 0f; gameObject.SetActive(false); }

    private void Update()
    {
        if (!_loading) return;
        progressBar.value = Mathf.MoveTowards(progressBar.value, _target, Time.unscaledDeltaTime * 0.7f);
        for (int i = 0; i < droneHosts.Length; i++)
        {
            float phase = Time.unscaledTime * 2f - i * 0.8f;
            droneHosts[i].anchoredPosition = _origins[i] + Vector2.up * (Mathf.Sin(phase) * 6f);
        }
    }

    private static void HideGroundEffects(ISkeletonRenderer renderer)
    {
        foreach (var slot in renderer.Skeleton.Slots)
            if (slot.Data.Name == "hover_light" || slot.Data.Name == "shadow") slot.Pose.Attachment = null;
    }
}
