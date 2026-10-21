using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>같은 종류를 행 또는 카드 하나로 합산하여 표시하는 재사용 뷰.</summary>
public sealed class DamageContributionRowView : IDisposable
{
    private readonly DamageChartSample _sample;
    private readonly Image _background, _portrait, _track, _portraitFrame;
    private readonly DamageChartSkinView _skin, _portraitSkin;
    private readonly RectTransform _bar;
    private readonly TMP_Text _rank, _name, _count, _damage, _share, _dps;
    private Vector2 _positionTarget = new Vector2(float.NaN, float.NaN);
    private Tween _positionTween, _flashTween;
    private DamageChartStyle _style;
    private double _targetDamage, _targetDps, _shownDamage, _shownDps;
    private float _targetShare, _shownShare, _barTarget, _shownBar, _textWait;

    /// <summary>행의 위치와 크기.</summary>
    public RectTransform Rect { get; }
    /// <summary>이 종류의 정확한 합산 데이터.</summary>
    public DamageContributionEntry Entry { get; }
    /// <summary>보간 중인 피해 비중.</summary>
    public float DisplayedShare => _shownShare;
    /// <summary>애니메이션 중인 표시 피해량.</summary>
    public double DisplayedDamage => _shownDamage;

    /// <summary>표시 요소를 한 번 만들고 이후 갱신에서 재사용한다.</summary>
    public DamageContributionRowView(Transform parent, DamageChartSample sample, DamageContributionEntry entry, DamageChartUiFactory ui)
    {
        _sample=sample; Entry=entry;
        Rect=ui.Rect("DamageRow_"+entry.Source.name,parent,Vector2.zero,Vector2.one);
        _background=Rect.gameObject.AddComponent<Image>(); _background.raycastTarget=false;
        _skin=new DamageChartSkinView(_background,ui);
        _portraitFrame=ui.Image("PortraitFrame",Rect,Vector2.zero,Vector2.one,Color.white);
        _portraitSkin=new DamageChartSkinView(_portraitFrame,ui);
        _portrait=ui.Image("Portrait",Rect,Vector2.zero,Vector2.one,Color.white);
        _portrait.sprite=sample.Portrait!=null?sample.Portrait:entry.Portrait; _portrait.preserveAspect=true;
        _portrait.enabled=_portrait.sprite!=null;
        _rank=ui.Text("Rank",Rect,"1",Vector2.zero,Vector2.one,17f,Color.white);
        _name=ui.Text("UnitName",Rect,string.IsNullOrEmpty(sample.DisplayNameOverride)?entry.DisplayName:sample.DisplayNameOverride,Vector2.zero,Vector2.one,20f,Color.white);
        _count=ui.Text("Count",Rect,"",Vector2.zero,Vector2.one,14f,Color.white);
        _damage=ui.Text("Damage",Rect,"0",Vector2.zero,Vector2.one,22f,Color.white,TextAlignmentOptions.Right);
        _share=ui.Text("Share",Rect,"0%",Vector2.zero,Vector2.one,15f,Color.white,TextAlignmentOptions.Right);
        _dps=ui.Text("Dps",Rect,"",Vector2.zero,Vector2.one,14f,Color.white);
        _track=ui.Image("BarTrack",Rect,Vector2.zero,Vector2.one,Color.gray);
        var fill=ui.Image("BarFill",_track.transform,Vector2.zero,Vector2.one,sample.Color); _bar=fill.rectTransform;
        _bar.anchorMax=new Vector2(0f,1f);
    }

    /// <summary>같은 행을 선택한 레이아웃에 맞춰 배치한다.</summary>
    public void ApplyStyle(DamageChartStyle style)
    {
        _flashTween?.Kill(); _style=style; _skin.Apply(style.RowSprite,style.FrameShapeSprite,style.RowColor);
        _portraitFrame.gameObject.SetActive(style.PortraitFrameSprite!=null);
        _portraitSkin.Apply(style.PortraitFrameSprite,style.FrameShapeSprite,Color.white);
        _count.gameObject.SetActive(!style.HideCountCaption);
        _name.color=_damage.color=style.TextColor;
        _rank.color=_share.color=style.AccentColor;
        _count.color=_dps.color=style.MutedColor; _track.color=style.MutedColor*new Color(1f,1f,1f,.18f);
        bool card=style.Layout==DamageChartLayoutKind.ContributionCards;
        bool ring=style.Layout==DamageChartLayoutKind.ShareRing;
        bool wide=style.Layout==DamageChartLayoutKind.WideBars;
        Font(_name,ring?15f:20f);Font(_damage,ring?17f:22f);
        Font(_rank,ring?14f:17f);Font(_count,ring?11f:wide?12f:14f);
        Font(_share,ring||wide?12f:15f);Font(_dps,wide?12f:14f);
        Place(_portrait.rectTransform,card?new Vector2(.06f,.56f):new Vector2(.065f,.22f),
            card?new Vector2(.35f,.94f):new Vector2(.185f,.90f));
        Place(_portraitFrame.rectTransform,_portrait.rectTransform.anchorMin,_portrait.rectTransform.anchorMax);
        if(style.PortraitFrameSprite!=null)
        {
            _portrait.rectTransform.offsetMin=new Vector2(6f,6f);
            _portrait.rectTransform.offsetMax=new Vector2(-6f,-6f);
        }
        Place(_rank.rectTransform,new Vector2(.012f,.43f),new Vector2(.065f,.88f));
        if(card)
        {
            Place(_name.rectTransform,new Vector2(.39f,.67f),new Vector2(.95f,.94f));
            Place(_count.rectTransform,new Vector2(.39f,.53f),new Vector2(.95f,.68f));
            Place(_damage.rectTransform,new Vector2(.06f,.27f),new Vector2(.94f,.53f));
            Place(_share.rectTransform,new Vector2(.55f,.08f),new Vector2(.94f,.27f));
            Place(_dps.rectTransform,new Vector2(.06f,.07f),new Vector2(.60f,.25f));
            Place(_track.rectTransform,new Vector2(.06f,.015f),new Vector2(.94f,.045f));
        }
        else
        {
            Place(_name.rectTransform,new Vector2(.20f,.54f),new Vector2(.57f,.96f));
            Place(_count.rectTransform,new Vector2(.20f,.20f),new Vector2(.48f,.55f));
            Place(_damage.rectTransform,new Vector2(.57f,.54f),new Vector2(.96f,.96f));
            Place(_share.rectTransform,new Vector2(.77f,.20f),new Vector2(.96f,.55f));
            Place(_dps.rectTransform,new Vector2(.48f,.20f),new Vector2(.77f,.55f));
            Place(_track.rectTransform,wide?new Vector2(.20f,.29f):new Vector2(.20f,.08f),
                wide?new Vector2(.96f,.52f):new Vector2(.96f,.13f));
            if(wide)
            {
                Place(_count.rectTransform,new Vector2(.20f,.00f),new Vector2(.44f,.27f));
                Place(_dps.rectTransform,new Vector2(.44f,.00f),new Vector2(.77f,.27f));
                Place(_share.rectTransform,new Vector2(.77f,.00f),new Vector2(.96f,.27f));
                _share.color=style.TextColor;
                _track.rectTransform.SetAsFirstSibling();
            }
        }
        if(ring)
        {
            Place(_name.rectTransform,new Vector2(.20f,.40f),new Vector2(.60f,.95f));
            Place(_count.rectTransform,new Vector2(.20f,.03f),new Vector2(.60f,.42f));
            Place(_damage.rectTransform,new Vector2(.61f,.40f),new Vector2(.96f,.95f));
            Place(_share.rectTransform,new Vector2(.65f,.03f),new Vector2(.96f,.42f));
        }
        if(style.RefinedWideLayout)
        {
            Font(_name,20f);Font(_damage,23f);Font(_rank,15f);Font(_share,15f);Font(_dps,12f);
            _rank.color=style.MutedColor;
            Place(_rank.rectTransform,new Vector2(.018f,.20f),new Vector2(.059f,.84f));
            Place(_portrait.rectTransform,new Vector2(.072f,.12f),new Vector2(.198f,.89f));
            Place(_name.rectTransform,new Vector2(.225f,.59f),new Vector2(.70f,1f));
            Place(_damage.rectTransform,new Vector2(.71f,.59f),new Vector2(.96f,1f));
            Place(_track.rectTransform,new Vector2(.225f,.37f),new Vector2(.96f,.54f));
            Place(_dps.rectTransform,new Vector2(.225f,.00f),new Vector2(.65f,.34f));
            Place(_share.rectTransform,new Vector2(.70f,.00f),new Vector2(.96f,.34f));
        }
        _dps.gameObject.SetActive(!ring);
        _track.rectTransform.SetAsFirstSibling();
        _track.gameObject.SetActive(!ring);
        _skin.SendToBack();
        _positionTarget=new Vector2(float.NaN,float.NaN);
    }

    /// <summary>순위 변화만 부드럽게 이동하며 행을 다시 생성하지 않는다.</summary>
    public void MoveTo(int rank, RectTransform host, int visibleSlots = 6)
    {
        bool card=_style.Layout==DamageChartLayoutKind.ContributionCards;
        float height=host.rect.height;
        float step=card?height/3f:height/Mathf.Max(1,visibleSlots);
        Rect.anchorMin=Rect.anchorMax=new Vector2(0f,1f); Rect.pivot=new Vector2(0f,1f);
        Rect.sizeDelta=new Vector2(card?host.rect.width*.487f:host.rect.width,step-(_style.RefinedWideLayout?9f:6f));
        Vector2 target=new Vector2(card?(rank%2)*host.rect.width*.513f:0f,-(card?rank/2:rank)*step);
        _rank.text=(rank+1).ToString();
        if(!float.IsNaN(_positionTarget.x) && (target-_positionTarget).sqrMagnitude<.01f) return;
        _positionTarget=target; _positionTween?.Kill();
        _positionTween=Rect.DOAnchorPos(target,_style.RankSeconds).SetEase(Ease.OutCubic).SetUpdate(true);
    }

    /// <summary>시안 대비 글자 크기를 조정한다.</summary>
    public void ScaleText(float scale)
    {
        foreach (var text in Rect.GetComponentsInChildren<TMP_Text>(true)) Font(text, text.fontSize * scale);
    }

    /// <summary>하단 HUD에서는 이름·순위를 빼고 큰 초상과 피해 막대를 우선한다.</summary>
    public void SetPortraitOnly(bool compact)
    {
        ApplyStyle(_style);
        _name.gameObject.SetActive(!compact); _rank.gameObject.SetActive(!compact);
        _damage.alignment = TextAlignmentOptions.Right;
        if (!compact) return;
        _name.gameObject.SetActive(false); _rank.gameObject.SetActive(false); _count.gameObject.SetActive(false);
        Place(_portrait.rectTransform, new Vector2(.012f, .06f), new Vector2(.125f, .94f));
        Place(_portraitFrame.rectTransform, _portrait.rectTransform.anchorMin, _portrait.rectTransform.anchorMax);
        Place(_damage.rectTransform, new Vector2(.14f, .40f), new Vector2(.82f, .96f));
        _damage.alignment = TextAlignmentOptions.Left;
        Place(_share.rectTransform, new Vector2(.83f, .40f), new Vector2(.99f, .96f));
        Place(_track.rectTransform, new Vector2(.14f, .08f), new Vector2(.99f, .34f));
        _dps.gameObject.SetActive(false);
    }

    /// <summary>정확한 집계값을 표시 보간의 목표로 전달한다.</summary>
    public void SetTargets(decimal damage,decimal dps,float share,float barRatio)
    {
        _targetDamage=(double)damage; _targetDps=(double)dps; _targetShare=share; _barTarget=barRatio;
        _count.text=Entry.UnitCount+"개체 합산";
    }

    /// <summary>종료 딜표의 숫자·비중·막대를 보간 중간값 없이 최종 목표로 표시한다.</summary>
    public void Settle()
    {
        _shownDamage=_targetDamage; _shownDps=_targetDps;
        _shownShare=_targetShare; _shownBar=_barTarget;
        _bar.anchorMax=new Vector2(_shownBar,1f);
        _damage.text=Format(_shownDamage);
        _share.text=(_shownShare*100f).ToString("0.0")+"%";
        _dps.text="DPS "+Format(_shownDps);
        _textWait=0f;
        _positionTween?.Complete();
    }

    /// <summary>숫자, 비중과 막대를 비스케일 시간으로 부드럽게 갱신한다.</summary>
    public void Tick(float delta)
    {
        if(_style==null) return;
        float alpha=1f-Mathf.Exp(-delta/_style.ValueSeconds);
        _shownDamage=Smooth(_shownDamage,_targetDamage,alpha);
        _shownDps=Smooth(_shownDps,_targetDps,alpha);
        _shownShare=Mathf.Lerp(_shownShare,_targetShare,alpha);
        _shownBar=Mathf.Lerp(_shownBar,_barTarget,alpha);
        _bar.anchorMax=new Vector2(_shownBar,1f);
        _textWait+=delta;
        if(_textWait<.04f) return;
        _textWait=0f; _damage.text=Format(_shownDamage);
        _share.text=(_shownShare*100f).ToString("0.0")+"%";
        _dps.text="DPS "+Format(_shownDps);
    }

    /// <summary>큰 피해에 작은 색 강조를 더한다.</summary>
    public void Flash()
    {
        _flashTween?.Kill(); _skin.TintTarget.color=Color.Lerp(_style.RowColor,_sample.Color,.25f);
        _flashTween=_skin.TintTarget.DOColor(_style.RowColor,_style.RankSeconds).SetUpdate(true);
    }

    /// <summary>큰 피해값을 짧게 표시한다.</summary>
    public static string Format(double value)
    {
        if(value>=100000000d) return (value/100000000d).ToString("0.00")+"억";
        if(value>=10000d) return (value/10000d).ToString("0.00")+"만";
        return value.ToString("N0");
    }
    private static void Font(TMP_Text text,float size)
    { text.fontSize=size;text.fontSizeMax=size;text.fontSizeMin=size*.72f; }
    private static double Smooth(double value,double target,float alpha) => Math.Abs(target-value)<.05d?target:value+(target-value)*alpha;
    private static void Place(RectTransform rect,Vector2 min,Vector2 max)
    { rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero; }

    /// <summary>소유한 위치 및 색 애니메이션을 종료한다.</summary>
    public void Dispose() { _positionTween?.Kill();_flashTween?.Kill(); }
}
