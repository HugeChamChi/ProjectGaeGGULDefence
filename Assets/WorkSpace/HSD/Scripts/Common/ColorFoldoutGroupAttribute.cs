using Alchemy.Inspector;

/// <summary>지정한 색상의 접이식 박스로 필드를 그룹화한다. 같은 groupPath를 쓰는 필드들이 한 박스에 모인다.</summary>
public sealed class ColorFoldoutGroupAttribute : PropertyGroupAttribute
{
    public ColorFoldoutGroupAttribute(string groupPath, string hexColor) : base(groupPath)
    {
        HexColor = hexColor;
    }

    public string HexColor { get; }
}
