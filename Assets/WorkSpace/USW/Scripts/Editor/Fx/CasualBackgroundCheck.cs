using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class CasualBackgroundCheck
{
    private const string Request = "Library/CasualBackgroundCheck.request";
    private static double _next;
    private static int _frame;
    static CasualBackgroundCheck() { EditorApplication.update += Update; }
    private static void Update()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (!EditorApplication.isPlaying)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != CasualChoiceLabMenu.ScenePath)
            { File.Delete(Request); Debug.LogError("Background check: wrong scene."); return; }
            EditorApplication.isPlaying = true;
            return;
        }
        var lab = Object.FindFirstObjectByType<CasualChoiceFxLab>();
        if (lab == null || lab.PreviewCamera == null) return;
        if (_next == 0) { _next = EditorApplication.timeSinceStartup + 2; return; }
        if (EditorApplication.timeSinceStartup < _next) return;
        var image = new Texture2D(540, 960, TextureFormat.RGB24, false);
        var rt = RenderTexture.GetTemporary(540, 960, 24);
        var camera = lab.PreviewCamera;
        var previous = camera.targetTexture;
        var active = RenderTexture.active;
        try
        {
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            image.ReadPixels(new Rect(0, 0, 540, 960), 0, 0);
            image.Apply();
            Directory.CreateDirectory("Temp/CasualBackgroundCheck");
            File.WriteAllBytes($"Temp/CasualBackgroundCheck/frame-{_frame}.png", image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previous;
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(rt);
            Object.DestroyImmediate(image);
        }
        if (++_frame < 2) { _next = EditorApplication.timeSinceStartup + 2; return; }
        File.Delete(Request);
        Debug.Log("[CasualBackgroundCheck] Captured background twice, two seconds apart.");
    }
}
