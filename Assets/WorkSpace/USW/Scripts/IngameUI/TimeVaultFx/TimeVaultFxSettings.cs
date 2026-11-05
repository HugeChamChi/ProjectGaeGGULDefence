using TMPro;
using UnityEngine;

/// <summary>
/// 시간 금고(9136) 유예 연출 설정 — 에셋 참조, 색, 타이밍, 화면 문구. TimeVaultFx가 읽는다.
/// 기준: design/gdd/overdrive-deck.md C06 시간 금고 (0초 → 15초 유예). 유예가 끝나면 연출만 걷고 기존 전투 종료 흐름으로 넘긴다.
/// 연출은 HTML 시안 C "시스템 오버라이드" (2026-11-05 사용자 확정). 문구는 현지화 시스템 연결 전까지 여기 둔다.
/// </summary>
[CreateAssetMenu(menuName = "USW/Fx/Time Vault Fx Settings", fileName = "TimeVaultFxSettings")]
public sealed class TimeVaultFxSettings : ScriptableObject
{
    [Header("Assets")]
    public TMP_FontAsset DisplayFont;
    public Material OverlayMaterial;

    [Header("Colors")]
    public Color OverrideColor = new Color(1f, 0.18f, 0.24f, 1f);
    public Color GlitchCyan = new Color(0.16f, 1f, 0.92f, 1f);
    public Color PanelColor = new Color(0.07f, 0.01f, 0.015f, 0.9f);
    public Color DenyColor = new Color(0.25f, 0.02f, 0.04f, 0.75f);

    [Header("Timing")]
    [Tooltip("유예 중 잠긴 UI(CanvasGroup)의 알파")]
    public float LockedAlpha = 0.4f;
    [Tooltip("유예가 끝나 연출을 걷어내는 시간(초). 그 뒤는 기존 전투 종료 흐름")]
    public float ExitSeconds = 0.3f;
    [Tooltip("남은 시간이 이 초 이하이면 배지가 깜빡인다")]
    public float HurrySeconds = 5f;

    [Header("Glitch")]
    [Tooltip("초당 글리치 발생 횟수 (시작 → 끝)")]
    public Vector2 GlitchRate = new Vector2(0.5f, 3.7f);
    [Tooltip("글리치 한 번 길이(초) 최소·최대")]
    public Vector2 GlitchBurstSeconds = new Vector2(0.08f, 0.18f);
    public float TriggerGlitchSeconds = 0.45f;
    [Tooltip("글리치 중 HUD 흔들림(px)")]
    public float GlitchShakePixels = 14f;

    [Header("Text")]
    public string[] TerminalLines =
    {
        "> TIME_REMAIN = 0",
        "> DEFEAT ... BYPASSED",
        "> run vault.exe --grace 15",
        "> ALL UNITS = LEGEND",
    };
    public string OverrideTitle = "OVERRIDE";
    public string OverrideBadge = "OVERRIDE";
    public string AccessDenied = "ACCESS DENIED";
}
