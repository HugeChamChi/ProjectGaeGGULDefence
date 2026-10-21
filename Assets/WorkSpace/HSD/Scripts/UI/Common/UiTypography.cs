/// <summary>1080 기준 UI 글씨의 공통 가독성 하한. 기존에 더 큰 글씨는 줄이지 않는다.</summary>
public static class UiTypography
{
    /// <summary>본문·설명의 기본 글씨 크기 하한.</summary>
    public const float BodySize = 32f;
    /// <summary>식량·비용 등 핵심 수치의 기본 글씨 크기 하한.</summary>
    public const float NumericSize = 40f;
    /// <summary>자동 크기 조절에서 허용하는 가장 작은 글씨 크기.</summary>
    public const float AutoSizeMinimum = 28f;
}
