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

/// <summary>Exercises the real settings wrappers, AudioManager and PlayerData preference path in isolated Play fixtures.</summary>
[InitializeOnLoad]
public static class SettingsGameIntegrationChecks
{
    private const string Flag = "UiAuthoring.Checks";
    private const string Output = "outputs/ui-authoring-verification/game-integration.json";
    private static readonly List<string> Checks = new List<string>();
    static SettingsGameIntegrationChecks() { EditorApplication.playModeStateChanged += StateChanged; }
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
            SessionState.SetBool(Flag, false); EditorApplication.Exit(SessionState.GetInt(Flag + ".exit", 1));
        }
    }
    private static async UniTaskVoid Execute()
    {
        var preferences = new Dictionary<string, (bool Exists, int Value)>(); var created = new List<GameObject>();
        string journal = "UiAuthoring.QA.Pending.fixture"; PlayerDataController player = null; GamePresentationSettings settings = null;
        int frameRate = Application.targetFrameRate; float timeScale = Time.timeScale; string failure = null;
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
                Check(host.UsesAuthoredUi && host.AuthoredView != null && host.AuthoredView.Elements.Count > 60, path + ": existing entry opens same real authored graph");
                Check(host.AuthoredBindings.CommandCount == 0 && audio.GetVolume(AudioGroup.BGM) == beforeBgm && audio.IsMuted(AudioGroup.BGM) == beforeMute && player.IsDirty == beforeDirty, "opening sends no commands and writes no preferences");
                var source = host.AuthoredView.Document.Root;
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
                if (SessionState.GetBool(Flag + ".roundtrip", false))
                    Check(Mathf.Abs(host.AuthoredView.Elements[(string)closeNode.Attribute("id")].anchoredPosition.x - 140) < .01f && source.Descendants("Element").Any(x => (string)x.Attribute("name") == "Edited Window name"), "designer-saved placement and renamed window reach the real game unchanged");
                host.AuthoredView.Elements[(string)closeNode.Attribute("id")].GetComponent<Button>().onClick.Invoke();
                await UniTask.Delay(400, DelayType.Realtime, cancellationToken: token);
                Check(closed == 1 && !host.gameObject.activeSelf, "authored close uses the existing host animation and closes once");
                for (int i = 0; i < 4; i++) { await host.OpenAsync(); await host.CloseAsync(); }
                await host.OpenAsync(); count = host.AuthoredBindings.CommandCount;
                var sound = Control<Slider>("Slider", "settings.sfx.volume"); sound.value = sound.value == 33 ? 34 : 33;
                Check(host.AuthoredBindings.CommandCount == count + 1, "repeated host open/close does not duplicate listeners");
                typeof(UI_SettingPanel_InGame).GetField("_allowOpening", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(host, false);
                await host.CloseAsync(); await host.OpenAsync(); Check(!host.gameObject.activeSelf, "existing tutorial AllowOpening guard remains authoritative");
                Object.Destroy(instance); await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, token); settings.SetDamageNumbers(true); settings.SetVibration(true);
            }
        }
        catch (Exception error) { failure = error.ToString(); Debug.LogException(error); }
        finally
        {
            foreach (var instance in created) if (instance != null) Object.Destroy(instance);
            settings?.Dispose(); player?.Dispose(); PlayerPrefs.DeleteKey(journal);
            foreach (var pair in preferences) if (pair.Value.Exists) PlayerPrefs.SetInt(pair.Key, pair.Value.Value); else PlayerPrefs.DeleteKey(pair.Key);
            PlayerPrefs.Save(); Application.targetFrameRate = frameRate;
            Directory.CreateDirectory(Path.GetDirectoryName(Output)); File.WriteAllText(Output, JsonUtility.ToJson(new Result { passed = failure == null, error = failure, checks = Checks.ToArray() }, true));
            SessionState.SetInt(Flag + ".exit", failure == null ? 0 : 1); SessionState.SetBool(Flag + ".done", true); EditorApplication.ExitPlaymode();
        }
    }
    private static object Get(object target, string name)
    { for (Type type = target.GetType(); type != null; type = type.BaseType) { var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly); if (field != null) return field.GetValue(target); } throw new MissingFieldException(name); }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); Checks.Add(message); }
    [Serializable] private sealed class Result { public bool passed; public string error; public string[] checks; }
}
