using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>실제 레일건 연출의 충전·발사에 맞춰 소리 시안을 터치로 비교하는 독립 실험실.</summary>
public sealed class DisigmanRailgunSoundLab : MonoBehaviour
{
    [SerializeField] private DisigmanRailgunSoundSettings _settings;
    [SerializeField] private DisigmanRailgunSoundVariant[] _variants;
    [SerializeField] private int _initialVariant;
    [SerializeField] private TMP_FontAsset _font;
    [SerializeField] private SpriteRenderer _unit;
    [SerializeField] private Sprite _idleSprite, _fireSprite;
    [SerializeField] private Transform _target;
    [SerializeField] private RailgunBeamFx _basicFx, _skillFx;
    [SerializeField] private Vector2 _muzzleOffset = new Vector2(0f, .6f);
    private readonly float[] _samples = new float[256];
    private AudioSource _chargeSource, _shotSource;
    private Button[] _variantButtons;
    private Button _repeatButton, _visualButton;
    private TMP_Text _description, _status, _volumeText;
    private Image _progress, _meter;
    private RailgunBeamFx _liveFx;
    private Sprite _whiteSprite;
    private Vector3 _unitHome;
    private double _startDsp, _fireDsp, _endDsp, _repeatAt;
    private float _volume, _level;
    private bool _pending, _active, _skill, _repeat, _visual = true;
    private int _selected;
    private static readonly Color TextColor = new Color(.94f, .96f, 1f);
    private static readonly Color Accent = new Color(.33f, .70f, .84f);

    /// <summary>현재 시안 인덱스.</summary>
    public int SelectedVariant => _selected;
    /// <summary>현재 재생이 예약되었거나 진행 중인지.</summary>
    public bool IsAuditioning => _pending || _active;
    /// <summary>반복 청취 여부.</summary>
    public bool IsRepeating => _repeat;
    /// <summary>충전 다음 발사음을 예약한 DSP 간격.</summary>
    public double ScheduledChargeSeconds => _fireDsp - _startDsp;
    /// <summary>동일한 비교 음량.</summary>
    public float PreviewVolume => _volume;
    /// <summary>표시 준비 완료 여부.</summary>
    public bool IsReady { get; private set; }

    private void Start()
    {
        if (_settings == null || _variants == null || _variants.Length < 4 || _variants.Length > 5 || _unit == null || _target == null)
        { Debug.LogError("Disigman sound lab references missing."); enabled = false; return; }
        foreach (var variant in _variants)
            if (variant == null || variant.Charge == null || variant.Basic == null || variant.Skill == null)
            { Debug.LogError("Disigman sound lab clips missing."); enabled = false; return; }
        _unitHome = _unit.transform.position;
        _whiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0,0,1,1), new Vector2(.5f,.5f));
        _chargeSource = NewSource("ChargeAudio"); _shotSource = NewSource("ShotAudio");
        _volume = _settings.PreviewVolume;
        BuildUi(); SetVolume(_volume); SelectVariant(Mathf.Clamp(_initialVariant, 0, _variants.Length-1)); IsReady = true;
    }

    private AudioSource NewSource(string label)
    {
        var go = new GameObject(label); go.transform.SetParent(transform, false);
        var source = go.AddComponent<AudioSource>(); source.playOnAwake = false;
        source.spatialBlend = 0f; source.loop = false; source.priority = 32;
        return source;
    }

    /// <summary>시안 변경 시 이전 예약·잔향·연출을 취소하여 소리가 섞이지 않게 한다.</summary>
    public void SelectVariant(int index)
    {
        if (_variants == null || index < 0 || index >= _variants.Length || _variantButtons == null) return;
        StopAudition(); _selected = index;
        _description.text = _variants[index].Description;
        for (int i = 0; i < _variantButtons.Length; i++)
            _variantButtons[i].targetGraphic.color = i == index ? new Color(.22f,.43f,.52f) : new Color(.13f,.17f,.24f);
        _repeatAt = AudioSettings.dspTime + _settings.RepeatSeconds;
    }

    /// <summary>동일한 시각 연출에 일반/스킬 소리를 DSP 시계로 예약한다.</summary>
    public void Audition(bool skill)
    {
        if (!IsReady) return;
        StopAudition(); _skill = skill;
        var variant = _variants[_selected];
        _startDsp = AudioSettings.dspTime + _settings.ScheduleLeadSeconds;
        _fireDsp = _startDsp + _settings.ChargeSeconds;
        var shot = skill ? variant.Skill : variant.Basic;
        _endDsp = _fireDsp + shot.length;
        _chargeSource.clip = skill && variant.SkillCharge != null ? variant.SkillCharge : variant.Charge; _shotSource.clip = shot;
        _chargeSource.PlayScheduled(_startDsp); _chargeSource.SetScheduledEndTime(_fireDsp);
        _shotSource.PlayScheduled(_fireDsp);
        _pending = true; _repeatAt = _startDsp + _settings.RepeatSeconds;
    }

    /// <summary>음량을 모든 비교 시안에 공통으로 적용한다.</summary>
    public void SetVolume(float value)
    {
        _volume = Mathf.Clamp01(value);
        if (_chargeSource != null) _chargeSource.volume = _volume;
        if (_shotSource != null) _shotSource.volume = _volume;
        if (_volumeText != null) _volumeText.text = $"미리듣기 음량  {Mathf.RoundToInt(_volume * 100)}%";
    }

    /// <summary>현재 예약과 소리, 복제 연출을 함께 정리한다.</summary>
    public void StopAudition()
    {
        _chargeSource?.Stop(); _shotSource?.Stop(); _pending = _active = false;
        if (_liveFx != null) Destroy(_liveFx.gameObject);
        _liveFx = null;
        if (_unit != null && IsReady) { _unit.transform.position = _unitHome; _unit.sprite = _idleSprite; }
        if (_status != null) _status.text = "재생 대기";
        if (_progress != null) _progress.fillAmount = 0f;
    }

    private void Update()
    {
        if (!IsReady) return;
        double now = AudioSettings.dspTime;
        if (_pending && now >= _startDsp)
        {
            _pending = false; _active = true;
            if (_visual)
            {
                _liveFx = Instantiate(_skill ? _skillFx : _basicFx);
                _liveFx.gameObject.SetActive(true);
                // Absorb the first-frame scheduling delay; fire matches DSP to within one rendered frame.
                _liveFx.Play(transform, _unitHome + (Vector3)_muzzleOffset, _target.position,
                    Mathf.Max(0f, (float)(_fireDsp - now)), _skill ? _settings.SkillVisualScale : 1f);
            }
        }
        if (_active)
        {
            float since = (float)(now - _fireDsp);
            _progress.fillAmount = Mathf.Clamp01((float)(now - _startDsp) / _settings.ChargeSeconds);
            _status.text = since < 0 ? "충전 중" : now < _endDsp ? (_skill ? "스킬 발사 · 잔향" : "일반 발사 · 잔향") : "재생 완료";
            float kick = Mathf.Clamp01(1f - since / _settings.RecoilSeconds);
            if (since < 0) kick = 0;
            _unit.transform.position = _unitHome + Vector3.down * (kick * kick * _settings.Recoil * (_skill ? 2f : 1f));
            _unit.sprite = _visual && since >= 0 && since < _settings.FirePoseSeconds ? _fireSprite : _idleSprite;
            if (now >= _endDsp) { _active = false; _unit.transform.position = _unitHome; _unit.sprite = _idleSprite; }
        }
        var source = now < _fireDsp ? _chargeSource : _shotSource;
        source.GetOutputData(_samples, 0);
        float peak = 0; foreach (float sample in _samples) peak = Mathf.Max(peak, Mathf.Abs(sample));
        _level = Mathf.Lerp(_level, Mathf.Clamp01(peak * 3f), 1f - Mathf.Exp(-Time.unscaledDeltaTime * 15f));
        _meter.fillAmount = _level;
        if (_repeat && !_pending && !_active && now >= _repeatAt) Audition(_skill);
    }

    private void OnDisable() { _repeat = false; StopAudition(); }
    private void OnDestroy() { if (_whiteSprite != null) Destroy(_whiteSprite); }

    private void ToggleRepeat()
    {
        _repeat = !_repeat; Label(_repeatButton).text = _repeat ? "반복 켜짐" : "반복 꺼짐";
        if (_repeat) Audition(_skill);
    }

    private void ToggleVisual()
    {
        StopAudition(); _visual = !_visual;
        Label(_visualButton).text = _visual ? "연출 함께" : "소리만 듣기";
    }

    private void BuildUi()
    {
        var root = new GameObject("RailgunSoundCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(540,1168); scaler.matchWidthOrHeight = 1f;
        if (EventSystem.current == null)
        { var events = new GameObject("SoundLabEventSystem", typeof(EventSystem), typeof(StandaloneInputModule)); events.transform.SetParent(transform, false); }
        var top = Box("Header", root.transform, new Vector2(0,.895f), Vector2.one, new Color(.08f,.11f,.17f,.97f));
        Text("Title",top,"디시그망 · 레일건 사운드",new Vector2(.05f,.48f),new Vector2(.95f,.9f),25);
        Text("Subtitle",top,"0.4초 충전 → 순간 발사 / 스킬 B → A 비교",new Vector2(.05f,.12f),new Vector2(.95f,.47f),17);
        var panel = Box("Comparison",root.transform,Vector2.zero,new Vector2(1,.355f),new Color(.075f,.10f,.16f,.98f));
        _variantButtons = new Button[_variants.Length];
        for(int i=0;i<_variants.Length;i++)
        {
            int index=i;float x=.045f+(i%2)*.465f, y=.845f-(i/2)*.14f;
            _variantButtons[i]=Button("Variant"+(char)('A'+i),panel,_variants[i].DisplayName,new Vector2(x,y),new Vector2(i==4?.955f:x+.445f,y+.12f),()=>SelectVariant(index));
        }
        _description=Text("Description",panel,"",new Vector2(.05f,.475f),new Vector2(.95f,.555f),16);
        Button("Basic",panel,"일반 발사",new Vector2(.045f,.33f),new Vector2(.325f,.46f),()=>Audition(false));
        Button("Skill",panel,"스킬 발사",new Vector2(.345f,.33f),new Vector2(.625f,.46f),()=>Audition(true));
        _repeatButton=Button("Repeat",panel,"반복 꺼짐",new Vector2(.645f,.33f),new Vector2(.955f,.46f),ToggleRepeat);
        _visualButton=Button("Visual",panel,"연출 함께",new Vector2(.045f,.185f),new Vector2(.485f,.305f),ToggleVisual);
        Button("Stop",panel,"정지",new Vector2(.505f,.185f),new Vector2(.955f,.305f),()=>{_repeat=false;Label(_repeatButton).text="반복 꺼짐";StopAudition();});
        _volumeText=Text("VolumeLabel",panel,"",new Vector2(.05f,.085f),new Vector2(.47f,.18f),16);
        var track=Box("Volume",panel,new Vector2(.50f,.108f),new Vector2(.95f,.153f),new Color(.22f,.27f,.34f));
        var slider=track.gameObject.AddComponent<Slider>();slider.minValue=0;slider.maxValue=1;slider.value=_settings.PreviewVolume;
        var handle=Box("Handle",track,new Vector2(0,0),new Vector2(0,1),Accent);
        handle.sizeDelta=new Vector2(18,20);handle.GetComponent<Image>().raycastTarget=true;
        slider.handleRect=handle;slider.direction=Slider.Direction.LeftToRight;slider.onValueChanged.AddListener(SetVolume);
        track.GetComponent<Image>().raycastTarget=true;
        var progressRoot=Box("ChargeTrack",root.transform,new Vector2(.05f,.372f),new Vector2(.95f,.379f),new Color(.10f,.17f,.21f,.95f));
        _progress=Fill(progressRoot,"Charge",Accent);
        _status=Text("Status",root.transform,"",new Vector2(.05f,.386f),new Vector2(.28f,.423f),18);
        var meterRoot=Box("OutputTrack",root.transform,new Vector2(.73f,.397f),new Vector2(.95f,.405f),new Color(.12f,.20f,.25f));
        _meter=Fill(meterRoot,"Output",new Color(.76f,.84f,.38f));
    }

    private RectTransform Box(string name,Transform parent,Vector2 min,Vector2 max,Color color)
    {
        var rect=Rect(name,parent,min,max);var image=rect.gameObject.AddComponent<Image>(); image.color=color;image.raycastTarget=false;return rect;
    }
    private RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max)
    {
        var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);
        rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;return rect;
    }
    private TMP_Text Text(string name,Transform parent,string value,Vector2 min,Vector2 max,float size)
    {
        var text=Rect(name,parent,min,max).gameObject.AddComponent<TextMeshProUGUI>();text.font=_font;text.text=value;text.fontSize=size;
        text.color=TextColor;text.raycastTarget=false;text.alignment=TextAlignmentOptions.MidlineLeft;
        text.enableAutoSizing=true;text.fontSizeMin=size*.8f;text.fontSizeMax=size;return text;
    }
    private Button Button(string name,Transform parent,string value,Vector2 min,Vector2 max,UnityEngine.Events.UnityAction action)
    {
        var rect=Box(name,parent,min,max,new Color(.13f,.17f,.24f));rect.GetComponent<Image>().raycastTarget=true;
        var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=rect.GetComponent<Image>();button.transition=Selectable.Transition.ColorTint;
        button.navigation=new Navigation { mode=Navigation.Mode.None };button.onClick.AddListener(action);
        var text=Text("Label",rect,value,new Vector2(.04f,0),new Vector2(.96f,1),19);text.alignment=TextAlignmentOptions.Center;return button;
    }
    private static TMP_Text Label(Button button) => button.GetComponentInChildren<TMP_Text>();
    private Image Fill(Transform parent,string name,Color color)
    {
        var image=Box(name,parent,Vector2.zero,Vector2.one,color).GetComponent<Image>();image.sprite=_whiteSprite;image.type=Image.Type.Filled;
        image.fillMethod=Image.FillMethod.Horizontal;image.fillAmount=0;return image;
    }
}
