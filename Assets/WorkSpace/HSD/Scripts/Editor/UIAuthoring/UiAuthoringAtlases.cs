using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GGD.UI.Authoring;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

/// <summary>Builds generated UI atlases outside Imports and verifies the installed image membership.</summary>
public static class UiAuthoringAtlases
{
    /// <summary>Bridge-owned atlas files; source artwork and its importer metadata are never rewritten.</summary>
    public const string DirectoryPath = "Assets/WorkSpace/HSD/Resources/UiAuthoring/Atlases";
    private const int MaxTextureSize = 4096;

    /// <summary>Regenerates exact packable lists from the publication, retaining each atlas's meta identity.</summary>
    public static AuthoringAssets.AtlasEntry[] Build(AuthoringWorkspace workspace, AuthoringAssets.ImageEntry[] images)
    {
        var groups = workspace.Documents["images.xml"].Root.Elements("Image")
            .Where(x => !string.IsNullOrEmpty((string)x.Attribute("atlas")))
            .GroupBy(x => (string)x.Attribute("atlas")).ToArray();
        var sprites = images.ToDictionary(x => x.Key, x => x.Asset);
        var owners = new Dictionary<Sprite, string>();
        foreach (var group in groups)
            foreach (var image in group)
            {
                var sprite = sprites[AuthoringWorkspace.Required(image, "key")];
                if (owners.TryGetValue(sprite, out string previous) && previous != group.Key) throw new FormatException("One sprite cannot belong to multiple UI atlases: " + sprite.name);
                owners[sprite] = group.Key;
            }
        if (groups.Length > 0 && EditorSettings.spritePackerMode == SpritePackerMode.Disabled)
            EditorSettings.spritePackerMode = SpritePackerMode.SpriteAtlasV2;
        Directory.CreateDirectory(DirectoryPath); AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var result = new List<AuthoringAssets.AtlasEntry>();
        foreach (var group in groups)
        {
            string path = DirectoryPath + "/" + group.Key + ".spriteatlasv2";
            var definition = new SpriteAtlasAsset();
            definition.Add(group.Select(x => (UnityEngine.Object)sprites[AuthoringWorkspace.Required(x, "key")]).Distinct().ToArray());
            SpriteAtlasAsset.Save(definition, path); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (SpriteAtlasImporter)AssetImporter.GetAtPath(path);
            // Atlas V2 keeps packing/texture settings on the importer (.meta), separately from packables.
            var packing = importer.packingSettings; packing.enableRotation = false; packing.enableTightPacking = false; packing.padding = 4;
            importer.packingSettings = packing;
            var texture = importer.textureSettings; texture.generateMipMaps = false; texture.readable = false; texture.sRGB = true; texture.filterMode = FilterMode.Bilinear;
            importer.textureSettings = texture;
            var platform = importer.GetPlatformSettings("DefaultTexturePlatform"); platform.maxTextureSize = MaxTextureSize; importer.SetPlatformSettings(platform);
            importer.includeInBuild = true; importer.SaveAndReimport();
            result.Add(new AuthoringAssets.AtlasEntry { Key = group.Key, Asset = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(path) ?? throw new FormatException("UI atlas import failed: " + path) });
        }
        // Retired groups must not continue packing a sprite into a second, stale atlas.
        foreach (string path in Directory.GetFiles(DirectoryPath, "*.spriteatlasv2"))
            if (!result.Any(x => AssetDatabase.GetAssetPath(x.Asset) == path.Replace('\\', '/')))
            {
                var importer = (SpriteAtlasImporter)AssetImporter.GetAtPath(path); importer.includeInBuild = false; importer.SaveAndReimport();
            }
        if (result.Count > 0) SpriteAtlasUtility.PackAtlases(result.Select(x => x.Asset).ToArray(), EditorUserBuildSettings.activeBuildTarget, false);
        Validate(workspace, images, result.ToArray());
        foreach (var screen in workspace.Documents.Where(x => x.Value.Root.Name == "Screen"))
        {
            var used = UiComposer.Resolve(workspace, screen.Key).Root.Descendants("Image").Select(x => (string)x.Attribute("sprite"))
                .Where(x => !string.IsNullOrEmpty(x)).Distinct().Select(x => sprites[x]).ToArray();
            int pages = used.Select(x => x.texture).Distinct().Count();
            Debug.Log("UI_AUTHORING_ATLAS " + screen.Key + ": " + used.Length + " sprites, " + pages + " packed textures");
            if (pages > 1) Debug.LogWarning("UI_AUTHORING_ATLAS " + screen.Key + ": multiple textures remain after packing; review atlas groups and dimensions.");
        }
        return result.ToArray();
    }

    /// <summary>Rejects disabled packing, excluded atlases and stale/missing installed sprite membership.</summary>
    public static void Validate(AuthoringWorkspace workspace, AuthoringAssets.ImageEntry[] images, AuthoringAssets.AtlasEntry[] atlases)
    {
        var sprites = images.ToDictionary(x => x.Key, x => x.Asset);
        foreach (var image in workspace.Documents["images.xml"].Root.Elements("Image"))
        {
            string group = (string)image.Attribute("atlas"); if (string.IsNullOrEmpty(group)) continue;
            var atlas = atlases.FirstOrDefault(x => x.Key == group)?.Asset;
            if (EditorSettings.spritePackerMode == SpritePackerMode.Disabled || atlas == null ||
                !(AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(atlas)) is SpriteAtlasImporter importer) || !importer.includeInBuild ||
                importer.packingSettings.enableRotation || importer.packingSettings.enableTightPacking ||
                !sprites.TryGetValue(AuthoringWorkspace.Required(image, "key"), out var sprite) || sprite == null || !atlas.CanBindTo(sprite))
                throw new FormatException("UI atlas does not include installed image: " + group + "/" + (string)image.Attribute("key"));
        }
    }
}
