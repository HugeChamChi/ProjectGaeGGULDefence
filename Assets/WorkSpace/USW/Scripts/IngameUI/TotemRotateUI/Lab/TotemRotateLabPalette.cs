using UnityEngine;

/// <summary>회전 UI 3차 시안(모던·반투명) 공용 색. 흰 선 + 어두운 반투명 유리 + 민트 포인트 하나(회전 배지 AvailableRing과 같은 색).</summary>
public static class TotemRotateLabPalette
{
    /// <summary>어두운 반투명 유리 면.</summary>
    public static readonly Color Glass = new Color(0.05f, 0.08f, 0.12f, 0.42f);
    /// <summary>더 옅은 유리 면.</summary>
    public static readonly Color GlassLight = new Color(0.05f, 0.08f, 0.12f, 0.22f);
    /// <summary>가는 흰 선.</summary>
    public static readonly Color Hairline = new Color(1f, 1f, 1f, 0.55f);
    /// <summary>희미한 흰 선(대기 상태).</summary>
    public static readonly Color Faint = new Color(1f, 1f, 1f, 0.22f);
    /// <summary>선택된 요소.</summary>
    public static readonly Color Active = new Color(1f, 1f, 1f, 0.96f);
    /// <summary>민트 포인트.</summary>
    public static readonly Color Accent = new Color(0.45f, 1f, 0.85f, 1f);
    /// <summary>흰 바탕 위 글리프.</summary>
    public static readonly Color Ink = new Color(0.07f, 0.1f, 0.14f, 1f);
    /// <summary>반투명 흰 원판(명일방주식).</summary>
    public static readonly Color Veil = new Color(1f, 1f, 1f, 0.1f);

    /// <summary>알파만 바꾼 색.</summary>
    public static Color WithAlpha(Color color, float alpha) { color.a = alpha; return color; }
}
