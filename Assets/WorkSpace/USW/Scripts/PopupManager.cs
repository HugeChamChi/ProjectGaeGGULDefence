using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using BackEnd;
using DG.Tweening;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// 팝업 통합 관리 클래스
/// 
/// </summary>
public class PopupManager : MonoBehaviour
{
    public static PopupManager Instance { get; private set; }

    [Header("배경 버튼")] 
    [SerializeField] private Button _backgroundButton;
    
    [Header("쿠폰 팝업")] 
    [SerializeField] private GameObject _couponPanel;
    [SerializeField] private TMP_Text _couponText;
    [SerializeField] private TMP_InputField _couponInputField;
    [SerializeField] private Button _couponSubmitButton;
    [SerializeField] private Button _couponCancelButton;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (_couponSubmitButton != null) _couponSubmitButton.onClick.AddListener(OnCouponSubmitClicked);
        if (_couponCancelButton != null) _couponCancelButton.onClick.AddListener(CloseCouponPanel);
        if (_backgroundButton != null) _backgroundButton.onClick.AddListener(CloseCouponPanel);
    }

    // 쿠폰 패널 GameObject 자체가 비활성 상태로 시작하므로, 외부에서 여는 시점(SetActive(true))에
    // OnEnable에서 입력창/결과 텍스트를 초기화한다. Awake는 오브젝트가 처음 활성화되기 전엔 호출되지 않아
    // Instance 참조만으로는 열기 시점을 잡을 수 없다.
    private void OnEnable()
    {
        if (_couponInputField != null) _couponInputField.text = string.Empty;
        if (_couponText != null) _couponText.text = string.Empty;
    }

    private void CloseCouponPanel()
    {
        if (_couponPanel != null) _couponPanel.SetActive(false);
    }

    private void OnCouponSubmitClicked()
    {
        string code = _couponInputField != null ? _couponInputField.text : string.Empty;

        Table.Coupon.UseCoupon(code, (success, message) =>
        {
            if (_couponText != null) _couponText.text = message;
        });
    }
}
