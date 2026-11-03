#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

public static class PlannerItemIconImporter
{
    [Serializable] public class Entry { public string address; public string file; public string sha256; }
    [Serializable] public class Manifest { public int version; public Entry[] icons; }
    public const string TargetFolder = "Assets/WorkSpace/USW/Art/ItemIcons";

    [MenuItem("Tools/Planner/Import Item Icons")]
    public static void ChooseFolder()
    {
        var folder = EditorUtility.OpenFolderPanel("기획툴에서 내보낸 ItemIcons 폴더 선택", "", "");
        if (string.IsNullOrEmpty(folder)) return;
        try { var count = Import(folder); EditorUtility.DisplayDialog("아이콘 등록 완료", count + "개 Sprite와 Addressables 주소를 설정했습니다.", "확인"); }
        catch (Exception e) { Debug.LogException(e); EditorUtility.DisplayDialog("아이콘 등록 실패", e.Message, "확인"); }
    }

    public static int Import(string folder)
    {
        if (!File.Exists(Path.Combine(folder,"item-icons.json"))) folder=Path.Combine(folder,"ItemIcons");
        var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(Path.Combine(folder,"item-icons.json")));
        if(manifest==null||manifest.version!=1||manifest.icons==null) throw new InvalidDataException("지원하지 않는 아이콘 목록입니다.");
        var settings=AddressableAssetSettingsDefaultObject.Settings;
        if(settings==null) throw new InvalidOperationException("Addressables 설정이 없습니다.");
        foreach(var icon in manifest.icons)
        {
            if(!Regex.IsMatch(icon.file??"",@"^[A-Za-z0-9_-]+\.png$")||icon.address!="ItemIcons/"+Path.GetFileNameWithoutExtension(icon.file)) throw new InvalidDataException("아이콘 이름/주소가 올바르지 않습니다.");
            var source=Path.Combine(folder,icon.file);var target=TargetFolder+"/"+icon.file;
            if(Hash(File.ReadAllBytes(source))!=icon.sha256) throw new InvalidDataException("PNG가 변경되었습니다: "+icon.file);
            if(File.Exists(target)&&Hash(File.ReadAllBytes(target))!=icon.sha256) throw new IOException("다른 이미지가 이미 존재합니다: "+target);
            if(settings.groups.Where(g=>g!=null).SelectMany(g=>g.entries).Any(e=>e.address==icon.address&&e.AssetPath!=target)) throw new InvalidOperationException("다른 에셋이 주소를 사용합니다: "+icon.address);
        }
        Directory.CreateDirectory(TargetFolder);
        var group=settings.FindGroup("ItemIcons")??settings.CreateGroup("ItemIcons",false,false,true,null,typeof(BundledAssetGroupSchema),typeof(ContentUpdateGroupSchema));
        foreach(var icon in manifest.icons)
        {
            var target=TargetFolder+"/"+icon.file;
            if(!File.Exists(target))File.Copy(Path.Combine(folder,icon.file),target);
            AssetDatabase.ImportAsset(target,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(target);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.SaveAndReimport();
            settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(target),group).address=icon.address;
        }
        settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification,null,true,true);AssetDatabase.SaveAssets();return manifest.icons.Length;
    }
    private static string Hash(byte[] bytes) { using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant(); }
}
#endif
