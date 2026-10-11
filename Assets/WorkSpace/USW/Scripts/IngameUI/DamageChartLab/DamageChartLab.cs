using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;

/// <summary>실제 보드를 배경으로 실시간 종류별 딜 기여도 UI를 비교하는 독립 씬.</summary>
public sealed class DamageChartLab : MonoBehaviour
{
    private const float MaxDemoStep = .10f;
    private static readonly Color UiText = new Color(.94f,.97f,.94f,1f);
    private static readonly Color SelectedColor = new Color(.23f,.47f,.46f,1f);
    [SerializeField] private DamageChartLabSettings _settings;
    [SerializeField] private DamageChartStyle[] _styles;
    [SerializeField] private TMP_FontAsset _font;
    [SerializeField] private int _initialStyle = 2;
    [Inject] private DamageContributionLedger _ledger;
    [Inject] private GameInitializer _initializer;
    [Inject] private UIManager _gameUi;
    [Inject] private TimeScaleService _timeScale;
    private readonly List<DamageContributionRowView> _rows = new List<DamageContributionRowView>();
    private readonly List<DamageContributionRowView> _sorted = new List<DamageContributionRowView>();
    private readonly System.Random _random = new System.Random(1012);
    private DamageChartUiFactory _ui;
    private RectTransform _chart, _body, _ringRoot;
    private Image _panel, _summaryDivider;
    private DamageChartSkinView _panelSkin;
    private DamageChartSkinView[] _buttonSkins;
    private Button[] _allButtons;
    private CanvasGroup _chartGroup;
    private DamageContributionRingGraphic _ring;
    private TMP_Text _total, _dps, _header, _subheader, _clock, _status, _ringTotal, _footer;
    private Button[] _styleButtons;
    private Button _pauseButton;
    private float[] _ringShares;
    private float _hitClock, _refreshClock, _textClock;
    private double _displayTotal, _displayDps;
    private int _styleIndex;
    private bool _paused;
    private Tween _reveal;

    /// <summary>실험실의 SO 설정.</summary>
    public DamageChartLabSettings Settings => _settings;
    /// <summary>정확한 종류별 집계 데이터.</summary>
    public DamageContributionLedger Ledger => _ledger;
    /// <summary>화면 준비 완료 여부.</summary>
    public bool IsReady { get; private set; }
    /// <summary>현재 선택한 시안.</summary>
    public int SelectedStyle => _styleIndex;
    /// <summary>시안 수.</summary>
    public int StyleCount => _styles?.Length ?? 0;
    /// <summary>예시 전투의 일시정지 여부.</summary>
    public bool IsDemoPaused => _paused;
    /// <summary>종류별 재사용 행.</summary>
    public IReadOnlyList<DamageContributionRowView> Rows => _rows;
    /// <summary>현재 레이아웃.</summary>
    public DamageChartLayoutKind CurrentLayout => _styles[_styleIndex].Layout;

    /// <summary>실험실이 있는 씬에서만 데이터와 집계 서비스를 DI 등록한다.</summary>
    public static void RegisterServices(IContainerBuilder builder, Scene scene)
    {
        foreach(var root in scene.GetRootGameObjects())
        {
            var labs=root.GetComponentsInChildren<DamageChartLab>(true);
            if(labs.Length==0) continue;
            if(labs.Length!=1 || labs[0]._settings==null) throw new InvalidOperationException("Damage chart lab requires one authored settings asset.");
            builder.RegisterInstance(labs[0]._settings);
            builder.Register<DamageContributionLedger>(Lifetime.Scoped);
            SceneComponentRegistration.RegisterOptional<DamageChartLab>(builder,scene);
            return;
        }
    }

    private void Start() => InitializeAsync().Forget();
    private async UniTaskVoid InitializeAsync()
    {
        var token=this.GetCancellationTokenOnDestroy();
        try
        {
            if(_settings==null || _settings.Samples==null || _styles==null || _styles.Length==0 || _ledger==null)
                throw new InvalidOperationException("DamageChartLab: data/DI references are missing.");
            _ui=new DamageChartUiFactory(_font);
            foreach(var sample in _settings.Samples) _ledger.SetUnitCount(sample.Unit,sample.Count);
            BuildUi();
            foreach(var sample in _settings.Samples)
            {
                DamageContributionEntry entry=null;
                foreach(var candidate in _ledger.Entries) if(candidate.Unit==sample.Unit) { entry=candidate;break; }
                if(entry==null) throw new InvalidOperationException("Sample kinds must be unique.");
                var row=new DamageContributionRowView(_body,sample,entry,_ui); _rows.Add(row);_sorted.Add(row);
            }
            _ringShares=new float[_rows.Count];
            var colors=new Color[_rows.Count];
            for(int i=0;i<colors.Length;i++) colors[i]=_settings.Samples[i].Color;
            _ring.Configure(colors);
            for(float t=0;t<_settings.WarmupSeconds;t+=_settings.HitInterval) AdvanceDemo(_settings.HitInterval);
            IsReady=true;SelectStyle(Mathf.Clamp(_initialStyle,0,_styles.Length-1));
            if(_initializer!=null) await UniTask.WaitUntil(()=>_initializer.IsReady,cancellationToken:token);
            _gameUi?.HideStartButton();_timeScale?.Pause(this);
        }
        catch(OperationCanceledException) when(token.IsCancellationRequested) {}
        catch(Exception error) { Debug.LogException(error,this); }
    }

    private void Update()
    {
        if(!IsReady) return;
        float delta=Mathf.Min(Time.unscaledDeltaTime,MaxDemoStep);
        if(!_paused) AdvanceDemo(delta);
        _refreshClock+=delta;
        if(_refreshClock>=_settings.RefreshInterval) { _refreshClock=0f;RefreshTargets(); }
        float alpha=1f-Mathf.Exp(-delta/_styles[_styleIndex].ValueSeconds);
        _displayTotal=Smooth(_displayTotal,(double)_ledger.TotalDamage,alpha);
        _displayDps=Smooth(_displayDps,(double)_ledger.RecentDps,alpha);
        for(int i=0;i<_rows.Count;i++) { _rows[i].Tick(delta);_ringShares[i]=_rows[i].DisplayedShare; }
        if(_ringRoot.gameObject.activeSelf) _ring.SetShares(_ringShares);
        _textClock+=delta;
        if(_textClock>=.05f)
        {
            _textClock=0f;_total.text=DamageContributionRowView.Format(_displayTotal);
            _dps.text="최근 "+_settings.RecentDpsWindow.ToString("0.#")+"초 DPS\n"+DamageContributionRowView.Format(_displayDps);
            _ringTotal.text=DamageContributionRowView.Format(_displayTotal);
            _clock.text=TimeSpan.FromSeconds(_ledger.CombatSeconds).ToString(@"mm\:ss");
        }
    }

    private void AdvanceDemo(float delta)
    {
        _ledger.Advance(delta); _hitClock+=delta;
        while(_hitClock>=_settings.HitInterval)
        {
            _hitClock-=_settings.HitInterval;
            for(int i=0;i<_settings.Samples.Length;i++)
            {
                var sample=_settings.Samples[i];int count=_rows.Count>i?_rows[i].Entry.UnitCount:sample.Count;
                for(int j=0;j<count;j++)
                {
                    double variation=.65+_random.NextDouble()*.7;
                    decimal damage=(decimal)(sample.DemoDamagePerSecond*_settings.HitInterval*variation);
                    _ledger.RecordDamage(sample.Unit,damage);
                }
            }
        }
    }

    /// <summary>전투 집계를 유지하면서 화면 시안만 전환한다.</summary>
    public void SelectStyle(int index)
    {
        if(!IsReady || index<0 || index>=_styles.Length || _styles[index]==null) return;
        _styleIndex=index;var style=_styles[index];
        _panelSkin.Apply(style.PanelSprite,style.FrameShapeSprite,style.PanelColor,style.RefinedWideLayout?3f:5f);_total.color=_header.color=_ringTotal.color=style.TextColor;
        _dps.color=_subheader.color=_footer.color=style.MutedColor;
        _subheader.text=style.HideGuideText?string.Empty:style.Description;
        _subheader.gameObject.SetActive(!style.HideGuideText);
        _footer.gameObject.SetActive(!style.HideGuideText);
        bool ring=style.Layout==DamageChartLayoutKind.ShareRing;
        _ringRoot.gameObject.SetActive(ring);
        ApplyPanelLayout(style.RefinedWideLayout,ring);
        _summaryDivider.gameObject.SetActive(style.RefinedWideLayout);
        _summaryDivider.color=new Color(style.MutedColor.r,style.MutedColor.g,style.MutedColor.b,.20f);
        Canvas.ForceUpdateCanvases();
        foreach(var row in _rows) row.ApplyStyle(style);
        RefreshTargets();
        for(int i=0;i<_allButtons.Length;i++)
        {
            bool selected=i<_styleButtons.Length && i==index;
            Color tint=style.ButtonSprite!=null
                ?(i>=_styleButtons.Length || selected?Color.white:new Color(.62f,.62f,.75f,1f))
                :(selected?SelectedColor:new Color(.12f,.21f,.26f,1f));
            bool primary=selected || _allButtons[i]==_pauseButton;
            Sprite buttonArt=style.RefinedWideLayout && !primary?null:style.ButtonSprite;
            if(style.RefinedWideLayout && !primary) tint=new Color(.12f,.14f,.22f,1f);
            _buttonSkins[i].Apply(buttonArt,style.FrameShapeSprite,tint,style.RefinedWideLayout?3f:5f);
        }
        _reveal?.Kill();_chartGroup.alpha=.25f;
        _reveal=_chartGroup.DOFade(1f,style.RevealSeconds).SetEase(Ease.OutCubic).SetUpdate(true);
    }

    private void ApplyPanelLayout(bool refined,bool ring)
    {
        Place(_chart,new Vector2(.055f,refined?.196f:.181f),new Vector2(.945f,refined?.74f:.728f));
        Place(_body,new Vector2(refined?.045f:.035f,refined?.03f:ring?.045f:.055f),
            new Vector2(refined?.955f:.965f,refined?.79f:ring?.45f:.73f));
        Place(_header.rectTransform,new Vector2(.045f,refined?.929f:.92f),new Vector2(.65f,.98f));
        Place(_total.rectTransform,new Vector2(.045f,refined?.82f:.811f),new Vector2(.62f,refined?.932f:.925f));
        Place(_dps.rectTransform,new Vector2(.64f,refined?.835f:.82f),new Vector2(.955f,refined?.95f:.967f));
    }

    private static void Place(RectTransform rect,Vector2 min,Vector2 max)
    {
        rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;
    }

    private void RefreshTargets()
    {
        _sorted.Sort((a,b)=>
        {
            int order=b.Entry.TotalDamage.CompareTo(a.Entry.TotalDamage);
            return order!=0?order:a.Entry.Unit.unitName.CompareTo(b.Entry.Unit.unitName);
        });
        decimal max=0m;foreach(var entry in _ledger.Entries) if(entry.TotalDamage>max) max=entry.TotalDamage;
        for(int rank=0;rank<_sorted.Count;rank++)
        {
            var row=_sorted[rank];row.MoveTo(rank,_body);
            float share=(float)_ledger.Share(row.Entry);
            float bar=CurrentLayout==DamageChartLayoutKind.WideBars && max>0m?(float)(row.Entry.TotalDamage/max):share;
            row.SetTargets(row.Entry.TotalDamage,_ledger.RecentDpsFor(row.Entry),share,bar);
        }
        _footer.text=_ledger.TotalDamage==0m?"아직 피해 기록이 없어요 · 전투 재생 시 집계":"같은 종류 · 전 등급 합산 / 배치 개체별 분리 없음";
    }

    /// <summary>예시 전투를 멈춰도 표시 애니메이션은 마무리한다.</summary>
    public void TogglePause()
    {
        _paused=!_paused;_pauseButton.GetComponentInChildren<TMP_Text>().text=_paused?"전투 재생":"일시정지";
        _status.text=_paused?"PAUSED · 예시 전투":"LIVE · 예시 전투";
    }

    /// <summary>피해가 적은 종류에 예시 피해를 더하여 순위 역전을 확인한다.</summary>
    public void TriggerBurst()
    {
        if(!IsReady) return;
        DamageContributionRowView target=null;
        foreach(var row in _rows)
            if(_settings.Samples[_rows.IndexOf(row)].DemoDamagePerSecond>0f &&
                (target==null || row.Entry.TotalDamage<target.Entry.TotalDamage)) target=row;
        if(target==null) return;
        decimal max=0m;foreach(var entry in _ledger.Entries) max=Math.Max(max,entry.TotalDamage);
        _ledger.RecordDamage(target.Entry.Unit,max-target.Entry.TotalDamage+(decimal)_settings.BurstDamage);
        RefreshTargets();target.Flash();
    }

    /// <summary>같은 종류가 한 개체 늘어도 행을 늘리지 않고 합산한다.</summary>
    public void AddUnit()
    {
        if(!IsReady || _rows.Count==0) return;
        var first=_rows[0].Entry;_ledger.SetUnitCount(first.Unit,first.UnitCount+1);RefreshTargets();_rows[0].Flash();
    }

    /// <summary>피해 0 상태를 유지하여 빈 집계 표시를 비교한다.</summary>
    public void ShowZeroDamage()
    {
        if(!_paused) TogglePause();_ledger.Reset();_hitClock=0f;RefreshTargets();
    }

    /// <summary>예시 전투의 집계와 배치 수를 초기화하고 재생한다.</summary>
    public void ResetDemo()
    {
        _ledger.Reset();_hitClock=0f;
        foreach(var sample in _settings.Samples) _ledger.SetUnitCount(sample.Unit,sample.Count);
        if(_paused) TogglePause();RefreshTargets();
    }

    private void BuildUi()
    {
        var canvasObject=new GameObject("DamageChartLabCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform,false);var canvas=canvasObject.GetComponent<Canvas>();
        canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=310;
        var scaler=canvasObject.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=new Vector2(540f,1168f);scaler.matchWidthOrHeight=.5f;
        _ui.Image("DamageBackdrop",canvasObject.transform,Vector2.zero,Vector2.one,new Color(.025f,.05f,.075f,.73f));
        _ui.Image("LabHeaderBackdrop",canvasObject.transform,new Vector2(0f,.862f),Vector2.one,new Color(.05f,.10f,.13f,.99f));
        _ui.Text("LabTitle",canvasObject.transform,"유닛 딜 기여도",new Vector2(.055f,.91f),new Vector2(.74f,.957f),28f,UiText);
        _status=_ui.Text("LiveStatus",canvasObject.transform,"LIVE · 예시 전투",new Vector2(.055f,.873f),new Vector2(.77f,.91f),15f,new Color(.48f,.85f,.72f));
        _clock=_ui.Text("CombatClock",canvasObject.transform,"00:00",new Vector2(.78f,.884f),new Vector2(.94f,.947f),22f,UiText,TextAlignmentOptions.Right);
        _styleButtons=new Button[_styles.Length];
        for(int i=0;i<_styles.Length;i++)
        {
            int choice=i;float x=.055f+(i%3)*.305f;float y=.809f-(i/3)*.058f;
            _styleButtons[i]=_ui.Button("DamageStyle_"+i,canvasObject.transform,_styles[i].DisplayName,new Vector2(x,y),new Vector2(x+.28f,y+.047f),()=>SelectStyle(choice));
        }
        _chart=_ui.Rect("DamageChartPanel",canvasObject.transform,new Vector2(.055f,.181f),new Vector2(.945f,.728f));
        _panel=_chart.gameObject.AddComponent<Image>();_panel.raycastTarget=false;
        _panelSkin=new DamageChartSkinView(_panel,_ui);
        _chartGroup=_chart.gameObject.AddComponent<CanvasGroup>();
        _summaryDivider=_ui.Image("SummaryDivider",_chart,new Vector2(.045f,.805f),new Vector2(.955f,.807f),Color.clear);
        _summaryDivider.gameObject.SetActive(false);
        _header=_ui.Text("ChartHeader",_chart,"전체 누적 피해",new Vector2(.045f,.92f),new Vector2(.65f,.98f),19f,Color.white);
        _total=_ui.Text("TotalDamage",_chart,"0",new Vector2(.045f,.811f),new Vector2(.62f,.925f),34f,Color.white);
        _dps=_ui.Text("RecentDps",_chart,"DPS 0",new Vector2(.64f,.82f),new Vector2(.955f,.967f),17f,Color.white,TextAlignmentOptions.Right);
        _subheader=_ui.Text("StyleDescription",_chart,"",new Vector2(.045f,.747f),new Vector2(.955f,.802f),14f,Color.white);
        _body=_ui.Rect("DamageRows",_chart,new Vector2(.035f,.055f),new Vector2(.965f,.73f));
        _body.gameObject.AddComponent<Canvas>();
        _ringRoot=_ui.Rect("DamageRing",_chart,new Vector2(.15f,.474f),new Vector2(.85f,.744f));
        _ring=_ringRoot.gameObject.AddComponent<DamageContributionRingGraphic>();
        _ringTotal=_ui.Text("RingTotal",_ringRoot,"0",new Vector2(.10f,.40f),new Vector2(.90f,.63f),26f,UiText,TextAlignmentOptions.Center);
        _ui.Text("RingCaption",_ringRoot,"전체 피해",new Vector2(.15f,.25f),new Vector2(.85f,.42f),13f,UiText,TextAlignmentOptions.Center);
        _footer=_ui.Text("DamageFooter",_chart,"",new Vector2(.03f,.005f),new Vector2(.97f,.042f),12f,Color.white,TextAlignmentOptions.Center);
        _pauseButton=_ui.Button("DamagePause",canvasObject.transform,"일시정지",new Vector2(.055f,.115f),new Vector2(.34f,.160f),TogglePause);
        _ui.Button("DamageBurst",canvasObject.transform,"순위 역전",new Vector2(.357f,.115f),new Vector2(.642f,.160f),TriggerBurst);
        _ui.Button("DamageAddUnit",canvasObject.transform,"같은 종류 +1",new Vector2(.66f,.115f),new Vector2(.945f,.160f),AddUnit);
        _ui.Button("DamageZero",canvasObject.transform,"0딜 보기",new Vector2(.055f,.052f),new Vector2(.485f,.097f),ShowZeroDamage);
        _ui.Button("DamageReset",canvasObject.transform,"초기화 · 재생",new Vector2(.515f,.052f),new Vector2(.945f,.097f),ResetDemo);
        _allButtons=canvasObject.GetComponentsInChildren<Button>(true);
        _buttonSkins=new DamageChartSkinView[_allButtons.Length];
        for(int i=0;i<_allButtons.Length;i++)
        {
            _buttonSkins[i]=new DamageChartSkinView((Image)_allButtons[i].targetGraphic,_ui);
            _buttonSkins[i].SendToBack();
        }
        Canvas.ForceUpdateCanvases();
    }

    private static double Smooth(double value,double target,float alpha) => Math.Abs(target-value)<.05d?target:value+(target-value)*alpha;
    private void OnDestroy()
    {
        IsReady=false;_reveal?.Kill();foreach(var row in _rows) row.Dispose();_timeScale?.Release(this);
    }
}
