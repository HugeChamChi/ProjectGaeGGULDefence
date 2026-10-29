using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using HSD.UI.Setting;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;
using Object = UnityEngine.Object;

/// <summary>Five editor-only samples per construction path, with identical authored content and a legacy prefab reference.</summary>
[InitializeOnLoad]
public static class SettingsUiPrefabComparison
{
    private const string Flag = "UiAuthoring.PrefabComparison";
    private const string Output = "outputs/ui-authoring-bake-20261028";
    private const int Repetitions = 5;
    private const int SteadyFrames = 16;
    private static readonly ProfilerMarker _firstFrame = new ProfilerMarker("UIAuthoring.FirstFrameEnd");
    private static readonly List<Row> _rows = new List<Row>();
    private static bool _allocationCounterAvailable;
    private static CancellationTokenSource _measurementCts;

    static SettingsUiPrefabComparison() { EditorApplication.playModeStateChanged += StateChanged; }

    /// <summary>Starts from a clean saved scene, restoring the exact scene setup and Play-start override afterward.</summary>
    public static void RunLive()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before comparing UI");
        var setup = EditorSceneManager.GetSceneManagerSetup();
        if (setup.Any(x => string.IsNullOrEmpty(x.path)) || Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount).Any(x => UnityEngine.SceneManagement.SceneManager.GetSceneAt(x).isDirty))
            throw new InvalidOperationException("Save existing scenes before starting the comparison");
        Directory.CreateDirectory(Output);
        SessionState.SetString(Flag + ".restore", JsonUtility.ToJson(new Restore { Paths = setup.Select(x => x.path).ToArray(), Loaded = setup.Select(x => x.isLoaded).ToArray(), Active = setup.Select(x => x.isActive).ToArray(), PlayStart = AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene) }));
        SessionState.SetBool(Flag, true); SessionState.SetBool(Flag + ".done", false);
        EditorSceneManager.playModeStartScene = null; EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); EditorApplication.EnterPlaymode();
    }

    private static void StateChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Flag, false)) return;
        if (state == PlayModeStateChange.ExitingPlayMode) _measurementCts?.Cancel();
        if (state == PlayModeStateChange.EnteredPlayMode) Measure().Forget();
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Flag, false);
            var restore = JsonUtility.FromJson<Restore>(SessionState.GetString(Flag + ".restore", ""));
            EditorSceneManager.RestoreSceneManagerSetup(restore.Paths.Select((path, index) => new SceneSetup { path = path, isLoaded = restore.Loaded[index], isActive = restore.Active[index] }).ToArray());
            EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(restore.PlayStart) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(restore.PlayStart);
        }
    }

    private static async UniTaskVoid Measure()
    {
        string error = null; int frameRate = Application.targetFrameRate; GameObject canvas = null, staging = null, instance = null;
        var preferences = new Dictionary<string, (bool Exists, int Value)>();
        _rows.Clear();
        using var timeout = new CancellationTokenSource();
        _measurementCts = timeout;
        using var deadline = timeout.CancelAfterSlim(TimeSpan.FromSeconds(150), DelayType.Realtime); var token = timeout.Token;
        using var draws = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
        using var gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
        try
        {
            Application.targetFrameRate = 30;
            long probeStart = GC.GetAllocatedBytesForCurrentThread();
            var probe = new byte[1024]; GC.KeepAlive(probe);
            _allocationCounterAvailable = GC.GetAllocatedBytesForCurrentThread() > probeStart;
            await UniTask.Delay(500, DelayType.Realtime, cancellationToken: token);
            var root = Object.FindFirstObjectByType<RootLifetimeScope>();
            if (root == null || root.Container == null) throw new InvalidOperationException("Production root services did not initialize");
            var audio = root.Container.Resolve<AudioManager>(); var settings = root.Container.Resolve<GamePresentationSettings>(); var scenes = root.Container.Resolve<SceneChangeManager>();
            // The legacy slider refresh can notify AudioManager even when only opening a fixture.
            foreach (AudioGroup group in Enum.GetValues(typeof(AudioGroup)))
                foreach (string key in new[] { "Audio_Vol_" + group, "Audio_Mute_" + group }) preferences[key] = (PlayerPrefs.HasKey(key), PlayerPrefs.GetInt(key));
            canvas = new GameObject("Portrait comparison", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1080, 1920);
            staging = new GameObject("Inactive fixture staging"); staging.SetActive(false);
            if (EventSystem.current == null) new GameObject("Comparison events", typeof(EventSystem), typeof(StandaloneInputModule));
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/WorkSpace/HSD/Prefab/UI/InGame/InGame_Setting_Popup.prefab");
            // Load the same assets before every path; these are Editor measurements with warm resources, not app cold starts.
            Resources.Load<GGD.UI.Authoring.AuthoringAssets>("UiAuthoring/Settings");
            for (int repeat = 0; repeat < Repetitions; repeat++)
            {
                string[] modes = repeat % 2 == 0 ? new[] { "xml", "prefab", "legacy" } : new[] { "legacy", "prefab", "xml" };
                foreach (string mode in modes)
                {
                    long start = Stopwatch.GetTimestamp(), allocation = GC.GetAllocatedBytesForCurrentThread();
                    instance = Object.Instantiate(prefab, staging.transform, false);
                    var row = new Row { Mode = mode, Repeat = repeat + 1, WrapperInstantiateMs = Elapsed(start), WrapperInstantiateBytes = AllocatedDelta(allocation) };
                    var host = instance.GetComponentInChildren<UI_SettingPanel_Base>(true); host.SetEditorComparisonMode(mode); host.Construct(audio, settings, scenes);
                    instance.transform.SetParent(canvas.transform, false);
                    start = Stopwatch.GetTimestamp(); allocation = GC.GetAllocatedBytesForCurrentThread();
                    var open = host.OpenAsync(); row.OpenSynchronousMs = Elapsed(start); row.OpenSynchronousBytes = AllocatedDelta(allocation);
                    await UniTask.WaitForEndOfFrame(token);
                    using (_firstFrame.Auto()) row.FirstFrameEndMs = Elapsed(start);
                    await open; row.AnimationCompleteMs = Elapsed(start);
                    row.ResolveMs = host.EditorResolveMilliseconds; row.CreateMs = host.EditorCreateMilliseconds;
                    row.ResolveBytes = _allocationCounterAvailable ? host.EditorResolveBytes : -1; row.CreateBytes = _allocationCounterAvailable ? host.EditorCreateBytes : -1;
                    row.Elements = host.AuthoredView?.Elements.Count ?? instance.GetComponentsInChildren<RectTransform>(true).Length;
                    if (mode == "prefab" && (host.AuthoredView == null || !host.AuthoredView.IsBaked || host.AuthoredView.Document != null)) throw new InvalidOperationException("Prefab comparison interpreted source at runtime");
                    if (mode == "xml" && (host.AuthoredView == null || host.AuthoredView.IsBaked)) throw new InvalidOperationException("XML comparison did not use source construction");
                    if (mode == "legacy" && host.UsesAuthoredUi) throw new InvalidOperationException("Legacy comparison unexpectedly used authored UI");
                    if (repeat == 0) { ScreenCapture.CaptureScreenshot(Output + "/" + mode + ".png"); await UniTask.NextFrame(cancellationToken: token); }
                    await UniTask.DelayFrame(3, cancellationToken: token);
                    long totalDraws = 0, totalGc = 0; int drawSamples = 0, gcSamples = 0;
                    for (int frame = 0; frame < SteadyFrames; frame++)
                    {
                        await UniTask.NextFrame(cancellationToken: token);
                        if (draws.Valid && draws.Count > 0) { totalDraws += draws.LastValue; drawSamples++; }
                        if (gc.Valid && gc.Count > 0) { totalGc += gc.LastValue; gcSamples++; }
                    }
                    row.DrawCalls = drawSamples > 0 ? (double)totalDraws / drawSamples : -1;
                    row.GcBytesPerFrame = gcSamples > 0 ? (double)totalGc / gcSamples : -1;
                    await host.CloseAsync(); start = Stopwatch.GetTimestamp(); allocation = GC.GetAllocatedBytesForCurrentThread();
                    var reopen = host.OpenAsync(); row.ReopenSynchronousMs = Elapsed(start); row.ReopenSynchronousBytes = AllocatedDelta(allocation);
                    await UniTask.WaitForEndOfFrame(token); row.ReopenFrameEndMs = Elapsed(start); await reopen;
                    _rows.Add(row); Object.Destroy(instance); instance = null; await UniTask.DelayFrame(3, cancellationToken: token);
                }
            }
        }
        catch (OperationCanceledException) { error = "Comparison cancelled"; }
        catch (Exception failure) { error = failure.ToString(); UnityEngine.Debug.LogException(failure); }
        finally
        {
            if (instance != null) Object.Destroy(instance); if (canvas != null) Object.Destroy(canvas); if (staging != null) Object.Destroy(staging);
            Application.targetFrameRate = frameRate;
            foreach (var preference in preferences)
                if (preference.Value.Exists) PlayerPrefs.SetInt(preference.Key, preference.Value.Value); else PlayerPrefs.DeleteKey(preference.Key);
            if (preferences.Count > 0) PlayerPrefs.Save();
            File.WriteAllText(Output + "/comparison.json", JsonUtility.ToJson(new Result { Passed = error == null && _rows.Count == Repetitions * 3, AllocationCounterAvailable = _allocationCounterAvailable, Error = error, Unity = Application.unityVersion, Platform = Application.platform.ToString(), Width = Screen.width, Height = Screen.height, Rows = _rows.ToArray() }, true));
            _measurementCts = null; SessionState.SetBool(Flag + ".done", true);
            EditorApplication.delayCall += () => { if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode(); };
        }
    }

    private static double Elapsed(long start) => (Stopwatch.GetTimestamp() - start) * 1000d / Stopwatch.Frequency;
    private static long AllocatedDelta(long start) => _allocationCounterAvailable ? GC.GetAllocatedBytesForCurrentThread() - start : -1;
    [Serializable] private sealed class Row
    {
        public string Mode; public int Repeat, Elements;
        public double WrapperInstantiateMs, ResolveMs, CreateMs, OpenSynchronousMs, FirstFrameEndMs, AnimationCompleteMs, ReopenSynchronousMs, ReopenFrameEndMs, DrawCalls, GcBytesPerFrame;
        public long WrapperInstantiateBytes, ResolveBytes, CreateBytes, OpenSynchronousBytes, ReopenSynchronousBytes;
    }
    [Serializable] private sealed class Result { public bool Passed, AllocationCounterAvailable; public string Error, Unity, Platform; public int Width, Height; public Row[] Rows; }
    [Serializable] private sealed class Restore { public string[] Paths; public bool[] Loaded, Active; public string PlayStart; }
}
