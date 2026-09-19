using System.Collections.Generic;
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
    [SerializeField] private float selfDestructDamage = 50f;

    [Header("Combat Drone Formation")]
    [SerializeField] private DroneFormationSettings _formation;

    /// <summary>SO의 좌상→우상→좌하→우하→하단 중앙 슬롯을 생성 순서대로 채운다.</summary>
    protected override Vector2[] SlotOffsets => _formation != null && _formation.SlotOffsets.Length > 0
        ? _formation.SlotOffsets : base.SlotOffsets;

    protected override void OnSkillFull()
    {
        SpawnOneDrone();
        SpawnSelfDestructDrones(selfDestructCount);
        FlashOwnedDrones();
    }

    /// <summary>기본 스킬 및 선택지 주기에서 요청한 자폭 드론을 생성한다.</summary>
    public virtual void SpawnSelfDestructDrones(int count)
    {
        if (IsStunned || unitData == null || selfDestructPrefab == null)
        {
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
                    bomb.Initialize(selfDestructDamage);
                }
            }
            else
            {
                Debug.LogError("[Drone_Betan] 자폭 드론을 풀에서 가져오지 못했습니다! 프리팹 설정을 확인하세요.");
            }
        }
    }
}
