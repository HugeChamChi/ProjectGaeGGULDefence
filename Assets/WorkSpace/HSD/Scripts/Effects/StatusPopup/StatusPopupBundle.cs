using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 한 대상에 함께 걸린 버프·디버프 문구를 세로로 줄 세워 띄우는 묶음 (사용자 확정 A안, 2026-10-22).
/// 줄마다 통 튀며 등장 → 마지막 줄 이후 잠깐 유지 → 묶음 전체가 위로 올라가며 위쪽부터 그라데이션으로 지워진다.
/// 계층은 코드로 만든다: Bundle → Mover(RectMask2D, 세로 softness) → Content(VerticalLayoutGroup) → Line(라벨 + 화살표).
/// 위치(대상 추적)는 StatusPopupSpawner가 Bundle에 쓰고, 퇴장 상승은 Mover가 맡아 서로 덮어쓰지 않는다.
/// </summary>
public class StatusPopupBundle : MonoBehaviour
{
    // 마스크는 묶음보다 넉넉히 커서, 퇴장 중 넓은 softness 띠가 아래쪽 경계에서는 글자에 닿지 않는다.
    private const float MaskWidth = 720f;
    private const float MaskHeight = 1000f;

    /// <summary>묶음이 다 사라졌을 때. 스포너가 풀로 돌려받는다.</summary>
    public event Action<StatusPopupBundle> OnFinished;

    /// <summary>퇴장 중이면 더 이상 줄을 받지 않는다.</summary>
    public bool IsExiting { get; private set; }

    private StatusPopupStyle _style;
    private RectTransform _mover;
    private RectTransform _content;
    private RectMask2D _mask;
    private readonly List<Line> _lines = new List<Line>();
    private int _usedLines;
    private float _nextLineTime;
    private float _enterEndTime;
    private Tween _holdTimer;
    private Sequence _exit;

    private class Line
    {
        public RectTransform Root;
        public TextMeshProUGUI Label;
        public RectTransform Arrow;
        public Image ArrowStroke;
        public Image ArrowFill;
        public string Text;
        public bool IsBuff;
    }

    /// <summary>풀에서 꺼낸 묶음을 비우고 새로 시작한다.</summary>
    public void Begin(StatusPopupStyle style)
    {
        _style = style;
        EnsureBuilt();
        KillTweens();
        IsExiting = false;
        _usedLines = 0;
        foreach (var line in _lines) line.Root.gameObject.SetActive(false);
        _mover.anchoredPosition = Vector2.zero;
        _mask.padding = Vector4.zero;
        _mask.softness = Vector2Int.zero;
        var layout = _content.GetComponent<VerticalLayoutGroup>();
        layout.spacing = style.lineSpacing;
        _nextLineTime = Time.time;
        _enterEndTime = Time.time;
        gameObject.SetActive(true);
    }

    /// <summary>
    /// 문구 한 줄을 더한다. 같은 문구가 이미 있으면 그 줄만 통 튀고, 줄이 꽉 차면 유지 시간만 늘린다.
    /// 퇴장 중이면 false — 호출자가 새 묶음을 열어야 한다.
    /// </summary>
    /// <param name="isBuff">true = ▲(버프 재질), false = ▼(디버프 재질)</param>
    /// <param name="color">글자·화살표 색. null이면 스타일의 버프/디버프 색</param>
    public bool Add(string text, bool isBuff, Color? color = null)
    {
        if (IsExiting || _style == null) return false;

        var existing = Find(text, isBuff);
        if (existing != null)
        {
            existing.Root.DOKill(true);
            existing.Root.localScale = Vector3.one;
            existing.Root.DOPunchScale(Vector3.one * _style.repeatPunch, _style.repeatPunchDuration, 1, 0f);
            RestartHold();
            return true;
        }

        if (_usedLines >= Mathf.Max(1, _style.maxLines))
        {
            RestartHold();
            return true;
        }

        var line = GetLine(_usedLines++);
        Apply(line, text, isBuff, color ?? (isBuff ? _style.buffColor : _style.debuffColor));
        line.Root.SetSiblingIndex(_usedLines - 1);
        line.Root.gameObject.SetActive(true);

        // 연달아 들어온 줄은 lineStagger 간격으로 차례로 튀어나온다.
        float now = Time.time;
        float delay = Mathf.Max(0f, _nextLineTime - now);
        _nextLineTime = now + delay + _style.lineStagger;
        _enterEndTime = Mathf.Max(_enterEndTime, now + delay + _style.enterDuration);

        line.Root.DOKill();
        line.Root.localScale = Vector3.zero;
        line.Root.DOScale(1f, _style.enterDuration).SetEase(Ease.OutBack, _style.enterOvershoot).SetDelay(delay);
        RestartHold();
        return true;
    }

    private Line Find(string text, bool isBuff)
    {
        for (int i = 0; i < _usedLines; i++)
            if (_lines[i].IsBuff == isBuff && _lines[i].Text == text) return _lines[i];
        return null;
    }

    private void RestartHold()
    {
        _holdTimer?.Kill();
        float wait = Mathf.Max(0f, _enterEndTime - Time.time) + _style.holdDuration;
        _holdTimer = DOVirtual.DelayedCall(wait, BeginExit, false).SetLink(gameObject);
    }

    private void BeginExit()
    {
        _holdTimer = null;
        IsExiting = true;

        // 웹 시안 A와 같은 방식: 묶음 높이만 한 그라데이션 띠가 묶음 위에서 아래로 지나가며 위쪽부터 부드럽게 지운다.
        // 띠의 불투명 끝이 글자 위 끝에 있을 때(전부 보임) → 띠의 투명 끝이 글자 아래 끝에 닿을 때(전부 사라짐).
        // UI/TMP 셰이더의 RectMask2D softness는 경계에서 softness의 절반 거리에서 이미 알파 1이 된다 → 실제 띠 폭 = softness / 2.
        LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
        float half = MaskHeight * 0.5f;
        float contentHeight = _content.rect.height;
        float band = Mathf.Max(1f, contentHeight * _style.fadeSoftnessRatio);
        _mask.softness = new Vector2Int(0, Mathf.RoundToInt(band * 2f));
        float from = Mathf.Max(0f, half - contentHeight * 0.5f - band);
        float to = half + contentHeight * 0.5f;
        SetTopPadding(from);

        _exit = DOTween.Sequence().SetLink(gameObject);
        _exit.Join(_mover.DOAnchorPosY(_style.exitRise, _style.exitRiseDuration).SetEase(Ease.InQuad));
        _exit.Join(DOVirtual.Float(from, to, _style.exitDuration, SetTopPadding).SetEase(Ease.InQuad));
        _exit.OnComplete(Finish);
    }

    private void SetTopPadding(float top)
    {
        var padding = _mask.padding;
        padding.w = top;
        _mask.padding = padding;
    }

    private void Finish()
    {
        _exit = null;
        gameObject.SetActive(false);
        OnFinished?.Invoke(this);
    }

    private void KillTweens()
    {
        _holdTimer?.Kill();
        _holdTimer = null;
        _exit?.Kill();
        _exit = null;
        if (_mover != null) _mover.DOKill();
        foreach (var line in _lines) line.Root.DOKill();
    }

    private void OnDisable()
    {
        // 퇴장 완료가 아닌 비활성화(씬 종료 등)에서도 트윈을 남기지 않는다.
        if (_exit != null || _holdTimer != null) KillTweens();
    }

    private void Apply(Line line, string text, bool isBuff, Color color)
    {
        line.Text = text;
        line.IsBuff = isBuff;

        var label = line.Label;
        if (_style.font != null) label.font = _style.font;
        var material = isBuff ? _style.buffMaterial : _style.debuffMaterial;
        if (material != null) label.fontSharedMaterial = material;
        label.fontSize = _style.fontSize;
        label.color = color;
        label.text = text;

        var arrowLayout = line.Arrow.GetComponent<LayoutElement>();
        arrowLayout.preferredWidth = arrowLayout.preferredHeight = _style.arrowSize;
        line.Arrow.localEulerAngles = new Vector3(0f, 0f, isBuff ? 0f : 180f);
        line.ArrowStroke.sprite = _style.arrowStroke;
        line.ArrowStroke.color = _style.arrowStrokeColor;
        line.ArrowFill.sprite = _style.arrowFill;
        line.ArrowFill.color = color;
        line.ArrowStroke.enabled = _style.arrowStroke != null;
        line.ArrowFill.enabled = _style.arrowFill != null;

        line.Root.GetComponent<HorizontalLayoutGroup>().spacing = _style.arrowSpacing;
    }

    private Line GetLine(int index)
    {
        while (_lines.Count <= index) _lines.Add(CreateLine());
        return _lines[index];
    }

    private Line CreateLine()
    {
        var root = NewRect("Line", _content);
        var row = root.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.childAlignment = TextAnchor.MiddleCenter;
        row.childControlWidth = row.childControlHeight = true;
        row.childForceExpandWidth = row.childForceExpandHeight = false;

        var label = NewRect("Label", root).gameObject.AddComponent<TextMeshProUGUI>();
        label.raycastTarget = false;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;

        var arrow = NewRect("Arrow", root);
        arrow.gameObject.AddComponent<LayoutElement>();
        var stroke = arrow.gameObject.AddComponent<Image>();
        stroke.raycastTarget = false;
        stroke.preserveAspect = true;
        var fillRect = NewRect("Fill", arrow);
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.sizeDelta = Vector2.zero;
        var fill = fillRect.gameObject.AddComponent<Image>();
        fill.raycastTarget = false;
        fill.preserveAspect = true;

        root.gameObject.SetActive(false);
        return new Line { Root = root, Label = label, Arrow = arrow, ArrowStroke = stroke, ArrowFill = fill };
    }

    private void EnsureBuilt()
    {
        if (_mover != null) return;

        var self = (RectTransform)transform;
        self.sizeDelta = Vector2.zero;

        _mover = NewRect("Mover", self);
        _mover.sizeDelta = new Vector2(MaskWidth, MaskHeight);
        _mask = _mover.gameObject.AddComponent<RectMask2D>();

        _content = NewRect("Content", _mover);
        var column = _content.gameObject.AddComponent<VerticalLayoutGroup>();
        column.childAlignment = TextAnchor.MiddleCenter;
        column.childControlWidth = column.childControlHeight = true;
        column.childForceExpandWidth = column.childForceExpandHeight = false;
        var fitter = _content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        return rect;
    }
}
