using System.Collections.Generic;
using BackEnd;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_MailboxPanel : UI_Base
{
    [SerializeField] private Transform listContent;
    [SerializeField] private UI_MailItem itemTemplate;
    [SerializeField] private Button adminTabButton;
    [SerializeField] private Button couponTabButton;
    [SerializeField] private Button receiveAllButton;
    [SerializeField] private Button couponEntryButton;
    [SerializeField] private GameObject couponPanelRoot;
    [SerializeField] private TextMeshProUGUI emptyText;

    private PostType _currentTab = PostType.Admin;
    private readonly List<UI_MailItem> _spawned = new();

    protected override void Awake()
    {
        base.Awake();

        itemTemplate.gameObject.SetActive(false);
        adminTabButton.onClick.AddListener(() => ShowTab(PostType.Admin));
        couponTabButton.onClick.AddListener(() => ShowTab(PostType.Coupon));
        receiveAllButton.onClick.AddListener(OnReceiveAllClicked);

        if (couponEntryButton != null)
            couponEntryButton.onClick.AddListener(() => couponPanelRoot?.SetActive(true));
    }

    public override async UniTask OpenAsync()
    {
        await base.OpenAsync();
        ShowTab(PostType.Admin);
    }

    private void ShowTab(PostType postType)
    {
        _currentTab = postType;
        Player.Mail.GetPostList(postType, RefreshList);
    }

    private void RefreshList()
    {
        foreach (var item in _spawned)
        {
            if (item != null) Destroy(item.gameObject);
        }
        _spawned.Clear();

        var posts = Player.Mail.GetCurrentPostList();

        if (emptyText != null) emptyText.gameObject.SetActive(posts.Count == 0);

        for (int i = 0; i < posts.Count; i++)
        {
            int index = i;
            var item = Instantiate(itemTemplate, listContent);
            item.gameObject.SetActive(true);
            item.Init(posts[i], () => OnReceiveClicked(index));
            _spawned.Add(item);
        }
    }

    private void OnReceiveClicked(int index)
    {
        Player.Mail.ReceivePost(_currentTab, index);
        RefreshList();
    }

    private void OnReceiveAllClicked()
    {
        Player.Mail.ReceiveAllPost(_currentTab);
        RefreshList();
    }
}
