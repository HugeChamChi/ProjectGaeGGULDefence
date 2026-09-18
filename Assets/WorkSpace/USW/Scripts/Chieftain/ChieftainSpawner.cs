using UnityEngine;

/// <summary>
/// 인게임 진입 시 선택된 족장을 그리드 중앙에 자동 배치
///
/// 데이터 흐름:
///   GlobalData.SelectedParty.chieftainData (1순위) → 테스트 유닛(2순위) → 기존 레거시 ID(3순위)
/// </summary>
public class ChieftainSpawner : MonoBehaviour
{
    private GridManager _gridManager;
    private UnitFactory _unitFactory;
    private UnitSpawner _unitSpawner;
    private GameManager _gameManager;

    [VContainer.Inject]
    public void Construct(GridManager gridManager, UnitFactory unitFactory, UnitSpawner unitSpawner, GameManager gameManager)
    {
        _gridManager = gridManager;
        _unitFactory = unitFactory;
        _unitSpawner = unitSpawner;
        _gameManager = gameManager;
    }

    [SerializeField] private ChieftainData[] chieftainDataList;

    [Header("테스트 소환 (아웃게임 미구현 시)")]
    [SerializeField] private bool     _useTestSpawn  = true;
    [Tooltip("테스트로 소환할 족장 UnitData SO — 비워두면 chieftainDataList 첫 번째 사용")]
    [SerializeField] private UnitData _testUnitData;

    /// <summary>현재 배치된 족장 유닛 — 족장 전용 버프 적용에 사용</summary>
    public UnitBase ChieftainUnit { get; private set; }

    public event System.Action<ChiefUnit> OnChieftainSpawned;
    /// <summary>현재 버튼이 표시할 액티브. 알팡은 ChieftainUnit 없이 존재한다.</summary>
    public IChiefActiveSkill ActiveSkill { get; private set; }
    /// <summary>선택된 독립 알팡 스킬 설정.</summary>
    public AlphanSkillData SelectedAlphanSkill { get; private set; }
    /// <summary>선택 데이터의 아이콘 폴백.</summary>
    public Sprite SelectedAlphanIcon { get; private set; }
    public event System.Action<IChiefActiveSkill> OnActiveSkillChanged;
    public event System.Action<AlphanSkillData, Sprite> OnAlphanSkillSelected;

    /// <summary>스킬 런타임/유닛 어댑터가 UI에 현재 액티브를 알린다.</summary>
    public void SetActiveSkill(IChiefActiveSkill skill)
    {
        ActiveSkill=skill;
        OnActiveSkillChanged?.Invoke(skill);
    }
    /// <summary>족장 선택을 독립 스킬로 전환한다. 그리드와 UnitFactory를 호출하지 않는다.</summary>
    public void SelectAlphanSkill(AlphanSkillData data, Sprite fallbackIcon = null)
    {
        ClearCurrentChieftain();
        SelectedAlphanSkill=data;SelectedAlphanIcon=fallbackIcon;
        OnAlphanSkillSelected?.Invoke(data,fallbackIcon);
    }
    private void ClearCurrentChieftain()
    {
        SelectedAlphanSkill=null;SelectedAlphanIcon=null;
        OnAlphanSkillSelected?.Invoke(null,null);
        SetActiveSkill(null);
        if (ChieftainUnit != null)
        {
            var cell=ChieftainUnit.currentCell;
            if (cell != null) cell.RemoveUnit();
            Destroy(ChieftainUnit.gameObject);
            ChieftainUnit=null;
        }
        OnChieftainSpawned?.Invoke(null);
    }

    /// <summary>소환과 동일한 우선순위로 시작 시 족장 풀을 해석한다. 누락 시 다른 족장 풀을 섞지 않는다.</summary>
    public LevelUpPoolData GetSelectedLevelUpPool()
    {
        if (GlobalData.SelectedParty?.chieftainData != null)
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
        // 1순위: 로비에서 선택한 파티 데이터에 족장 데이터가 있으면 스폰
        if (GlobalData.SelectedParty != null && GlobalData.SelectedParty.chieftainData != null)
        {
            SpawnChieftainByUnitData(GlobalData.SelectedParty.chieftainData);
            return;
        }

        // 2순위: 에디터 테스트용 할당 스폰
        if (_useTestSpawn && _testUnitData != null)
        {
            SpawnChieftainByUnitData(_testUnitData);
            return;
        }

        // 3순위: (레거시) Player.Chief.SelectedChiefId 기반 소환
        int selectedId = Player.Chief.SelectedChiefId;

        if (selectedId == 0 && _useTestSpawn)
            selectedId = GetTestChieftainId();

        if (selectedId == 0)
        {
            Debug.Log("ChieftainSpawner: 선택된 족장 없음 (SelectedChiefId = 0)");
            return;
        }

        SpawnChieftainById(selectedId);
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

    public void ChangeChieftain(int selectedId)
    {
        ClearCurrentChieftain();
        SpawnChieftainById(selectedId);
    }

    private void SpawnChieftainById(int selectedId)
    {
        var data = System.Array.Find(
            chieftainDataList,
            d => d != null && d.chieftainId == selectedId);

        if (data == null)
        {
            Debug.LogWarning($"ChieftainSpawner: chieftainId={selectedId} 에 맞는 ChieftainData 없음");
            return;
        }

        PlaceChieftain(data);
    }

    private void PlaceChieftain(ChieftainData data)
    {
        if (data.AlphanSkill != null) { SelectAlphanSkill(data.AlphanSkill); return; }
        SpawnToCenter(data.unitType);
    }

    private void SpawnChieftainByUnitData(UnitData unitData)
    {
        if (unitData.AlphanSkill != null) { SelectAlphanSkill(unitData.AlphanSkill,unitData.icon); return; }
        ClearCurrentChieftain();
        var cell = _gridManager.GetCenterCell();
        if (cell == null || !cell.IsAvailable)
        {
            Debug.LogWarning("ChieftainSpawner: 중앙 셀 배치 불가");
            return;
        }

        var unit = _unitFactory.CreateUnitFromData(unitData, Tier.Chieftain);
        if (unit == null) return;

        ChieftainUnit = unit;
        _unitSpawner.PlaceUnitWithEffect(unit, cell);
        OnChieftainSpawned?.Invoke(unit as ChiefUnit);
        SetActiveSkill(unit is ChiefUnit chief ? new UnitChiefActiveSkill(chief) : null);
    }

    private void SpawnToCenter(int unitType)
    {
        ClearCurrentChieftain();
        var cell = _gridManager.GetCenterCell();
        if (cell == null || !cell.IsAvailable)
        {
            Debug.LogWarning("ChieftainSpawner: 중앙 셀 배치 불가");
            return;
        }

        var unit = _unitFactory.CreateUnit(unitType);
        if (unit == null) return;

        unit.currentTier = Tier.Chieftain;
        ChieftainUnit = unit;
        _unitSpawner.PlaceUnitWithEffect(unit, cell);
        OnChieftainSpawned?.Invoke(unit as ChiefUnit);
        SetActiveSkill(unit is ChiefUnit chief ? new UnitChiefActiveSkill(chief) : null);
    }
}
