using UnityEngine;

/// <summary>
/// 셰이더 전역 _USWUnscaledTime에 Time.unscaledTime을 공급한다.
/// 레벨업처럼 timeScale = 0인 동안에도 UI 셰이더(DiagonalFlowBackground 등)가 계속 흐르게 한다.
/// </summary>
public class UnscaledShaderTime : MonoBehaviour
{
    private static readonly int TimeId = Shader.PropertyToID("_USWUnscaledTime");

    private void Update() => Shader.SetGlobalFloat(TimeId, Time.unscaledTime);
}
