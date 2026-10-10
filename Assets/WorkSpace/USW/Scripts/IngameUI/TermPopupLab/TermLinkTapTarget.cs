using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 용어 팝업 실험실: 카드 설명 TMP의 용어 링크 탭을 판정한다.
/// 링크 밖을 누르면 누름·뗌·클릭을 부모 카드로 그대로 넘겨, 글자 위를 눌러도 카드 선택과 길게 누르기가 막히지 않는다.
/// 링크 판정은 글자 사각형보다 <see cref="_hitPadding"/>만큼 넓고, 겹치면 가장 가까운 링크를 고른다.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public sealed class TermLinkTapTarget : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    [Tooltip("링크 글자 사각형을 상하좌우로 넓히는 양 (캔버스 기준 단위)")]
    [SerializeField, Min(0f)] private float _hitPadding = 18f;

    private readonly List<Rect> _rects = new List<Rect>();
    private TMP_Text _text;
    private GameObject _forwardTarget;
    private Action<TermLinkTapTarget, string> _onTermTapped;
    private int _linkPointer = int.MinValue;
    private int _pressedLink = -1;
    private Vector2 _down;

    /// <summary>링크가 들어 있는 설명 글자.</summary>
    public TMP_Text Text => _text;

    /// <summary>링크 밖 누름을 넘길 카드와 링크 탭 콜백을 연결한다.</summary>
    public void Configure(GameObject forwardTarget, Action<TermLinkTapTarget, string> onTermTapped, float hitPadding)
    {
        _text = GetComponent<TMP_Text>();
        _text.raycastTarget = true;
        _forwardTarget = forwardTarget;
        _onTermTapped = onTermTapped;
        _hitPadding = hitPadding;
        // 마지막 줄 단어 바로 아래를 누르면 글자 상자 밖(카드 테두리)에 맞아 바로 선택된다 → 글자 상자도 여유만큼 넓힌다.
        // 넓힌 곳의 링크 밖 누름은 어차피 카드로 넘어간다.
        _text.raycastPadding = new Vector4(-hitPadding, -hitPadding, -hitPadding, -hitPadding);
    }

    /// <summary>링크 위 누름은 이 컴포넌트가 갖고, 나머지는 카드로 넘긴다.</summary>
    public void OnPointerDown(PointerEventData data)
    {
        _linkPointer = int.MinValue;
        _pressedLink = -1;
        int link = FindLink(data.position, data.pressEventCamera);
        if (link < 0) { Forward(data, ExecuteEvents.pointerDownHandler); return; }
        _linkPointer = data.pointerId;
        _pressedLink = link;
        _down = data.position;
    }

    /// <summary>링크 누름의 뗌은 클릭을 기다리고, 카드로 넘긴 누름은 뗌도 넘긴다.</summary>
    public void OnPointerUp(PointerEventData data)
    {
        if (data.pointerId == _linkPointer) return;
        Forward(data, ExecuteEvents.pointerUpHandler);
    }

    /// <summary>같은 링크 위에서 끝난 짧은 탭만 용어 탭으로 인정한다.</summary>
    public void OnPointerClick(PointerEventData data)
    {
        if (data.pointerId != _linkPointer) { Forward(data, ExecuteEvents.pointerClickHandler); return; }
        int pressed = _pressedLink;
        _linkPointer = int.MinValue;
        _pressedLink = -1;
        if (data.dragging || Vector2.Distance(_down, data.position) > DragThreshold ||
            FindLink(data.position, data.pressEventCamera) != pressed) return;
        _onTermTapped?.Invoke(this, _text.textInfo.linkInfo[pressed].GetLinkID());
        data.Use();
    }

    /// <summary>링크 ID의 줄별 글자 사각형을 월드 좌표 최소/최대점으로 합쳐 돌려준다.</summary>
    public bool TryGetLinkWorldBounds(string linkId, out Vector3 min, out Vector3 max)
    {
        min = max = Vector3.zero;
        var info = _text.textInfo;
        for (int i = 0; i < info.linkCount; i++)
        {
            if (info.linkInfo[i].GetLinkID() != linkId) continue;
            CollectRects(i);
            if (_rects.Count == 0) return false;
            var union = _rects[0];
            for (int r = 1; r < _rects.Count; r++)
                union = Rect.MinMaxRect(Mathf.Min(union.xMin, _rects[r].xMin), Mathf.Min(union.yMin, _rects[r].yMin),
                                        Mathf.Max(union.xMax, _rects[r].xMax), Mathf.Max(union.yMax, _rects[r].yMax));
            min = _text.rectTransform.TransformPoint(union.min);
            max = _text.rectTransform.TransformPoint(union.max);
            return true;
        }
        return false;
    }

    private float DragThreshold => EventSystem.current != null ? EventSystem.current.pixelDragThreshold : 10f;

    private void Forward<T>(PointerEventData data, ExecuteEvents.EventFunction<T> handler) where T : IEventSystemHandler
    {
        if (_forwardTarget != null) ExecuteEvents.Execute(_forwardTarget, data, handler);
    }

    private int FindLink(Vector2 screen, Camera cam)
    {
        if (_text == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(_text.rectTransform, screen, cam, out var local))
            return -1;
        int best = -1;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < _text.textInfo.linkCount; i++)
        {
            CollectRects(i);
            foreach (var r in _rects)
            {
                float dx = Mathf.Max(r.xMin - local.x, 0f, local.x - r.xMax);
                float dy = Mathf.Max(r.yMin - local.y, 0f, local.y - r.yMax);
                if (dx > _hitPadding || dy > _hitPadding) continue;
                float distance = dx * dx + dy * dy;
                if (distance < bestDistance) { bestDistance = distance; best = i; }
            }
        }
        return best;
    }

    // 링크 하나가 여러 줄에 걸치면 줄마다 사각형 하나 (TMP 로컬 좌표).
    private void CollectRects(int linkIndex)
    {
        _rects.Clear();
        var info = _text.textInfo;
        var link = info.linkInfo[linkIndex];
        int end = Mathf.Min(link.linkTextfirstCharacterIndex + link.linkTextLength, info.characterCount);
        int line = -1;
        Rect current = default;
        for (int c = link.linkTextfirstCharacterIndex; c < end; c++)
        {
            var ch = info.characterInfo[c];
            if (!ch.isVisible) continue;
            var r = Rect.MinMaxRect(ch.bottomLeft.x, ch.descender, ch.topRight.x, ch.ascender);
            if (ch.lineNumber != line)
            {
                if (line >= 0) _rects.Add(current);
                current = r;
                line = ch.lineNumber;
            }
            else
            {
                current = Rect.MinMaxRect(Mathf.Min(current.xMin, r.xMin), Mathf.Min(current.yMin, r.yMin),
                                          Mathf.Max(current.xMax, r.xMax), Mathf.Max(current.yMax, r.yMax));
            }
        }
        if (line >= 0) _rects.Add(current);
    }
}
