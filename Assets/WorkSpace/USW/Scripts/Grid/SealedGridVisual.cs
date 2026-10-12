using UnityEngine;

/// <summary>실제 봉인 상태의 작은 자물쇠. 텍스트나 배치 판정은 추가하지 않는다.</summary>
public sealed class SealedGridVisual : MonoBehaviour
{
    private Canvas _canvas;
    /// <summary>표시가 처음 필요할 때만 월드 UI를 만들고 이후 재사용한다.</summary>
    public void Refresh(SpriteRenderer tile, SealedGridShaderVariant style, bool visible)
    {
        if (_canvas == null && visible && tile != null && style != null)
        {
            var go = new GameObject("SealedGrid_SmallLock", typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(transform, false);
            var bounds = tile.bounds;
            go.transform.position = bounds.center;
            var parentScale = transform.lossyScale;
            const float worldScale = .01f;
            go.transform.localScale = new Vector3(worldScale / Mathf.Max(.001f, Mathf.Abs(parentScale.x)), worldScale / Mathf.Max(.001f, Mathf.Abs(parentScale.y)), worldScale);
            ((RectTransform)go.transform).sizeDelta = (Vector2)bounds.size / worldScale;
            _canvas = go.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.worldCamera = Camera.main;
            _canvas.sortingLayerID = tile.sortingLayerID;
            _canvas.sortingOrder = tile.sortingOrder + 2;
            var graphic = go.AddComponent<SealedGridPatternGraphic>();
            graphic.raycastTarget = false; graphic.Configure(style, true);
        }
        if (_canvas != null) _canvas.gameObject.SetActive(visible);
    }
}
