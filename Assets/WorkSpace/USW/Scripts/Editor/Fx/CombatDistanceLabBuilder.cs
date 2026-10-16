using System;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>기존 인게임을 복사해 실제 전투 거리 비교용 씬을 만든다.</summary>
public static class CombatDistanceLabBuilder
{
    /// <summary>거리 비교 씬 경로.</summary>
    public const string ScenePath = "Assets/WorkSpace/USW/Scene/FxLab_CombatDistance.unity";
    private const string Prefix = "Assets/WorkSpace/USW/Scene/FxLab_CombatDistance";
    private const string Source = "Assets/Scenes/IngameScene.unity";

    /// <summary>Tools 메뉴에서 거리 비교 씬을 생성하거나 연다.</summary>
    [MenuItem("Tools/USW/Fx/Open Combat Distance Lab")]
    public static void Open()
    {
        if (EditorApplication.isPlaying) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        BuildScene();
    }

    /// <summary>저장된 인게임 복사본에만 테스트 컨트롤을 추가한다.</summary>
    public static string BuildScene()
    {
        if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Stop Play and save the current scene first");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
        {
            EditorSceneManager.OpenScene(ScenePath);
            return ScenePath;
        }
        if (!AssetDatabase.CopyAsset(Source,ScenePath)) throw new InvalidOperationException("Scene copy failed");
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var settings = CreateSettings();
        var font = CreateFont();
        var scope = UnityEngine.Object.FindAnyObjectByType<InGameLifetimeScope>();
        var background = GameObject.Find("bg").GetComponent<SpriteRenderer>();
        var lab = new GameObject("Combat Distance Lab").AddComponent<CombatDistanceLab>();

        var root = new GameObject("Distance Controls",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=31000;
        var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=new Vector2(1080,2340);scaler.matchWidthOrHeight=0f;
        var panel=Rect(root.transform,"Distance panel",24,-265,720,200);
        var backing=panel.gameObject.AddComponent<Image>();backing.color=new Color(.04f,.07f,.12f,.93f);backing.raycastTarget=false;
        var status=Text(panel,font,"거리 비교 · 준비 중",16,-10,688,50,30);
        var buttons=new Image[3];
        string[] labels={"현재","조금 가까움","많이 가까움"};
        for(int i=0;i<3;i++)
        {
            var button=Button(panel,font,labels[i],16+i*232,-66,224);
            UnityEventTools.AddIntPersistentListener(button.onClick,lab.SelectPreset,i);
            buttons[i]=button.GetComponent<Image>();
        }
        var pause=Button(panel,font,"일시정지",16,-140,180);
        UnityEventTools.AddPersistentListener(pause.onClick,lab.TogglePause);
        Text(panel,font,"같은 배치 · 카메라와 크기 고정",214,-143,490,46,23);
        var so=new SerializedObject(lab);
        so.FindProperty("_scope").objectReferenceValue=scope;
        so.FindProperty("_settings").objectReferenceValue=settings;
        so.FindProperty("_background").objectReferenceValue=background;
        so.FindProperty("_status").objectReferenceValue=status;
        so.FindProperty("_pauseLabel").objectReferenceValue=pause.GetComponentInChildren<TMP_Text>();
        var list=so.FindProperty("_buttons");list.arraySize=buttons.Length;
        for(int i=0;i<buttons.Length;i++)list.GetArrayElementAtIndex(i).objectReferenceValue=buttons[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(scene,ScenePath);
        Selection.activeGameObject=lab.gameObject;
        return ScenePath;
    }

    private static CombatDistanceSettings CreateSettings()
    {
        var settings=ScriptableObject.CreateInstance<CombatDistanceSettings>();
        settings.Boss=AssetDatabase.LoadAssetAtPath<BossData>("Assets/WorkSpace/USW/Data/BossData/BossData_Round01.asset");
        string[] units={"Betan","Gamman","Disigman","Zeltan","Deltan"};
        settings.Units=new UnitData[units.Length];
        for(int i=0;i<units.Length;i++)settings.Units[i]=AssetDatabase.LoadAssetAtPath<UnitData>("Assets/WorkSpace/USW/Data/DroneUnits/UnitData_"+units[i]+".asset");
        settings.Cells=new[] {new Vector2Int(0,0),new Vector2Int(2,0),new Vector2Int(4,0),
            new Vector2Int(1,1),new Vector2Int(3,1),new Vector2Int(5,1),
            new Vector2Int(0,2),new Vector2Int(2,2),new Vector2Int(4,2),
            new Vector2Int(1,3),new Vector2Int(3,3),new Vector2Int(5,3)};
        AssetDatabase.CreateAsset(settings,Prefix+"Settings.asset");
        return settings;
    }

    private static TMP_FontAsset CreateFont()
    {
        var source=AssetDatabase.LoadAssetAtPath<Font>("Assets/WorkSpace/USW/Prefab/etc/RixInooAriDuriRound.ttf");
        var font=TMP_FontAsset.CreateFontAsset(source,64,8,UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,1024,1024);
        AssetDatabase.CreateAsset(font,Prefix+"Font.asset");AssetDatabase.AddObjectToAsset(font.material,font);
        font.TryAddCharacters("거리 비교 · 준비 중 현재 조금 가까움 많이 가까움 일시정지 전투 재생 같은 배치 카메라와 크기 고정 거리 비교를 준비하지 못했습니다",out string missing);
        foreach(var atlas in font.atlasTextures)if(!AssetDatabase.Contains(atlas))AssetDatabase.AddObjectToAsset(atlas,font);
        font.atlasPopulationMode=AtlasPopulationMode.Static;
        EditorUtility.SetDirty(font);AssetDatabase.SaveAssetIfDirty(font);
        return font;
    }

    private static RectTransform Rect(Transform p,string name,float x,float y,float w,float h)
    {
        var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(p,false);
        r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);
        r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(w,h);return r;
    }
    private static TMP_Text Text(Transform p,TMP_FontAsset font,string value,float x,float y,float w,float h,float size)
    {
        var t=Rect(p,"Label",x,y,w,h).gameObject.AddComponent<TextMeshProUGUI>();
        t.font=font;t.text=value;t.fontSize=size;t.color=Color.white;t.raycastTarget=false;
        t.alignment=TextAlignmentOptions.Center;t.textWrappingMode=TextWrappingModes.NoWrap;return t;
    }
    private static Button Button(Transform p,TMP_FontAsset font,string value,float x,float y,float w)
    {
        var r=Rect(p,value,x,y,w,58);var image=r.gameObject.AddComponent<Image>();image.color=new Color(.12f,.16f,.23f,.96f);
        var b=r.gameObject.AddComponent<Button>();b.targetGraphic=image;Text(r,font,value,0,0,w,58,26);return b;
    }
}
