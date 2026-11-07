using System;

/// <summary>보스 한 생명 주기의 스택 수지. 확보분은 상한에 포함하며 취소 시 한 번만 반환한다.</summary>
public sealed class HackingStackLedger
{
    /// <summary>취소 또는 확정 가능한 단일 소비 요청.</summary>
    public sealed class Reservation
    {
        internal int Epoch;
        internal HackingStackLedger Owner;
        internal bool Closed;
        /// <summary>이번 공격이 확보한 스택 수.</summary>
        public int Amount { get; internal set; }
        /// <summary>확보한 물리칸 중 과열 스택 수.</summary>
        public int OverheatedAmount { get; internal set; }
        /// <summary>확보 당시의 피해 가중치. 카드 제거/새 생산으로 변하지 않는다.</summary>
        public float DamageAmount { get; internal set; }
    }

    private int _epoch;
    /// <summary>풀에서 재사용한 동일 보스도 이전 발사의 대상으로 취급하지 않는다.</summary>
    public int Generation => _epoch;
    /// <summary>지금 다른 공격이 사용할 수 있는 스택.</summary>
    public int Available { get; private set; }
    /// <summary>진행 중 공격에 확보된 스택.</summary>
    public int Reserved { get; private set; }
    /// <summary>즉시 소비 가능한 과열 스택 수. Available에 포함된다.</summary>
    public int Overheated { get; private set; }
    /// <summary>진행 중 공격에 확보된 과열 수. Reserved에 포함된다.</summary>
    public int ReservedOverheated { get; private set; }
    private float _overheatWeight = 1f;
    /// <summary>저장된 과열의 피해 가중치. 제거/이월 시에도 기존 스택은 보존한다.</summary>
    public float OverheatedDamageWeight => _overheatWeight;
    /// <summary>확보분을 포함한 최대 보관량.</summary>
    public int Capacity { get; private set; }
    /// <summary>새 보스로 넘어가며 이전 요청을 무효화한다.</summary>
    public void Reset(int capacity)
    {
        Capacity = Math.Max(1, capacity);
        Available = Reserved = Overheated = ReservedOverheated = 0;
        _overheatWeight = 1f;
        _epoch++;
    }
    /// <summary>초과를 버리고 실제 들어온 양을 반환한다.</summary>
    public void Resize(int capacity)
    {
        Capacity = Math.Max(1, capacity);
        Available = Math.Min(Available, Math.Max(0, Capacity - Reserved));
        Overheated = Math.Min(Overheated, Available);
    }
    /// <summary>예약되지 않은 일반1개를 과열1개로 바꾸며 총 점유는 유지한다.</summary>
    public bool TryOverheat(float damageWeight)
    {
        if (!(damageWeight >= 1f) || float.IsInfinity(damageWeight)) throw new ArgumentOutOfRangeException(nameof(damageWeight));
        if (Available <= Overheated) return false;
        Overheated++;
        _overheatWeight = damageWeight;
        return true;
    }
    /// <summary>이월된 과열을 일반칸으로 바꾸지 않고 복원한다.</summary>
    public int AddOverheated(int amount, float damageWeight)
    {
        int added = Add(amount);
        for (int i = 0; i < added; i++) TryOverheat(damageWeight);
        return added;
    }
    public int Add(int amount)
    {
        int accepted = Math.Min(Math.Max(0, amount), Math.Max(0, Capacity - Available - Reserved));
        Available += accepted;
        return accepted;
    }
    /// <summary>부족하면 남은 만큼만 확보하며 스택0 요청도 허용한다.</summary>
    public Reservation Reserve(int maximum)
    {
        int amount = Math.Min(Available, Math.Max(0, maximum));
        int overheated = Math.Min(Overheated, amount);
        Available -= amount;
        Reserved += amount;
        Overheated -= overheated;
        ReservedOverheated += overheated;
        return new Reservation { Owner = this, Epoch = _epoch, Amount = amount, OverheatedAmount = overheated,
            DamageAmount = amount + overheated * (_overheatWeight - 1f) };
    }
    /// <summary>현재 보스의 미확정 요청을 한 번만 소비한다.</summary>
    public bool Commit(Reservation reservation) => Close(reservation, false);
    /// <summary>현재 보스의 미확정 요청을 한 번만 환불한다.</summary>
    public bool Refund(Reservation reservation) => Close(reservation, true);
    private bool Close(Reservation reservation, bool refund)
    {
        if (reservation == null || reservation.Owner != this || reservation.Closed || reservation.Epoch != _epoch) return false;
        reservation.Closed = true;
        Reserved -= reservation.Amount;
        ReservedOverheated -= reservation.OverheatedAmount;
        if (refund)
        {
            int returned = Math.Min(reservation.Amount, Math.Max(0, Capacity - Reserved - Available));
            Available += returned;
            Overheated += Math.Min(returned, reservation.OverheatedAmount);
        }
        return true;
    }
}
