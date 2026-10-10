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
    }

    private int _epoch;
    /// <summary>풀에서 재사용한 동일 보스도 이전 발사의 대상으로 취급하지 않는다.</summary>
    public int Generation => _epoch;
    /// <summary>지금 다른 공격이 사용할 수 있는 스택.</summary>
    public int Available { get; private set; }
    /// <summary>진행 중 공격에 확보된 스택.</summary>
    public int Reserved { get; private set; }
    /// <summary>확보분을 포함한 최대 보관량.</summary>
    public int Capacity { get; private set; }
    /// <summary>새 보스로 넘어가며 이전 요청을 무효화한다.</summary>
    public void Reset(int capacity)
    {
        Capacity = Math.Max(1, capacity);
        Available = Reserved = 0;
        _epoch++;
    }
    /// <summary>초과를 버리고 실제 들어온 양을 반환한다.</summary>
    public int Add(int amount)
    {
        int accepted = Math.Min(Math.Max(0, amount), Capacity - Available - Reserved);
        Available += accepted;
        return accepted;
    }
    /// <summary>부족하면 남은 만큼만 확보하며 스택0 요청도 허용한다.</summary>
    public Reservation Reserve(int maximum)
    {
        int amount = Math.Min(Available, Math.Max(0, maximum));
        Available -= amount;
        Reserved += amount;
        return new Reservation { Owner = this, Epoch = _epoch, Amount = amount };
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
        if (refund) Available += reservation.Amount;
        return true;
    }
}
