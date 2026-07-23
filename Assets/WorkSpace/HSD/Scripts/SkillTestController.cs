using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
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
/// 싶으면 Tools/GGD_Editor/Skill Editor를 같이 쓰면 된다.
/// </summary>
public class SkillTestController : MonoBehaviour
{
    [SerializeField] private UnitData testUnitData;
    [Tooltip("씬 초기화(오디오 프리로드 등)를 기다리는 시간(초). 부족하면 늘리세요.")]
    [SerializeField] private float initialDelaySeconds = 2f;
    [Tooltip("원래 소환 버튼이 있던 자리에 만든 실제 UI 버튼. 누르면 테스트 유닛의 스킬이 즉시 발동한다.")]
    [SerializeField] private Button skillExecuteButton;

    private UnitBase _placedUnit;

    private void Awake()
    {
        if (skillExecuteButton != null)
            skillExecuteButton.onClick.AddListener(FireSkillNow);
    }

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

        // 보스가 죽어 웨이브가 넘어가면 테스트 흐름이 끊기므로, 이 씬 전용으로 보스를
        // 무적으로 만든다(체력바는 정상 표시되지만 TakeDamage가 무시됨). 보스 스폰이
        // 몇 프레임 늦을 수 있어 잠깐 폴링한다.
        BossBase boss = null;
        for (int i = 0; i < 60 && boss == null; i++)
        {
            boss = FindFirstObjectByType<BossBase>();
            if (boss == null) yield return null;
        }
        if (boss != null)
        {
            boss.Invincible = true;
            Debug.Log("[SkillTestController] 보스 무적 설정 완료.");
        }
        else
        {
            Debug.LogWarning("[SkillTestController] 보스를 찾지 못해 무적 설정을 건너뜁니다.");
        }

        // 제한시간이 다 되면 GameManager가 GameState.Lose로 넘겨버려 테스트가 끊긴다.
        // 이 씬은 스킬 테스트만 하면 되므로 타이머를 멈춰서 사실상 무한으로 만든다.
        var timerController = FindFirstObjectByType<TimerController>();
        if (timerController != null)
        {
            timerController.StopTimer();
            Debug.Log("[SkillTestController] 제한시간 정지(무한) 설정 완료.");
        }

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

    /// <summary>스킬실행 버튼(디버그 오버레이 버튼 포함) 공용 핸들러. TestUnit은 CanAutoSkill이
    /// 꺼져있어 쿨타임 기반 자동 발동이 없으므로, 이 호출이 스킬을 내보내는 유일한 경로다.</summary>
    private void FireSkillNow()
    {
        var combat = _placedUnit != null ? _placedUnit.GetComponent<UnitCombatComponent>() : null;
        if (combat != null) combat.TriggerSkillManually();
        else Debug.LogWarning("[SkillTestController] 배치된 테스트 유닛이 없습니다.");
    }

#if UNITY_EDITOR
    private Vector2 _scroll;
    private bool _listExpanded;
    private List<SkillData> _allSkills;

    private GUIStyle _labelStyle;
    private GUIStyle _buttonStyle;
    private GUIStyle _scrollbarTrackStyle;
    private GUIStyle _scrollbarThumbStyle;

    private static Texture2D MakeSolidTexture(Color color)
    {
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, color);
        tex.Apply();
        return tex;
    }

    private void EnsureStyles()
    {
        if (_labelStyle != null) return;
        _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 60 };
        _buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 60 };

        // 기본 스크롤바가 스킬 목록이 길어졌을 때 너무 얇고 흐려서 잘 안 보이길래, 두껍고 흰색인
        // 커스텀 스타일을 만들어서 스크롤 중엔 GUI.skin의 스크롤바 스타일을 이걸로 잠깐 바꿔치기한다
        // (BeginScrollView의 스타일 인자는 트랙만 받고 썸(thumb)은 항상 GUI.skin에서 찾기 때문).
        _scrollbarTrackStyle = new GUIStyle(GUI.skin.verticalScrollbar) { fixedWidth = 40 };
        _scrollbarTrackStyle.normal.background = MakeSolidTexture(new Color(0.2f, 0.2f, 0.2f));
        _scrollbarThumbStyle = new GUIStyle(GUI.skin.verticalScrollbarThumb) { fixedWidth = 40 };
        _scrollbarThumbStyle.normal.background = MakeSolidTexture(Color.white);
    }

    private void OnGUI()
    {
        EnsureStyles();

        GUILayout.BeginArea(new Rect(10, 10, 1000, 1400), GUI.skin.box);
        GUILayout.Label("Skill Test", _labelStyle);

        if (testUnitData == null)
        {
            GUILayout.Label("testUnitData 미설정", _labelStyle);
            GUILayout.EndArea();
            return;
        }

        GUILayout.Label($"현재 스킬: {(testUnitData.skillData != null ? testUnitData.skillData.name : "(없음)")}", _labelStyle);

        if (GUILayout.Button("스킬 즉시 발동", _buttonStyle, GUILayout.Height(140)))
        {
            FireSkillNow();
        }

        GUILayout.Space(8);
        if (GUILayout.Button(_listExpanded ? "SkillData 목록 닫기" : "SkillData 목록 열기", _buttonStyle, GUILayout.Height(140)))
        {
            _listExpanded = !_listExpanded;
            if (_listExpanded) RefreshList();
        }

        if (_listExpanded)
        {
            var originalTrack = GUI.skin.verticalScrollbar;
            var originalThumb = GUI.skin.verticalScrollbarThumb;
            GUI.skin.verticalScrollbar = _scrollbarTrackStyle;
            GUI.skin.verticalScrollbarThumb = _scrollbarThumbStyle;

            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(1000));
            foreach (var skill in _allSkills)
            {
                if (skill == null) continue;
                if (GUILayout.Button($"{skill.name}  ({skill.skillName})", _buttonStyle, GUILayout.Height(140)))
                {
                    testUnitData.skillData = skill;
                    Debug.Log($"[SkillTestController] '{skill.name}' 적용됨.");
                }
            }
            GUILayout.EndScrollView();

            GUI.skin.verticalScrollbar = originalTrack;
            GUI.skin.verticalScrollbarThumb = originalThumb;
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
