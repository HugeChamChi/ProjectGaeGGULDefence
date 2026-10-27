using System;
using System.Linq;
using TMPro;
using UnityEngine;

namespace GGD.UI.Authoring
{
    /// <summary>Build-time authored references to installed fonts/images and packaged XML; no runtime operating data migration.</summary>
    public sealed class AuthoringAssets : ScriptableObject
    {
        /// <summary>Manifest hash installed by the developer bridge, used to reject stale game builds.</summary>
        public string ContentHash;
        /// <summary>Immutable source publication installed in this game build.</summary>
        public string SourceVersion;
        /// <summary>Exact TMP assets from the exported production screen.</summary>
        public FontEntry[] Fonts = Array.Empty<FontEntry>();
        /// <summary>Installed or newly imported sprites keyed by the exported catalog.</summary>
        public ImageEntry[] Images = Array.Empty<ImageEntry>();
        /// <summary>Text snapshots copied from the same authoring originals for Android builds.</summary>
        public TextEntry[] Documents = Array.Empty<TextEntry>();
        /// <summary>Finds the required exact font, never replacing it with an OS font.</summary>
        public TMP_FontAsset Font(string key) => Fonts.FirstOrDefault(x => x.Key == key)?.Asset ?? throw new FormatException("Bundled font missing: " + key);
        /// <summary>Loads the same XML source model from build-included text snapshots.</summary>
        public ResolvedUiDocument Resolve(string screenPath)
        {
            return UiComposer.Resolve(AuthoringWorkspace.FromSources(Documents.ToDictionary(x => x.Path, x => x.Xml)), screenPath);
        }
        /// <summary>One installed font mapping.</summary>
        [Serializable] public sealed class FontEntry { public string Key; public TMP_FontAsset Asset; }
        /// <summary>One installed sprite mapping.</summary>
        [Serializable] public sealed class ImageEntry { public string Key; public Sprite Asset; }
        /// <summary>One installed original XML snapshot.</summary>
        [Serializable] public sealed class TextEntry { public string Path; [TextArea] public string Xml; }
    }
}
