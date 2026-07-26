using System;

/// <summary>SelectableReference 드롭다운 등 인스펙터에 표시될 클래스의 이름을 지정한다.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class DisplayNameAttribute : Attribute
{
    public string Name { get; }
    public DisplayNameAttribute(string name) => Name = name;
}
