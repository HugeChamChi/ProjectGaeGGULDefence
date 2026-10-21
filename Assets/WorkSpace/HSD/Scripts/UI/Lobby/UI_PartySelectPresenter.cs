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
    private readonly EndlessBalanceService _balance;
    
    private PartyDataSO _selectedParty;

    [Inject]
    public UI_PartySelectPresenter(SceneChangeManager sceneChangeManager,
        System.Collections.Generic.IEnumerable<StaminaInsufficientPopup> staminaPopups = null,
        GaeGGUL.Tutorial.IngameTutorialProgress tutorial = null,
        EndlessBalanceService balance = null)
    {
        _sceneChangeManager = sceneChangeManager;
        _tutorial = tutorial;
        _balance = balance;
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

    public void StartDefaultGame(PartyLobbyDataSO lobbyData)
    {
        if (_isProcessing) return;
        _selectedParty = GlobalData.SelectedParty;
        if (_selectedParty == null && lobbyData != null && lobbyData.partyList.Count > 0)
            _selectedParty = lobbyData.partyList[0];
        OnStartGameClicked();
    }

    private void OnStartGameClicked()
    {
        if (_isProcessing || _selectedParty == null) return;
        _view.ChooseMode(test =>
        {
            if (!_isProcessing) StartSelectedAsync(test).Forget();
        });
    }

    private async UniTaskVoid StartSelectedAsync(bool test)
    {
        _isProcessing = true;
        try
        {
            if (_sceneChangeManager == null) { _staminaPopup?.ShowMessage("씬 이동을 준비하지 못했습니다."); return; }
            GlobalData.SelectedParty = _selectedParty;
            if (!test && _tutorial != null && !_tutorial.IsCompleted)
            { await _sceneChangeManager.TransitionToSceneAsync("TutorialScene"); return; }
            var config = RM.Load<StaminaConfig>("Data/StaminaConfig");
            if (config == null || config.stageEntryCost <= 0) { _staminaPopup?.ShowMessage("출격 비용 설정을 확인해 주세요."); return; }
            try
            {
                if (_balance == null) throw new System.InvalidOperationException("전투 설정 서비스를 준비하지 못했습니다.");
                var mode = _view.GetBattleMode(test);
                if (mode == null) throw new System.InvalidOperationException("전투 모드가 연결되지 않았습니다.");
                await _balance.PrepareEntryAsync(_view.GetCancellationTokenOnDestroy(), mode);
            }
            catch (System.OperationCanceledException) { return; }
            catch (System.Exception error)
            {
                Debug.LogWarning("전투 설정 확인 실패: " + error.Message);
                _staminaPopup?.ShowMessage("전투 설정을 불러오지 못했습니다. 잠시 후 다시 시도해 주세요.");
                return;
            }
            var player = Player.PlayerData;
            if (!await player.ReserveEntryAsync(config.stageEntryCost))
            {
                if (player.IsReady && string.IsNullOrEmpty(player.Data.PendingEntryId))
                    _staminaPopup?.Show(player.Data.Stamina, config.stageEntryCost);
                else _staminaPopup?.ShowMessage(player.SaveStatus + "\n저장 확인 후 출격할 수 있습니다.");
                return;
            }
            await _sceneChangeManager.TransitionToSceneAsync(test ? "HardIngameScene" : "IngameScene");
        }
        catch (System.Exception error)
        {
            Debug.LogWarning("출격 실패: " + error.Message);
            _staminaPopup?.ShowMessage("출격을 완료하지 못했습니다. 예약된 출격은 다시 시도할 때 사용됩니다.");
        }
        finally { _isProcessing = false; }
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
