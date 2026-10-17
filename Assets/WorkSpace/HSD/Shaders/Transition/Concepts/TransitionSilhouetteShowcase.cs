using Spine.Unity;
using UnityEngine;

public sealed class TransitionSilhouetteShowcase : MonoBehaviour
{
    [SerializeField] private SkeletonDataAsset[] units;
    [SerializeField] private Material silhouetteTemplate;
    private readonly RectTransform[] hosts=new RectTransform[3];
    private Material silhouette;
    private Material props;
    private TransitionParadeScenery scenery;
    private float started;
    private float exitStarted=-1;
    private float exitDuration=1;
    public float LoadingProgress { get; private set; }
    public bool IsExiting => exitStarted>=0;

    private void Awake()
    {
        silhouette=Instantiate(silhouetteTemplate);
        props=Instantiate(silhouetteTemplate);
        props.SetColor("_Color",Color.white);
        var background=CreateRect("Parade mountains and loading bar");
        background.sizeDelta=new Vector2(2600,700);
        scenery=background.gameObject.AddComponent<TransitionParadeScenery>();
        scenery.material=props;
        scenery.maskable=false;
        scenery.raycastTarget=false;
        for(int i=0;i<hosts.Length;i++)
        {
            hosts[i]=CreateRect("Unit "+i);
            var components=SkeletonGraphic.NewSkeletonGraphicGameObject(units[i],hosts[i],silhouette);
            var graphic=components.skeletonRenderer;
            var animation=components.skeletonAnimation;
            graphic.allowMultipleCanvasRenderers=true;
            graphic.additiveMaterial=graphic.multiplyMaterial=graphic.screenMaterial=silhouette;
            graphic.raycastTarget=false;
            graphic.maskable=false;
            animation.unscaledTime=true;
            animation.AnimationState.SetAnimation(0,"idle",true).TrackTime=i*.27f;
            animation.UpdateComplete+=HideEffects;
            animation.Update(0);
            HideEffects(graphic);
            graphic.MatchRectTransformWithBounds();
            var bounds=graphic.rectTransform.rect.size;
            float scale=Mathf.Min(210/bounds.x,210/bounds.y);
            var rect=graphic.rectTransform;
            rect.anchorMin=rect.anchorMax=Vector2.one*.5f;
            rect.localScale=Vector3.one*scale;
            rect.anchoredPosition=Vector2.Scale(rect.pivot-Vector2.one*.5f,bounds)*scale+Vector2.up*105;
        }
        ResetPreview();
    }
    private RectTransform CreateRect(string label)
    {
        var rect=new GameObject(label,typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(transform,false);
        rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.one*.5f;
        rect.sizeDelta=new Vector2(210,210);
        return rect;
    }
    public void ResetPreview()
    {
        started=Time.unscaledTime;
        exitStarted=-1;
        SetLoading(0);
    }
    public void SetLoading(float value)
    {
        LoadingProgress=Mathf.Clamp01(value);
        if(scenery!=null) scenery.Fill=LoadingProgress;
    }
    public void BeginExit(float duration)
    {
        SetLoading(1);
        exitStarted=Time.unscaledTime;
        exitDuration=Mathf.Max(.01f,duration);
    }
    public void CancelExit() { exitStarted=-1; }
    public void SetProgress(float progress)
    {
        float aspect=(float)Screen.width/Mathf.Max(1,Screen.height);
        silhouette.SetFloat("_Progress",progress);
        props.SetFloat("_Progress",progress);
        silhouette.SetFloat("_Aspect",aspect);
        props.SetFloat("_Aspect",aspect);
    }
    private void Update()
    {
        float t=Time.unscaledTime-started;
        float exit=IsExiting?Mathf.Clamp01((Time.unscaledTime-exitStarted)/exitDuration):0;
        scenery.TravelTime=t+exit*2;
        float distance=((RectTransform)transform.parent).rect.width*.5f+650;
        for(int i=0;i<hosts.Length;i++)
        {
            float enter=Mathf.SmoothStep(0,1,Mathf.Clamp01((t-(2-i)*.14f)/.6f));
            float x=(i-1)*205+Mathf.Lerp(-350,0,enter)+distance*exit*exit;
            float stride=Mathf.Abs(Mathf.Sin(t*7-i*.9f));
            float y=stride*(IsExiting?22:14);
            hosts[i].anchoredPosition=new Vector2(x,y-80);
            // Original art faces left, so mirror each centered unit host.
            hosts[i].localScale=new Vector3(-.76f,.76f+stride*.025f,1);
            hosts[i].localEulerAngles=new Vector3(0,0,-7-(IsExiting?9:0)+Mathf.Sin(t*7-i*.9f)*2);
        }
    }
    private static void HideEffects(ISkeletonRenderer renderer)
    {
        foreach(var slot in renderer.Skeleton.Slots)
            if(slot.Data.Name=="hover_light" || slot.Data.Name=="shadow") slot.Pose.Attachment=null;
    }
    private void OnDestroy()
    {
        if(silhouette!=null) Destroy(silhouette);
        if(props!=null) Destroy(props);
    }
}
