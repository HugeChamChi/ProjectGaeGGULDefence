using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VContainer;

public class UI_ChiefSkillButtonView : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button btn_Skill;
    [SerializeField] private Image img_CooldownFill;
    [SerializeField] private TMP_Text txt_Cooldown;
    [SerializeField] private Image img_Icon;

    private UI_ChiefSkillPresenter _presenter;

    private ChieftainSpawner _spawner;

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
            var spawner = FindObjectOfType<ChieftainSpawner>();
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

    public void SetCooldownFillAmount(float fillAmount)
    {
        if (img_CooldownFill != null)
        {
            img_CooldownFill.fillAmount = fillAmount;
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

}
