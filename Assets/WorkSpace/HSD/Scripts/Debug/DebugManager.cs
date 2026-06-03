using UnityEngine;

public static class DebugManager
{
#if DEVELOPMENT_BUILD || UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnableDeveloperConsole()
    {
        Debug.developerConsoleVisible = true;
    }
#endif
}
