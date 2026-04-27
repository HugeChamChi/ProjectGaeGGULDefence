using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class BottomPanelController : MonoBehaviour
{
    [System.Serializable]
    public class PanelButton
    {
        public Button _button;
        public RectTransform _mainRect;
        public Image _background;
        public Image _light;
        public Image _innerShadow;
        public GameObject _panel;
        public Image _buttonImage;

        [Header("Sprite")] public Sprite _activeSprite;
        public Sprite _inactiveSprite;
    }

    [Header("Panel Buttons (5개)")] public PanelButton[] panelButtons = new PanelButton[5];

    private static readonly Color selectedBg = ColorHex.Hex("7550D2");
    private static readonly Color selectedLight = ColorHex.Hex("8F8FFF");
    private static readonly Color selectedAdditional = ColorHex.Hex("593DA1");

    private static readonly Color deselectedBg = ColorHex.Hex("3B324D");
    private static readonly Color deselectedLight = ColorHex.Hex("565076");
    private static readonly Color deselectedAdditional = ColorHex.Hex("332B47");

    private int currentIndex = 2;

    void Start()
    {
        for (int i = 0; i < 5; i++)
        {
            int index = i;
            panelButtons[i]._button.onClick.AddListener(() => SelectButton(index));
        }

        for (int i = 0; i < 5; i++)
        {
            if (i == currentIndex)
            {
                SetSelected(i, true);
                panelButtons[i]._panel.SetActive(true);
            }
            else
            {
                SetSelected(i, false);
                panelButtons[i]._panel.SetActive(false);
            }
        }
    }

    public void SelectButton(int index)
    {
        if (index == currentIndex) return;

        // 이전 버튼 deselect
        AnimateDeselect(currentIndex);
        panelButtons[currentIndex]._panel.SetActive(false);

        // 새 버튼 select
        AnimateSelect(index);
        panelButtons[index]._panel.SetActive(true);

        currentIndex = index;
    }

    void AnimateSelect(int index)
    {
        var btn = panelButtons[index];


        btn._mainRect.DOSizeDelta(new Vector2(btn._mainRect.sizeDelta.x, 238f), 0.15f).SetEase(Ease.Linear);


        btn._background.DOColor(selectedBg, 0.15f).SetEase(Ease.Linear);


        btn._light.DOColor(selectedLight, 0.05f).SetEase(Ease.Linear);
        btn._innerShadow.DOColor(selectedAdditional, 0.05f).SetEase(Ease.Linear);
        
        btn._buttonImage.sprite = btn._activeSprite;
    }

    void AnimateDeselect(int index)
    {
        var btn = panelButtons[index];


        btn._mainRect.DOSizeDelta(new Vector2(btn._mainRect.sizeDelta.x, 200f), 0.05f).SetEase(Ease.Linear);


        btn._background.DOColor(deselectedBg, 0.05f).SetEase(Ease.Linear);
        btn._light.DOColor(deselectedLight, 0.05f).SetEase(Ease.Linear);
        btn._innerShadow.DOColor(deselectedAdditional, 0.05f).SetEase(Ease.Linear);
        
        btn._buttonImage.sprite = btn._inactiveSprite;
    }

    void SetSelected(int index, bool selected)
    {
        var btn = panelButtons[index];

        if (selected)
        {
            btn._mainRect.sizeDelta = new Vector2(btn._mainRect.sizeDelta.x, 238f);
            btn._background.color = selectedBg;
            btn._light.color = selectedLight;
            btn._innerShadow.color = selectedAdditional;
            btn._buttonImage.sprite = btn._activeSprite;
        }
        else
        {
            btn._mainRect.sizeDelta = new Vector2(btn._mainRect.sizeDelta.x, 200f);
            btn._background.color = deselectedBg;
            btn._light.color = deselectedLight;
            btn._innerShadow.color = deselectedAdditional;
            btn._buttonImage.sprite = btn._inactiveSprite;
        }
    }
}