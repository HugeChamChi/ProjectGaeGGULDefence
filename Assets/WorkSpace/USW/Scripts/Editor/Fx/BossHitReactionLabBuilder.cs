using System;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>거리 비교 씬의 원래 거리 전투를 복사해 보스 피격 반응만 비교한다.</summary>
public static class BossHitReactionLabBuilder
{
    /// <summary>추가 피격 반응 비교 씬 경로.</summary>
    public const string ScenePath="Assets/WorkSpace/USW/Scene/FxLab_BossHitReaction.unity";
    private const string Prefix="Assets/WorkSpace/USW/Scene/FxLab_BossHitReaction";

    /// <summary>피격 반응 비교 씬을 생성하거나 연다.</summary>
    [MenuItem("Tools/USW/Fx/Open Boss Hit Reaction Lab")]
    public static void Open()
    {
        if(EditorApplication.isPlaying)return;
        if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        BuildScene();
    }

    /// <summary>기존 거리 비교 씬을 보존하고 독립 비교 씬을 생성한다.</summary>
    public static string BuildScene()
    {
        if(EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Stop Play and save first");
        if(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
        {
            EditorSceneManager.OpenScene(ScenePath);
            UpgradeChiefPreview();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),ScenePath);
            return ScenePath;
        }
        if(!AssetDatabase.CopyAsset(CombatDistanceLabBuilder.ScenePath,ScenePath))throw new InvalidOperationException("Distance source scene missing");
        var scene=EditorSceneManager.OpenScene(ScenePath);
        GameObject.Find("Distance Controls").SetActive(false);
        var combat=UnityEngine.Object.FindAnyObjectByType<CombatDistanceLab>();
        var lab=new GameObject("Boss Hit Reaction Lab").AddComponent<BossHitReactionLab>();
        var settings=ScriptableObject.CreateInstance<BossHitReactionSettings>();
        AssetDatabase.CreateAsset(settings,Prefix+"Settings.asset");
        var font=CreateFont();
        var root=new GameObject("Reaction Controls",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=31001;
        var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=new Vector2(1080,2340);scaler.matchWidthOrHeight=0;
        var panel=Rect(root.transform,"Reaction panel",24,-265,720,205);
        var backing=panel.gameObject.AddComponent<Image>();backing.color=new Color(.04f,.07f,.12f,.93f);backing.raycastTarget=false;
        var status=Text(panel,font,"피격 반응 비교 · 준비 중",16,-10,688,48,29);
        var current=Button(panel,font,"현재",16,-65,336);
        var reaction=Button(panel,font,"충격 반응",368,-65,336);
        UnityEventTools.AddIntPersistentListener(current.onClick,lab.SelectMode,0);
        UnityEventTools.AddIntPersistentListener(reaction.onClick,lab.SelectMode,1);
        var shot=Button(panel,font,"디시그망 보기",16,-139,252);
        UnityEventTools.AddPersistentListener(shot.onClick,lab.ShowHeavyAttack);
        var chief=Button(panel,font,"족장 스킬 보기",284,-139,252);
        UnityEventTools.AddPersistentListener(chief.onClick,lab.ShowChiefAttack);
        var pause=Button(panel,font,"일시정지",552,-139,152);
        UnityEventTools.AddPersistentListener(pause.onClick,lab.TogglePause);
        var so=new SerializedObject(lab);
        so.FindProperty("_combat").objectReferenceValue=combat;
        so.FindProperty("_scope").objectReferenceValue=UnityEngine.Object.FindAnyObjectByType<InGameLifetimeScope>();
        so.FindProperty("_settings").objectReferenceValue=settings;
        so.FindProperty("_status").objectReferenceValue=status;
        so.FindProperty("_pauseLabel").objectReferenceValue=pause.GetComponentInChildren<TMP_Text>();
        var images=so.FindProperty("_buttons");images.arraySize=2;
        images.GetArrayElementAtIndex(0).objectReferenceValue=current.GetComponent<Image>();
        images.GetArrayElementAtIndex(1).objectReferenceValue=reaction.GetComponent<Image>();
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(scene,ScenePath);Selection.activeGameObject=lab.gameObject;
        return ScenePath;
    }

    private static void UpgradeChiefPreview()
    {
        var lab=UnityEngine.Object.FindAnyObjectByType<BossHitReactionLab>();
        var panel=GameObject.Find("Reaction Controls").transform.Find("Reaction panel");
        var shot=panel.Find("큰 공격 보기") ?? panel.Find("디시그망 보기");
        var font=shot.GetComponentInChildren<TMP_Text>().font;
        font.atlasPopulationMode=AtlasPopulationMode.Dynamic;
        font.TryAddCharacters("디시그망 보기 족장 스킬 보기",out string missing);
        foreach(var texture in font.atlasTextures)if(!AssetDatabase.Contains(texture))AssetDatabase.AddObjectToAsset(texture,font);
        font.atlasPopulationMode=AtlasPopulationMode.Static;
        EditorUtility.SetDirty(font);AssetDatabase.SaveAssetIfDirty(font);
        shot.name="디시그망 보기";
        shot.GetComponent<RectTransform>().sizeDelta=new Vector2(252,58);
        var label=shot.GetComponentInChildren<TMP_Text>();label.text="디시그망 보기";
        label.rectTransform.sizeDelta=new Vector2(252,58);
        if(panel.Find("족장 스킬 보기") == null)
        {
            var chief=Button(panel,font,"족장 스킬 보기",284,-139,252);
            UnityEventTools.AddPersistentListener(chief.onClick,lab.ShowChiefAttack);
        }
        var pause=panel.Find("일시정지").GetComponent<RectTransform>();
        pause.anchoredPosition=new Vector2(552,-139);pause.sizeDelta=new Vector2(152,58);
        pause.GetComponentInChildren<TMP_Text>().rectTransform.sizeDelta=new Vector2(152,58);
        var so=new SerializedObject(lab);
        so.FindProperty("_scope").objectReferenceValue=UnityEngine.Object.FindAnyObjectByType<InGameLifetimeScope>();
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static TMP_FontAsset CreateFont()
    {
        var source=AssetDatabase.LoadAssetAtPath<Font>("Assets/WorkSpace/USW/Prefab/etc/RixInooAriDuriRound.ttf");
        var font=TMP_FontAsset.CreateFontAsset(source,64,8,UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,1024,1024);
        AssetDatabase.CreateAsset(font,Prefix+"Font.asset");AssetDatabase.AddObjectToAsset(font.material,font);
        font.TryAddCharacters("피격 반응 비교 · 준비 중 현재 충격 반응 충격을 받고 버티기 큰 공격 보기 디시그망 보기 족장 스킬 보기 일시정지 전투 재생 비교를 준비하지 못했습니다",out string missing);
        foreach(var texture in font.atlasTextures)if(!AssetDatabase.Contains(texture))AssetDatabase.AddObjectToAsset(texture,font);
        font.atlasPopulationMode=AtlasPopulationMode.Static;EditorUtility.SetDirty(font);AssetDatabase.SaveAssetIfDirty(font);
        return font;
    }
    private static RectTransform Rect(Transform parent,string name,float x,float y,float w,float h)
    {
        var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);
        r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);return r;
    }
    private static TMP_Text Text(Transform parent,TMP_FontAsset font,string value,float x,float y,float w,float h,float size)
    {
        var text=Rect(parent,"Label",x,y,w,h).gameObject.AddComponent<TextMeshProUGUI>();text.font=font;text.text=value;
        text.fontSize=size;text.color=Color.white;text.raycastTarget=false;text.alignment=TextAlignmentOptions.Center;
        text.textWrappingMode=TextWrappingModes.NoWrap;return text;
    }
    private static Button Button(Transform parent,TMP_FontAsset font,string value,float x,float y,float w)
    {
        var r=Rect(parent,value,x,y,w,58);var image=r.gameObject.AddComponent<Image>();image.color=new Color(.12f,.16f,.23f,.96f);
        var button=r.gameObject.AddComponent<Button>();button.targetGraphic=image;Text(r,font,value,0,0,w,58,26);return button;
    }
}
