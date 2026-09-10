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

    [Header("쿨타임 중 비활성화 연출")]
    [SerializeField] private Color disabledColor = new Color(0.5f, 0.5f, 0.5f, 1f);
    private Color _normalIconColor = Color.white;
    private Color _normalButtonColor = Color.white;

    private UI_ChiefSkillPresenter _presenter;

    private ChieftainSpawner _spawner;

    private void Awake()
    {
        if (img_Icon != null) _normalIconColor = img_Icon.color;
        if (btn_Skill != null && btn_Skill.targetGraphic != null) _normalButtonColor = btn_Skill.targetGraphic.color;
    }

    [Inject]
    public void Construct(ChieftainSpawner chieftainSpawner)
    {
        if (_presenter != null) return;
        _spawner = chieftainSpawner;
        _presenter = new UI_ChiefSkillPresenter(this);

        if (_spawner != null)
        {
            _spawner.OnChieftainSpawned += OnChieftainSpawned;
            
            if (_spawner.ChieftainUnit is ChiefUnit chief)
            {
                _presenter.SetChiefUnit(chief);
            }
        }
    }

    private void Start()
    {
        if (_presenter == null)
        {
            var spawner = FindFirstObjectByType<ChieftainSpawner>();
            if (spawner != null)
            {
                Construct(spawner);
                Debug.Log("[UI_ChiefSkillButtonView] VContainer 주입 누락 감지 - FindObjectOfType으로 ChieftainSpawner를 수동 할당하고 이벤트를 구독합니다.");
            }
            else
            {
                Debug.LogError("[UI_ChiefSkillButtonView] ChieftainSpawner를 찾을 수 없습니다!");
            }
        }
    }

    private void OnChieftainSpawned(ChiefUnit chief)
    {
        _presenter?.SetChiefUnit(chief);
    }

    private void OnDestroy()
    {
        if (_spawner != null)
        {
            _spawner.OnChieftainSpawned -= OnChieftainSpawned;
        }
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
        if (img_Icon != null && icon != null)
        {
            img_Icon.sprite = icon;
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
