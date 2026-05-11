using UnityEngine;

[CreateAssetMenu(fileName = "ButtonReactionData", menuName = "UI/Button Reaction Data")]
public class ButtonReactionData : ScriptableObject
{
    [Header("Scaling Settings")]
    public float shrinkScale = 0.92f;
    public float duration = 0.15f;

    [Header("Motion Curves")]
    public AnimationCurve pressCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public AnimationCurve releaseCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
}
