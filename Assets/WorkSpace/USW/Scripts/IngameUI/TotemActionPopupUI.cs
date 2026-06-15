using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GaeGGUL.Animation;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 토템 클릭 시 나타나는 액션 팝업.
///
/// 외부 API
///   Show(totem)          — 팝업 표시
///   Hide()               — 팝업 숨기기
///   OnDismissRequested   — 외부 클릭으로 닫힘 요청 시 발행
///   OnSellTotemRequested — 판매 버튼 클릭 시 발행
///
/// 씬 구조
///   TotemActionPopup  ← 이 컴포넌트, Pivot (0.5, 0.5)
///     ├── RotateButton  ← Button, anchoredPosition (-60, -100) = 7시
///     └── SellButton    ← Button, anchoredPosition ( 60, -100) = 5시
/// </summary>
public class TotemActionPopupUI : MonoBehaviour
{
    [SerializeField] private RotateButtonUI rotateButton;
    [SerializeField] private SellTotemButtonUI sellButton;

    public event Action              OnDismissRequested;
    public event Action<TotemBase>   OnSellTotemRequested;

    private bool          _isShowing;
    private bool          _justShown;
    private TotemBase     _currentTotem;



    public void Show(TotemBase totem)
    {
        _justShown    = true;
        _currentTotem = totem;

        rotateButton.SetTotem(totem);
        sellButton.SetTotem(totem);
        sellButton.SetPopup(this);

        gameObject.SetActive(true);

        _isShowing = true;
    }

    public void Hide()
    {
        if (!_isShowing) return;
        _isShowing    = false;
        _currentTotem = null;

        gameObject.SetActive(false);
    }

    internal void RaiseSellRequested(TotemBase totem)
    {
        OnSellTotemRequested?.Invoke(totem);
    }

    private void LateUpdate()
    {
        if (_justShown) { _justShown = false; return; }
        if (!_isShowing || !Input.GetMouseButtonDown(0)) return;

        var pointer = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, results);

        foreach (var r in results)
        {
            if (r.gameObject.transform.IsChildOf(transform)) return;
        }

        Hide();
        OnDismissRequested?.Invoke();
    }

}
