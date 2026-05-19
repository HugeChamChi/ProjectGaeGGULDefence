using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(CanvasRenderer))]
public class UISpawnLine : Graphic
{
    [Header("Settings")]
    public float duration = 0.4f;
    public float lineLength = 0.3f;
    public int resolution = 20;
    public float width = 10f;
    
    [Tooltip("애니메이션 속도 조절 (예: Ease Out)")]
    public AnimationCurve speedCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("Curve Control")]
    public float curveHeight = 100f;

    private Vector2 startPos;
    private Vector2 endPos;
    private Vector2 controlPoint;
    private float headT = 0f;
    private float tailT = 0f;
    private bool isAnimating = false;

    protected override void Awake()
    {
        base.Awake();
        // Ensure it doesn't block raycasts since it's just an effect
        raycastTarget = false;
        
        // Stretch across parent to ensure vertices aren't clipped if parent has mask/bounds
        if (rectTransform != null)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.sizeDelta = Vector2.zero;
        }
    }

    public void Fire(Vector2 from, Vector2 to)
    {
        startPos = from;
        endPos = to;
        controlPoint = Vector2.Lerp(from, to, 0.5f) + Vector2.up * curveHeight;

        StopAllCoroutines();
        StartCoroutine(AnimateLine());
    }

    private IEnumerator AnimateLine()
    {
        float elapsed = 0f;
        isAnimating = true;
        canvasRenderer.SetAlpha(1f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            
            // 선형 진행도 (0 ~ 1)
            float normalizedTime = Mathf.Clamp01(elapsed / duration);
            
            // AnimationCurve를 통한 가감속(Easing) 적용
            float curveT = speedCurve.Evaluate(normalizedTime);

            headT = Mathf.Clamp01(curveT);
            tailT = Mathf.Clamp01(headT - lineLength);

            SetAllDirty();
            yield return null;
        }

        // 애니메이션이 끝나면 자기 자신을 즉시 풀에 반환(파괴)
        isAnimating = false;
        RM.Destroy(gameObject);
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (!isAnimating) return;

        List<Vector2> points = new List<Vector2>();
        for (int i = 0; i < resolution; i++)
        {
            float t = Mathf.Lerp(tailT, headT, (float)i / (resolution - 1));
            points.Add(MathUtils.Bezier(startPos, controlPoint, endPos, t));
        }

        for (int i = 0; i < points.Count - 1; i++)
        {
            AddLineSegment(vh, points[i], points[i + 1], (float)i / (points.Count - 1));
        }
    }

    private void AddLineSegment(VertexHelper vh, Vector2 p0, Vector2 p1, float t)
    {
        Vector2 dir = (p1 - p0).normalized;
        Vector2 normal = new Vector2(-dir.y, dir.x);
        
        // Taper effect: thinner at the tail
        float currentWidth = width * t;

        Vector2 v0 = p0 - normal * currentWidth * 0.5f;
        Vector2 v1 = p0 + normal * currentWidth * 0.5f;
        Vector2 v2 = p1 + normal * currentWidth * 0.5f;
        Vector3 v3 = p1 - normal * currentWidth * 0.5f;

        Color32 col = color;
        int count = vh.currentVertCount;

        vh.AddVert(v0, col, Vector2.zero);
        vh.AddVert(v1, col, Vector2.up);
        vh.AddVert(v2, col, Vector2.one);
        vh.AddVert(v3, col, Vector2.right);

        vh.AddTriangle(count, count + 1, count + 2);
        vh.AddTriangle(count, count + 2, count + 3);
    }
}
