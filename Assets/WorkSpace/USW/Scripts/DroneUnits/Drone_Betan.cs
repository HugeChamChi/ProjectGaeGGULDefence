using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using VContainer;
using UnityEngine;

/// <summary>
/// 드론 생산자.
/// 스킬마다 영구 드론 1마리 추가 (최대 maxDroneCount). 자폭 드론은 캡 없이 매 스킬 소환.
/// 유닛 제거 시 소유 드론 전체를 풀로 반환한다.
/// </summary>
public class Drone_Betan : DroneSpawnerBase
{
    [Header("Drone Producer Settings")]
    [SerializeField] private SelfDestructDrone selfDestructPrefab;
    [SerializeField] private int selfDestructCount = 1;
    [SerializeField] private BetangSpineVisual _skillVisual;
    [SerializeField] private SelfDestructDrone _skillSelfDestructPrefab;
    [Inject] private FieldPauseVisuals _fieldPause;
    [Inject] private GameManager _gameManager;
    private int _bombRequestLifetime;
    private int _pendingSkillBombs;
    private BossBase _pendingSkillBoss;
    private float _pendingSkillDamageMultiplier = 1f;
    private int _fleetVolleyIndex = -1;

    public enum SelfDestructOrigin { BasicSkill, PeriodicSelection, FleetSelection }
    public override float SkillCooldownMultiplier => base.SkillCooldownMultiplier
        * (LevelBonuses?.PeekCycle(UnitLevelBonusController.BasicSkillCycle).ChargeTimeMultiplier ?? 1f);

    protected override void OnSkillFull()
    {
        SpawnOneDrone();
        // A completed charge owns its ordinal even if the following animation is interrupted.
        float damageMultiplier = LevelBonuses?.CompleteCycle(UnitLevelBonusController.BasicSkillCycle).DamageMultiplier ?? 1f;
        if (_skillVisual != null && _skillVisual.HasSkillLaunchEvent)
        {
            _pendingSkillBombs = selfDestructCount;
            _pendingSkillBoss = SkillDebuffTarget;
            _pendingSkillDamageMultiplier = damageMultiplier;
        }
        else SpawnBombs(selfDestructCount, selfDestructPrefab, null, SelfDestructOrigin.BasicSkill, damageMultiplier);
        FlashOwnedDrones();
    }

    /// <summary>선택지 추가 자폭. 기본 스킬의 순번과 보너스를 사용하지 않는다.</summary>
    public virtual void SpawnSelfDestructDrones(int count)
        => SpawnBombs(count, selfDestructPrefab, null,
            _fleetVolleyIndex >= 0 ? SelfDestructOrigin.FleetSelection : SelfDestructOrigin.PeriodicSelection, 1f, _fleetVolleyIndex);

    /// <summary>9103 일제 출격 한 기. 기존 생성 경로(SpawnSelfDestructDrones)를 그대로 쓰고 출격 순번만 붙인다.</summary>
    public void SpawnFleetBomb(int volleyIndex)
    {
        _fleetVolleyIndex = volleyIndex;
        try { SpawnSelfDestructDrones(1); }
        finally { _fleetVolleyIndex = -1; }
    }

    /// <summary>Spine 버튼 누름 이벤트에서 예약된 스킬 자폭을 한 번 발사한다.</summary>
    public void LaunchPendingSkillBombs()
    {
        int count = _pendingSkillBombs;
        var target = _pendingSkillBoss;
        float multiplier = _pendingSkillDamageMultiplier;
        CancelPendingSkillBombs();
        if (count <= 0 || target == null || target.IsDead || target != SkillDebuffTarget) return;
        SpawnBombs(count, _skillSelfDestructPrefab != null ? _skillSelfDestructPrefab : selfDestructPrefab, target,
            SelfDestructOrigin.BasicSkill, multiplier);
    }

    /// <summary>스킬 모션 중단·유닛 제거·풀 반환 때 아직 발사하지 않은 요청을 폐기한다.</summary>
    public void CancelPendingSkillBombs()
    { _pendingSkillBombs = 0; _pendingSkillBoss = null; _pendingSkillDamageMultiplier = 1f; }

    private void SpawnBombs(int count, SelfDestructDrone prefab, BossBase expectedTarget,
        SelfDestructOrigin origin, float damageMultiplier, int volleyIndex = -1)
    {
        if (count <= 0 || !isActiveAndEnabled || currentCell == null || IsStunned || IsCellSealed || unitData == null || prefab == null)
        {
            return;
        }

        if (_fieldPause?.AttacksHeld == true)
        {
            SpawnAfterHoldAsync(count, _bombRequestLifetime, prefab, expectedTarget, origin, damageMultiplier, volleyIndex).Forget();
            return;
        }
        if (_gameManager != null && _gameManager.CurrentState != GameManager.GameState.Playing) return;
        var target = SkillDebuffTarget;
        if (target == null || target.IsDead || expectedTarget is not null && target != expectedTarget) return;
        float coefficient = unitData.GetSelfDestructCoefficient(currentTier);
        if (!(coefficient > 0f) || float.IsInfinity(coefficient))
        {
            Debug.LogError("[Drone_Betan] A finite positive self-destruct coefficient must be authored on UnitData.");
            return;
        }

        for (int i = 0; i < count; i++)
        {
            // Flight owns its world pose; the producer's breathing/flip must not distort it.
            var bombObj = RM.Instantiate(prefab.gameObject, transform.position, Quaternion.identity, null, true);
            if (bombObj != null)
            {
                var bomb = bombObj.GetComponent<SelfDestructDrone>();
                if (bomb != null)
                {
                    // Each accepted bomb owns one attack roll, including the run penalty and existing rounding.
                    var effects = DroneEffects;
                    if (effects?.HasBombHacking == true && unitData.Hacking != null && _droneManager != null)
                    {
                        var runtime=_droneManager.Hacking;runtime.Configure(unitData.Hacking);
                        bomb.InitializeHacking(target,runtime,effects.HackingStacksPerBomb,volleyIndex);
                    }
                    else
                    {
                        int damage = ComputeAttackDamageFrom(GetUpgradedAtk() * coefficient, 1f, out bool critical,
                            origin == SelfDestructOrigin.BasicSkill ? damageMultiplier : 1f);
                        bomb.Initialize(damage, critical, target, unitData, volleyIndex);
                    }
                }
                else RM.Destroy(bombObj);
            }
            else
            {
                Debug.LogError("[Drone_Betan] 자폭 드론을 풀에서 가져오지 못했습니다! 프리팹 설정을 확인하세요.");
            }
        }
    }

    private async UniTaskVoid SpawnAfterHoldAsync(int count, int lifetime, SelfDestructDrone prefab, BossBase expectedTarget,
        SelfDestructOrigin origin, float damageMultiplier, int volleyIndex)
    {
        if (await _fieldPause.WaitForAttacksAsync(this.GetCancellationTokenOnDestroy()).SuppressCancellationThrow()) return;
        if (_bombRequestLifetime != lifetime || !isActiveAndEnabled || currentCell == null) return;
        SpawnBombs(count, prefab, expectedTarget, origin, damageMultiplier, volleyIndex);
    }

    protected override void OnUnitRemoved()
    {
        _bombRequestLifetime++;
        CancelPendingSkillBombs();
        base.OnUnitRemoved();
    }

    protected override void OnDisable()
    {
        _bombRequestLifetime++;
        CancelPendingSkillBombs();
        base.OnDisable();
    }
}
