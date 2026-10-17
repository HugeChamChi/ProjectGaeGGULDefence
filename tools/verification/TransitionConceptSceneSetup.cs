using System;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.IO;
using Cysharp.Threading.Tasks;

public static class TransitionConceptSceneSetup
{
    const string Root="Assets/WorkSpace/HSD/Shaders/Transition/Concepts/";
    public static string Build()
    {
        if(EditorApplication.isPlaying) throw new Exception("Stop play mode before authoring.");
        bool dirty=false;
        for(int i=0;i<SceneManager.sceneCount;i++) dirty|=SceneManager.GetSceneAt(i).isDirty;
        if(dirty) throw new Exception("Open scene has unsaved changes; save it before rebuilding the test scene.");
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        SceneManager.SetActiveScene(scene);
        var camera=new GameObject("Preview Camera").AddComponent<Camera>();
        camera.clearFlags=CameraClearFlags.SolidColor;
        camera.backgroundColor=new Color(.2f,.3f,.27f);
        camera.tag="MainCamera";
        var canvas=new GameObject("Transition Preview",typeof(RectTransform),typeof(Canvas)).GetComponent<Canvas>();
        canvas.renderMode=RenderMode.ScreenSpaceOverlay;
        var scaler=canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=new Vector2(1080,1920);
        scaler.matchWidthOrHeight=.5f;
        var image=new GameObject("Preview",typeof(RectTransform),typeof(RawImage)).GetComponent<RawImage>();
        image.transform.SetParent(canvas.transform,false);
        image.rectTransform.anchorMin=Vector2.zero; image.rectTransform.anchorMax=Vector2.one;
        image.rectTransform.offsetMin=image.rectTransform.offsetMax=Vector2.zero;
        image.texture=Texture2D.whiteTexture; image.raycastTarget=false;
        var stage=new GameObject("Unit silhouette stage",typeof(RectTransform)).GetComponent<RectTransform>();
        stage.SetParent(canvas.transform,false);
        stage.anchorMin=stage.anchorMax=new Vector2(.5f,.55f);
        stage.sizeDelta=new Vector2(720,360);
        var showcase=stage.gameObject.AddComponent<TransitionSilhouetteShowcase>();
        string silhouettePath=Root+"UnitSilhouette.mat";
        var silhouette=AssetDatabase.LoadAssetAtPath<Material>(silhouettePath);
        if(silhouette==null)
        {
            silhouette=new Material(Shader.Find("GaeGGUL/UI/TransitionUnitSilhouette"));
            AssetDatabase.CreateAsset(silhouette,silhouettePath);
        }
        var ss=new SerializedObject(showcase);
        ss.FindProperty("silhouetteTemplate").objectReferenceValue=silhouette;
        var units=ss.FindProperty("units"); units.arraySize=3;
        string art="Assets/Imports/GGD_ArtWork/LHH_Artwork/Drone_Deck/Spine_Artwork/";
        string[] unitNames={"SPN_Char_Alphan_SkeletonData","SPN_Char_Betang_SkeletonData","SPN_Char_Deltan_SkeletonData"};
        for(int i=0;i<3;i++) units.GetArrayElementAtIndex(i).objectReferenceValue=
            AssetDatabase.LoadAssetAtPath<Spine.Unity.SkeletonDataAsset>(art+unitNames[i]+".asset");
        ss.ApplyModifiedPropertiesWithoutUndo();
        var lab=new GameObject("Transition Test Controls").AddComponent<TransitionTestScene>();
        var so=new SerializedObject(lab);
        so.FindProperty("preview").objectReferenceValue=image;
        so.FindProperty("showcase").objectReferenceValue=showcase;
        var parade=AssetDatabase.LoadAssetAtPath<Material>(Root+"ParadeTransition.mat");
        EditorUtility.SetDirty(parade);
        so.FindProperty("transitionMaterial").objectReferenceValue=parade;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(scene,Root+"TransitionTestScene.unity");
        AssetDatabase.SaveAssets();
        return "Saved standalone parade-only test scene. Build list unchanged.";
    }
    public static async Task<string> RecordParade()
    {
        var lab=UnityEngine.Object.FindFirstObjectByType<TransitionTestScene>();
        var show=UnityEngine.Object.FindFirstObjectByType<TransitionSilhouetteShowcase>();
        bool priorLoop=lab.Loop;
        lab.Loop=false;
        lab.PlayCycle();
        string folder="outputs/transition-concepts/parade-journey";
        Directory.CreateDirectory(folder);
        bool full=false,exit=false;
        float previous=0;
        for(int i=0;i<95;i++)
        {
            await UniTask.WaitForEndOfFrame(lab);
            if(show.LoadingProgress<previous) throw new Exception("Progress moved backwards");
            previous=show.LoadingProgress;
            full|=previous==1;
            if(show.IsExiting && previous!=1) throw new Exception("Exited before loading completed");
            exit|=show.IsExiting;
            for(int u=0;u<3;u++)
                if(show.transform.Find("Unit "+u).localScale.x>=0) throw new Exception("Unit is not facing right");
            var source=ScreenCapture.CaptureScreenshotAsTexture();
            var rt=RenderTexture.GetTemporary(360,640,0);
            var saved=RenderTexture.active;
            Graphics.Blit(source,rt); RenderTexture.active=rt;
            var tex=new Texture2D(360,640,TextureFormat.RGB24,false);
            tex.ReadPixels(new Rect(0,0,360,640),0,0); tex.Apply();
            File.WriteAllBytes(folder+"/"+i.ToString("D3")+".png",tex.EncodeToPNG());
            RenderTexture.active=saved; RenderTexture.ReleaseTemporary(rt);
            UnityEngine.Object.Destroy(source); UnityEngine.Object.Destroy(tex);
            await Task.Delay(75);
        }
        if(!full || !exit || lab.Progress!=1 || lab.IsRunning) throw new Exception("Full -> run-out -> clear cycle incomplete");
        for(int u=0;u<3;u++)
            if(((RectTransform)show.transform.Find("Unit "+u)).anchoredPosition.x<600) throw new Exception("Unit did not run off right");
        lab.Loop=priorLoop;
        return "PASS right-facing trio, monotonic progress, full before departure, rightward run-out and clear endpoint. Captured 95 frames.";
    }
    public static string CheckVisuals()
    {
        var shader=Shader.Find("GaeGGUL/CasualTransition");
        if(ShaderUtil.ShaderHasError(shader)) throw new Exception("Transition shader compilation failed");
        var m=new Material(AssetDatabase.LoadAssetAtPath<Material>(Root+"ParadeTransition.mat"));
        m.SetFloat("_Preview",0);
        try
        {
            foreach(var size in new[]{new Vector2Int(560,560),new Vector2Int(315,560),new Vector2Int(560,315)})
            {
                m.SetFloat("_Aspect",(float)size.x/size.y);
                foreach(float p in new[]{0f,.5f,1f})
                {
                    m.SetFloat("_Progress",p);
                    var tex=Render(m,size.x,size.y);
                    foreach(var c in tex.GetPixels32()) if(p==.5f ? c.a<254 : c.a>1) throw new Exception("Endpoint failure");
                    UnityEngine.Object.DestroyImmediate(tex);
                }
                foreach(float p in new[]{.2f,.7f})
                {
                    m.SetFloat("_Progress",p);
                    var tex=Render(m,size.x,size.y);
                    float top=0,bottom=0;
                    for(int x=0;x<size.x/3;x++) for(int y=0;y<size.y/3;y++)
                    {
                        top+=tex.GetPixel(x,size.y-1-y).a;
                        bottom+=tex.GetPixel(size.x-1-x,y).a;
                    }
                    UnityEngine.Object.DestroyImmediate(tex);
                    if(p<.5f ? top<=bottom : top>=bottom) throw new Exception("Wrong diagonal direction");
                }
            }
            m.SetFloat("_Aspect",1); m.SetFloat("_Progress",.5f); m.SetFloat("_Clock",0);
            var a=Render(m,560,560);
            m.SetFloat("_Clock",1);
            var b=Render(m,560,560);
            float error=0; int count=0,peaks=0;
            bool wasIcon=false;
            float cover=m.GetColor("_Color").r;
            for(int x=0;x<560;x++)
            {
                bool icon=a.GetPixel(x,300).r<cover-.015f;
                if(icon && !wasIcon) peaks++;
                wasIcon=icon;
            }
            for(int x=12;x<530;x+=4) for(int y=12;y<530;y+=4)
            {
                var d=a.GetPixel(x,y)-b.GetPixel(x+24,y+24);
                error+=Mathf.Abs(d.r)+Mathf.Abs(d.g)+Mathf.Abs(d.b); count++;
            }
            UnityEngine.Object.DestroyImmediate(a); UnityEngine.Object.DestroyImmediate(b);
            if(peaks<13 || peaks>15) throw new Exception("Unexpected icon density: "+peaks);
            if(error/count>.005f) throw new Exception("Upper-right scroll displacement mismatch");
            return "PASS: 9 alpha endpoints, 6 diagonal direction checks, 14 icons/axis and 24px upper-right scroll per second at 560px (3x previous speed).";
        }
        finally { UnityEngine.Object.DestroyImmediate(m); }
    }
    private static Texture2D Render(Material material,int width,int height)
    {
        var target=RenderTexture.GetTemporary(width,height,0,RenderTextureFormat.ARGB32);
        var previous=RenderTexture.active;
        try
        {
            RenderTexture.active=target;
            GL.Clear(true,true,Color.clear);
            Graphics.Blit(Texture2D.whiteTexture,target,material);
            var texture=new Texture2D(width,height,TextureFormat.RGBA32,false);
            texture.ReadPixels(new Rect(0,0,width,height),0,0); texture.Apply();
            return texture;
        }
        finally { RenderTexture.active=previous; RenderTexture.ReleaseTemporary(target); }
    }
    public static string Dependencies()
    {
        Directory.CreateDirectory("outputs/transition-concepts");
        var paths=AssetDatabase.GetDependencies(Root+"TransitionTestScene.unity",true);
        File.WriteAllLines("outputs/transition-concepts/dependencies.txt",paths);
        return "Wrote "+paths.Length+" scene dependencies.";
    }
}
