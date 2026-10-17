using UnityEngine;

/// <summary>Disables Unity's on-screen developer console while preserving normal diagnostic logging.</summary>
public static class DebugManager
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void DisableDeveloperConsole()
    {
        Debug.developerConsoleVisible = false;
        Debug.developerConsoleEnabled = false;
    }
}
