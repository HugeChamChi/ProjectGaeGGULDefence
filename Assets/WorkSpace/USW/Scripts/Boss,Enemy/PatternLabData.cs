using UnityEngine;

/// <summary>외형과 독립적인 보스 패턴의 저작 수치. 런타임에는 변경하지 않는다.</summary>
[CreateAssetMenu(menuName = "Game/Boss/Pattern Mechanic")]
public sealed class PatternLabData : ScriptableObject
{
    /// <summary>기믹의 실제 판정 방식.</summary>
    public PatternLabMechanic Mechanic;
    /// <summary>플레이어에게 표시할 패턴 이름.</summary>
    public string DisplayName;
    /// <summary>피하거나 해제하는 방법.</summary>
    [TextArea] public string Instruction;
    /// <summary>첫 판정 전 준비 시간.</summary>
    [Min(.1f)] public float Windup = 3f;
    /// <summary>이동 벽·붉은 신호가 작동하는 시간.</summary>
    [Min(.1f)] public float ActiveDuration = 3f;
    /// <summary>판정/해제 결과를 보여줄 시간.</summary>
    [Min(.1f)] public float Recovery = .7f;
    /// <summary>적중한 유닛의 기절 시간.</summary>
    [Min(0)] public float StunDuration = 1.2f;
    /// <summary>셀 약화 효과의 유지 시간.</summary>
    [Min(.1f)] public float EffectDuration = 3f;
    /// <summary>기억 발판 실패 칸의 공격속도 감소율.</summary>
    [Range(0f,.9f)] public float SlowRate = .4f;
    /// <summary>사슬을 끊을 수 있는 두 유닛 사이 그리드 거리.</summary>
    [Min(1f)] public float TetherDistance = 3f;
    /// <summary>이동 폭탄이 따라다니는 정사각형의 셀 반경.</summary>
    [Min(1)] public int BlastRadius = 1;
    /// <summary>폭풍에서 안전한 연속 열 수.</summary>
    [Min(1)] public int SafeColumns = 3;
    /// <summary>기억해야 할 안전 발판 수.</summary>
    [Min(1)] public int MemorySafeCount = 8;
    /// <summary>유도 포격 조준 고정 시점 / 기억 발판을 보여주는 시간.</summary>
    [Min(.1f)] public float LockTime = 1.4f;
    /// <summary>포격 선의 폭. 한 칸 너비에 대한 비율.</summary>
    [Min(.1f)] public float BeamWidthCells = .65f;
    /// <summary>첫 판정 전 족장 스킬로 끊을 수 있는지 여부.</summary>
    public bool AllowChiefCounter;
    /// <summary>기믹을 구분하는 강조 색. 안전 표시는 공통 청록색이다.</summary>
    public Color Accent = new Color(1f,.2f,.25f);
    /// <summary>적중 시 사용하는 기존 기절 연출 설정.</summary>
    public StunVisualSettings StunVisual;
}
