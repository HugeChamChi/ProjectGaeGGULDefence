using UnityEngine;

/// <summary>
/// 인게임 진입 시 선택된 족장의 독립 액티브 스킬을 설정한다.
///
/// 데이터 흐름:
///   GlobalData.SelectedParty.chieftainData (1순위) → 테스트 선택 데이터(2순위) → 저장된 족장 ID(3순위)
/// </summary>
public class ChieftainSpawner : MonoBehaviour
{
    private GameManager _gameManager;

    /// <summary>게임 시작 이벤트를 제공하는 씬 매니저를 주입한다.</summary>
    [VContainer.Inject]
    public void Construct(GameManager gameManager)
    {
        _gameManager = gameManager;
    }

    [SerializeField] private ChieftainData[] chieftainDataList;

    [Header("테스트 족장 스킬 선택")]
    [SerializeField] private bool     _useTestSpawn  = true;
    [SerializeField] private bool _useAuthoredSelection;
    [Tooltip("테스트 족장 스킬을 지정한 UnitData SO — 비워두면 chieftainDataList 첫 번째 사용")]
    [SerializeField] private UnitData _testUnitData;

    /// <summary>현재 족장 스킬 버튼이 표시할 독립 액티브.</summary>
    public IChiefActiveSkill ActiveSkill { get; private set; }
    /// <summary>선택된 독립 알팡 스킬 설정.</summary>
    public AlphanSkillData SelectedAlphanSkill { get; private set; }
    /// <summary>선택 데이터의 아이콘 폴백.</summary>
    public Sprite SelectedAlphanIcon { get; private set; }
    public event System.Action<IChiefActiveSkill> OnActiveSkillChanged;
    public event System.Action<AlphanSkillData, Sprite> OnAlphanSkillSelected;

    /// <summary>스킬 런타임이 UI에 현재 액티브를 알린다.</summary>
    public void SetActiveSkill(IChiefActiveSkill skill)
    {
        ActiveSkill=skill;
        OnActiveSkillChanged?.Invoke(skill);
    }
    /// <summary>족장 선택을 독립 스킬로 전환한다. 그리드와 UnitFactory를 호출하지 않는다.</summary>
    public void SelectAlphanSkill(AlphanSkillData data, Sprite fallbackIcon = null)
    {
        ClearCurrentSkill();
        SelectedAlphanSkill=data;SelectedAlphanIcon=fallbackIcon;
        OnAlphanSkillSelected?.Invoke(data,fallbackIcon);
    }
    private void ClearCurrentSkill()
    {
        SelectedAlphanSkill=null;SelectedAlphanIcon=null;
        OnAlphanSkillSelected?.Invoke(null,null);
        SetActiveSkill(null);
    }

    /// <summary>스킬 선택과 동일한 우선순위로 시작 시 족장 풀을 해석한다. 누락 시 다른 족장 풀을 섞지 않는다.</summary>
    public LevelUpPoolData GetSelectedLevelUpPool()
    {
        if (!_useAuthoredSelection && GlobalData.SelectedParty?.chieftainData != null)
            return GlobalData.SelectedParty.chieftainData.LevelUpPool;
        if (_useTestSpawn && _testUnitData != null)
            return _testUnitData.LevelUpPool;

        int selectedId = Player.Chief.SelectedChiefId;
        if (selectedId == 0 && _useTestSpawn) selectedId = GetTestChieftainId();
        if (selectedId == 0 || chieftainDataList == null) return null;
        return System.Array.Find(chieftainDataList,
            data => data != null && data.chieftainId == selectedId)?.LevelUpPool;
    }

    public void Init()
    {
        if (_gameManager != null)
        {
            _gameManager.OnGameStart += HandleGameStart;
        }
        else
        {
            HandleGameStart();
        }
    }

    private void HandleGameStart()
    {
        // 1순위: 로비에서 선택한 파티의 족장 스킬
        if (!_useAuthoredSelection && GlobalData.SelectedParty != null && GlobalData.SelectedParty.chieftainData != null)
        {
            SelectChieftainByUnitData(GlobalData.SelectedParty.chieftainData);
            return;
        }

        // 2순위: 에디터 테스트용 족장 스킬
        if (_useTestSpawn && _testUnitData != null)
        {
            SelectChieftainByUnitData(_testUnitData);
            return;
        }

        // 3순위: 저장된 족장 ID로 스킬 선택
        int selectedId = Player.Chief.SelectedChiefId;

        if (selectedId == 0 && _useTestSpawn)
            selectedId = GetTestChieftainId();

        if (selectedId == 0)
        {
            ClearCurrentSkill();
            Debug.Log("ChieftainSpawner: 선택된 족장 없음 (SelectedChiefId = 0)");
            return;
        }

        SelectChieftainById(selectedId);
    }

    private void OnDestroy()
    {
        if (_gameManager != null)
        {
            _gameManager.OnGameStart -= HandleGameStart;
        }
    }

    private int GetTestChieftainId()
    {
        if (chieftainDataList != null && chieftainDataList.Length > 0 && chieftainDataList[0] != null)
            return chieftainDataList[0].chieftainId;
        return 0;
    }

    /// <summary>지정 ID의 족장 스킬로 선택을 변경한다.</summary>
    public void ChangeChieftain(int selectedId)
    {
        SelectChieftainById(selectedId);
    }

    private void SelectChieftainById(int selectedId)
    {
        if (chieftainDataList == null)
        {
            SelectAlphanSkill(null);
            Debug.LogWarning("ChieftainSpawner: 족장 선택 데이터 미연결");
            return;
        }
        var data = System.Array.Find(
            chieftainDataList,
            d => d != null && d.chieftainId == selectedId);

        if (data == null)
        {
            SelectAlphanSkill(null);
            Debug.LogWarning($"ChieftainSpawner: chieftainId={selectedId} 에 맞는 ChieftainData 없음");
            return;
        }

        SelectAlphanSkill(data.AlphanSkill);
        if (data.AlphanSkill == null)
            Debug.LogWarning($"ChieftainSpawner: [{data.chieftainName}] 족장 스킬 미연결");
    }

    private void SelectChieftainByUnitData(UnitData unitData)
    {
        SelectAlphanSkill(unitData.AlphanSkill, unitData.icon);
        if (unitData.AlphanSkill == null)
            Debug.LogWarning($"ChieftainSpawner: [{unitData.unitName}] 족장 스킬 미연결");
    }
}
