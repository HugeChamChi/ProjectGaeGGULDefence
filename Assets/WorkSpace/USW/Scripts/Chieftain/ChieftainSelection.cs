using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>Resolves the party's chieftain selection and connects its independent active skill.</summary>
public sealed class ChieftainSelection : MonoBehaviour
{
    [SerializeField] private ChieftainData[] chieftainDataList;
    [FormerlySerializedAs("_useTestSpawn")]
    [SerializeField] private bool _useTestSelection = true;
    [SerializeField] private bool _useAuthoredSelection;
    [SerializeField] private ChieftainData _testSelection;
    private GameManager _gameManager;
    private bool _initialized;

    /// <summary>Authored choices available to the in-game debug selector.</summary>
    public System.Collections.Generic.IEnumerable<ChieftainData> AvailableSelections =>
        chieftainDataList ?? Array.Empty<ChieftainData>();

    /// <summary>The selected independent active skill displayed by the HUD.</summary>
    public IChiefActiveSkill ActiveSkill { get; private set; }
    /// <summary>The current selection, without unit, cell or tier state.</summary>
    public ChieftainData SelectedChieftain { get; private set; }
    /// <summary>The selected Alphan configuration, or null for an unconfigured selection.</summary>
    public AlphanSkillData SelectedAlphanSkill => SelectedChieftain?.AlphanSkill;
    /// <summary>Selection portrait used if the skill has no icon.</summary>
    public Sprite SelectedAlphanIcon => SelectedChieftain?.Icon;
    /// <summary>Notifies the HUD when the active skill becomes available or is cleared.</summary>
    public event Action<IChiefActiveSkill> OnActiveSkillChanged;
    /// <summary>Configures or cancels the scene-owned Alphan runtime.</summary>
    public event Action<AlphanSkillData, Sprite> OnAlphanSkillSelected;

    /// <summary>Receives the scene's game start event source.</summary>
    [VContainer.Inject]
    public void Construct(GameManager gameManager) => _gameManager = gameManager;

    /// <summary>Connects the runtime skill to the HUD.</summary>
    public void SetActiveSkill(IChiefActiveSkill skill)
    {
        ActiveSkill = skill;
        OnActiveSkillChanged?.Invoke(skill);
    }

    /// <summary>Uses the same selection precedence for pool initialization and game start.</summary>
    public ChieftainData ResolveInitialSelection()
    {
        if (!_useAuthoredSelection && GlobalData.SelectedParty?.Chieftain != null)
            return GlobalData.SelectedParty.Chieftain;
        if (_useTestSelection && _testSelection != null) return _testSelection;
        int id = Player.Chief.SelectedChiefId;
        if (id == 0 && _useTestSelection && chieftainDataList != null && chieftainDataList.Length > 0)
            return chieftainDataList[0];
        return FindById(id);
    }

    /// <summary>Returns only the selected chieftain's pool; never mixes other decks.</summary>
    public LevelUpPoolData GetSelectedLevelUpPool() => ResolveInitialSelection()?.LevelUpPool;

    /// <summary>Subscribes once, preserving GameInitializer's existing initialization order.</summary>
    public void Init()
    {
        if (_initialized) return;
        _initialized = true;
        if (_gameManager != null) _gameManager.OnGameStart += HandleGameStart;
        else HandleGameStart();
    }

    private void HandleGameStart() => Select(ResolveInitialSelection());

    /// <summary>Changes the active selection without spawning a unit or altering the run's fixed card pool.</summary>
    public void Select(ChieftainData data)
    {
        SelectedChieftain = data;
        SetActiveSkill(null);
        OnAlphanSkillSelected?.Invoke(data?.AlphanSkill, data?.Icon);
    }

    /// <summary>Resolves an existing saved/debug ID without rewriting player save data.</summary>
    public void SelectById(int id) => Select(FindById(id));

    private ChieftainData FindById(int id) => id == 0 || chieftainDataList == null ? null :
        Array.Find(chieftainDataList, data => data != null && data.ChieftainId == id);

    private void OnDestroy()
    {
        if (_initialized && _gameManager != null) _gameManager.OnGameStart -= HandleGameStart;
        Select(null);
        OnActiveSkillChanged = null;
        OnAlphanSkillSelected = null;
    }
}
