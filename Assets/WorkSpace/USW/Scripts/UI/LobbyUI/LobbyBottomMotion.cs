using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Settled D decoration and icon motion, independent of page transitions.</summary>
public sealed class LobbyBottomMotion : System.IDisposable
{
    private const float SurfaceSeconds = 0.28f;
    private const float NormalHeight = 200f;
    private const float SelectedHeight = 228f;
    private const float IconRise = 24f;
    private const float IconScale = 1.10f;
    private static readonly Color InactiveIcon = new Color(0.75f, 0.77f, 0.85f);
    private readonly BottomPanelController.PanelButton[] _tabs;
    private readonly LobbyTabFlowGraphic[] _flowSurfaces;
    private readonly RectTransform _plate;
    private readonly Image _marker;
    private Sequence _motion;
    private int _selectedIndex;
    private float _direction = 1f;

    /// <summary>Creates decorations from existing tab references without changing scene wiring.</summary>
    public LobbyBottomMotion(BottomPanelController.PanelButton[] tabs, int selectedIndex)
    {
        _tabs = tabs;
        _selectedIndex = selectedIndex;
        var parent = tabs[0]._mainRect.parent;
        var plateImage = Decoration("SharedSelectionPlate", parent, tabs[selectedIndex]._background.rectTransform.rect.size);
        _plate = plateImage.rectTransform;
        _plate.SetAsFirstSibling();
        plateImage.sprite = tabs[0]._background.sprite;
        plateImage.type = Image.Type.Sliced;
        plateImage.color = new Color(0.46f, 0.31f, 0.82f);
        // The shared moving rect drives each frame's clipped opaque surface.
        // Rendering behind those surfaces would hide its travel between distant tabs.
        plateImage.enabled = false;
        _marker = Decoration("SelectionUnderline", parent, new Vector2(64f, 5f));
        _marker.color = new Color(1f, 0.85f, 0.51f);
        _flowSurfaces = new LobbyTabFlowGraphic[tabs.Length];
        for (int i = 0; i < tabs.Length; i++)
        {
            _flowSurfaces[i] = CreateFlowSurface(tabs[i]);
            var color = tabs[i]._background.color; color.a = 0f;
            tabs[i]._background.color = color;
            if (tabs[i]._selectionFill != null) tabs[i]._selectionFill.fillAmount = 0f;
            if (tabs[i]._innerShadow != null)
            {
                color = tabs[i]._innerShadow.color; color.a = 0f;
                tabs[i]._innerShadow.color = color;
            }
        }
        Canvas.ForceUpdateCanvases();
        Settle(selectedIndex);
    }

    private static Image Decoration(string name, Transform parent, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = size;
        go.GetComponent<LayoutElement>().ignoreLayout = true;
        var image = go.GetComponent<Image>(); image.raycastTarget = false;
        return image;
    }

    /// <summary>Retargets from the displayed position to the latest clicked tab.</summary>
    public void Select(int index)
    {
        if (index == _selectedIndex) return;
        float direction = index > _selectedIndex ? 1f : -1f;
        _direction = direction;
        _selectedIndex = index;
        Stop();
        _motion = DOTween.Sequence().SetUpdate(true);
        _motion.Insert(0f, _plate.DOAnchorPos(PlatePosition(index), SurfaceSeconds).SetEase(Ease.OutCubic));
        _motion.Insert(0f, _marker.rectTransform.DOAnchorPos(MarkerPosition(index), SurfaceSeconds).SetEase(Ease.OutCubic));
        for (int i = 0; i < _tabs.Length; i++) AnimateTab(i, i == index, direction);
        _motion.OnUpdate(RefreshSurfaces);
        _motion.OnComplete(() => _motion = null);
    }

    private void AnimateTab(int index, bool selected, float direction)
    {
        var t = _tabs[index];
        if (t._label != null) _motion.Insert(0f, t._label.DOFade(selected ? 1f : 0.65f, 0.20f));
        if (t._buttonImage == null) return;
        var icon = t._buttonImage.rectTransform;
        var sprite = selected ? t._activeSprite : t._inactiveSprite;
        if (sprite != null) t._buttonImage.sprite = sprite;
        _motion.Insert(0f, t._buttonImage.DOColor(selected ? Color.white : InactiveIcon, 0.20f));
        Vector2 target = t.IconOrigin + Vector2.up * (selected ? IconRise : 0f);
        Vector3 targetScale = t.IconScale * (selected ? IconScale : 1f);
        if (selected)
        {
            _motion.Insert(0f, icon.DOScale(Vector3.Scale(t.IconScale, new Vector3(1.06f, 0.94f, 1f)), 0.055f));
            _motion.Insert(0f, icon.DOAnchorPos(t.IconOrigin + Vector2.down * 4f, 0.055f));
            _motion.Insert(0.055f, icon.DOAnchorPos(target + Vector2.up * 8f, 0.12f).SetEase(Ease.OutCubic));
            _motion.Insert(0.055f, icon.DOScale(t.IconScale * 1.15f, 0.12f).SetEase(Ease.OutCubic));
            _motion.Insert(0.175f, icon.DOAnchorPos(target, 0.165f).SetEase(Ease.OutSine));
            _motion.Insert(0.175f, icon.DOScale(targetScale, 0.165f).SetEase(Ease.OutSine));
        }
        else
        {
            _motion.Insert(0f, icon.DOAnchorPos(target, 0.22f).SetEase(Ease.OutCubic));
            _motion.Insert(0f, icon.DOScale(targetScale, 0.22f).SetEase(Ease.OutCubic));
        }
    }

    /// <summary>Stops owned tweens and restores a settled selection.</summary>
    public void Settle(int index)
    {
        Stop(); _selectedIndex = index;
        for (int i = 0; i < _tabs.Length; i++)
        {
            var t = _tabs[i]; bool selected = i == index;
            t._mainRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, selected ? SelectedHeight : NormalHeight);
            _flowSurfaces[i].Reveal = selected ? 1f : 0f;
            if (t._label != null) t._label.alpha = selected ? 1f : 0.65f;
            if (t._buttonImage == null) continue;
            var sprite = selected ? t._activeSprite : t._inactiveSprite;
            if (sprite != null) t._buttonImage.sprite = sprite;
            t._buttonImage.color = selected ? Color.white : InactiveIcon;
            t._buttonImage.rectTransform.anchoredPosition = t.IconOrigin + Vector2.up * (selected ? IconRise : 0f);
            t._buttonImage.rectTransform.localScale = t.IconScale * (selected ? IconScale : 1f);
        }
        // Background and border share the raised frame; neither may overshoot.
        _plate.sizeDelta = _tabs[index]._background.rectTransform.rect.size;
        _plate.anchoredPosition = PlatePosition(index);
        _marker.rectTransform.anchoredPosition = MarkerPosition(index);
        RefreshSurfaces();
    }

    private void RefreshSurfaces()
    {
        var left = _plate.TransformPoint(new Vector3(_plate.rect.xMin, 0f));
        var right = _plate.TransformPoint(new Vector3(_plate.rect.xMax, 0f));
        for (int i = 0; i < _flowSurfaces.Length; i++)
        {
            var surface = _flowSurfaces[i];
            var rect = surface.rectTransform;
            float width = rect.rect.width;
            if (width <= 0f) continue;
            surface.SetSelectionWindow((rect.InverseTransformPoint(left).x - rect.rect.xMin) / width,
                (rect.InverseTransformPoint(right).x - rect.rect.xMin) / width, _direction);
            // Follow the moving selection, including tabs crossed on distant jumps.
            // Resizing their common parent keeps border, mask and fill together.
            float coverage = Mathf.SmoothStep(0f, 1f, surface.Reveal);
            _tabs[i]._mainRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                Mathf.Lerp(NormalHeight, SelectedHeight, coverage));
        }
    }

    private void Stop() { _motion?.Kill(); _motion = null; }

    /// <summary>Disposes owned tweens and runtime decorations.</summary>
    public void Dispose()
    {
        Stop();
        if (_plate != null) Object.Destroy(_plate.gameObject);
        if (_marker != null) Object.Destroy(_marker.gameObject);
        foreach (var flow in _flowSurfaces)
            if (flow != null) Object.Destroy(flow.transform.parent.gameObject);
    }
    private static LobbyTabFlowGraphic CreateFlowSurface(BottomPanelController.PanelButton tab)
    {
        var maskObject = new GameObject("FlowSurfaceMask", typeof(RectTransform), typeof(Image), typeof(Mask), typeof(LayoutElement));
        var maskRect = (RectTransform)maskObject.transform;
        maskRect.SetParent(tab._mainRect, false);
        var backgroundRect = tab._background.rectTransform;
        maskRect.anchorMin = backgroundRect.anchorMin; maskRect.anchorMax = backgroundRect.anchorMax;
        maskRect.pivot = backgroundRect.pivot;
        maskRect.sizeDelta = backgroundRect.sizeDelta;
        maskRect.anchoredPosition = backgroundRect.anchoredPosition;
        maskRect.SetSiblingIndex(tab._buttonImage.transform.GetSiblingIndex());
        maskObject.GetComponent<LayoutElement>().ignoreLayout = true;
        var maskImage = maskObject.GetComponent<Image>();
        maskImage.sprite = tab._background.sprite; maskImage.type = Image.Type.Sliced; maskImage.raycastTarget = false;
        maskObject.GetComponent<Mask>().showMaskGraphic = false;
        var surfaceObject = new GameObject("FlowSurface", typeof(RectTransform), typeof(CanvasRenderer), typeof(LobbyTabFlowGraphic));
        var surfaceRect = (RectTransform)surfaceObject.transform;
        surfaceRect.SetParent(maskRect, false);
        surfaceRect.anchorMin = Vector2.zero; surfaceRect.anchorMax = Vector2.one;
        surfaceRect.offsetMin = surfaceRect.offsetMax = Vector2.zero;
        var surface = surfaceObject.GetComponent<LobbyTabFlowGraphic>(); surface.raycastTarget = false;
        return surface;
    }

    private Vector2 PlatePosition(int index)
    {
        var parent = (RectTransform)_plate.parent;
        var tab = _tabs[index]._background.rectTransform;
        var bottom = (Vector2)parent.InverseTransformPoint(tab.TransformPoint(new Vector3(tab.rect.center.x, tab.rect.yMin)));
        return bottom - new Vector2(parent.rect.center.x, parent.rect.yMin);
    }
    private Vector2 MarkerPosition(int index) => PlatePosition(index) + Vector2.up * 8f;

}
