using UnityEngine;
using UnityEngine.UI;

/// <summary>성급 배지와 셰이더 게이지를 한 프리팹으로 표시합니다.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class UnitStatusGraphic : MaskableGraphic
{
    [Header("Layout (prefab UI units)")]
    [SerializeField, Min(4f)] private float _badgeSize = 20f;
    [SerializeField] private float _badgeGap = -4f;
    [SerializeField, Min(4f)] private float _gaugeHeight = 12f;
    [Header("Cell safe area")]
    [SerializeField, Range(0.3f, 0.9f)] private float _cellWidthFraction = 0.76f;
    [SerializeField, Range(0.05f, 0.25f)] private float _cellBottomInset = 0.1f;
    [Header("Badge palette (gauge colors: Material)")]
    [SerializeField] private Color _frame = new Color32(15, 24, 24, 255);
    [SerializeField] private Color _badge = new Color32(29, 40, 39, 255);
    [SerializeField] private Color _legend = new Color32(255, 210, 84, 255);
    [Header("Prefab preview only")]
    [SerializeField] private Tier _previewTier = Tier.Legend;
    [SerializeField, Range(0f, 1f)] private float _previewProgress = 0.65f;
    [SerializeField] private bool _previewPassive;
    private static readonly string[] Digits = { "010110010010111", "111001111100111", "111001111001111" };
    private Vector3 _authoredScale;
    private Tier _tier;
    private float _progress;
    private bool _passive;
    private bool _bound;
    private DragHandler _dragHandler;

    /// <summary>이 표시에 연결된 유닛입니다.</summary>
    public UnitBase Unit { get; private set; }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (canvas != null)
            canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2;
    }

    /// <summary>실제 유닛을 연결하고 프리팹의 크기와 스케일을 유지합니다.</summary>
    public void Bind(UnitBase unit)
    {
        Unit = unit;
        _dragHandler = unit != null ? unit.GetComponent<DragHandler>() : null;
        _bound = true;
        _authoredScale = rectTransform.localScale;
        raycastTarget = false;
        Refresh();
        SetVerticesDirty();
    }

    /// <summary>실제 배치, 성급, 쿨다운을 반영합니다. 숨겨진 표시도 소유자가 갱신합니다.</summary>
    public void Refresh()
    {
        bool visible = Unit != null && Unit.isActiveAndEnabled && Unit.currentCell != null && Unit.currentCell.OccupyingUnit == Unit;
        if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
        if (!visible) return;
        PositionWithinCell();
        bool passive = !Unit.HasSkillCooldownGauge;
        float progress = passive ? 0f : Unit.SkillGaugeProgress;
        if (float.IsNaN(progress) || float.IsInfinity(progress)) progress = 0f;
        progress = Mathf.Clamp01(progress);
        if (_tier == Unit.currentTier && _passive == passive && Mathf.Approximately(_progress, progress)) return;
        _tier = Unit.currentTier;
        _passive = passive;
        _progress = progress;
        SetVerticesDirty();
    }

    private void PositionWithinCell()
    {
        var parent = rectTransform.parent;
        Vector2 size = rectTransform.rect.size;
        if (!Unit.currentCell.TryGetVisualBounds(out var bounds))
        {
            rectTransform.localScale = _authoredScale;
            rectTransform.position = Unit.transform.position;
            return;
        }
        var min = parent.InverseTransformPoint(bounds.min);
        var max = parent.InverseTransformPoint(bounds.max);
        float width = size.x * Mathf.Abs(_authoredScale.x);
        float height = size.y * Mathf.Abs(_authoredScale.y);
        float factor = Mathf.Min(1f, (max.x - min.x) * _cellWidthFraction / Mathf.Max(width, 0.0001f),
            (max.y - min.y) * (1f - 2f * _cellBottomInset) / Mathf.Max(height, 0.0001f));
        rectTransform.localScale = _authoredScale * factor;
        var pivot = rectTransform.pivot;
        rectTransform.localPosition = new Vector3((min.x + max.x) * 0.5f + (pivot.x - 0.5f) * width * factor,
            min.y + (max.y - min.y) * _cellBottomInset + pivot.y * height * factor, 0f);
        // 배치 중에는 셀 안전 영역을 유지하고, 드래그 중에는 유닛과 같은 월드 이동량을 적용합니다.
        if (_dragHandler != null) rectTransform.position += _dragHandler.DragOffset;
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        var rect = rectTransform.rect;
        if (rect.width < 4f || rect.height < 4f) return;
        var tier = _bound ? _tier : _previewTier;
        float progress = _bound ? _progress : _previewProgress;
        bool passive = _bound ? _passive : _previewPassive;
        float badgeSize = Mathf.Min(_badgeSize, rect.height - 2f, rect.width - 2f);
        float gaugeStart = Mathf.Clamp(badgeSize + _badgeGap, 0f, rect.width - 3f);
        float gaugeHeight = Mathf.Min(_gaugeHeight, rect.height - 2f);
        var gauge = new Rect(rect.xMin + gaugeStart, rect.center.y - gaugeHeight * 0.5f, rect.width - gaugeStart - 1f, gaugeHeight);
        Box(mesh, new Rect(gauge.x + 1f, gauge.y - 1f, gauge.width, gauge.height), new Color32(0, 0, 0, 90));
        // The full quad stays fixed. UV1 carries progress/state; the shader selects the filled pixels.
        Box(mesh, gauge, _frame, new Vector2(progress, passive ? 2f : 1f),
            new Vector2(Mathf.Min(0.45f, 2f / gauge.width), Mathf.Min(0.45f, 2f / gauge.height)));
        var badge = new Rect(rect.xMin, rect.center.y - badgeSize * 0.5f, badgeSize, badgeSize);
        Box(mesh, new Rect(badge.x, badge.y - 1f, badge.width, badge.height), new Color32(0, 0, 0, 90));
        Box(mesh, badge, _frame);
        Box(mesh, new Rect(badge.x + 2f, badge.y + 2f, Mathf.Max(0f,badge.width - 4f), Mathf.Max(0f,badge.height - 4f)), _badge);
        float scale = badgeSize / 20f;
        if (tier == Tier.Legend) Star(mesh, badge.center, scale);
        else if (tier == Tier.Chieftain) Crown(mesh, badge.position, scale);
        else
        {
            string pixels = Digits[Mathf.Clamp((int)tier, 0, 2)];
            for (int row = 0; row < 5; row++)
            for (int col = 0; col < 3; col++)
                if (pixels[row * 3 + col] == '1')
                    Box(mesh, new Rect(badge.x + (6.4f + col * 2.4f) * scale, badge.y + (4f + (4 - row) * 2.4f) * scale, 2.4f * scale, 2.4f * scale), Color.white);
        }
    }

    private void Star(VertexHelper mesh, Vector2 center, float scale)
    {
        int start = mesh.currentVertCount;
        Vertex(mesh, center, _legend);
        for (int i = 0; i < 10; i++)
        {
            float angle = (90f - i * 36f) * Mathf.Deg2Rad;
            float radius = (i % 2 == 0 ? 7.8f : 3.7f) * scale;
            Vertex(mesh, center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, _legend);
        }
        for (int i = 0; i < 10; i++) mesh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % 10);
    }

    private void Crown(VertexHelper mesh, Vector2 origin, float scale)
    {
        Box(mesh, new Rect(origin.x + 4 * scale, origin.y + 4 * scale, 12 * scale, 4 * scale), _legend);
        Triangle(mesh, origin + new Vector2(4,8)*scale, origin + new Vector2(3,15)*scale, origin + new Vector2(9,8)*scale);
        Triangle(mesh, origin + new Vector2(6,8)*scale, origin + new Vector2(10,17)*scale, origin + new Vector2(14,8)*scale);
        Triangle(mesh, origin + new Vector2(11,8)*scale, origin + new Vector2(17,15)*scale, origin + new Vector2(16,8)*scale);
    }

    private void Triangle(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c)
    {
        int start = mesh.currentVertCount;
        Vertex(mesh,a,_legend); Vertex(mesh,b,_legend); Vertex(mesh,c,_legend);
        mesh.AddTriangle(start,start+1,start+2);
    }

    private void Box(VertexHelper mesh, Rect rect, Color tint, Vector2 state = default, Vector2 border = default)
    {
        if (rect.width <= 0f || rect.height <= 0f) return;
        int start = mesh.currentVertCount;
        Vertex(mesh,new Vector2(rect.xMin,rect.yMin),tint,new Vector2(0,0),state,border);
        Vertex(mesh,new Vector2(rect.xMin,rect.yMax),tint,new Vector2(0,1),state,border);
        Vertex(mesh,new Vector2(rect.xMax,rect.yMax),tint,new Vector2(1,1),state,border);
        Vertex(mesh,new Vector2(rect.xMax,rect.yMin),tint,new Vector2(1,0),state,border);
        mesh.AddTriangle(start,start+1,start+2); mesh.AddTriangle(start+2,start+3,start);
    }

    private void Vertex(VertexHelper mesh, Vector2 point, Color tint, Vector2 uv = default, Vector2 state = default, Vector2 border = default)
    {
        var vertex = UIVertex.simpleVert;
        vertex.position = point; vertex.color = tint * color;
        vertex.uv0 = uv; vertex.uv1 = state; vertex.uv2 = border;
        mesh.AddVert(vertex);
    }
}
