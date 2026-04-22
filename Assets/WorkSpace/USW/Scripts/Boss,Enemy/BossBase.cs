using UnityEngine;
using System;

/// <summary>
/// 모든 보스의 기반 추상 클래스
///
/// 변경 사항:
///   - Patterns 프로퍼티 추가: 프리팹 Inspector에서 패턴 SO를 직접 설정
///     BossPatternController가 이 배열을 등록하여 사용
///
/// 책임:
///   - HP 보유 및 데미지 처리
///   - 이벤트 발행 (OnHpChanged, OnDamaged, OnDeath)
///   - 패턴 실행 인터페이스 제공
///   - 패턴 데이터 보유 (프리팹 단위로 설정)
///
/// 보스 종류 추가 방법:
///   BossNormal, BossShaman, BossDragon 등 이 클래스를 상속
///   ExecutePattern() 오버라이드로 패턴별 동작 구현
/// </summary>
public abstract class BossBase : MonoBehaviour
{
    // ── 패턴 데이터 (프리팹 Inspector에서 설정) ────────────────────
    [Header("보스 패턴")]
    [Tooltip("이 보스가 사용할 패턴 SO 목록 — 프리팹에 직접 설정")]
    [SerializeField] private BossPatternData[] _patterns;

    public BossPatternData[] Patterns => _patterns;

    // ── 이벤트 ─────────────────────────────────────────────────────
    /// <summary>(현재HP, 최대HP) — UIManager가 구독하여 HP바 갱신</summary>
    public event Action<int, int> OnHpChanged;

    /// <summary>데미지량 — ExpManager가 구독하여 경험치 추가</summary>
    public event Action<int>      OnDamaged;

    /// <summary>사망 — WaveManager가 구독하여 다음 웨이브 처리</summary>
    public event Action           OnDeath;

    /// <summary>보스 처치 시 전역 알림 — TotemBossKillStack 등에서 구독</summary>
    public static event Action OnAnyBossDied;

    // ── 상태 ────────────────────────────────────────────────────────
    private int _maxHp;
    private int _currentHp;

    public int  MaxHp     => _maxHp;
    public int  CurrentHp => _currentHp;
    public bool IsDead    => _currentHp <= 0;

    // ── 초기화 ──────────────────────────────────────────────────────
    /// <summary>BossManager.SpawnBosses() 내부에서 WaveData의 hp 주입</summary>
    public void Init(int hp)
    {
        _maxHp     = hp;
        _currentHp = hp;
    }

    // ── 데미지 처리 ─────────────────────────────────────────────────
    public void TakeDamage(int amount)
    {
        if (IsDead) return;

        _currentHp = Mathf.Max(0, _currentHp - amount);

        OnHpChanged?.Invoke(_currentHp, _maxHp);
        OnDamaged?.Invoke(amount);

        if (IsDead)
        {
            OnDeath?.Invoke();
            OnAnyBossDied?.Invoke();
        }
    }

    // ── 패턴 실행 (자식 구현) ───────────────────────────────────────
    /// <summary>BossPatternController가 타이밍에 맞춰 호출</summary>
    public abstract void ExecutePattern(BossPatternData patternData);

    // ── 정리 ────────────────────────────────────────────────────────
    /// <summary>보스 교체/웨이브 종료 시 이벤트 구독 해제용</summary>
    public void ClearListeners()
    {
        OnHpChanged = null;
        OnDamaged   = null;
        OnDeath     = null;
    }
}
