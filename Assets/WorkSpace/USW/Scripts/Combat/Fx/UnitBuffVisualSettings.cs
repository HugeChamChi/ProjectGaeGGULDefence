using UnityEngine;

/// <summary>본게임 토템 버프의 시안과 획득 알림 시간. 실제 버프 수치에는 영향을 주지 않는다.</summary>
[CreateAssetMenu(menuName = "USW/Fx/Unit Buff Visual Settings")]
public sealed class UnitBuffVisualSettings : ScriptableObject
{
    public StatBuffFxStyle Attack;
    public StatBuffFxStyle Speed;
    public StatBuffFxStyle Mixed;
    public StatBuffFxStyle Disabled;
    public Material AdditiveMaterial;
    [Min(.1f)] public float PermanentSeconds = 1.5f;
    [Min(.05f)] public float Scale = .55f;
    public int FrontOffset = 3;
    public int BehindOffset = -3;
}
