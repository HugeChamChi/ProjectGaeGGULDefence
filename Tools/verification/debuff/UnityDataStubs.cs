using System;

// Only Unity data/UI types and legacy rounding are stubbed. Debuff/HP/parser logic uses game sources.
namespace UnityEngine
{
    public class ScriptableObject { }
    public class Sprite { }
    public sealed class HeaderAttribute : Attribute { public HeaderAttribute(string header) { } }
    public sealed class SerializeField : Attribute { }
    public sealed class CreateAssetMenuAttribute : Attribute { public string menuName; public string fileName; }
    public static class Mathf { public static int RoundToInt(float value) => (int)Math.Round(value); }
}
public enum Tier { Normal, Rare, Epic, Legend }
