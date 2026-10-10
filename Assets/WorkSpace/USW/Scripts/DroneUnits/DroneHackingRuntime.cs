using UnityEngine;

/// <summary>DroneManager가 소유하는 씬 수명 해킹 자원과 보스 표시. 보스 변경 시 잔고/예약을 버린다.</summary>
public sealed class DroneHackingRuntime : MonoBehaviour
{
    private BossManager _bosses;
    private GameManager _game;
    private LevelUpManager _levelUp;
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
    public void Initialize(BossManager bosses, GameManager game, LevelUpManager levelUp = null) { _bosses = bosses; _game = game; _levelUp = levelUp; }
    /// <summary>동일 저작 데이터를 사용하는 생산자/소비자를 연결한다.</summary>
    public void Configure(DroneHackingData data)
    {
        if (data == null || _data == data) return;
        _data = data;
        Ledger.Reset(data.Capacity);
        Synchronize();
    }
    private void Update() => Synchronize();
    private void Synchronize()
    {
        if (_game != null && _game.IsFinished) _carry = 0;
        if (_target != null && _target.IsDead) OnTargetDied();
        var current = _game != null && _game.IsFinished ? null : _bosses != null ? _bosses.CurrentBoss : null;
        if (current != null && current.IsDead) current = null;
        if (ReferenceEquals(current, _target)) return;
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
        var carry = _levelUp?.DroneSelections.Get(DroneSelectionKind.HackingCarryover);
        _carry = carry != null && (_game == null || !_game.IsFinished)
            ? Mathf.FloorToInt((Ledger.Available + Ledger.Reserved) * carry.Value) : 0;
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
