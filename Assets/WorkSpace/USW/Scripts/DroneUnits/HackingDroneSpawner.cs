using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>해킹 시전의 취소·접촉·복귀 수명. SkillData 액션에서만 시전을 시작한다.</summary>
public abstract class HackingDroneSpawner : DroneSpawnerBase
{
    private CancellationTokenSource _castCts;
    private HackingCastVisual _castVisual;
    /// <summary>현재 시전 중에는 같은 개체의 중복 시전을 막는다.</summary>
    public bool IsHacking { get; private set; }
    /// <inheritdoc />
    public override bool CanUseSkill => unitData?.Hacking != null && unitData.skillData != null;
    /// <inheritdoc />
    public override bool CanAutoSkill => base.CanAutoSkill && !IsHacking;
    /// <summary>대상과 상태가 유효할 때 스킬 액션을 수락한다.</summary>
    protected bool TryGetHackingTarget(out DroneHackingRuntime runtime, out BossBase target)
    {
        runtime = null; target = null;
        if (IsHacking || unitData?.Hacking == null || _droneManager == null || !CanContinue() || IsHeld()) return false;
        runtime = _droneManager.Hacking;
        runtime.Configure(unitData.Hacking);
        target = runtime.Target;
        return target != null;
    }
    private bool CanContinue() => isActiveAndEnabled && currentCell != null;
    private bool IsHeld() => Combat == null || Combat.AttacksHeld || IsStunned || currentCell == null
        || currentCell.Model.IsSealed || currentCell.Model.IsAttackDisabled || currentCell.Model.TotemAttackDisabled;
    /// <summary>원래 대상에 한 번 실행하며 확보 요청은 접촉 전 취소 시 반환한다.</summary>
    protected void StartHacking(DroneHackingRuntime runtime, BossBase target, int produced,
        HackingStackLedger.Reservation reservation, int damage, bool critical)
    {
        CancelHacking();
        _castCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        IsHacking = true;
        ExecuteHackingAsync(runtime, target, unitData.Hacking, produced, reservation, damage, critical, _castCts)
            .Forget(error => { if (error is not OperationCanceledException) Debug.LogException(error); });
    }
    private async UniTask ExecuteHackingAsync(DroneHackingRuntime runtime, BossBase target, DroneHackingData data,
        int produced, HackingStackLedger.Reservation reservation, int damage, bool critical, CancellationTokenSource source)
    {
        var token = source.Token;
        int generation = runtime.Ledger.Generation;
        bool committed = false;
        try
        {
            if (_castVisual == null) _castVisual = gameObject.AddComponent<HackingCastVisual>();
            var spriteSource = OwnedDroneCount > 0 ? OwnedDrones[0].GetComponent<SpriteRenderer>() : null;
            _castVisual.Begin(spriteSource, target, data, reservation != null);
            float duration = reservation != null ? data.ContactSeconds : data.HackFlightSeconds;
            float elapsed = 0;
            while (elapsed < duration || IsHeld())
            {
                token.ThrowIfCancellationRequested();
                if (!CanContinue() || target == null || (runtime.Target != target || runtime.Ledger.Generation != generation)) return;
                if (!IsHeld())
                {
                    elapsed += Time.deltaTime;
                    _castVisual.Draw(duration > 0 ? Mathf.Clamp01(elapsed / duration) : 1f);
                }
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
            if ((runtime.Target != target || runtime.Ledger.Generation != generation) || target == null || target.IsDead || !CanContinue()) return;
            if (reservation == null) OnHackingProduced(produced, runtime.Add(target, produced));
            else if (runtime.Commit(target, reservation))
            {
                committed = true;
                target.TakeDamage(damage, target.transform.position, critical ? BossDamageKind.Critical : BossDamageKind.Normal);
            }
            _castVisual.Draw(1f);
            elapsed = 0;
            while (elapsed < data.RecoverySeconds)
            {
                token.ThrowIfCancellationRequested();
                if (!CanContinue() || target == null || (runtime.Target != target || runtime.Ledger.Generation != generation)) return;
                if (!IsHeld()) elapsed += Time.deltaTime;
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }
        finally
        {
            if (!committed && reservation != null && runtime != null) runtime.Refund(target, reservation);
            if (ReferenceEquals(_castCts, source))
            {
                _castVisual?.Clear();
                IsHacking = false;
                _castCts = null;
            }
            source.Dispose();
        }
    }
    /// <summary>생산자 자신이 만든 초과 생산분의 후속 효과.</summary>
    protected virtual void OnHackingProduced(int requested, int accepted) { }
    private void CancelHacking()
    {
        _castCts?.Cancel();
        _castCts?.Dispose();
        _castCts = null;
        _castVisual?.Clear();
        IsHacking = false;
    }
    /// <inheritdoc />
    protected override void OnUnitRemoved() { CancelHacking(); base.OnUnitRemoved(); }
    /// <inheritdoc />
    protected override void OnDisable() { CancelHacking(); base.OnDisable(); }
}
