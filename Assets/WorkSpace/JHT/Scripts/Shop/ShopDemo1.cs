using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ShopDemo1 : MonoBehaviour
{
    [SerializeField] private Button shopButton;
    [SerializeField] private Button profileButton;
    [SerializeField] private Button InvokeFuncButton;

    [SerializeField] private GameObject shopCanvas;
    [SerializeField] private GameObject profileCanvas;

    BackendFunction backendFunc;
    private bool isShopOpen = false;
    private bool isProfileOpen = false;
    private void OnEnable()
    {
        shopButton.onClick.AddListener(InitShopData);
        profileButton.onClick.AddListener(InitProfileData);
        //InvokeFuncButton.onClick.AddListener();
    }

    private void OnDisable()
    {
        shopButton.onClick.RemoveAllListeners();
        profileButton.onClick.RemoveAllListeners();
        //InvokeFuncButton.onClick.RemoveAllListeners();
    }

    private void Start()
    {
        backendFunc = new BackendFunction();
    }

    private void InitShopData()
    {
        isShopOpen = !isShopOpen;

        shopCanvas.SetActive(isShopOpen);
    }

    private void InitProfileData()
    {
        isProfileOpen = !isProfileOpen;
        profileCanvas.SetActive(isProfileOpen);
        profileCanvas.GetComponent<UserProfilePanel>().OpenProfile();
    }

}
