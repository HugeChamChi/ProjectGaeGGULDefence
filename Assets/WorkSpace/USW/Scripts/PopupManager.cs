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
    

}
