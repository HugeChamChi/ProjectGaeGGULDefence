using UnityEditor.Build;
using UnityEditor.Build.Reporting;

/// <summary>게임 빌드 전에 분류 원본/풀/생성 SO 불일치를 거절한다. 런타임 추첨에는 관여하지 않는다.</summary>
public sealed class DroneChoiceConceptBuildValidator : IPreprocessBuildWithReport
{
    /// <summary>빌드 전 검증 순서.</summary>
    public int callbackOrder => 0;

    /// <summary>현재 분류 데이터가 Import된 상태인지 확인한다.</summary>
    public void OnPreprocessBuild(BuildReport report) => DroneChoiceConceptImporter.ValidateCurrent();
}
