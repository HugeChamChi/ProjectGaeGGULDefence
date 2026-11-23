#if UNITY_EDITOR
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

/// <summary>Direct Editor play uses a disposable offline profile, never the production NORMAL contract.</summary>
[DisallowMultipleComponent]
public sealed class IngameEditorPreview : MonoBehaviour
{
    internal const string TotemTestScene = "InGame_TotemTestScene";
    private EndlessModeData _mode;
    /// <summary>The cloned local profile; the authored NORMAL asset remains unchanged.</summary>
    public EndlessModeData Mode => _mode;

    /// <summary>Configures the existing run loop before initialization; no remote failure fallback is involved.</summary>
    public void Configure(WaveManager waves)
    {
        if (waves.EndlessMode == null) throw new InvalidOperationException("Editor preview requires an authored mode.");
        _mode = Instantiate(waves.EndlessMode);
        _mode.name = waves.EndlessMode.name + " (Editor Preview)";
        _mode.hideFlags = HideFlags.DontSave;
        var edit = new SerializedObject(_mode);
        edit.FindProperty("_key").stringValue = "EDITOR_PREVIEW";
        edit.ApplyModifiedPropertiesWithoutUndo();
        if (!waves.TryConfigureMode(_mode)) throw new InvalidOperationException("Cannot configure an active run as a preview.");
        Debug.Log("[IngameEditorPreview] Offline authored profile. No stamina, round records or account EXP rewards.");
    }

    internal static async UniTask SeedTotemTestInventoryAsync(TotemInventory inventory, CancellationToken token)
    {
        foreach (var key in new[] { "LT2001", "LT2002", "LT2003" })
        {
            var totem = Resources.Load<TotemData>("LevelTotems/" + key);
            await totem.LoadAssetsAsync().AttachExternalCancellation(token);
            if (!inventory.TryAdd(totem)) break;
        }
    }

    private void OnGUI()
    {
        GUI.Box(new Rect(8f, 8f, Mathf.Min(380f, Screen.width - 16f), 28f),
            "EDITOR PREVIEW / OFFLINE / NO REWARDS");
    }

    private void OnDestroy()
    {
        if (_mode != null) Destroy(_mode);
    }
}
#endif
