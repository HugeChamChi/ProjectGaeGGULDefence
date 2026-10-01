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
        builder.RegisterInstance(new SceneComponentCollection(gameObject.scene));
        builder.RegisterEntryPoint<GaeGGUL.Tutorial.TutorialSceneBinding>();
        SceneComponentRegistration.RegisterOptional<Test_TutorialSystem>(builder, gameObject.scene);
        SceneComponentRegistration.RegisterOptional<GaeGGUL.Tutorial.TutorialActor>(builder, gameObject.scene);
        SceneComponentRegistration.RegisterOptional<StaminaInsufficientPopup>(builder, gameObject.scene);
        SceneComponentRegistration.RegisterOptional<BackendManager>(builder, gameObject.scene);
        SceneComponentRegistration.RegisterOptional<LobbyFeatureNavigation>(builder, gameObject.scene);
        // 명시적 할당을 우선하고, 없으면 이 씬의 비활성 View까지 등록한다.
        if (partySelectView != null) 
        {
            builder.RegisterComponent(partySelectView);
        }
        else 
        {
            SceneComponentRegistration.RegisterOptional<UI_PartySelectView>(builder, gameObject.scene);
        }
            
        if (lobbyData != null) 
            builder.RegisterInstance(lobbyData);

        // 유저님 아이디어 반영: View가 Presenter를 컨트롤하기 위해 일반 클래스로 등록합니다.
        builder.Register<UI_PartySelectPresenter>(Lifetime.Scoped);
        builder.RegisterEntryPoint<LobbyPresenter>();
    }
}
