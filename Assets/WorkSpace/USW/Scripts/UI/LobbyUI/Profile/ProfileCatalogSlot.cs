using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One authored cosmetic choice, including an empty disabled placeholder.</summary>
public sealed class ProfileCatalogSlot : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private Image _art;
    [SerializeField] private Image _selection;
    [SerializeField] private GameObject _locked;
    [SerializeField] private GameObject _equipped;
    [SerializeField] private TMP_Text _label;
    private Action<IProfileItem> _select;
    private IProfileItem _item;
    /// <summary>Choice identity; null for a resource placeholder.</summary>
    public IProfileItem Item => _item;
    private void Awake() => _button.onClick.AddListener(Select);
    private void Select() => _select?.Invoke(_item);
    /// <summary>Renders ownership and equipment separately from the staged selection.</summary>
    public void Render(IProfileItem item, Sprite artwork, bool owned, bool selected, bool equipped, Action<IProfileItem> select)
    {
        _item = item; _select = select;
        _button.interactable = item != null;
        _art.sprite = artwork; _art.enabled = artwork != null;
        _selection.enabled = selected; _locked.SetActive(item != null && !owned); _equipped.SetActive(item != null && equipped);
        _label.text = item != null && item.Id == 0 ? "기본" : "";
    }
    private void OnDestroy() { if (_button != null) _button.onClick.RemoveListener(Select); _select = null; }
}
