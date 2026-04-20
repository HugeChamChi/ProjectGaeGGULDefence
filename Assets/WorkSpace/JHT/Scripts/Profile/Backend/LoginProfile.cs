using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LoginProfile : MonoBehaviour
{
    [SerializeField] private Button loginButton;
    [SerializeField] private TMP_InputField id;
    [SerializeField] private TMP_InputField pass;
    public GameObject profile;

    public event Action OnLoginFinish;

    private void Start()
    {
        profile.gameObject.SetActive(false);
    }

    public void Init()
    {
        BtnActive(true);
        loginButton.onClick.AddListener(ClickCheck);
    }

    public void Outit()
    {
        loginButton.onClick.RemoveAllListeners();
    }

    private void ClickCheck()
    {
        var bro = BackendLogin.Instance.CustomLogin(id.text, pass.text);

        if (bro == null)
            return;

        BtnActive(false);
        if (bro.IsSuccess())
        {
            OnLoginFinish.Invoke();
            profile.SetActive(true);
            gameObject.SetActive(false);
        }
        else
        {
            BtnActive(true);
        }

    }

    public void BtnActive(bool value)
    {
        loginButton.interactable = value;
    }

    private void OnDisable()
    {
        loginButton.onClick.RemoveAllListeners();
    }
}
