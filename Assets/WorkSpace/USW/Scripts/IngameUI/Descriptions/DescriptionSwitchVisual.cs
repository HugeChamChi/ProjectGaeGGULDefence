using UnityEngine;
using UnityEngine.UI;

public sealed class DescriptionSwitchVisual : MonoBehaviour
{
    public Toggle Toggle;
    public Image Track;
    public RectTransform Knob;

    private void LateUpdate()
    {
        if (Toggle == null || Track == null || Knob == null) return;
        if (Track.sprite == null) Track.sprite = DescriptionToggleView.TrackSprite;
        var knobImage = Knob.GetComponent<Image>();
        if (knobImage.sprite == null) knobImage.sprite = DescriptionToggleView.KnobSprite;
        Knob.anchoredPosition = new Vector2(Toggle.isOn ? 68 : 24, 0);
        Track.color = Toggle.isOn ? new Color(0.56f, 0.35f, 0.77f) : new Color(0.28f, 0.28f, 0.32f);
    }
}
