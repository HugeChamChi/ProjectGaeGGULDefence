using UnityEngine;

/// <summary>별 스턴 연출의 공용 데이터. 별 세 개는 유닛당 한 메시로 그린다.</summary>
[CreateAssetMenu(menuName = "Game/Stun Visual Settings")]
public sealed class StunVisualSettings : ScriptableObject
{
    public Material Material;
    public Color Color = new Color(1f, 0.85f, 0.15f, 1f);
    [Min(0.01f)] public float StarRadius = 0.08f;
    [Min(0.01f)] public float OrbitRadius = 0.25f;
    [Min(0f)] public float OrbitHeight = 0.07f;
    public float HeadOffset = 0.12f;
    public float DegreesPerSecond = 240f;
}
