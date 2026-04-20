using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LoginSample : MonoBehaviour
{
    [SerializeField] private Button loginButton;
    [SerializeField] private TMP_InputField id;
    [SerializeField] private TMP_InputField pass;

    public event Action OnLoginFinish;

    public void Init()
    {
        BtnActive(true);
        loginButton.onClick.AddListener(ClickCheck);
    }

    private void OnDisable()
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

}
