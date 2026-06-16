using System.Collections.Generic;
using VContainer;
using UnityEngine;

/// <summary>
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
    [Header("Drone Producer Settings")]
    [SerializeField] private SelfDestructDrone selfDestructPrefab;
    [SerializeField] private int selfDestructCount = 1;
    [SerializeField] private float selfDestructDamage = 50f;

    protected override void OnSkillFull()
    {
        onSkillFull?.Invoke();
        SpawnOneDrone();

        if (unitData == null || selfDestructPrefab == null) 
        {
            return;
        }

        for (int i = 0; i < selfDestructCount; i++)
        {
            var bombObj = RM.Instantiate(selfDestructPrefab.gameObject, transform.position, Quaternion.identity, transform, true);
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
                Debug.LogError("[DroneProducer] 자폭 드론을 풀에서 가져오지 못했습니다! 프리팹 설정을 확인하세요.");
            }
        }
    }
}
