using UnityEditor;
using UnityEngine;

/// <summary>저장된 씬을 바꾸지 않고 지진과 별 VFX를 플레이 모드에서 확인한다.</summary>
public static class EarthquakePrototypeMenu
{
    private const string Path = "Assets/WorkSpace/USW/Data/BossData/Crocodile/CrocodileEarthquake.asset";
    [MenuItem("Tools/GaeGGUL/Boss/Select Earthquake Settings")]
    private static void SelectSettings() => Selection.activeObject = AssetDatabase.LoadAssetAtPath<BossPatternData>(Path);

    [MenuItem("Tools/GaeGGUL/Boss/Stun Placed Units (Play Mode)")]
    private static void StunUnits()
    {
        var data = AssetDatabase.LoadAssetAtPath<BossPatternData>(Path);
        var grid = Object.FindFirstObjectByType<GridManager>();
        if (data == null || grid == null) return;
        foreach (var cell in grid.AllCells()) cell?.OccupyingUnit?.ApplyStun(data.duration, data.StunVisual);
        var camera = Camera.main;
        if (data.UseScreenShake && camera != null)
            (camera.GetComponent<BattleCameraShake>() ?? camera.gameObject.AddComponent<BattleCameraShake>()).Play(data.ShakeDuration, data.ShakeIntensity);
    }

    [MenuItem("Tools/GaeGGUL/Boss/Apply Earthquake To Current Boss (Play Mode)")]
    private static void ApplyPattern()
    {
        var boss = Object.FindFirstObjectByType<BossManager>()?.CurrentBoss;
        var controller = Object.FindFirstObjectByType<BossPatternController>();
        var data = AssetDatabase.LoadAssetAtPath<BossPatternData>(Path);
        if (boss != null && controller != null && data != null)
        {
            controller.RegisterBoss(boss, new[] { data });
            Debug.Log("현재 보스의 테스트 패턴을 지진으로 교체했습니다. 20초 후 시전합니다. 다음 보스/플레이 종료 시 원래 설정으로 돌아갑니다.");
        }
    }
    [MenuItem("Tools/GaeGGUL/Boss/Stun Placed Units (Play Mode)", true)]
    [MenuItem("Tools/GaeGGUL/Boss/Apply Earthquake To Current Boss (Play Mode)", true)]
    private static bool CanTest() => EditorApplication.isPlaying;
}
