using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Shows <see cref="TotemBagPopup"/> while the bottom totem drawer is open and idle, closes both on an outside tap,
/// and shows a stored totem's info when its drawer slot is tapped. Created by <see cref="TotemInventoryUI"/>; no scene
/// or prefab wiring. Hidden during drags, placement and guided (tutorial) placement so the field stays reachable.
/// </summary>
public sealed class TotemBagController : MonoBehaviour
{
    private const string SkinResource = "TotemBagSkin";

    private TotemInventoryUI _drawer;
    private TotemInventory _inventory;
    private GridManager _grid;
    private RectTransform _strip;
    private TotemBagPopup _popup;
    private bool _shown;

    /// <summary>Creates the controller in the drawer's scene. <paramref name="strip"/> is the drawer area left uncovered.</summary>
    public static TotemBagController Attach(TotemInventoryUI drawer, TotemInventory inventory, GridManager grid, RectTransform strip)
    {
        var go = new GameObject(nameof(TotemBagController));
        SceneManager.MoveGameObjectToScene(go, drawer.gameObject.scene);
        var controller = go.AddComponent<TotemBagController>();
        controller._drawer = drawer; controller._inventory = inventory; controller._grid = grid; controller._strip = strip;
        drawer.BagController = controller;
        drawer.OnSlotTapped += controller.HandleSlotTapped;
        return controller;
    }

    private void LateUpdate()
    {
        if (_drawer == null) { Destroy(gameObject); return; }
        bool want = _drawer.IsOpen && !_drawer.IsClosing && !_drawer.IsDraggingOrPlacing && _drawer.AllowClose;
        if (want == _shown) return;
        if (want && !EnsurePopup()) return;
        _shown = want;
        if (want) _popup.Open();
        else _popup.Close();
    }

    private bool EnsurePopup()
    {
        if (_popup != null) return true;
        if (_grid == null || _grid.Columns <= 0 || _grid.Rows <= 0 || _grid.GetCell(0, 0) == null) return false;
        var skin = Resources.Load<TotemBagSkin>(SkinResource);
        if (skin == null) { Debug.LogWarning("[TotemBag] Missing Resources/" + SkinResource); enabled = false; return false; }
        var host = new GameObject(nameof(TotemBagPopup), typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        host.transform.SetParent(transform, false);
        _popup = host.AddComponent<TotemBagPopup>();
        _popup.Bind(_grid, skin, _strip);
        _popup.RemovePlaced = _inventory.TryDeletePlaced;
        _popup.OnOutsideTapped += _drawer.RequestClose;
        return true;
    }

    internal void HideForStoredDrag()
    {
        if (!_shown) return;
        _shown = false;
        _popup.Close();
    }

    private void HandleSlotTapped(int index)
    {
        if (!_shown || index < 0 || index >= _inventory.Items.Count) return;
        _popup.ShowStored(_inventory.Items[index], _drawer.GetSlotRect(index));
    }

    private void OnDestroy()
    {
        if (_drawer != null) _drawer.OnSlotTapped -= HandleSlotTapped;
        if (_popup != null && _drawer != null) _popup.OnOutsideTapped -= _drawer.RequestClose;
    }
}
