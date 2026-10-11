using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class TotemChoiceMotionLabBuilder
{
    public const string ScenePath = "Assets/WorkSpace/USW/Scene/FxLab_TotemChoice.unity";
    private static TMP_FontAsset _font;
    private static readonly Color Ink = new Color(.045f, .052f, .08f);

    [MenuItem("Tools/USW/Fx/Build Totem Choice Motion Lab")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) return;
        // Preserve open scenes, including any unsaved work, while authoring the lab.
        var current = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        bool cleanUntitled = string.IsNullOrEmpty(current.path) && !current.isDirty;
        if (string.IsNullOrEmpty(current.path) && current.isDirty &&
            !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
            cleanUntitled ? NewSceneMode.Single : NewSceneMode.Additive);
        UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/WorkSpace/USW/Prefab/etc/RixInooAriDuriRound SDF.asset");
        var camera = new GameObject("Totem Lab Camera", typeof(Camera)).GetComponent<Camera>();
        camera.tag = "MainCamera";
        camera.orthographic = true;
        camera.transform.position = new Vector3(0, 0, -10);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Ink;
        var root = Rect("Totem Choice Motion Lab", null, Vector2.zero, new Vector2(1080, 2340));
        var canvas = root.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 5;
        var scaler = root.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 2340);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        root.gameObject.AddComponent<GraphicRaycaster>();
        var stage = Rect("Portrait Stage", root, Vector2.zero, new Vector2(1080, 2340));
        Fill("Backdrop", stage, Vector2.zero, new Vector2(1080,2340), Ink);
        Text("Eyebrow", stage, "BONUS PICK  /  TOTEM", new Vector2(0, 1110), new Vector2(950,50), 28, new Color(1,.8f,.18f));
        Text("Heading", stage, "토템을 선택하세요!", new Vector2(0, 1040), new Vector2(1000,100), 67, Color.white);
        var board = Rect("Diagonal Choices", stage, new Vector2(0,-5), new Vector2(1080,1900));
        board.gameObject.AddComponent<RectMask2D>();
        var lab = root.gameObject.AddComponent<TotemChoiceMotionLab>();
        lab.Bands = new RectTransform[3]; lab.Icons = new RectTransform[3]; lab.Copy = new RectTransform[3];
        lab.Groups = new CanvasGroup[3]; lab.SpeedLines = new CanvasGroup[3]; lab.Choices = new Button[3]; lab.Modes = new Button[2];
        var colors = new[] { new Color(0,.80f,.91f), new Color(1,.23f,.065f), new Color(1,.75f,0) };
        var icons = new[] { "SPR_TD1005", "TD_1007", "TD_1010" };
        var names = new[] { "TD1008(미정)", "TD1013(미정)", "TD1022(미정)" };
        var descriptions = new[] { "유닛의 등급을 임시로\n한 단계 올립니다.", "유닛의 일반 공격을\n그림자가 재현합니다.", "10초마다 <color=#FFED9B>식량 30</color>을\n생산합니다." };
        var points = new[] {
            new[] {new Vector2(0,1),new Vector2(1,1),new Vector2(1,.78f),new Vector2(0,.62f)},
            new[] {new Vector2(0,.62f),new Vector2(1,.78f),new Vector2(1,.43f),new Vector2(0,.22f)},
            new[] {new Vector2(0,.22f),new Vector2(1,.43f),new Vector2(1,0),new Vector2(0,0)}
        };
        for (int i = 0; i < 3; i++)
        {
            var band = Rect("Choice " + (i+1), board, Vector2.zero, board.sizeDelta);
            lab.Bands[i] = band;
            lab.Groups[i] = band.gameObject.AddComponent<CanvasGroup>();
            var shape = band.gameObject.AddComponent<UIPolygonGraphic>();
            shape.color = colors[i];
            shape.SetShape(points[i], i == 0 ? new[]{2} : i == 1 ? new[]{0,2} : new[]{0}, Ink, 16);
            var button = band.gameObject.AddComponent<Button>();
            button.targetGraphic = shape; button.transition = Selectable.Transition.None;
            lab.Choices[i] = button;
            bool right = i == 1;
            float iconY = i == 0 ? 600 : i == 1 ? 120 : -600;
            float textY = i == 0 ? 670 : i == 1 ? -35 : -490;
            var icon = Fill("Totem Artwork", band, new Vector2(right ? 300 : -310,iconY), new Vector2(365,365), Color.white);
            icon.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Imports/GGD_ArtWork/KHJ_Artwork/Totem_Sprite/" + icons[i] + ".png");
            icon.preserveAspect = true;
            lab.Icons[i] = icon.rectTransform;
            var copy = Rect("Name and Description", band, new Vector2(right ? -190 : 180,textY), new Vector2(600,240));
            lab.Copy[i] = copy;
            Text("Name", copy, names[i], new Vector2(0,90), new Vector2(610,95), 61, Color.white);
            Fill("Description Backplate", copy, new Vector2(0,-40), new Vector2(575,150), new Color(0,0,0,.19f));
            Text("Description", copy, descriptions[i], new Vector2(0,-40), new Vector2(540,130), 39, Color.white);
            var lines = Rect("Impact Speed Lines", band, Vector2.zero, board.sizeDelta);
            lab.SpeedLines[i] = lines.gameObject.AddComponent<CanvasGroup>();
            lab.SpeedLines[i].alpha = 0;
            for (int j = 0; j < 5; j++)
            {
                var line = Fill("Streak " + j, lines, new Vector2((j%2==0 ? -1:1)*430,iconY + (j-2)*65), new Vector2(140+j*19,7+j%2*4), Color.white);
                line.rectTransform.localRotation = Quaternion.Euler(0,0,18);
            }
        }
        lab.Status = Text("Selection Hint", stage, "마음에 드는 토템을 골라주세요!", new Vector2(0,-985), new Vector2(1040,55), 31, Color.white);
        lab.ModeLabel = Text("Motion Label", stage, "02  DIAGONAL RUSH", new Vector2(0,-1032), new Vector2(1000,40), 23, new Color(.68f,.75f,.86f));
        var modeNames = new[] { "1  슬래시", "2  러시" };
        for (int i = 0; i < 2; i++) lab.Modes[i] = MakeButton(modeNames[i], stage, new Vector2(-345+i*345,-1100), new Vector2(325,83));
        lab.Replay = MakeButton("다시 보기", stage, new Vector2(345,-1100), new Vector2(325,83));
        new GameObject("Lab EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        EditorSceneManager.SaveScene(scene, ScenePath);
        TotemChoiceCardLabBuilder.AddComparison();
        Selection.activeGameObject = root.gameObject;
        Debug.Log("[TotemChoiceMotionLab] Built " + ScenePath);
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false); rt.sizeDelta = size; rt.anchoredPosition = position;
        return rt;
    }

    private static Image Fill(string name, Transform parent, Vector2 position, Vector2 size, Color color)
    {
        var image = Rect(name,parent,position,size).gameObject.AddComponent<Image>();
        image.color = color; image.raycastTarget = false; return image;
    }

    private static TMP_Text Text(string name, Transform parent, string value, Vector2 position, Vector2 size, float fontSize, Color color)
    {
        var text = Rect(name,parent,position,size).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = _font; text.text = value; text.fontSize = fontSize; text.color = color;
        text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
        text.outlineColor = Ink; text.outlineWidth = .18f;
        return text;
    }

    private static Button MakeButton(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var image = Fill(name,parent,position,size,new Color(.21f,.24f,.33f));
        image.raycastTarget = true;
        var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        Text("Label",image.transform,name,Vector2.zero,size,30,Color.white);
        return button;
    }
}
