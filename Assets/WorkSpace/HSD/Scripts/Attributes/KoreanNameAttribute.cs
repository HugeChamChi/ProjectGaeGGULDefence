using System;

/// <summary>SelectableReference 드롭다운 등 인스펙터에 표시될 클래스의 한국어 이름을 지정한다.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class KoreanNameAttribute : Attribute
{
    public string Name { get; }
    public KoreanNameAttribute(string name) => Name = name;
}
