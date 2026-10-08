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
    [Inject] private FieldPauseVisuals _fieldPause;
    [Inject] private GameManager _gameManager;
    private int _bombRequestLifetime;

    protected override void OnSkillFull()
    {
        SpawnOneDrone();
        SpawnSelfDestructDrones(selfDestructCount);
        FlashOwnedDrones();
    }

    /// <summary>기본 스킬 및 선택지 주기에서 요청한 자폭 드론을 생성한다.</summary>
    public virtual void SpawnSelfDestructDrones(int count)
    {
        if (count <= 0 || !isActiveAndEnabled || currentCell == null || IsStunned || IsCellSealed || unitData == null || selfDestructPrefab == null)
        {
            return;
        }

        if (_fieldPause?.AttacksHeld == true)
        {
            SpawnAfterHoldAsync(count, _bombRequestLifetime).Forget();
            return;
        }
        if (_gameManager != null && _gameManager.CurrentState != GameManager.GameState.Playing) return;
        var target = SkillDebuffTarget;
        if (target == null || target.IsDead) return;
        float coefficient = unitData.SelfDestructAttackCoefficient;
        if (!(coefficient > 0f) || float.IsInfinity(coefficient))
        {
            Debug.LogError("[Drone_Betan] A finite positive self-destruct coefficient must be authored on UnitData.");
            return;
        }

        for (int i = 0; i < count; i++)
        {
            // Flight owns its world pose; the producer's breathing/flip must not distort it.
            var bombObj = RM.Instantiate(selfDestructPrefab.gameObject, transform.position, Quaternion.identity, null, true);
            if (bombObj != null)
            {
                var bomb = bombObj.GetComponent<SelfDestructDrone>();
                if (bomb != null)
                {
                    // Each accepted bomb owns one attack roll, including the run penalty and existing rounding.
                    var hacking = DroneSelections?.Get(DroneSelectionKind.BetanHackingBomb);
                    if (hacking != null && unitData.Hacking != null && _droneManager != null)
                    {
                        var runtime=_droneManager.Hacking;runtime.Configure(unitData.Hacking);
                        bomb.InitializeHacking(target,runtime,hacking.Count);
                    }
                    else
                    {
                        int damage = ComputeAttackDamageFrom(GetUpgradedAtk() * coefficient, 1f, out bool critical);
                        bomb.Initialize(damage, critical, target);
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

    private async UniTaskVoid SpawnAfterHoldAsync(int count, int lifetime)
    {
        if (await _fieldPause.WaitForAttacksAsync(this.GetCancellationTokenOnDestroy()).SuppressCancellationThrow()) return;
        if (_bombRequestLifetime != lifetime || !isActiveAndEnabled || currentCell == null) return;
        SpawnSelfDestructDrones(count);
    }

    protected override void OnUnitRemoved()
    {
        _bombRequestLifetime++;
        base.OnUnitRemoved();
    }
}
