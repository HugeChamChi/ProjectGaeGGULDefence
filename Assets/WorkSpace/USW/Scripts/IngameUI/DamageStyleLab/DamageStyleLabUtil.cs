using TMPro;
using UnityEngine;

/// <summary>데미지 표시 테스트 공용 도우미: 보스 화면 영역 계산, 좌표 변환, 숫자 글자 만들기, 숫자 표기.</summary>
public static class DamageStyleLabUtil
{
    // 보스가 없을 때 쓰는 가짜 영역 (화면 비율)
    private static readonly Vector2 FallbackCenter = new Vector2(0.5f, 0.62f);
    private static readonly Vector2 FallbackSize = new Vector2(0.4f, 0.2f);
    private static readonly Vector2 TextRectSize = new Vector2(900f, 240f);

    /// <summary>보스 스프라이트가 화면에서 차지하는 영역(화면 픽셀). 보스가 없으면 화면 위쪽 가운데의 가짜 영역.</summary>
    public static Rect BossScreenRect(Transform boss, SpriteRenderer renderer)
    {
        var cam = Camera.main;
        if (boss != null && cam != null)
        {
            if (renderer != null)
            {
                var b = renderer.bounds;
                Vector2 min = RectTransformUtility.WorldToScreenPoint(cam, b.min);
                Vector2 max = RectTransformUtility.WorldToScreenPoint(cam, b.max);
                return Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
            }
            Vector2 p = RectTransformUtility.WorldToScreenPoint(cam, boss.position);
            return new Rect(p.x - Screen.width * FallbackSize.x * 0.5f, p.y - Screen.height * FallbackSize.y * 0.5f,
                Screen.width * FallbackSize.x, Screen.height * FallbackSize.y);
        }
        var size = new Vector2(Screen.width * FallbackSize.x, Screen.height * FallbackSize.y);
        var center = new Vector2(Screen.width * FallbackCenter.x, Screen.height * FallbackCenter.y);
        return new Rect(center - size * 0.5f, size);
    }

    /// <summary>화면 픽셀 → 컨테이너 로컬 좌표 (중앙 앵커 기준).</summary>
    public static Vector2 ScreenToLocal(RectTransform container, Vector2 screen)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(container, screen, UiCamera(container), out var local);
        return local;
    }

    /// <summary>컨테이너 로컬 1단위가 화면 몇 픽셀인지.</summary>
    public static float LocalToScreenScale(RectTransform container)
    {
        var cam = UiCamera(container);
        Vector3 w0 = container.TransformPoint(Vector3.zero);
        Vector3 w1 = container.TransformPoint(new Vector3(0f, 100f, 0f));
        Vector2 p0 = cam == null ? (Vector2)w0 : RectTransformUtility.WorldToScreenPoint(cam, w0);
        Vector2 p1 = cam == null ? (Vector2)w1 : RectTransformUtility.WorldToScreenPoint(cam, w1);
        return Mathf.Max(0.0001f, Mathf.Abs(p1.y - p0.y) / 100f);
    }

    private static Camera UiCamera(RectTransform container)
    {
        var canvas = container != null ? container.GetComponentInParent<Canvas>() : null;
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay) return null;
        return canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
    }

    /// <summary>컨테이너를 꽉 채우는 빈 묶음 오브젝트 (방식별로 하나).</summary>
    public static RectTransform CreateRoot(RectTransform container, string name)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(container, false);
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    /// <summary>숫자 한 개 (TMP + 투명도용 CanvasGroup). 중앙 앵커, 비활성으로 만든다.</summary>
    public static TextMeshProUGUI CreateText(RectTransform parent, string name, Vector2 pivot, TextAlignmentOptions alignment, out CanvasGroup group)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = pivot;
        rect.sizeDelta = TextRectSize;
        group = go.GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        var text = go.AddComponent<TextMeshProUGUI>();
        text.raycastTarget = false;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.enableVertexGradient = true;
        text.color = Color.white;
        go.SetActive(false);
        return text;
    }

    /// <summary>작은 숫자용 짧은 표기: 12,345 / 123.4K / 1.23M.</summary>
    public static string Short(decimal amount)
    {
        if (amount < 10m) return amount.ToString("0.#");
        if (amount < 100_000m) return amount.ToString("N0");
        if (amount < 10_000_000m) return (amount / 1_000m).ToString("0.#") + "K";
        if (amount < 10_000_000_000m) return (amount / 1_000_000m).ToString("0.##") + "M";
        return (amount / 1_000_000_000m).ToString("0.##") + "B";
    }

    /// <summary>큰 숫자용 전체 표기: 1,234,567.</summary>
    public static string Full(decimal amount) => amount < 10m ? amount.ToString("0.#") : decimal.Round(amount).ToString("N0");

    /// <summary>0 → 1+overshoot → 1 로 튀는 등장 크기.</summary>
    public static float Pop(float age, float popSeconds, float overshoot)
    {
        if (popSeconds <= 0f) return 1f;
        if (age < popSeconds) return Mathf.Lerp(0f, 1f + overshoot, age / popSeconds);
        return 1f + overshoot * Mathf.Max(0f, 1f - (age - popSeconds) / popSeconds);
    }
}
