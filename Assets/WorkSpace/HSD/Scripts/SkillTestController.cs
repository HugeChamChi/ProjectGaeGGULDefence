using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// SkillTestScene 전용 부트스트랩 + 디버그 오버레이.
///
/// 이 씬은 IngameScene을 복제한 것이라 GameInitializer 등 기존 초기화가 그대로 실행된다 — 그게
/// 끝나길 잠깐 기다렸다가 게임을 시작(GameManager.OnStartButtonPressed, 곧 보스가 뜬다)하고
/// 테스트 유닛 1기를 그리드에 배치한다.
///
/// Play 모드에서 화면 왼쪽 위 오버레이로 원하는 SkillData를 테스트 유닛에 바로 적용하고,
/// 쿨타임과 무관하게 스킬을 즉시 발동시켜볼 수 있다. SkillData 자체의 세부 수치를 바꾸고
/// 싶으면 Tools/Skill Editor를 같이 쓰면 된다.
/// </summary>
public class SkillTestController : MonoBehaviour
{
    [SerializeField] private UnitData testUnitData;
    [Tooltip("씬 초기화(오디오 프리로드 등)를 기다리는 시간(초). 부족하면 늘리세요.")]
    [SerializeField] private float initialDelaySeconds = 2f;

    private UnitBase _placedUnit;

    private IEnumerator Start()
    {
        yield return new WaitForSeconds(initialDelaySeconds);

        var gameManager = FindFirstObjectByType<GameManager>();
        // 씬에 UnitFactory 타입 컴포넌트가 2개(일반 유닛용/드론용) 있어서 FindFirstObjectByType으로는
        // 어느 쪽이 잡힐지 보장이 안 된다 — GameInitializer가 실제로 Init()해주는 "UnitFactory"
        // 이름의 오브젝트를 명시적으로 찾는다(드론 전용 "DronUnitFactory"는 Init 대상이 아니라
        // _deps가 계속 null이라, 잘못 잡으면 유닛 생성 시 NullReferenceException이 난다).
        var unitFactory = GameObject.Find("UnitFactory")?.GetComponent<UnitFactory>();
        var unitSpawner = FindFirstObjectByType<UnitSpawner>();
        var gridManager = FindFirstObjectByType<GridManager>();

        if (gameManager == null || unitFactory == null || unitSpawner == null || gridManager == null)
        {
            Debug.LogError("[SkillTestController] 필수 매니저를 찾지 못했습니다.");
            yield break;
        }

        gameManager.OnStartButtonPressed();

        if (testUnitData == null)
        {
            Debug.LogWarning("[SkillTestController] testUnitData가 비어있어 유닛을 배치하지 않습니다.");
            yield break;
        }

        var cells = gridManager.GetEmptyCells();
        if (cells.Count == 0)
        {
            Debug.LogError("[SkillTestController] 빈 셀이 없습니다.");
            yield break;
        }

        _placedUnit = unitFactory.CreateUnitFromData(testUnitData);
        if (_placedUnit == null)
        {
            Debug.LogError("[SkillTestController] 테스트 유닛 생성 실패.");
            yield break;
        }

        unitSpawner.PlaceUnitWithEffect(_placedUnit, cells[0]);
        Debug.Log("[SkillTestController] 테스트 유닛 배치 완료.");
    }

#if UNITY_EDITOR
    private Vector2 _scroll;
    private bool _listExpanded;
    private List<SkillData> _allSkills;

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 320, 440), GUI.skin.box);
        GUILayout.Label("Skill Test");

        if (testUnitData == null)
        {
            GUILayout.Label("testUnitData 미설정");
            GUILayout.EndArea();
            return;
        }

        GUILayout.Label($"현재 스킬: {(testUnitData.skillData != null ? testUnitData.skillData.name : "(없음)")}");

        if (GUILayout.Button("스킬 즉시 발동 (쿨타임 무시)"))
        {
            var combat = _placedUnit != null ? _placedUnit.GetComponent<UnitCombatComponent>() : null;
            if (combat != null) combat.SkillTimer = 9999f;
            else Debug.LogWarning("[SkillTestController] 배치된 테스트 유닛이 없습니다.");
        }

        GUILayout.Space(8);
        if (GUILayout.Button(_listExpanded ? "SkillData 목록 닫기" : "SkillData 목록 열기"))
        {
            _listExpanded = !_listExpanded;
            if (_listExpanded) RefreshList();
        }

        if (_listExpanded)
        {
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(280));
            foreach (var skill in _allSkills)
            {
                if (skill == null) continue;
                if (GUILayout.Button($"{skill.name}  ({skill.skillName})"))
                {
                    testUnitData.skillData = skill;
                    Debug.Log($"[SkillTestController] '{skill.name}' 적용됨.");
                }
            }
            GUILayout.EndScrollView();
        }

        GUILayout.EndArea();
    }

    private void RefreshList()
    {
        _allSkills = new List<SkillData>();
        foreach (var guid in AssetDatabase.FindAssets("t:SkillData"))
        {
            var skill = AssetDatabase.LoadAssetAtPath<SkillData>(AssetDatabase.GUIDToAssetPath(guid));
            if (skill != null) _allSkills.Add(skill);
        }
    }
#endif
}
