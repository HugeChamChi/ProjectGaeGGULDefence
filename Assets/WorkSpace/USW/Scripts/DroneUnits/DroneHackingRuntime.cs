using UnityEngine;

/// <summary>DroneManager가 소유하는 씬 수명 해킹 자원과 보스 표시. 보스 변경 시 잔고/예약을 버린다.</summary>
public sealed class DroneHackingRuntime : MonoBehaviour
{
    private BossManager _bosses;
    private GameManager _game;
    private IDroneEffectReader _effects;
    private int _carry;
    /// <summary>사망한 보스로부터 다음 보스 한 번에 적용할 잔고.</summary>
    public int PendingCarry => _carry;
    private BossBase _target;
    private DroneHackingData _data;
    private HackingBossVisual _visual;
    /// <summary>현재 보스의 공유 잔고.</summary>
    public HackingStackLedger Ledger { get; } = new HackingStackLedger();
    /// <summary>마지막으로 확정한 소비량. HUD 소비 피드백에 사용한다.</summary>
    public int LastConsumed { get; private set; }
    /// <summary>소비 사건 번호.</summary>
    public int ConsumptionVersion { get; private set; }
    /// <summary>동일 프레임의 여러 기폭도 HUD가 합산할 수 있는 누적 소비량.</summary>
    public long TotalConsumed { get; private set; }
    /// <summary>현재 잔고가 귀속된 보스.</summary>
    public BossBase Target { get { Synchronize(); return _target; } }
    /// <summary>생성 직후 씬 주입 의존성을 연결한다.</summary>
    public void Initialize(BossManager bosses, GameManager game, IDroneEffectReader effects = null)
    { _bosses = bosses; _game = game; _effects = effects; }
    /// <summary>동일 저작 데이터를 사용하는 생산자/소비자를 연결한다.</summary>
    public void Configure(DroneHackingData data)
    {
        if (data == null || _data == data) return;
        _data = data;
        Ledger.Reset(data.Capacity);
        Synchronize();
    }
    private BossBase _residueTarget;
    private UnityEngine.Object _residueSource;
    private float _residueRemaining, _residueTimer, _residueInterval;
    private int _residueTicks;
    /// <summary>현재 보스에 남은 기폭 여운 피해(방어 적용 전). 9109 넹...</summary>
    public float ResidueRemaining => _residueTicks > 0 ? _residueRemaining : 0f;
    /// <summary>남은 여운 틱 수.</summary>
    public int ResidueTicksLeft => _residueTicks;
    /// <summary>남은 여운에 새 여운을 합산하고 틱 수와 간격을 처음부터 다시 시작한다. 보스당 여운은 하나다.</summary>
    public void AddResidue(BossBase target, float amount, float duration, int ticks, UnityEngine.Object source)
    {
        Synchronize();
        if (target == null || !ReferenceEquals(target, _target) || target.IsDead || amount <= 0f || duration <= 0f || ticks <= 0) return;
        if (!ReferenceEquals(_residueTarget, target)) ClearResidue();
        _residueTarget = target; _residueSource = source;
        _residueRemaining += amount; _residueTicks = ticks;
        _residueInterval = duration / ticks; _residueTimer = _residueInterval;
    }
    private void ClearResidue()
    {
        _residueTarget = null; _residueSource = null;
        _residueRemaining = _residueTimer = _residueInterval = 0f; _residueTicks = 0;
    }
    // 잠복 등 대상 불가 동안은 틱 시간을 멈춰 피해를 버리지 않는다. 피해는 틱마다 일반 피해 경로(방어 포함)로 넣는다.
    private void TickResidue(float deltaTime)
    {
        if (_residueTicks <= 0) return;
        if (_residueTarget == null || _residueTarget.IsDead || !ReferenceEquals(_residueTarget, _target)) { ClearResidue(); return; }
        if (deltaTime <= 0f || !_residueTarget.IsTargetable) return;
        _residueTimer -= deltaTime;
        while (_residueTimer <= 0f && _residueTicks > 0)
        {
            float tick = _residueRemaining / _residueTicks;
            _residueRemaining -= tick; _residueTicks--; _residueTimer += _residueInterval;
            var boss = _residueTarget;
            boss.TakeDamage((decimal)tick, boss.transform.position, BossDamageKind.Burn, _residueSource);
            if (_residueTarget == null || boss.IsDead) { ClearResidue(); return; }
        }
        if (_residueTicks == 0) ClearResidue();
    }
    private void Update()
    {
        Synchronize();
        TickResidue(Time.deltaTime);
    }
    private void Synchronize()
    {
        if (_game != null && _game.IsFinished) _carry = 0;
        if (_target != null && _target.IsDead) OnTargetDied();
        var current = _game != null && _game.IsFinished ? null : _bosses != null ? _bosses.CurrentBoss : null;
        if (current != null && current.IsDead) current = null;
        if (ReferenceEquals(current, _target)) return;
        ClearResidue();
        if (_target != null) _target.OnDeath -= OnTargetDied;
        _target = current;
        if (_target != null) _target.OnDeath += OnTargetDied;
        Ledger.Reset(_data != null ? _data.Capacity : 1);
        if (_visual != null) Destroy(_visual.gameObject);
        _visual = null;
        if (_target != null && _carry > 0)
        {
            Ledger.Add(_carry); _carry=0; RefreshVisual();
        }
    }
    private void OnTargetDied()
    {
        if (_target == null || !_target.IsDead) return;
        float carry = _effects?.HackingCarryoverRatio ?? 0f;
        _carry = carry > 0f && (_game == null || !_game.IsFinished)
            ? Mathf.FloorToInt((Ledger.Available + Ledger.Reserved) * carry) : 0;
        if (_target != null) _target.OnDeath -= OnTargetDied;
        _target = null;
        Ledger.Reset(_data != null ? _data.Capacity : 1);
        if (_visual != null) Destroy(_visual.gameObject);
        _visual = null;
    }
    private void OnDestroy() { if (_target != null) _target.OnDeath -= OnTargetDied; }
    private void RefreshVisual()
    {
        if (_target == null || _data == null) return;
        if (_visual == null && Ledger.Available + Ledger.Reserved > 0)
        {
            var go = new GameObject("HackingMarks");
            go.transform.SetParent(_target.transform, false);
            _visual = go.AddComponent<HackingBossVisual>();
            _visual.Initialize(_target, _data);
        }
        _visual?.SetStacks(Ledger.Available + Ledger.Reserved, Ledger.Capacity);
    }
    /// <summary>생산 효과는 원래 대상이 여전히 현재 보스일 때만 적용한다.</summary>
    public int Add(BossBase target, int amount)
    {
        if (target == null || Target != target) return 0;
        int added = Ledger.Add(amount);
        RefreshVisual();
        return added;
    }
    /// <summary>해킹 자폭 적중 위치·실제 수용량(용량 초과 시 0)·적중 직후 잔고. 표시 전용 알림이다.</summary>
    public event System.Action<Vector3, int, int> OnBombStacksAccepted;
    /// <summary>해킹 자폭 적중에서 스택을 생산하고 적중 표시를 알린다. 수치 처리는 Add와 같다.</summary>
    public int AddFromBomb(BossBase target, int amount, Vector3 impact)
    {
        int added = Add(target, amount);
        OnBombStacksAccepted?.Invoke(impact, added, Ledger.Available + Ledger.Reserved);
        return added;
    }
    /// <summary>스택0도 유효한 기본 스킬 요청이다.</summary>
    public HackingStackLedger.Reservation Reserve(BossBase target, int maximum)
    {
        if (target == null || Target != target) return null;
        return Ledger.Reserve(maximum);
    }
    /// <summary>접촉 시 한 번 소비하고 표식의 실제 소비분만 연쇄 기폭한다.</summary>
    public bool Commit(BossBase target, HackingStackLedger.Reservation reservation)
    {
        if (target == null || Target != target || !Ledger.Commit(reservation)) return false;
        LastConsumed = reservation.Amount;
        TotalConsumed += reservation.Amount;
        ConsumptionVersion++;
        _visual?.Detonate(Ledger.Available + Ledger.Reserved, Ledger.Capacity, reservation.Amount);
        return true;
    }
    /// <summary>접촉 전 취소는 동일 보스에게만 반환한다.</summary>
    public void Refund(BossBase target, HackingStackLedger.Reservation reservation)
    {
        if (target != null && Target == target && Ledger.Refund(reservation)) RefreshVisual();
    }
}
