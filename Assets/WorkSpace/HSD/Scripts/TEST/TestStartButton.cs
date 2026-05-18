using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TestStartButton : MonoBehaviour
{
    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.interactable = false;
        GameDataManager.Instance.OnLoaded += () => button.interactable = true;
    }
}
