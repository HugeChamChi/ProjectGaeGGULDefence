using UnityEngine;

[CreateAssetMenu(fileName = "HealthBarSettings", menuName = "ScriptableObjects/HealthBarSettingsSO", order = 1)]
public class HealthBarSettingsSO : ScriptableObject
{
    [Tooltip("체력 몇 당 1줄로 취급할 것인지 설정")]
    public int healthPerLine = 1000;

    [Tooltip("체력 줄 수에 따른 색상 배열 (루프됨)")]
    public Color[] lineColors;
}
