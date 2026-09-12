using System;

// Only serialization/legacy rounding symbols are stubbed. All debuff/HP/parser logic is linked from game sources.
namespace UnityEngine
{
    public class ScriptableObject { }
    public sealed class SerializeField : Attribute { }
    public sealed class CreateAssetMenuAttribute : Attribute { public string menuName; public string fileName; }
    public static class Mathf { public static int RoundToInt(float value) => (int)Math.Round(value); }
}
public enum Tier { Normal, Rare, Epic, Legend }
