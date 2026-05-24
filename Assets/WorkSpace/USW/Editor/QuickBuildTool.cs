using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public static class QuickBuildTool
{
    private const string DISABLE_GPGS_SYMBOL = "DISABLE_GPGS";
    private const string BUILD_PATH = "Builds/Android/DevBuild_GPGS_Disabled.apk";

    [MenuItem("Tools/Build/Dev Build (GPGS Disabled)")]
    public static void BuildDevWithGpgsDisabled()
    {
        BuildTargetGroup targetGroup = BuildTargetGroup.Android;
        string originalSymbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup);
        
        try
        {
            // 1. DISABLE_GPGS 심볼 추가
            AddDefineSymbol(targetGroup, DISABLE_GPGS_SYMBOL);
            Debug.Log($"[QuickBuild] Added {DISABLE_GPGS_SYMBOL}. Current symbols: {PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup)}");

            // 2. 빌드 옵션 설정
            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
            buildPlayerOptions.scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();
            
            buildPlayerOptions.locationPathName = BUILD_PATH;
            buildPlayerOptions.target = BuildTarget.Android;
            buildPlayerOptions.options = BuildOptions.Development | BuildOptions.AllowDebugging | BuildOptions.ConnectWithProfiler;

            // 3. 빌드 실행
            Debug.Log("[QuickBuild] Starting Development Build...");
            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"<color=green>[QuickBuild] Build Succeeded!</color> Size: {summary.totalSize / 1024 / 1024} MB");
                EditorUtility.RevealInFinder(BUILD_PATH);
            }
            else if (summary.result == BuildResult.Failed)
            {
                Debug.LogError("[QuickBuild] Build Failed!");
            }
        }
        finally
        {
            // 4. 원래 심볼 상태로 복구
            PlayerSettings.SetScriptingDefineSymbolsForGroup(targetGroup, originalSymbols);
            Debug.Log("[QuickBuild] Restored original scripting define symbols.");
        }
    }

    private static void AddDefineSymbol(BuildTargetGroup group, string symbol)
    {
        string symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
        List<string> symbolList = symbols.Split(';').ToList();

        if (!symbolList.Contains(symbol))
        {
            symbolList.Add(symbol);
            PlayerSettings.SetScriptingDefineSymbolsForGroup(group, string.Join(";", symbolList));
        }
    }
}
