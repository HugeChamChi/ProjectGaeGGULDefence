using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>One captured term gesture; a cancelled term never becomes a card click.</summary>
public sealed class DescriptionLinkInteraction
{
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
        int link = TMP_TextUtilities.FindIntersectingLink(text, data.position, data.pressEventCamera);
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
        int link = TMP_TextUtilities.FindIntersectingLink(_text, data.position, data.pressEventCamera);
        bool valid = !_cancelled && Input.touchCount <= 1 && Time.unscaledTime - _started < maxSeconds &&
            link >= 0 && _text.textInfo.linkInfo[link].GetLinkID() == _id;
        if (valid) id = _id;
        Reset();
        return valid;
    }
    public void Tick() { if (Input.touchCount > 1) _cancelled = true; }
    public void Cancel() { _cancelled = true; }
    public void Reset() { _pointer = null; _id = null; _text = null; _cancelled = false; }
}
