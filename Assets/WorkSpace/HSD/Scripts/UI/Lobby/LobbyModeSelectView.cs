using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Chooses the destination before either mode reserves stamina.</summary>
public sealed class LobbyModeSelectView : MonoBehaviour
{
    [SerializeField] private GameObject _panel;
    [SerializeField] private Button _normalButton;
    [SerializeField] private Button _testButton;
    [SerializeField] private Button _closeButton;
    private Action<bool> _selected;

    private void Awake()
    {
        _normalButton.onClick.AddListener(SelectNormal);
        _testButton.onClick.AddListener(SelectTest);
        _closeButton.onClick.AddListener(Hide);
        Hide();
    }

    /// <summary>Opens two choices; dismissing the panel never starts entry.</summary>
    public void Show(Action<bool> selected)
    {
        _selected = selected;
        transform.SetAsLastSibling();
        _panel.SetActive(true);
        _panel.transform.SetAsLastSibling();
    }

    /// <summary>Closes the chooser and discards any unsubmitted choice.</summary>
    public void Hide()
    {
        _selected = null;
        _panel.SetActive(false);
    }

    private void SelectNormal() => Select(false);
    private void SelectTest() => Select(true);
    private void Select(bool test)
    {
        var selected = _selected;
        Hide();
        selected?.Invoke(test);
    }

    private void OnDisable() => Hide();
    private void OnDestroy()
    {
        _normalButton.onClick.RemoveListener(SelectNormal);
        _testButton.onClick.RemoveListener(SelectTest);
        _closeButton.onClick.RemoveListener(Hide);
    }
}
