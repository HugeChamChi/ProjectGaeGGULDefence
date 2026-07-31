using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_MailItem : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI contentText;
    [SerializeField] private Button receiveButton;

    public void Init(Post post, Action onReceive)
    {
        if (titleText != null) titleText.text = post.title;
        if (contentText != null) contentText.text = post.content;

        if (receiveButton != null)
        {
            receiveButton.onClick.RemoveAllListeners();
            receiveButton.interactable = post.isCanReceive;
            receiveButton.onClick.AddListener(() => onReceive?.Invoke());
        }
    }
}
