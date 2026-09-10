using VContainer;
using VContainer.Unity;
using UnityEngine;

public class LobbyLifetimeScope : LifetimeScope
{
    [Header("Views")]
    [SerializeField] private UI_PartySelectView partySelectView;
    
    [Header("Data")]
    [SerializeField] private PartyLobbyDataSO lobbyData;

    protected override void Configure(IContainerBuilder builder)
    {
        // 팝업이 꺼져있을(Inactive) 경우 RegisterComponentInHierarchy가 찾지 못하므로 명시적 할당 우선
        if (partySelectView != null) 
        {
            builder.RegisterComponent(partySelectView);
        }
        else 
        {
            // 인스펙터 슬롯이 비어있다면, 강제로 꺼진(Inactive) 오브젝트까지 뒤져서 등록합니다.
            var view = FindFirstObjectByType<UI_PartySelectView>(FindObjectsInactive.Include);
            if (view != null) builder.RegisterComponent(view);
        }
            
        if (lobbyData != null) 
            builder.RegisterInstance(lobbyData);

        // 유저님 아이디어 반영: View가 Presenter를 컨트롤하기 위해 일반 클래스로 등록합니다.
        builder.Register<UI_PartySelectPresenter>(Lifetime.Scoped);
        builder.RegisterEntryPoint<LobbyPresenter>();
    }
}
