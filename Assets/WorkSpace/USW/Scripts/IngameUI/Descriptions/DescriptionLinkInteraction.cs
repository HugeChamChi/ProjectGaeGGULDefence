using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>One captured term gesture; a cancelled term never becomes a card click.</summary>
public sealed class DescriptionLinkInteraction
{
    // Text-local UI units follow the authored canvas scale across resolutions.
    private const float TouchPadding = 18f;
    private TMP_Text _text;
    private int? _pointer;
    private string _id;
    private Vector2 _down;
    private float _started;
    private bool _cancelled;
    public bool HasPress => _pointer.HasValue;
    public bool TryBegin(TMP_Text text, PointerEventData data)
    {
        if (_pointer.HasValue) { _cancelled = true; return false; }
        if (Input.touchCount > 1 || data.button != PointerEventData.InputButton.Left || text == null) return false;
        int link = FindLink(text, data.position, data.pressEventCamera);
        if (link < 0) return false;
        _text = text; _pointer = data.pointerId; _id = text.textInfo.linkInfo[link].GetLinkID();
        _down = data.position; _started = Time.unscaledTime; _cancelled = false;
        return true;
    }
    public void Observe(PointerEventData data)
    {
        float threshold = EventSystem.current != null ? EventSystem.current.pixelDragThreshold : 10f;
        if (data.pointerId != _pointer || data.dragging || Vector2.Distance(data.position, _down) > threshold) _cancelled = true;
    }
    public bool TryRelease(PointerEventData data, float maxSeconds, out string id)
    {
        id = null;
        if (_pointer != data.pointerId) return false;
        Observe(data);
        int link = FindLink(_text, data.position, data.pressEventCamera);
        bool valid = !_cancelled && Input.touchCount <= 1 && Time.unscaledTime - _started < maxSeconds &&
            link >= 0 && _text.textInfo.linkInfo[link].GetLinkID() == _id;
        if (valid) id = _id;
        Reset();
        return valid;
    }
    public void Tick() { if (Input.touchCount > 1) _cancelled = true; }
    public void Cancel() { _cancelled = true; }
    public void Reset() { _pointer = null; _id = null; _text = null; _cancelled = false; }

    private static int FindLink(TMP_Text text, Vector2 screenPoint, Camera camera)
    {
        if (text == null) return -1;
        int exact = TMP_TextUtilities.FindIntersectingLink(text, screenPoint, camera);
        if (exact >= 0) return exact;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(text.rectTransform, screenPoint, camera, out var point)) return -1;

        int nearest = -1;
        float nearestDistance = TouchPadding * TouchPadding;
        var info = text.textInfo;
        for (int linkIndex = 0; linkIndex < info.linkCount; linkIndex++)
        {
            var link = info.linkInfo[linkIndex];
            int end = Mathf.Min(link.linkTextfirstCharacterIndex + link.linkTextLength, info.characterCount);
            for (int first = link.linkTextfirstCharacterIndex; first < end;)
            {
                int line = info.characterInfo[first].lineNumber;
                float left = float.PositiveInfinity, right = float.NegativeInfinity;
                float bottom = float.PositiveInfinity, top = float.NegativeInfinity;
                do
                {
                    var character = info.characterInfo[first++];
                    left = Mathf.Min(left, character.bottomLeft.x);
                    right = Mathf.Max(right, character.topRight.x);
                    bottom = Mathf.Min(bottom, character.descender);
                    top = Mathf.Max(top, character.ascender);
                } while (first < end && info.characterInfo[first].lineNumber == line);
                // Wrapped lines retain separate regions; gaps are not one large link rectangle.
                float dx = Mathf.Max(Mathf.Max(left - point.x, point.x - right), 0f);
                float dy = Mathf.Max(Mathf.Max(bottom - point.y, point.y - top), 0f);
                float distance = dx * dx + dy * dy;
                if (distance >= nearestDistance) continue;
                nearestDistance = distance;
                nearest = linkIndex;
            }
        }
        return nearest;
    }
}
