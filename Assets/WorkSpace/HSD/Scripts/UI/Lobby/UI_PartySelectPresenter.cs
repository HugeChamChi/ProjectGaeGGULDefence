using UnityEngine;
using VContainer;
using VContainer.Unity;
using Cysharp.Threading.Tasks;

public class UI_PartySelectPresenter
{
    private UI_PartySelectView _view;
    private readonly SceneChangeManager _sceneChangeManager;
    private readonly StaminaInsufficientPopup _staminaPopup;
    private readonly GaeGGUL.Tutorial.IngameTutorialProgress _tutorial;
    
    private PartyDataSO _selectedParty;

    [Inject]
    public UI_PartySelectPresenter(SceneChangeManager sceneChangeManager,
        System.Collections.Generic.IEnumerable<StaminaInsufficientPopup> staminaPopups = null,
        GaeGGUL.Tutorial.IngameTutorialProgress tutorial = null)
    {
        _sceneChangeManager = sceneChangeManager;
        _tutorial = tutorial;
        if (staminaPopups != null)
            foreach (var popup in staminaPopups) { _staminaPopup = popup; break; }
    }

    // View가 자신이 켜질 때(Awake/Start) 이 Presenter를 직접 호출하여 통제합니다.
    public void Initialize(UI_PartySelectView view)
    {
        _view = view;
        if (_view != null)
        {
            _view.Init(OnPartySelected, OnStartGameClicked);
        }
    }

    private void OnPartySelected(PartyDataSO partyData)
    {
        _selectedParty = partyData;
        Debug.Log($"Party Selected: {_selectedParty.partyName}");
    }

    private bool _isProcessing = false;

    private void OnStartGameClicked()
    {
        if (_isProcessing) return;

        if (_selectedParty != null)
        {
            if (_tutorial != null && !_tutorial.IsCompleted)
            {
                _isProcessing = true;
                GlobalData.SelectedParty = _selectedParty;
                OpenFirstTutorialAsync().Forget();
                return;
            }
            var staminaConfig = RM.Load<StaminaConfig>("Data/StaminaConfig");
            int cost = staminaConfig != null ? staminaConfig.stageEntryCost : 5;

            if (!Player.PlayerData.UseStamina(cost))
            {
                Debug.LogWarning($"UI_PartySelectPresenter: 스태미나가 부족합니다. 현재: {Player.PlayerData.Data.Stamina} / 필요: {cost}");
                _staminaPopup?.Show(Player.PlayerData.Data.Stamina, cost);
                return;
            }

            _isProcessing = true;

            // 전역 데이터에 선택된 파티 저장
            GlobalData.SelectedParty = _selectedParty;
            Debug.Log($"Game Started with Party: {GlobalData.SelectedParty.partyName}");

            // IngameScene으로 씬 전환
            if (_sceneChangeManager != null)
            {
                _sceneChangeManager.TransitionToSceneAsync("IngameScene",
                    beforeLoad: async () =>
                    {
                        // 인게임 시작 전, 선택된 파티 정보 등 저장 필요한 데이터가 있다면 여기서 저장
                        await Player.UpdateDirtyDataAsync();
                    }
                ).Forget();
            }
            else
            {
                Debug.LogWarning("[UI_PartySelectPresenter] SceneChangeManager is not injected! Falling back to standard SceneManager with manual Fade.");
                
                // 의존성 주입이 안 된 순수 테스트 환경이라도 버튼이 동작하고 페이드 효과가 나오도록 수동 처리
                ManualFadeAndLoad().Forget();
            }
        }
        else
        {
            Debug.LogWarning("UI_PartySelectPresenter: 파티가 선택되지 않았습니다.");
        }
    }

    private async UniTask OpenFirstTutorialAsync()
    {
        try { await _sceneChangeManager.TransitionToSceneAsync("TutorialScene"); }
        catch (System.Exception error) { Debug.LogWarning("튜토리얼을 열지 못했습니다: " + error.Message); }
        finally { _isProcessing = false; }
    }

    private async UniTaskVoid ManualFadeAndLoad()
    {
        // 1. 임시 페이드 화면 강제 생성 (테스트 환경용)
        var fade = new DefaultFadeScreen();
        fade.Initialize();

        // 2. 화면 어두워짐 (Fade In)
        await fade.FadeInAsync(0.5f);

        // 3. 인게임 시작 전 데이터 저장 시뮬레이션
        await Player.UpdateDirtyDataAsync();

        // 4. 씬 전환
        await UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("IngameScene").ToUniTask();

        // 5. 화면 밝아짐 (Fade Out)
        await fade.FadeOutAsync(0.5f);
    }
}
