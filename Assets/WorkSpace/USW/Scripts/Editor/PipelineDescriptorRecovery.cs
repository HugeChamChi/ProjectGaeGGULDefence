using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>현재 에디터의 Pipeline 연결 파일이 유실됐을 때 살아 있는 서버에서 복구한다.</summary>
[InitializeOnLoad]
public static class PipelineDescriptorRecovery
{
    private static double _nextAttempt;

    static PipelineDescriptorRecovery()
    {
        EditorApplication.update += Recover;
    }

    private static void Recover()
    {
        if (EditorApplication.timeSinceStartup < _nextAttempt) return;
        _nextAttempt = EditorApplication.timeSinceStartup + 5d;
        string project = Path.GetDirectoryName(Application.dataPath);
        string descriptor = Path.Combine(project, "Library/Pipeline/.unity-pipeline-port");
        if (File.Exists(descriptor)) return;
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type startup = assembly.GetType("Unity.Pipeline.Editor.PipelineServerStartup");
            if (startup == null) continue;
            object server = startup.GetProperty("Server", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if (server == null) return;
            Type serverType = server.GetType();
            bool running = (bool)(serverType.GetProperty("IsRunning")?.GetValue(server) ?? false);
            if (!running) return;
            MethodInfo create = serverType.GetMethod("CreateInstanceDescriptor", BindingFlags.NonPublic | BindingFlags.Instance);
            if (create == null) return;
            try
            {
                create.Invoke(server, null);
                if (File.Exists(descriptor))
                    Debug.Log("Pipeline 연결 파일 복구 완료 (기존 에디터/서버 유지).");
            }
            catch (Exception error)
            {
                Debug.LogWarning("Pipeline 연결 파일 복구 실패: " + (error.InnerException?.Message ?? error.Message));
            }
            return;
        }
    }
}
