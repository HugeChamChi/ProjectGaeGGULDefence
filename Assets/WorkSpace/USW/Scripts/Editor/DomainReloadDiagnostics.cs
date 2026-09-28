using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 도메인 리로드("Running managed callbacks")가 오래 걸릴 때 원인 플러그인을 찾기 위한 진단 도구.
/// Preferences > Diagnostics > Core > EnableDomainReloadTimings 스위치를 켜고 끈다 (Unity 내부 API, 리플렉션).
/// 켜면 다음 리로드부터 Editor.log에 [InitializeOnLoad] 등 콜백별 시간이 찍힌다.
/// </summary>
public static class DomainReloadDiagnostics
{
    private const string SwitchName = "EnableDomainReloadTimings";

    [MenuItem("Tools/USW/Diagnostics/Domain Reload Timings ON")]
    public static void On() => Set(true);

    [MenuItem("Tools/USW/Diagnostics/Domain Reload Timings OFF")]
    public static void Off() => Set(false);

    [MenuItem("Tools/USW/Diagnostics/Reload Scripts Now")]
    public static void ReloadNow() => EditorUtility.RequestScriptReload();

    [MenuItem("Tools/USW/Diagnostics/Open Editor.log Folder")]
    public static void OpenLog() => EditorUtility.RevealInFinder(Application.consoleLogPath);

    private static void Set(bool on)
    {
        var sw = FindSwitch();
        if (sw == null)
        {
            Debug.LogWarning($"[DomainReloadDiagnostics] {SwitchName} 스위치를 찾지 못함 — Preferences > Diagnostics > Core 에서 직접 켜 주세요.");
            return;
        }

        var type = sw.GetType();
        var persistent = type.GetProperty("persistentValue", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (persistent != null && persistent.CanWrite)
        {
            persistent.SetValue(sw, on);
            Debug.Log($"[DomainReloadDiagnostics] {SwitchName} = {on} (persistent). 다음 리로드부터 적용 — Editor.log: {Application.consoleLogPath}");
            return;
        }

        var props = string.Join(", ", type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Select(p => $"{p.Name}{(p.CanWrite ? "(w)" : "")}"));
        Debug.LogWarning($"[DomainReloadDiagnostics] 설정 불가. {type.FullName} 속성: {props}");
    }

    private static object FindSwitch()
    {
        const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        foreach (var owner in new[] { typeof(Debug), typeof(EditorApplication).Assembly.GetType("UnityEditor.EditorDiagnostics") })
        {
            if (owner == null) continue;
            var getter = owner.GetMethod("GetDiagnosticSwitch", Static, null, new[] { typeof(string) }, null);
            if (getter != null)
            {
                try { var sw = getter.Invoke(null, new object[] { SwitchName }); if (sw != null) return sw; }
                catch (Exception e) { Debug.LogWarning($"[DomainReloadDiagnostics] {owner.Name}.GetDiagnosticSwitch 실패: {e.InnerException?.Message ?? e.Message}"); }
            }
            var all = owner.GetProperty("diagnosticSwitches", Static)?.GetValue(null) as System.Collections.IEnumerable;
            if (all == null) continue;
            foreach (var sw in all)
                if ((sw.GetType().GetField("name")?.GetValue(sw) ?? sw.GetType().GetProperty("name")?.GetValue(sw)) as string == SwitchName)
                    return sw;
        }
        return null;
    }
}
