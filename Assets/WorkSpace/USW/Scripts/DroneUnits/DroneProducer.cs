using System.Collections.Generic;
using VContainer;
using UnityEngine;

/// <summary>
/// 드론 생산자.
/// 스킬마다 영구 드론 1마리 추가 (최대 maxDroneCount). 자폭 드론은 캡 없이 매 스킬 소환.
/// 유닛 제거 시 소유 드론 전체를 풀로 반환한다.
/// </summary>
public class DroneProducer : DroneSpawnerBase
{
    [SerializeField] private DroneProducerData[] _dataByTier; // 0=Normal 1=Rare 2=Epic 3=Legend

    private DroneProducerData Data =>
        unitData != null && _dataByTier != null && (int)unitData.unitTier < _dataByTier.Length
            ? _dataByTier[(int)unitData.unitTier] : null;

    protected override bool HasValidData() => Data != null;
    protected override float GetDroneAtk() => Data.droneAtk;
    protected override float GetDroneAttackInterval() => Data.droneAttackInterval;

    protected override void OnSkillFull()
    {
        onSkillFull?.Invoke();
        SpawnOneDrone();

        if (Data == null || _dronePoolManager == null) 
        {
            Debug.Log($"[DroneProducer] OnSkillFull: Data={Data != null}, Pool={_dronePoolManager != null}");
            return;
        }

        Debug.Log($"[DroneProducer] 스킬 발동! (Tier: {unitData?.unitTier}) | 자폭 드론 소환 개수: {Data.selfDestructCount}");

        for (int i = 0; i < Data.selfDestructCount; i++)
        {
            var bomb = _dronePoolManager.GetSelfDestruct(Data.selfDestructDamage, transform.position);
            if (bomb == null) Debug.LogError("[DroneProducer] 자폭 드론을 풀에서 가져오지 못했습니다! 풀 설정을 확인하세요.");
        }
    }
}
