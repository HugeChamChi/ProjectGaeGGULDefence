using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

public class UI_PartySelectView : UI_Base
{
    [SerializeField] private RectTransform contentTransform;
    [SerializeField] private UI_PartyItem partyItemPrefab;
    [SerializeField] private Button startGameButton;

    private List<UI_PartyItem> _spawnedItems = new List<UI_PartyItem>();
    private Action<PartyDataSO> _onPartySelected;
    private Action _onStartGameClicked;
    
    [SerializeField] private PartyLobbyDataSO _lobbyData; // 인스펙터에서도 할당 가능하도록 변경
    private UI_PartySelectPresenter _presenter; // 유저님 아이디어: View가 Presenter를 가집니다!
    private bool _isPopulated = false;

    [Inject]
    public void Construct(UI_PartySelectPresenter presenter, IObjectResolver resolver)
    {
        _presenter = presenter;
        
        try
        {
            var lobbyData = resolver.Resolve<PartyLobbyDataSO>();
            if (lobbyData != null)
            {
                _lobbyData = lobbyData;
            }
        }
        catch (System.Exception)
        {
            // VContainer에 등록되어 있지 않으면 인스펙터 할당값 사용
        }
    }

    protected override void Awake()
    {
        base.Awake();
        
        // VContainer를 통해 주입받지 못한 경우(예: 런타임 동적 생성 또는 두 번째 인스턴스) 직접 Resolve 시도
        if (_presenter == null)
        {
            var scope = FindObjectOfType<VContainer.Unity.LifetimeScope>();
            if (scope != null && scope.Container != null)
            {
                try
                {
                    _presenter = scope.Container.Resolve<UI_PartySelectPresenter>();
                    Debug.Log("[UI_PartySelectView] Dynamically resolved UI_PartySelectPresenter from LifetimeScope.");
                }
                catch (System.Exception) 
                {
                    // LobbyLifetimeScope가 없거나 등록이 안 되어 있으면 수동 생성 (테스트용/독립 실행용)
                    try
                    {
                        var sceneManager = scope.Container.Resolve<SceneChangeManager>();
                        _presenter = new UI_PartySelectPresenter(sceneManager);
                        Debug.Log("[UI_PartySelectView] Manually created UI_PartySelectPresenter (Fallback).");
                    }
                    catch (System.Exception) { }
                }
            }
            
            // Scope조차 없다면 완전 깡통으로라도 생성해 줍니다 (에러 방지 최후의 수단)
            if (_presenter == null)
            {
                _presenter = new UI_PartySelectPresenter(null);
                Debug.Log("[UI_PartySelectView] Manually created UI_PartySelectPresenter with null SceneManager.");
            }
        }

        // View가 Awake될 때 (즉, 켜질 때) 스스로 Presenter를 통제하여 초기화시킵니다!
        if (_presenter != null)
        {
            _presenter.Initialize(this);
        }
    }

    public void Init(Action<PartyDataSO> onPartySelected, Action onStartGameClicked)
    {
        _onPartySelected = onPartySelected;
        _onStartGameClicked = onStartGameClicked;

        if (startGameButton != null)
        {
            startGameButton.onClick.RemoveAllListeners();
            startGameButton.onClick.AddListener(() => _onStartGameClicked?.Invoke());
            startGameButton.interactable = false; // 선택되기 전에는 시작 불가
        }
        else
        {
            Debug.LogError("[UI_PartySelectView] startGameButton is NULL! Please assign it in the Inspector.");
        }

        // 씬 시작 시점에 팝업이 이미 켜져있는 상태라면 Open()을 기다리지 않고 즉시 세팅합니다.
        if (gameObject.activeInHierarchy && !_isPopulated)
        {
            if (_lobbyData != null)
            {
                Debug.Log($"[UI_PartySelectView] Panel is active on Start. Populating list immediately with {_lobbyData.partyList.Count} items.");
                PopulateList(_lobbyData);
                _isPopulated = true;
            }
            else
            {
                Debug.LogError("[UI_PartySelectView] Cannot populate immediately because _lobbyData is NULL!");
            }
        }
    }

    public override async Cysharp.Threading.Tasks.UniTask OpenAsync()
    {
#if UNITY_EDITOR
        // 에디터에서 인스펙터 할당을 깜빡했거나 런타임에 동적으로 열릴 때를 대비한 자동 할당 (편의성)
        if (_lobbyData == null)
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:PartyLobbyDataSO");
            if (guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                _lobbyData = UnityEditor.AssetDatabase.LoadAssetAtPath<PartyLobbyDataSO>(path);
                Debug.Log("[UI_PartySelectView] Automatically loaded PartyLobbyDataSO via AssetDatabase in Editor.");
            }
        }
#endif

        Debug.Log($"[UI_PartySelectView] OpenAsync called! _isPopulated: {_isPopulated}, _lobbyData is null?: {_lobbyData == null}");
        
        // 처음 열 때 아직 세팅(생성)이 안 되어 있다면 생성합니다.
        if (!_isPopulated)
        {
            if (_lobbyData != null)
            {
                Debug.Log($"[UI_PartySelectView] Populating list with {_lobbyData.partyList.Count} items.");
                PopulateList(_lobbyData);
                _isPopulated = true;
            }
            else
            {
                Debug.LogError("[UI_PartySelectView] _lobbyData is NULL! Injection failed or SO is missing in Inspector.");
            }
        }
        
        await base.OpenAsync();
    }

    private void PopulateList(PartyLobbyDataSO lobbyData)
    {
        ClearList();

        if (lobbyData == null || lobbyData.partyList == null) return;

        foreach (var partyData in lobbyData.partyList)
        {
            var item = Instantiate(partyItemPrefab, contentTransform);
            item.Init(partyData, HandlePartySelected);
            _spawnedItems.Add(item);
        }
    }

    private void HandlePartySelected(PartyDataSO selectedData)
    {
        foreach (var item in _spawnedItems)
        {
            item.SetSelected(item.GetPartyData() == selectedData);
        }

        startGameButton.interactable = true;
        _onPartySelected?.Invoke(selectedData);
    }

    private void ClearList()
    {
        foreach (var item in _spawnedItems)
        {
            if (item != null) Destroy(item.gameObject);
        }
        _spawnedItems.Clear();
    }
}
