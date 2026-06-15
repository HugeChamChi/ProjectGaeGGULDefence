using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GaeGGUL.Animation;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 유닛 클릭 시 나타나는 액션 팝업.
///
/// 외부 API
///   Show(unit, canMerge) — 팝업 표시
///   Hide()               — 팝업 숨기기
///   OnDismissRequested   — 외부 클릭으로 닫힘 요청 시 발행 (InGameInstaller가 구독)
///
/// 내부 구현(애니메이션·위치·클릭 감지)은 외부에 노출하지 않음.
/// </summary>
public class UnitActionPopupUI : MonoBehaviour
{
    [SerializeField] private MergeButtonUI mergeButton;
    [SerializeField] private SellButtonUI  sellButton;

    public event Action OnDismissRequested;

    private bool          _isShowing;
    private bool          _justShown;

    private void Awake()
    {
        if (mergeButton != null) mergeButton.OnMergeRequested += Hide;
        if (sellButton != null) sellButton.OnSellRequested += (_) => Hide();
    }

    public void Show(UnitBase unit, bool canMerge)
    {
        if (mergeButton == null) { Debug.LogError("[UnitActionPopupUI] mergeButton 미연결 (Inspector 확인)"); return; }
        if (sellButton  == null) { Debug.LogError("[UnitActionPopupUI] sellButton 미연결 (Inspector 확인)"); return; }

        gameObject.SetActive(true);
        _justShown = true;

        mergeButton.SetState(canMerge);
        sellButton.SetUnit(unit);

        _isShowing = true;
    }

    public void Hide()
    {
        if (!_isShowing) return;
        _isShowing = false;
        gameObject.SetActive(false);
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
