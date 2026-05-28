using UnityEngine;
using TMPro;

[CreateAssetMenu(fileName = "DamageStyle_", menuName = "Data/Damage Floater Style")]
public class DamageFloaterStyle : ScriptableObject
{
    [Header("Text Settings")]
    public TMP_FontAsset fontAsset;
    public Material fontMaterial;
    public Color textColor = Color.white;
    public float fontSize = 50f; // 픽셀 단위 캔버스이므로 크기를 현실적으로 키움
    public bool isBold = false; 

    [Header("Animation Settings")]
    public float duration = 0.8f;
    public float moveDistance = 150f; // 위로 이동할 픽셀 거리

    [Tooltip("시간에 따른 크기 변화 (0 -> 1.2 -> 1.0 -> 0 처럼 설정 가능)")]
    public AnimationCurve scaleCurve = new AnimationCurve(
        new Keyframe(0, 0), 
        new Keyframe(0.2f, 1.2f), 
        new Keyframe(0.4f, 1.0f), 
        new Keyframe(1.0f, 0.8f)
    );

    [Tooltip("시간에 따른 투명도(알파) 변화")]
    public AnimationCurve alphaCurve = AnimationCurve.Linear(0, 1, 1, 0);

    [Header("Random Offset")]
    public float randomizeOffsetX = 30f;
    public float randomizeOffsetY = 30f;
}
