using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Xml.Linq;
using Cysharp.Threading.Tasks;
using GGD.UI.Authoring;
using HSD.UI.Setting;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;
using Object = UnityEngine.Object;
using TMPro;

/// <summary>Exercises the real settings wrappers, AudioManager and PlayerData preference path in isolated Play fixtures.</summary>
[InitializeOnLoad]
public static class SettingsGameIntegrationChecks
{
    private const string Flag = "UiAuthoring.Checks";
    private static string Output => SessionState.GetBool(Flag + ".native", false) ? "outputs/ui-native-prefab-20261029/game-integration.json" : "outputs/ui-authoring-verification/game-integration.json";
    private static readonly List<string> Checks = new List<string>();
    static SettingsGameIntegrationChecks() { EditorApplication.playModeStateChanged += StateChanged; }
    /// <summary>Verifies the original prefab path with actual game services and restores the existing installed mode afterwards.</summary>
    public static void RunNativeLive() { SessionState.SetBool(Flag + ".native", true); BeginLive(); }
    /// <summary>Runs the targeted production-host fixtures in a live Editor and restores the prior scene setup.</summary>
    public static void RunLive() { SessionState.SetBool(Flag + ".native", false); BeginLive(); }
    private static void BeginLive()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before starting settings checks");
        var setup = EditorSceneManager.GetSceneManagerSetup();
        if (setup.Any(x => string.IsNullOrEmpty(x.path)) || Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount).Any(x => UnityEngine.SceneManagement.SceneManager.GetSceneAt(x).isDirty))
            throw new InvalidOperationException("Settings checks require saved, clean scenes so their exact setup can be restored");
        SessionState.SetString(Flag + ".scenes", JsonUtility.ToJson(new SceneRestore { Paths = setup.Select(x => x.path).ToArray(), Loaded = setup.Select(x => x.isLoaded).ToArray(), Active = setup.Select(x => x.isActive).ToArray(), PlayStart = AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene) }));
        SessionState.SetBool(Flag + ".live", true); Checks.Clear();
        EditorSceneManager.playModeStartScene = null; Run();
    }
    /// <summary>Batch entry. Creates no saved scene and exits only after the Play checks and cleanup complete.</summary>
    public static void Run()
    {
        string[] args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "--designer-kit");
        if (index >= 0 && index + 1 < args.Length)
        {
            const string backup = "outputs/ui-authoring-verification/roundtrip-backup";
            string[] paths = Directory.GetFiles("Data/UI/Authoring", "*.xml", SearchOption.AllDirectories).Where(x => !x.Replace('\\', '/').Contains("/Published/versions/")).Concat(new[] { "Assets/WorkSpace/HSD/Resources/UiAuthoring/Settings.asset" }).ToArray();
            Directory.CreateDirectory(backup);
            foreach (string path in paths) { string target = Path.Combine(backup, path); Directory.CreateDirectory(Path.GetDirectoryName(target)); File.Copy(path, target, true); }
            File.WriteAllLines(backup + "/files.txt", paths); SessionState.SetBool(Flag + ".roundtrip", true);
            SettingsAuthoringBridge.Install(args[index + 1]);
        }
        SessionState.SetBool(Flag, true); SessionState.SetBool(Flag + ".done", false); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); EditorApplication.EnterPlaymode();
    }
    private static void StateChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Flag, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode) Execute().Forget();
        else if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Flag + ".done", false))
        {
            if (SessionState.GetBool(Flag + ".roundtrip", false))
            {
                const string backup = "outputs/ui-authoring-verification/roundtrip-backup";
                foreach (string path in File.ReadAllLines(backup + "/files.txt")) File.Copy(Path.Combine(backup, path), path, true);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport); SessionState.SetBool(Flag + ".roundtrip", false);
            }
            SessionState.SetBool(Flag, false);
            if (SessionState.GetBool(Flag + ".live", false))
            {
                SessionState.SetBool(Flag + ".live", false);
                var restore = JsonUtility.FromJson<SceneRestore>(SessionState.GetString(Flag + ".scenes", ""));
                EditorSceneManager.RestoreSceneManagerSetup(restore.Paths.Select((path, index) => new SceneSetup { path = path, isLoaded = restore.Loaded[index], isActive = restore.Active[index] }).ToArray());
                EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(restore.PlayStart) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(restore.PlayStart);
            }
            else EditorApplication.Exit(SessionState.GetInt(Flag + ".exit", 1));
        }
    }
    private static async UniTaskVoid Execute()
    {
        var preferences = new Dictionary<string, (bool Exists, int Value)>(); var created = new List<GameObject>();
        string journal = "UiAuthoring.QA.Pending.fixture"; PlayerDataController player = null; GamePresentationSettings settings = null;
        int frameRate = Application.targetFrameRate; float timeScale = Time.timeScale; string failure = null;
        var installed = Resources.Load<GGD.UI.Authoring.AuthoringAssets>("UiAuthoring/Settings");
        bool wasNative = installed.NativeSourceInstalled;
        bool native = SessionState.GetBool(Flag + ".native", false);
        if (native) installed.NativeSourceInstalled = true;
        using var timeout = new CancellationTokenSource(); using var deadline = timeout.CancelAfterSlim(TimeSpan.FromSeconds(80), DelayType.Realtime); var token = timeout.Token;
        try
        {
            await UniTask.Delay(500, DelayType.Realtime, cancellationToken: token);
            var root = Object.FindFirstObjectByType<RootLifetimeScope>();
            Check(root != null && root.Container != null, "existing production root scope initializes");
            var audio = root.Container.Resolve<AudioManager>();
            foreach (AudioGroup group in Enum.GetValues(typeof(AudioGroup)))
                foreach (string key in new[] { "Audio_Vol_" + group, "Audio_Mute_" + group }) preferences[key] = (PlayerPrefs.HasKey(key), PlayerPrefs.GetInt(key));
            Check(audio.GetVolume(AudioGroup.BGM) >= 0 && Get(audio, "_unityProvider") != null, "actual AudioManager/UnityAudioProvider initialized");
            player = new PlayerDataController(() => "fixture", () => 2000000000, "UiAuthoring.QA.Pending.");
            typeof(PlayerDataController).GetField("_account", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player, "fixture");
            player.RefreshUI(new PlayerData { ShowDamageNumbers = true, VibrationEnabled = true }); settings = new GamePresentationSettings(player);
            var canvas = new GameObject("QA PortraitCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)); created.Add(canvas);
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1080, 1920); scaler.matchWidthOrHeight = 0;
            if (EventSystem.current == null) { var events = new GameObject("QA Events", typeof(EventSystem), typeof(StandaloneInputModule)); created.Add(events); }
            string[] prefabPaths = { "Assets/WorkSpace/USW/Prefabs/UI/LobbySettingsPopup.prefab", "Assets/WorkSpace/USW/Prefab/UI/IngameOptionsMenu.prefab" };
            foreach (string path in prefabPaths)
            {
                var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path), canvas.transform); created.Add(instance);
                var host = instance.GetComponentInChildren<UI_SettingPanel_InGame>(true); host.Construct(audio, settings, root.Container.Resolve<SceneChangeManager>());
                int beforeBgm = audio.GetVolume(AudioGroup.BGM); bool beforeMute = audio.IsMuted(AudioGroup.BGM); bool beforeDirty = player.IsDirty;
                UI_Base entry = instance.GetComponent<LobbySettingsPopup>();
                if (entry != null) await entry.OpenAsync(); else
                {
                    var menu = instance.GetComponentInChildren<IngameHudMenu>(true);
                    if (menu != null) { menu.SettingsButton.onClick.Invoke(); await UniTask.Delay(400, DelayType.Realtime, cancellationToken: token); }
                    else await host.OpenAsync();
                }
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, token); Canvas.ForceUpdateCanvases();
                if (native)
                {
                    Check(!host.UsesAuthoredUi && host.AuthoredView == null, path + ": original prefab supplies the visual tree");
                    Check(audio.GetVolume(AudioGroup.BGM) == beforeBgm && audio.IsMuted(AudioGroup.BGM) == beforeMute && player.IsDirty == beforeDirty, "native opening preserves existing settings without writes");
                    var row = host.GetComponentsInChildren<UI_SoundSettingSlot>(true).First(x => x.Group == AudioGroup.BGM);
                    var slider = (Slider)Get(row, "_slider"); slider.value = slider.maxValue <= 1.01f ? .37f : 37;
                    Check(audio.GetVolume(AudioGroup.BGM) == 37 && PlayerPrefs.GetInt("Audio_Vol_" + AudioGroup.BGM) == 37, "native volume reaches actual AudioManager and local preferences");
                    var nativeToggle = (Toggle)Get(row, "_muteToggle"); nativeToggle.isOn = false;
                    Check(audio.IsMuted(AudioGroup.BGM), "native mute retains original inverse mapping");
                    var presentation = (HSD.UI.Utils.UI_SlidingToggle)Get(host, "toggle_DamageFloater"); bool previousDamage = settings.ShowDamageNumbers; presentation.Toggle();
                    Check(settings.ShowDamageNumbers != previousDamage && player.IsDirty, "native presentation toggle reaches isolated player data");
                    var animation = (GaeGGUL.Animation.Anim_InOutBase)Get(host, "targetAnim");
                    var target = (Transform)Get(animation, "animationTarget"); var resting = target.localScale;
                    await host.CloseAsync(); var opening = host.OpenAsync();
                    await UniTask.Delay(60, DelayType.Realtime, cancellationToken: token);
                    Check(target.localScale != resting, "original popup entrance animates over actual Play frames");
                    await opening; Check(target.localScale == resting, "original popup entrance finishes at authored scale");
                    var lobbyButton = (Button)Get(host, "btn_GoToLobby");
                    if (lobbyButton != null && lobbyButton.gameObject.activeInHierarchy)
                    {
                        lobbyButton.onClick.Invoke(); var confirm = (UI_Popup_Confirm)Get(host, "confirmPopup");
                        Check(confirm.gameObject.activeInHierarchy, "native lobby button opens existing confirmation without changing scenes"); await confirm.CloseAsync();
                    }
                    int nativeClosed = 0; host.OnClosed += () => nativeClosed++;
                    ((Button)Get(host, "btn_Close")).onClick.Invoke(); await UniTask.Delay(500, DelayType.Realtime, cancellationToken: token);
                    Check(nativeClosed == 1 && !host.gameObject.activeSelf, "native close runs original exit animation once");
                    for (int repeat = 0; repeat < 3; repeat++) { await host.OpenAsync(); await host.CloseAsync(); }
                    Check(target.localScale == resting, "native repeat open/close preserves resting transform");
                    UnityEngine.Object.Destroy(instance); await UniTask.Yield(PlayerLoopTiming.Update, token); continue;
                }
                Check(host.UsesAuthoredUi && host.AuthoredView != null && host.AuthoredView.Elements.Count > 60, path + ": existing entry opens same real authored graph");
                Check(host.AuthoredBindings.CommandCount == 0 && audio.GetVolume(AudioGroup.BGM) == beforeBgm && audio.IsMuted(AudioGroup.BGM) == beforeMute && player.IsDirty == beforeDirty, "opening sends no commands and writes no preferences");
                Check(host.AuthoredView.IsBaked && host.AuthoredView.Document == null, "P4 production host instantiates its baked prefab without runtime XML");
                // Source is inspected by this editor test only; the production host never resolves it.
                var source = Resources.Load<GGD.UI.Authoring.AuthoringAssets>("UiAuthoring/Settings").Resolve("Screens/UI3030.xml").Root;
                var allTexts = host.AuthoredView.Root.GetComponentsInChildren<TMP_Text>(true);
                var outlined = allTexts.Where(x => x.fontSharedMaterial.GetFloat("_OutlineWidth") > 0).ToArray();
                File.WriteAllLines("outputs/ui-authoring-optimization/materials.txt", allTexts.Select(x => x.name + ": " + x.fontSharedMaterial.name + " / " + x.fontSharedMaterial.GetInstanceID() + " / " + x.fontSharedMaterial.GetFloat("_OutlineWidth")));
                int expectedOutlines = source.Descendants("Text").Count(x => AuthoringWorkspace.Number(x, "outlineWidth") > 0);
                Check(expectedOutlines > 1 && outlined.Length == expectedOutlines && outlined.Select(x => x.fontSharedMaterial).Distinct().Count() == 1, "P1 composed production labels share one outline material (labels=" + outlined.Length + ", materials=" + outlined.Select(x => x.fontSharedMaterial).Distinct().Count() + ")");
                var material = outlined[0].fontSharedMaterial;
                Check(host.AuthoredView.Root.GetComponent<Canvas>() is Canvas ownCanvas && !ownCanvas.overrideSorting && host.AuthoredView.Root.GetComponent<GraphicRaycaster>() != null, "P3 screen Canvas inherits the production host sorting and owns its raycaster");
                Check(source.Descendants("Image").All(x => host.AuthoredView.Elements[(string)x.Parent.Attribute("id")].GetComponent<Image>().raycastTarget == UiPublishLint.ImageRaycastTarget(x)), "P1 all existing explicit touch targets are preserved");
                Check(host.AuthoredView.Root.GetComponentsInChildren<Image>(true).Where(x => x.sprite != null).Select(x => x.mainTexture).Distinct().Count() == 1, "P2 production image graphics use one packed texture");
                if (SessionState.GetBool(Flag + ".live", false))
                {
                    Directory.CreateDirectory("outputs/ui-authoring-bake-20261028");
                    ScreenCapture.CaptureScreenshot("outputs/ui-authoring-bake-20261028/" + Path.GetFileNameWithoutExtension(path) + ".png");
                    await UniTask.NextFrame(cancellationToken: token);
                }
                T Control<T>(string tag, string key) where T : Component
                { var element = source.Descendants("Element").First(x => (string)x.Element(tag)?.Attribute("bind") == key); return host.AuthoredView.Elements[(string)element.Attribute("id")].GetComponent<T>(); }
                int count = host.AuthoredBindings.CommandCount;
                Control<Slider>("Slider", "settings.bgm.volume").value = beforeBgm == 27 ? 28 : 27;
                Check(audio.GetVolume(AudioGroup.BGM) == (beforeBgm == 27 ? 28 : 27) && PlayerPrefs.GetInt("Audio_Vol_BGM") == audio.GetVolume(AudioGroup.BGM) && host.AuthoredBindings.CommandCount == count + 1, "actual volume input persists through AudioManager exactly once");
                var toggle = Control<Toggle>("Toggle", "settings.bgm.enabled"); toggle.isOn = !toggle.isOn;
                Check(audio.IsMuted(AudioGroup.BGM) == !toggle.isOn && PlayerPrefs.GetInt("Audio_Mute_BGM") == (toggle.isOn ? 0 : 1), "enabled checkbox preserves the production mute inversion/save contract");
                var damage = Control<AuthoredSwitch>("Switch", "settings.damageNumbers"); damage.OnPointerClick(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });
                Check(!settings.ShowDamageNumbers && player.IsDirty && PlayerPrefs.HasKey(journal), "damage preference reaches actual PlayerData dirty/journal path");
                var vibration = Control<AuthoredSwitch>("Switch", "settings.vibration"); vibration.OnPointerClick(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });
                Check(!settings.VibrationEnabled && player.Data.VibrationEnabled == false, "vibration preference reaches actual PlayerData");
                foreach (string command in new[] { "settings.frame.next", "settings.frame.next", "settings.frame.previous", "settings.frame.previous" }) host.AuthoredBindings.Execute(command);
                Check(Application.targetFrameRate == 30 && (float)host.AuthoredBindings.Read("settings.frameRate") == 30, "frame controls preserve 30/60 boundaries and session-only application");
                Check(Mathf.Approximately(Time.timeScale, timeScale), "settings itself never pauses gameplay");
                if (entry == null)
                {
                    host.AuthoredBindings.Execute("settings.lobby");
                    var confirm = (UI_Popup_Confirm)Get(host, "confirmPopup");
                    Check(confirm != null && confirm.gameObject.activeInHierarchy, "ingame lobby command delegates to the existing native confirmation");
                    await confirm.CloseAsync();
                }
                int closed = 0; host.OnClosed += () => closed++;
                var closeNode = source.Descendants("Element").First(x => (string)x.Attribute("key") == "closeButton");
                var closeRect = host.AuthoredView.Elements[(string)closeNode.Attribute("id")];
                var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, closeRect.TransformPoint(closeRect.rect.center)) };
                var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
                Check(hits.Count > 0 && (hits[0].gameObject.transform == closeRect || hits[0].gameObject.transform.IsChildOf(closeRect)), "P3 actual EventSystem raycast reaches close through the nested screen Canvas");
                host.AuthoredView.SetInput(false); hits.Clear(); EventSystem.current.RaycastAll(pointer, hits);
                Check(!hits.Any(x => x.gameObject.transform.IsChildOf(host.AuthoredView.Root.transform)), "P3 view input blocking still filters the nested Canvas"); host.AuthoredView.SetInput(true);
                if (SessionState.GetBool(Flag + ".roundtrip", false))
                    Check(Mathf.Abs(host.AuthoredView.Elements[(string)closeNode.Attribute("id")].anchoredPosition.x - 140) < .01f && source.Descendants("Element").Any(x => (string)x.Attribute("name") == "Edited Window name"), "designer-saved placement and renamed window reach the real game unchanged");
                host.AuthoredView.Elements[(string)closeNode.Attribute("id")].GetComponent<Button>().onClick.Invoke();
                await UniTask.Delay(400, DelayType.Realtime, cancellationToken: token);
                Check(closed == 1 && !host.gameObject.activeSelf, "authored close uses the existing host animation and closes once");
                for (int i = 0; i < 4; i++) { await host.OpenAsync(); await host.CloseAsync(); }
                await host.OpenAsync(); count = host.AuthoredBindings.CommandCount;
                Check(host.AuthoredView.Root.GetComponentsInChildren<TMP_Text>(true).Where(x => x.fontSharedMaterial.GetFloat("_OutlineWidth") > 0).All(x => x.fontSharedMaterial == material), "P1 reopen reuses the existing shared outline material");
                var sound = Control<Slider>("Slider", "settings.sfx.volume"); sound.value = sound.value == 33 ? 34 : 33;
                Check(host.AuthoredBindings.CommandCount == count + 1, "repeated host open/close does not duplicate listeners");
                typeof(UI_SettingPanel_InGame).GetField("_allowOpening", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(host, false);
                await host.CloseAsync(); await host.OpenAsync(); Check(!host.gameObject.activeSelf, "existing tutorial AllowOpening guard remains authoritative");
                Object.Destroy(instance); await UniTask.NextFrame(cancellationToken: token);
                Check(material != null && EditorUtility.IsPersistent(material), "P4 destroying the host preserves the prefab-owned shared material asset");
                settings.SetDamageNumbers(true); settings.SetVibration(true);
            }
        }
        catch (Exception error) { failure = error.ToString(); Debug.LogException(error); }
        finally
        {
            installed.NativeSourceInstalled = wasNative;
            foreach (var instance in created) if (instance != null) Object.Destroy(instance);
            settings?.Dispose(); player?.Dispose(); PlayerPrefs.DeleteKey(journal);
            foreach (var pair in preferences) if (pair.Value.Exists) PlayerPrefs.SetInt(pair.Key, pair.Value.Value); else PlayerPrefs.DeleteKey(pair.Key);
            PlayerPrefs.Save(); Application.targetFrameRate = frameRate;
            string output = SessionState.GetBool(Flag + ".native", false) ? Output : SessionState.GetBool(Flag + ".live", false) ? "outputs/ui-authoring-live-" + DateTime.Now.ToString("yyyyMMdd") + "/game-integration.json" : Output;
            Directory.CreateDirectory(Path.GetDirectoryName(output)); File.WriteAllText(output, JsonUtility.ToJson(new Result { passed = failure == null, error = failure, checks = Checks.ToArray() }, true));
            SessionState.SetInt(Flag + ".exit", failure == null ? 0 : 1); SessionState.SetBool(Flag + ".done", true); EditorApplication.ExitPlaymode();
        }
    }
    private static object Get(object target, string name)
    { for (Type type = target.GetType(); type != null; type = type.BaseType) { var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly); if (field != null) return field.GetValue(target); } throw new MissingFieldException(name); }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); Checks.Add(message); }
    [Serializable] private sealed class Result { public bool passed; public string error; public string[] checks; }
    [Serializable] private sealed class SceneRestore { public string[] Paths; public bool[] Loaded; public bool[] Active; public string PlayStart; }
}
