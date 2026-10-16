using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 회전 5차(R 시안) 공용 월드 범위 표시. 토템 주변 UI 없이 범위 칸만으로 방향을 보여준다.
/// - 칸 채움: 인게임과 같은 TotemRangeStripes 머티리얼(사선) + 칸별 색·각도·흐름 속도(MaterialPropertyBlock)
/// - R1은 사선 셰이더만 표시. 나머지 시안의 외곽선은 범위 칸 묶음의 바깥 둘레만 그리며 회전 스윕이 가능하다.
/// 시안별 연출(스윕·전후 비교·흐름·웨이브)은 Style로 고른다.
/// </summary>
public sealed class TotemRotateLabRangeView
{
    /// <summary>R 시안 종류.</summary>
    public enum Style { OutlineStripes, Sweep, BeforeAfter, Flow, Wave }

    private static readonly int StripeColorId = Shader.PropertyToID("_StripeColor");
    private static readonly int FillColorId = Shader.PropertyToID("_FillColor");
    private static readonly int StripeAngleId = Shader.PropertyToID("_StripeAngle");
    private static readonly int ScrollSpeedId = Shader.PropertyToID("_ScrollSpeed");
    private static readonly int RangeTimeId = Shader.PropertyToID("_TotemRangeUnscaledTime");
    private static readonly Vector2Int[] Sides = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
    private static readonly Color OutlineColor = new Color(0.98f, 0.95f, 0.84f, 0.95f);
    private static readonly Color GhostColor = new Color(1f, 1f, 1f, 0.45f);

    private const float OutlineWidth = 0.06f;
    private const float GhostWidth = 0.035f;
    private const float CancelAlpha = 0.4f;
    private const float SweepSeconds = 0.24f;
    private const float WaveStepSeconds = 0.07f;
    private const float WaveFadeSeconds = 0.12f;
    private const float WavePopScale = 0.84f;
    private const float FlowSpeed = 1.4f;

    private readonly SpriteRenderer[,] _cells;
    private readonly Vector3[,] _cellScales;
    private readonly Material _defaultMaterial;
    private readonly Material _rangeMaterial;
    private readonly Func<int, int, Vector3> _cellCenter;
    private readonly Vector2Int _totemCell;
    private readonly float _cellWorld;
    private readonly Transform _pivot, _ghostPivot;
    private readonly Sprite _pixel;
    private readonly List<SpriteRenderer> _lines = new List<SpriteRenderer>();
    private readonly List<SpriteRenderer> _ghostLines = new List<SpriteRenderer>();
    private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
    private readonly HashSet<Vector2Int> _set = new HashSet<Vector2Int>();
    private readonly List<(Vector2Int cell, float delay)> _wave = new List<(Vector2Int, float)>();

    private Style _style;
    private IReadOnlyList<Vector2Int> _effect, _disabled;
    private Color _tint;
    private int _shownStep = -1;
    private float _pivotFrom, _pivotTo, _sweepAt = -1f, _waveAt = -1f;
    private bool _stripesPending;

    /// <summary>보드 칸 렌더러와 머티리얼, 토템 칸 기준으로 만든다.</summary>
    public TotemRotateLabRangeView(SpriteRenderer[,] cells, Material defaultMaterial, Material rangeMaterial, Sprite pixel,
        Func<int, int, Vector3> cellCenter, Vector2Int totemCell, float cellWorld, Transform parent)
    {
        _cells = cells;
        _defaultMaterial = defaultMaterial;
        _rangeMaterial = rangeMaterial;
        _pixel = pixel;
        _cellCenter = cellCenter;
        _totemCell = totemCell;
        _cellWorld = cellWorld;
        _cellScales = new Vector3[cells.GetLength(0), cells.GetLength(1)];
        for (int c = 0; c < cells.GetLength(0); c++)
        for (int r = 0; r < cells.GetLength(1); r++)
            _cellScales[c, r] = cells[c, r].transform.localScale;
        var center = cellCenter(totemCell.x, totemCell.y) + Vector3.back * 0.02f;
        _pivot = new GameObject("RangeOutlinePivot").transform;
        _pivot.SetParent(parent, false);
        _pivot.position = center;
        _ghostPivot = new GameObject("RangeGhostPivot").transform;
        _ghostPivot.SetParent(parent, false);
        _ghostPivot.position = center;
    }

    /// <summary>
    /// 범위를 다시 그린다. candidate는 회전 모드에서 고른 방향(-1이면 아직 없음/취소), armed는 회전 모드 여부.
    /// 회전 모드가 아니면 확정 방향 범위를 평소처럼 보여준다.
    /// </summary>
    public void Present(Style style, IReadOnlyList<Vector2Int> effect, IReadOnlyList<Vector2Int> disabled, Color tint,
        int facing, int candidate, bool armed, float now)
    {
        bool styleChanged = style != _style;
        _style = style; _effect = effect; _disabled = disabled; _tint = tint;
        bool choosing = armed && candidate >= 0;
        int step = choosing ? candidate : facing;
        bool cancelled = armed && candidate < 0;

        ClearCells();
        SetLines(_ghostLines, 0);

        // R3: 바꾸기 전 범위를 희미한 외곽선으로 남겨 "어디서 → 어디로"를 보여준다.
        if (style == Style.BeforeAfter && choosing && candidate != facing)
            BuildOutline(_ghostLines, _ghostPivot, facing, GhostColor, GhostWidth, 1);
        _ghostPivot.localRotation = Quaternion.Euler(0f, 0f, -90f * facing);

        float alpha = cancelled ? CancelAlpha : 1f;
        bool outline = !cancelled;
        bool previous = _shownStep >= 0 && !styleChanged;

        if (style == Style.Sweep && choosing && previous && _shownStep != step)
        {
            // 외곽선이 이전 방향에서 토템을 축으로 돌아 새 방향에 닿은 뒤에 사선이 채워진다.
            _pivotFrom = _pivot.localEulerAngles.z;
            _pivotTo = _pivotFrom + Mathf.DeltaAngle(_pivotFrom, -90f * step);
            _sweepAt = now;
            _stripesPending = true;
        }
        else
        {
            _sweepAt = -1f;
            _stripesPending = false;
            _pivot.localRotation = Quaternion.Euler(0f, 0f, -90f * step);
        }

        if (style == Style.Wave && choosing && (!previous || _shownStep != step)) StartWave(step, now);
        else _waveAt = -1f;

        if (outline && style != Style.OutlineStripes) BuildOutline(_lines, _pivot, step, OutlineColor, OutlineWidth, 3);
        else SetLines(_lines, 0);
        if (!_stripesPending && _waveAt < 0f) PaintStep(step, alpha, choosing);
        _shownStep = outline ? step : -1;
    }

    /// <summary>표시를 모두 지운다.</summary>
    public void Clear()
    {
        ClearCells();
        SetLines(_lines, 0);
        SetLines(_ghostLines, 0);
        _shownStep = -1;
        _sweepAt = _waveAt = -1f;
        _stripesPending = false;
    }

    /// <summary>매 프레임: 사선 흐름 시계, 스윕 회전, 웨이브 순차 채움.</summary>
    public void Tick(float now)
    {
        Shader.SetGlobalFloat(RangeTimeId, now);
        if (_sweepAt >= 0f)
        {
            float t = Mathf.Clamp01((now - _sweepAt) / SweepSeconds);
            float eased = 1f + 2.2f * Mathf.Pow(t - 1f, 3f) + 1.2f * Mathf.Pow(t - 1f, 2f); // OutBack 근사
            _pivot.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpUnclamped(_pivotFrom, _pivotTo, eased));
            if (t >= 1f)
            {
                _sweepAt = -1f;
                if (_stripesPending) { _stripesPending = false; PaintStep(_shownStep, 1f, true); }
            }
        }
        if (_waveAt >= 0f)
        {
            bool done = true;
            foreach (var (cell, delay) in _wave)
            {
                float t = Mathf.Clamp01((now - _waveAt - delay) / WaveFadeSeconds);
                if (t < 1f) done = false;
                var renderer = _cells[cell.x, cell.y];
                renderer.color = new Color(1f, 1f, 1f, t);
                float pop = t <= 0f ? WavePopScale : Mathf.Lerp(WavePopScale, 1f, 1f - (1f - t) * (1f - t));
                renderer.transform.localScale = _cellScales[cell.x, cell.y] * pop;
            }
            if (done) _waveAt = -1f;
        }
    }

    private void StartWave(int step, float now)
    {
        _wave.Clear();
        PaintStep(step, 0f, true);
        foreach (var o in Cells(step, _effect))
            _wave.Add((o, (Mathf.Abs(o.x - _totemCell.x) + Mathf.Abs(o.y - _totemCell.y) - 1) * WaveStepSeconds));
        foreach (var o in Cells(step, _disabled))
            _wave.Add((o, (Mathf.Abs(o.x - _totemCell.x) + Mathf.Abs(o.y - _totemCell.y) - 1) * WaveStepSeconds));
        _waveAt = now;
    }

    private void PaintStep(int step, float alpha, bool choosing)
    {
        if (step < 0) return;
        bool flow = _style == Style.Flow && choosing;
        // 흐름: 사선이 고른 방향과 수직으로 서서 토템에서 바깥으로 흘러간다(위 90°, 오른쪽 0°, 아래 -90°, 왼쪽 180°).
        float angle = 90f - 90f * step;
        foreach (var cell in Cells(step, _effect)) PaintCell(cell, _tint, alpha, flow, angle);
        foreach (var cell in Cells(step, _disabled)) PaintCell(cell, new Color(0.1f, 0.1f, 0.5f, 1f), alpha, flow, angle);
    }

    private void PaintCell(Vector2Int cell, Color tint, float alpha, bool flow, float angle)
    {
        var renderer = _cells[cell.x, cell.y];
        renderer.sharedMaterial = _rangeMaterial != null ? _rangeMaterial : _defaultMaterial;
        renderer.color = new Color(1f, 1f, 1f, alpha);
        if (_rangeMaterial == null) { renderer.color = new Color(tint.r, tint.g, tint.b, 0.6f * alpha); return; }
        var stripe = tint; stripe.a *= _rangeMaterial.GetColor(StripeColorId).a;
        var fill = tint; fill.a *= _rangeMaterial.GetColor(FillColorId).a;
        renderer.GetPropertyBlock(_block);
        _block.Clear();
        _block.SetColor(StripeColorId, stripe);
        _block.SetColor(FillColorId, fill);
        _block.SetFloat(StripeAngleId, flow ? angle : _rangeMaterial.GetFloat(StripeAngleId));
        _block.SetFloat(ScrollSpeedId, flow ? FlowSpeed : _rangeMaterial.GetFloat(ScrollSpeedId));
        renderer.SetPropertyBlock(_block);
    }

    private void ClearCells()
    {
        for (int c = 0; c < _cells.GetLength(0); c++)
        for (int r = 0; r < _cells.GetLength(1); r++)
        {
            var renderer = _cells[c, r];
            renderer.sharedMaterial = _defaultMaterial;
            renderer.SetPropertyBlock(null);
            renderer.color = new Color(1f, 1f, 1f, 0f);
            renderer.transform.localScale = _cellScales[c, r];
        }
    }

    // 회전 후 보드 안에 들어오는 칸만.
    private IEnumerable<Vector2Int> Cells(int step, IReadOnlyList<Vector2Int> offsets)
    {
        if (offsets == null) yield break;
        foreach (var o in offsets)
        {
            var cell = _totemCell + Rotate(o, step);
            if (cell.x >= 0 && cell.x < _cells.GetLength(0) && cell.y >= 0 && cell.y < _cells.GetLength(1)) yield return cell;
        }
    }

    // 외곽선은 위(step 0) 기준 좌표로 만들고 피벗을 돌린다 → 스윕 회전과 보드 밖 칸 제외를 함께 처리.
    private void BuildOutline(List<SpriteRenderer> pool, Transform pivot, int step, Color color, float width, int order)
    {
        _set.Clear();
        if (_effect != null)
            foreach (var o in _effect)
            {
                var cell = _totemCell + Rotate(o, step);
                if (cell.x >= 0 && cell.x < _cells.GetLength(0) && cell.y >= 0 && cell.y < _cells.GetLength(1)) _set.Add(o);
            }
        int used = 0;
        foreach (var o in _set)
            foreach (var side in Sides)
            {
                if (_set.Contains(o + side)) continue;
                var line = Line(pool, pivot, used++, color, width, order);
                var center = ((Vector2)o + (Vector2)side * 0.5f) * _cellWorld;
                line.transform.localPosition = new Vector3(center.x, center.y, 0f);
                line.transform.localScale = side.x == 0 ? new Vector3(_cellWorld + width, width, 1f) : new Vector3(width, _cellWorld + width, 1f);
            }
        SetLines(pool, used);
    }

    private SpriteRenderer Line(List<SpriteRenderer> pool, Transform pivot, int index, Color color, float width, int order)
    {
        while (pool.Count <= index)
        {
            var renderer = new GameObject("RangeEdge").AddComponent<SpriteRenderer>();
            renderer.transform.SetParent(pivot, false);
            renderer.sprite = _pixel;
            renderer.sortingLayerName = "Default";
            pool.Add(renderer);
        }
        var line = pool[index];
        line.color = color;
        line.sortingOrder = order;
        line.gameObject.SetActive(true);
        return line;
    }

    private static void SetLines(List<SpriteRenderer> pool, int used)
    {
        for (int i = used; i < pool.Count; i++) pool[i].gameObject.SetActive(false);
    }

    // TotemBase.RotateOffset와 같은 90° 시계 방향 회전.
    private static Vector2Int Rotate(Vector2Int offset, int steps)
    {
        for (int i = 0; i < steps; i++) offset = new Vector2Int(offset.y, -offset.x);
        return offset;
    }

    /// <summary>만든 선 오브젝트를 정리한다.</summary>
    public void Dispose()
    {
        if (_pivot != null) UnityEngine.Object.Destroy(_pivot.gameObject);
        if (_ghostPivot != null) UnityEngine.Object.Destroy(_ghostPivot.gameObject);
    }
}
