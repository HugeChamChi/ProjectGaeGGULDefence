using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.U2D;

namespace GGD.UI.Authoring
{
    /// <summary>Installed baked game prefab, resource references and source snapshots for authoring and editor validation.</summary>
    public sealed class AuthoringAssets : ScriptableObject
    {
        private static readonly Unity.Profiling.ProfilerMarker _resolveMarker = new Unity.Profiling.ProfilerMarker("UIAuthoring.Resolve");
        /// <summary>Manifest hash installed by the developer bridge, used to reject stale game builds.</summary>
        public string ContentHash;
        /// <summary>Immutable source publication installed in this game build.</summary>
        public string SourceVersion;
        /// <summary>Game-ready serialized graph installed from the validated publication by the Unity bridge.</summary>
        public BakedUiDocument BakedScreen;
        /// <summary>Native pilot: the original prefab owns the visual tree and its existing behaviour.</summary>
        public bool NativeSourceInstalled;
        /// <summary>Hash of the original prefab after the validated native application.</summary>
        public string NativeSourceHash;
        /// <summary>Exact TMP assets from the exported production screen.</summary>
        public FontEntry[] Fonts = Array.Empty<FontEntry>();
        /// <summary>Installed or newly imported sprites keyed by the exported catalog.</summary>
        public ImageEntry[] Images = Array.Empty<ImageEntry>();
        /// <summary>Installed atlases kept reachable by the same build-included settings asset.</summary>
        public AtlasEntry[] Atlases = Array.Empty<AtlasEntry>();
        /// <summary>Text snapshots copied from the same authoring originals for Android builds.</summary>
        public TextEntry[] Documents = Array.Empty<TextEntry>();
        /// <summary>Finds the required exact font, never replacing it with an OS font.</summary>
        public TMP_FontAsset Font(string key) => Fonts.FirstOrDefault(x => x.Key == key)?.Asset ?? throw new FormatException("Bundled font missing: " + key);
        /// <summary>Resolves source snapshots for authoring and explicit comparisons. The production host uses BakedScreen.</summary>
        public ResolvedUiDocument Resolve(string screenPath)
        {
            using var sample = _resolveMarker.Auto();
            return UiComposer.Resolve(AuthoringWorkspace.FromSources(Documents.ToDictionary(x => x.Path, x => x.Xml)), screenPath);
        }
        /// <summary>One installed font mapping.</summary>
        [Serializable] public sealed class FontEntry { public string Key; public TMP_FontAsset Asset; }
        /// <summary>One installed sprite mapping.</summary>
        [Serializable] public sealed class ImageEntry { public string Key; public Sprite Asset; }
        /// <summary>One authored atlas group installed by the Unity bridge.</summary>
        [Serializable] public sealed class AtlasEntry { public string Key; public SpriteAtlas Asset; }
        /// <summary>One installed original XML snapshot.</summary>
        [Serializable] public sealed class TextEntry { public string Path; [TextArea] public string Xml; }
    }
}
