using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VContainer;

public class UI_ChiefSkillButtonView : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button btn_Skill;
    [SerializeField] private Slider slider_Cooldown;
    [SerializeField] private TMP_Text txt_Cooldown;
    [SerializeField] private Image img_Icon;
    [SerializeField] private ChiefSkillReadyBanner _readyBanner;
    [Tooltip("충전 완료 시 틀 위로 솟아오르고 쿨타임 중 가라앉는 초상화 연출 (선택). 비우면 자식에서 찾음")]
    [SerializeField] private ChiefSkillIconMotion _iconMotion;
    [Tooltip("지정하면 족장 스킬 아이콘 대신 이 이미지를 항상 표시한다 (임시 고정 이미지용). 비우면 스킬 아이콘 사용")]
    [SerializeField] private Sprite iconOverride;

    /// <summary>실제 사용 가능 여부와 독립적인 충전 완료 위치 표시.</summary>
    public void SetChargedPresentation(bool charged, bool immediate = false)
    {
        _readyBanner?.SetCharged(charged, immediate);
        _iconMotion?.SetCharged(charged, immediate);
    }

    [Header("쿨타임 중 비활성화 연출")]
    [SerializeField] private Color disabledColor = new Color(0.5f, 0.5f, 0.5f, 1f);
    private Color _normalIconColor = Color.white;
    private Color _normalButtonColor = Color.white;
    private Sprite _defaultIcon;

    private UI_ChiefSkillPresenter _presenter;

    private ChieftainSelection _selection;

    private void Awake()
    {
        if (_iconMotion == null) _iconMotion = GetComponentInChildren<ChiefSkillIconMotion>(true);
        if (img_Icon != null)
        {
            _normalIconColor = img_Icon.color;
            _defaultIcon = img_Icon.sprite;
        }
        if (btn_Skill != null && btn_Skill.targetGraphic != null) _normalButtonColor = btn_Skill.targetGraphic.color;
    }

    [Inject]
    public void Construct(ChieftainSelection chieftainSelection)
    {
        if (_presenter != null) return;
        _selection = chieftainSelection;
        _presenter = new UI_ChiefSkillPresenter(this);
        if (isActiveAndEnabled) Subscribe();
    }

    private void OnEnable() => Subscribe();

    private void Subscribe()
    {
        if (_selection == null || _presenter == null) return;
        _selection.OnActiveSkillChanged -= OnActiveSkillChanged;
        _selection.OnActiveSkillChanged += OnActiveSkillChanged;
        _presenter.SetActiveSkill(_selection.ActiveSkill);
    }

    private void OnDisable()
    {
        if (_selection != null) _selection.OnActiveSkillChanged -= OnActiveSkillChanged;
        _presenter?.SetActiveSkill(null);
    }

    // 연결은 InGameInstaller.Start가 Construct로 수행한다. 이 컴포넌트의 Start가 먼저 돌 수 있으므로
    // 검색(Find)으로 우회하지 않고, 한 프레임 뒤에도 연결이 없을 때만 실제 배선 누락으로 보고한다.
    private void Start() => VerifyWiringAsync().Forget();

    private async UniTaskVoid VerifyWiringAsync()
    {
        if (await UniTask.Yield(this.GetCancellationTokenOnDestroy()).SuppressCancellationThrow()) return;
        if (_presenter == null)
            Debug.LogError("[UI_ChiefSkillButtonView] 연결되지 않음 — InGameInstaller의 _chiefSkillButtonView 참조를 확인하세요.", this);
    }

    private void OnActiveSkillChanged(IChiefActiveSkill skill)
    {
        _presenter?.SetActiveSkill(skill);
    }

    private void OnDestroy()
    {
        OnDisable();
        if (btn_Skill != null && _presenter != null)
            btn_Skill.onClick.RemoveListener(_presenter.ExecuteSkill);
    }

    public void BindSkillButton(UnityEngine.Events.UnityAction action)
    {
        if (btn_Skill != null)
        {
            btn_Skill.onClick.RemoveAllListeners();
            btn_Skill.onClick.AddListener(action);
        }
    }

    private void Update()
    {
        _presenter?.OnUpdate(Time.deltaTime);
    }

    public void SetButtonInteractable(bool isInteractable)
    {
        if (btn_Skill != null && btn_Skill.interactable != isInteractable)
        {
            btn_Skill.interactable = isInteractable;
        }
    }

    public void SetCooldownSliderValue(float value)
    {
        if (slider_Cooldown != null)
        {
            slider_Cooldown.value = value;
        }
    }

    public void SetCooldownText(string text)
    {
        if (txt_Cooldown != null)
        {
            txt_Cooldown.text = text;
        }
    }

    public void SetIcon(Sprite icon)
    {
        if (img_Icon != null)
        {
            // 족장이 정해지기 전(게임 시작 전 대기)에는 씬에 지정된 기본 이미지를 유지한다.
            var sprite = iconOverride != null ? iconOverride : icon != null ? icon : _defaultIcon;
            img_Icon.sprite = sprite;
            img_Icon.enabled = sprite != null;
        }
    }

    /// <summary>쿨타임 중일 때 아이콘/버튼을 회색으로, 사용 가능할 때 원래 색으로 되돌린다.</summary>
    public void SetDisabledVisual(bool isOnCooldown)
    {
        if (img_Icon != null)
        {
            img_Icon.color = isOnCooldown ? disabledColor : _normalIconColor;
        }
        if (btn_Skill != null && btn_Skill.targetGraphic != null)
        {
            btn_Skill.targetGraphic.color = isOnCooldown ? disabledColor : _normalButtonColor;
        }
    }
}
