using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class TotemChoiceCardLabBuilder
{
    private static readonly Color Ink = new Color(.055f, .105f, .14f);
    private static TMP_FontAsset _font;
    private static Material _textMaterial;
    public const string TextMaterialPath = "Assets/WorkSpace/USW/Materials/TotemChoiceCardText.mat";

    [MenuItem("Tools/USW/Fx/Add Card Format To Totem Lab")]
    public static void AddComparison()
    {
        if (EditorApplication.isPlaying) return;
        var diagonal = Object.FindFirstObjectByType<TotemChoiceMotionLab>();
        if (diagonal == null || diagonal.GetComponent<TotemChoiceCardLab>() != null) return;
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/WorkSpace/USW/Prefab/etc/RixInooAriDuriRound SDF.asset");
        _textMaterial = AssetDatabase.LoadAssetAtPath<Material>(TextMaterialPath);
        if (_textMaterial == null)
        {
            _textMaterial = new Material(_font.material) { name = "TotemChoiceCardText" };
            _textMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0);
            AssetDatabase.CreateAsset(_textMaterial, TextMaterialPath);
        }
        var lab = diagonal.gameObject.AddComponent<TotemChoiceCardLab>();
        lab.Diagonal = diagonal;
        lab.DiagonalStage = diagonal.transform.Find("Portrait Stage").gameObject;
        lab.DiagonalStage.transform.Find("Eyebrow").gameObject.SetActive(false);
        var stage = Rect("Card Format", diagonal.transform, Vector2.zero, new Vector2(1080, 2340));
        lab.CardStage = stage.gameObject;
        Round("Background", stage, Vector2.zero, stage.sizeDelta, 0, Ink);
        Round("Background Bubble Left", stage, new Vector2(-420, 830), new Vector2(440, 440), 220, new Color(.075f,.16f,.20f));
        Round("Background Bubble Right", stage, new Vector2(430,-750), new Vector2(610,610),305,new Color(.075f,.16f,.20f));
        Label("Heading",stage,"보너스 토템",new Vector2(0,1010),new Vector2(1000,120),75,Color.white);
        Label("Subtitle",stage,"이번 전투의 한 수를 골라보세요",new Vector2(0,913),new Vector2(950,70),34,new Color(.70f,.83f,.84f));
        Label("Section",stage,"세 가지 능력, 하나의 선택!",new Vector2(0,804),new Vector2(950,55),29,new Color(1,.82f,.34f));
        lab.Cards = new RectTransform[3]; lab.Icons = new RectTransform[3];
        lab.Groups = new CanvasGroup[3]; lab.Choices = new Button[3]; lab.SelectedBadges = new GameObject[3];
        string[] names = { "등급 상승", "그림자 공격", "식량 풍차" };
        string[] ids = { "TD1008", "TD1013", "TD1022" };
        string[] tags = { "한 단계 더 강하게", "한 번 더 공격!", "차곡차곡 식량 충전" };
        string[] descriptions = { "유닛의 등급을 임시로\n한 단계 올립니다.", "유닛의 일반 공격을\n그림자가 재현합니다.", "10초마다 식량 30을\n생산합니다." };
        string[] assets = { "SPR_TD1005", "TD_1007", "TD_1010" };
        Color[] accents = { new Color(.15f,.82f,.81f), new Color(1,.48f,.34f), new Color(1,.78f,.23f) };
        Color[] surfaces = { new Color(.87f,.98f,.95f), new Color(1,.93f,.86f), new Color(1,.98f,.85f) };
        for (int i=0;i<3;i++)
        {
            var card = Rect("Card " + (i+1),stage,new Vector2(0,510-i*540),new Vector2(960,470));
            lab.Cards[i]=card;
            lab.Groups[i]=card.gameObject.AddComponent<CanvasGroup>();
            Round("Drop Shadow",card,new Vector2(0,-16),new Vector2(960,470),48,new Color(0,0,0,.35f));
            Round("Outline",card,Vector2.zero,new Vector2(960,470),48,Ink);
            var surface=Round("Card Surface",card,Vector2.zero,new Vector2(946,456),42,surfaces[i]);
            surface.raycastTarget=true;
            var button=surface.gameObject.AddComponent<Button>();
            button.targetGraphic=surface; button.transition=Selectable.Transition.None;
            lab.Choices[i]=button;
            Round("Icon Rim",card,new Vector2(-290,2),new Vector2(298,298),149,Ink);
            Round("Icon Badge",card,new Vector2(-290,6),new Vector2(284,284),142,accents[i]);
            Round("Icon Highlight",card,new Vector2(-325,53),new Vector2(156,156),78,new Color(1,1,1,.19f));
            var icon=Rect("Totem Artwork",card,new Vector2(-290,18),new Vector2(310,310)).gameObject.AddComponent<Image>();
            icon.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Imports/GGD_ArtWork/KHJ_Artwork/Totem_Sprite/"+assets[i]+".png");
            icon.preserveAspect=true; icon.raycastTarget=false;
            lab.Icons[i]=icon.rectTransform;
            Label("Number",card,"0"+(i+1),new Vector2(-409,182),new Vector2(90,50),30,Ink);
            Label("Id",card,ids[i],new Vector2(-290,-185),new Vector2(270,50),25,new Color(.29f,.40f,.43f));
            Round("Ability Tag",card,new Vector2(169,146),new Vector2(440,62),31,accents[i]);
            Label("Tag",card,tags[i],new Vector2(169,146),new Vector2(430,60),28,Ink);
            Label("Name",card,names[i],new Vector2(169,55),new Vector2(460,95),58,Ink);
            Label("Description",card,descriptions[i],new Vector2(169,-49),new Vector2(460,125),35,Ink);
            Label("Action",card,"이 토템 선택  >",new Vector2(201,-171),new Vector2(400,55),28,Ink);
            var badge=Round("Selected Badge",card,new Vector2(201,-171),new Vector2(400,64),32,Ink);
            Label("Selected Label",badge.transform,"선택 완료!",Vector2.zero,new Vector2(380,60),28,Color.white);
            lab.SelectedBadges[i]=badge.gameObject; badge.gameObject.SetActive(false);
        }
        lab.Status=Label("Selection Hint",stage,"원하는 토템 카드를 눌러주세요",new Vector2(0,-910),new Vector2(1000,70),32,Color.white);
        lab.Replay=Button("다시 보기",stage,new Vector2(0,-1020),new Vector2(540,100),new Color(.18f,.29f,.34f));
        Label("Footer",stage,"TOTEM  /  BONUS PICK",new Vector2(0,-1110),new Vector2(900,50),23,new Color(.48f,.66f,.70f));
        var tabs=Rect("Format Comparison",diagonal.transform,new Vector2(0,1120),new Vector2(800,70));
        lab.DiagonalTab=Button("A  대각선",tabs,new Vector2(-204,0),new Vector2(390,70),new Color(.17f,.23f,.29f));
        lab.CardTab=Button("B  카드형",tabs,new Vector2(204,0),new Vector2(390,70),new Color(1,.8f,.27f));
        lab.DiagonalStage.SetActive(false);
        EditorSceneManager.MarkSceneDirty(diagonal.gameObject.scene);
        EditorSceneManager.SaveScene(diagonal.gameObject.scene);
    }

    private static RectTransform Rect(string name,Transform parent,Vector2 position,Vector2 size)
    {
        var rt=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent,false); rt.anchoredPosition=position; rt.sizeDelta=size;
        return rt;
    }

    // Convex rounded shapes use the project's existing polygon UI and raycast filter.
    private static UIPolygonGraphic Round(string name,Transform parent,Vector2 position,Vector2 size,float radius,Color color)
    {
        var graphic=Rect(name,parent,position,size).gameObject.AddComponent<UIPolygonGraphic>();
        var points=new List<Vector2>();
        radius=Mathf.Min(radius,Mathf.Min(size.x,size.y)*.5f);
        for(int corner=0;corner<4;corner++)
        {
            var center=new Vector2(corner==0||corner==3?size.x-radius:radius,corner<2?size.y-radius:radius);
            for(int step=0;step<=10;step++)
            {
                float angle=(corner*90f+step*9f)*Mathf.Deg2Rad;
                var point=center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius;
                points.Add(new Vector2(point.x/size.x,point.y/size.y));
            }
        }
        graphic.SetShape(points,null,Color.clear,0); graphic.color=color; graphic.raycastTarget=false;
        return graphic;
    }

    private static TMP_Text Label(string name,Transform parent,string text,Vector2 position,Vector2 size,float fontSize,Color color)
    {
        var label=Rect(name,parent,position,size).gameObject.AddComponent<TextMeshProUGUI>();
        label.font=_font; label.text=text; label.fontSize=fontSize; label.color=color;
        label.fontSharedMaterial=_textMaterial;
        label.alignment=TextAlignmentOptions.Center; label.raycastTarget=false;
        return label;
    }

    private static Button Button(string name,Transform parent,Vector2 position,Vector2 size,Color color)
    {
        var graphic=Round(name,parent,position,size,size.y*.5f,color); graphic.raycastTarget=true;
        var button=graphic.gameObject.AddComponent<Button>(); button.targetGraphic=graphic;
        Label("Label",graphic.transform,name,Vector2.zero,size,30,Color.white);
        return button;
    }
}
