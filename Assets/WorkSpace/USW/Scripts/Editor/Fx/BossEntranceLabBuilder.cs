using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class BossEntranceLabBuilder
{
    public const string ScenePath = "Assets/WorkSpace/USW/Scene/FxLab_BossEntrance.unity";
    private static TMP_FontAsset _font;
    private static Sprite _round;
    private static TMP_FontAsset _latin;
    private static readonly Color Ink = new Color(.045f, .055f, .075f);
    private static readonly Color Red = new Color(1, .16f, .23f);
    private static readonly Color Cream = new Color(1, .98f, .93f);

    [MenuItem("Tools/USW/Fx/Build Boss Entrance Lab")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        PrepareFont();
        _latin = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        _round = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        var camera = new GameObject("Boss Entrance Camera", typeof(Camera)).GetComponent<Camera>();
        camera.tag = "MainCamera";
        camera.transform.position = new Vector3(0, 0, -10);
        camera.orthographic = true;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Ink;
        var root = Rect("Boss Entrance Lab", null, 0, 0, 1080, 1920);
        var canvas = root.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 5;
        var scaler = root.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        root.gameObject.AddComponent<GraphicRaycaster>();
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        var lab = root.gameObject.AddComponent<BossEntranceLab>();
        var stage = Rect("Portrait Preview", root, 0, 0, 1080, 1920);
        stage.gameObject.AddComponent<RectMask2D>();
        Background(stage, lab);
        var edge = Rect("Pulsing red border", root, 0, 0, 0, 0);
        edge.anchorMin = Vector2.zero;
        edge.anchorMax = Vector2.one;
        var glow = edge.gameObject.AddComponent<BossEntranceGlow>();
        glow.color = new Color(1, .025f, .06f, .85f);
        glow.raycastTarget = false;
        lab.BorderGlow = edge.gameObject.AddComponent<CanvasGroup>();
        lab.BorderGlow.blocksRaycasts = false;
        var warning = Rect("Opposing warning tapes", root, 0, 70, 1080, 900);
        lab.Warning = warning.gameObject.AddComponent<CanvasGroup>();
        lab.Warning.blocksRaycasts = false;
        Tape(warning, lab);
        Controls(root, lab);
        lab.RenderAt(.65f);
        SaveFont(root);
        EditorSceneManager.SaveScene(scene, ScenePath);
        Selection.activeGameObject = root.gameObject;
    }

    private static void PrepareFont()
    {
        const string path = "Assets/WorkSpace/USW/Materials/BossEntranceLabFont.asset";
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (_font != null) { _font.atlasPopulationMode = AtlasPopulationMode.Dynamic; return; }
        var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/WorkSpace/USW/Prefab/etc/RixInooAriDuriRound.ttf");
        _font = TMP_FontAsset.CreateFontAsset(source, 72, 8,
            UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 2048, 2048);
        _font.name = "BossEntranceLabFont";
        AssetDatabase.CreateAsset(_font, path);
        AssetDatabase.AddObjectToAsset(_font.material, _font);
    }

    private static void SaveFont(RectTransform root)
    {
        var characters = "자동 반복 OFF";
        foreach (var text in root.GetComponentsInChildren<TMP_Text>(true)) characters += text.text;
        _font.TryAddCharacters(characters, out string missing);
        if (!string.IsNullOrEmpty(missing)) Debug.LogWarning("Boss lab font missing: " + missing);
        foreach (var atlas in _font.atlasTextures)
            if (!AssetDatabase.Contains(atlas)) AssetDatabase.AddObjectToAsset(atlas, _font);
        _font.atlasPopulationMode = AtlasPopulationMode.Static;
        EditorUtility.SetDirty(_font);
        AssetDatabase.SaveAssetIfDirty(_font);
    }

    private static void Background(RectTransform p, BossEntranceLab lab)
    {
        Box("Arena", p, 0, 0, 1080, 1920, new Color(.1f, .2f, .24f));
        for (int i = -5; i <= 5; i++) Box("Grid column", p, i * 110, 0, 2, 1700, new Color(.2f,.34f,.37f,.3f));
        for (int i = -7; i <= 7; i++) Box("Grid row", p, 0, i * 110, 1080, 2, new Color(.2f,.34f,.37f,.3f));
        Label("Lab", p, "BOSS ENCOUNTER / MOTION STUDY", 0, 858, 980, 50, 25, new Color(.6f,.8f,.81f));
        Label("Title", p, "보스 등장 연출", 0, 787, 980, 95, 53, Cream);
        Label("Wave", p, "WAVE 10     /     BOSS STAGE", 0, 682, 840, 60, 28, new Color(.6f,.8f,.81f));
        Box("Lane", p, 0, -455, 880, 7, new Color(.37f,.7f,.7f,.4f));
        for (int i = 0; i < 3; i++)
        {
            var unit = Rect("Friendly unit", p, (i - 1) * 245, -465, 130, 130);
            Disc("Shadow", unit, 0, -35, 145, new Color(0,0,0,.2f));
            Box("Body", unit, 0, 0, 116, 100, new Color(.47f,.83f,.78f), true);
            Box("Visor", unit, 0, 8, 82, 34, Ink, true);
            Box("Eye L", unit, -20, 8, 10, 12, Cream);
            Box("Eye R", unit, 20, 8, 10, 12, Cream);
        }
        Label("Standby", p, "방어선을 지켜 주세요!", 0, -590, 900, 60, 28, new Color(.65f,.83f,.82f));
        lab.Boss = Rect("Boss arrival marker", p, 0, 500, 280, 240);
        lab.BossAlpha = lab.Boss.gameObject.AddComponent<CanvasGroup>();
        Face(lab.Boss, 0, 0, 1);
        Label("Boss name", lab.Boss, "BOSS", 0, -133, 300, 50, 30, Cream);
    }

    private static void Tape(RectTransform p, BossEntranceLab lab)
    {
        p.localEulerAngles = new Vector3(0, 0, -5);
        // Round Korean type supplies the casual accent; the tapes use crisp italic sans.
        lab.Headline = Rect("Title slide", p, 0, 0, 1000, 270);
        lab.TitleAlpha = lab.Headline.gameObject.AddComponent<CanvasGroup>();
        var title = Label("Headline", lab.Headline, "보스 출현", 0, -4, 960, 138, 104, Cream);
        title.fontStyle = FontStyles.Italic;
        lab.Tapes = new RectTransform[2];
        lab.TapeContents = new RectTransform[2];
        for (int i = 0; i < 2; i++)
        {
            var tape = Rect(i == 0 ? "Upper tape - left" : "Lower tape - right", p, 0, i == 0 ? 185 : -185, 2200, 78);
            lab.Tapes[i] = tape;
            var background = tape.gameObject.AddComponent<Image>();
            background.color = new Color(1, .045f, .09f, .48f);
            background.raycastTarget = false;
            // Stencil masking follows the tilted tape; RectMask2D assumes an axis-aligned rectangle.
            tape.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var content = Rect("Seamless text conveyor", tape, 0, 0, 4320, 78);
            lab.TapeContents[i] = content;
            for (int j = 0; j < 12; j++)
            {
                float x = (j - 5.5f) * BossEntranceLab.TileWidth;
                var word = Label("WARNING tile " + j, content, "WARNING", x - 22, 0, 290, 74, 45, Cream);
                word.font = _latin; word.fontStyle = FontStyles.Bold | FontStyles.Italic;
                word.characterSpacing = 1;
                var slash = Box("Separator", content, x + 152, 0, 8, 30, new Color(1, .98f, .93f, .65f));
                slash.rectTransform.localEulerAngles = new Vector3(0, 0, -20);
            }
        }
    }

    private static void Controls(RectTransform root, BossEntranceLab lab)
    {
        var p = Rect("Preview controls", root, 0, -793, 1020, 240);
        Box("Controls backing", p, 0, 0, 1020, 240, Ink, true);
        var caption = Label("Caption", p, "OPPOSING WARNING  /  1.95 SEC", 0, 73, 960, 50, 26, Cream);
        caption.font = _latin; caption.fontStyle = FontStyles.Bold; caption.characterSpacing = 2;
        Label("Direction", p, "상단은 왼쪽으로  /  하단은 오른쪽으로", 0, 20, 960, 45, 23, new Color(.6f,.69f,.73f));
        var replay = Button(p, "다시 재생", -245, -52, 455, 72);
        replay.GetComponent<Image>().color = Red;
        UnityEventTools.AddPersistentListener(replay.onClick, lab.Replay);
        var loop = Button(p, "자동 반복 ON", 245, -52, 455, 72);
        lab.Playback = loop.GetComponentInChildren<TMP_Text>();
        UnityEventTools.AddPersistentListener(loop.onClick, lab.ToggleRepeat);
        Box("Track", p, 0, -112, 955, 4, new Color(.23f,.32f,.36f));
        lab.Progress = Box("Progress", p, 0, -112, 955, 4, Red);
        lab.Progress.sprite = _round; lab.Progress.type = Image.Type.Filled;
        lab.Progress.fillMethod = Image.FillMethod.Horizontal;
    }
    private static void Face(Transform p, float x, float y, float scale)
    {
        var face = Rect("Boss badge", p, x, y, 220, 190); face.localScale = Vector3.one * scale;
        Box("Face", face, 0, 0, 215, 160, Ink, true);
        var left = Box("Horn L", face, -82, 87, 47, 75, Cream, true); left.rectTransform.localEulerAngles = new Vector3(0,0,25);
        var right = Box("Horn R", face, 82, 87, 47, 75, Cream, true); right.rectTransform.localEulerAngles = new Vector3(0,0,-25);
        Box("Eye L", face, -46, 13, 32, 25, Cream, true);
        Box("Eye R", face, 46, 13, 32, 25, Cream, true);
        Box("Mouth", face, 0, -39, 60, 13, Red, true);
    }

    private static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
    {
        var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        r.SetParent(parent, false); r.sizeDelta = new Vector2(w,h); r.anchoredPosition = new Vector2(x,y); return r;
    }
    private static Image Box(string name, Transform p, float x, float y, float w, float h, Color c, bool rounded = false)
    {
        var image = Rect(name,p,x,y,w,h).gameObject.AddComponent<Image>();
        image.color=c; image.raycastTarget=false;
        if (rounded) { image.sprite=_round; image.type=Image.Type.Sliced; }
        return image;
    }
    private static void Disc(string name, Transform p, float x, float y, float size, Color c)
    {
        var graphic=Rect(name,p,x,y,size,size).gameObject.AddComponent<UIPolygonGraphic>();
        var points=new Vector2[96];
        for(int i=0;i<points.Length;i++)
        {
            float angle=i*Mathf.PI*2/points.Length;
            points[i]=new Vector2(.5f+Mathf.Cos(angle)*.5f,.5f+Mathf.Sin(angle)*.5f);
        }
        graphic.color=c; graphic.raycastTarget=false;
        graphic.SetShape(points,null,c,0);
    }
    private static TMP_Text Label(string name, Transform p, string text, float x, float y, float w, float h, float size, Color color)
    {
        var t=Rect(name,p,x,y,w,h).gameObject.AddComponent<TextMeshProUGUI>();
        t.font=_font; t.text=text; t.fontSize=size; t.color=color;
        t.alignment=TextAlignmentOptions.Center; t.raycastTarget=false;
        t.textWrappingMode=TextWrappingModes.NoWrap;
        return t;
    }
    private static Button Button(Transform p, string text, float x, float y, float w, float h)
    {
        var image=Box(text,p,x,y,w,h,new Color(.16f,.23f,.28f),true); image.raycastTarget=true;
        var b=image.gameObject.AddComponent<Button>(); b.targetGraphic=image;
        Label("Label",image.transform,text,0,0,w-12,h-6,26,Cream); return b;
    }
}
