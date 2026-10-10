using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>기존 승천 결과 연출 전에 두 후보 중 하나를 터치로 선택한다.</summary>
public sealed class PenaltyChoiceUI : MonoBehaviour
{
    private GameObject _root;
    /// <summary>취소 시 UI를 정리하며 자동 선택 없이 선택 결과를 반환한다.</summary>
    public async UniTask<RunPenaltyData> ChooseAsync(IReadOnlyList<RunPenaltyData> choices, CancellationToken token)
    {
        if (choices == null || choices.Count != 2) throw new System.ArgumentException("Two penalty choices required.");
        var completion=new UniTaskCompletionSource<RunPenaltyData>();
        _root=new GameObject("AscensionChoice",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        _root.transform.SetParent(transform,false);
        var canvas=_root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=30000;
        var scaler=_root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1080,1920);scaler.matchWidthOrHeight=.5f;
        var shade=Panel("Shade",_root.transform,new Color(.015f,.02f,.04f,.94f));
        shade.rectTransform.anchorMin=Vector2.zero;shade.rectTransform.anchorMax=Vector2.one;shade.rectTransform.offsetMin=shade.rectTransform.offsetMax=Vector2.zero;
        var content = new GameObject("SafeContent", typeof(RectTransform));
        content.transform.SetParent(_root.transform, false);
        var frame = content.AddComponent<SafeAreaContentFrame>();
        Canvas.ForceUpdateCanvases();
        frame.Refresh();
        Label("다음 승천 패널티를 선택하세요",content.transform,new Vector2(0,400),new Vector2(940,130),44);
        for(int i=0;i<choices.Count;i++)
        {
            var data=choices[i];
            var panel=Panel("Choice"+i,content.transform,new Color(.16f,.14f,.08f,1));
            panel.rectTransform.sizeDelta=new Vector2(900,250);panel.rectTransform.anchoredPosition=new Vector2(0,180-i*310);
            var button=panel.gameObject.AddComponent<Button>();button.targetGraphic=panel;
            Label(data.DisplayName+"\n<size=75%>"+PenaltyRevealFx.Describe(data)+"</size>",panel.transform,Vector2.zero,new Vector2(830,210),40);
            button.onClick.AddListener(()=>completion.TrySetResult(data));
        }
        Label("선택한 패널티는 다음 라운드부터 적용됩니다",content.transform,new Vector2(0,-390),new Vector2(940,100),28);
        try { return await completion.Task.AttachExternalCancellation(token); }
        finally { if(_root!=null)Destroy(_root);_root=null; }
    }
    private static Image Panel(string name,Transform parent,Color color)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);
        var image=go.GetComponent<Image>();image.color=color;return image;
    }
    private static void Label(string text,Transform parent,Vector2 position,Vector2 size,float fontSize)
    {
        var go=new GameObject("Label",typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);
        var label=go.GetComponent<TextMeshProUGUI>();label.font=TMP_Settings.defaultFontAsset;label.text=text;label.fontSize=fontSize;label.alignment=TextAlignmentOptions.Center;label.raycastTarget=false;
        label.rectTransform.anchoredPosition=position;label.rectTransform.sizeDelta=size;
    }
}
