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
    [SerializeField] private Vector2 _formationSpacing = new Vector2(0.4f, 0.5f);
    [SerializeField] private Vector2 _formationOrigin = new Vector2(0f, 0.7f);

    /// <summary>위 3기/아래 2기의 역 사다리꼴. 간격과 기준점을 본체 프리팹에서 조정한다.</summary>
    protected override Vector2[] SlotOffsets => new[]
    {
        _formationOrigin + Vector2.Scale(new Vector2(-2f, 0f), _formationSpacing),
        _formationOrigin,
        _formationOrigin + Vector2.Scale(new Vector2(2f, 0f), _formationSpacing),
        _formationOrigin + Vector2.Scale(new Vector2(-1f, -1f), _formationSpacing),
        _formationOrigin + Vector2.Scale(new Vector2(1f, -1f), _formationSpacing)
    };

    protected override void OnSkillFull()
    {
        SpawnOneDrone();
        SpawnSelfDestructDrones(selfDestructCount);
        FlashOwnedDrones();
    }

    /// <summary>기본 스킬 및 선택지 주기에서 요청한 자폭 드론을 생성한다.</summary>
    public virtual void SpawnSelfDestructDrones(int count)
    {
        if (unitData == null || selfDestructPrefab == null) 
        {
            return;
        }

        for (int i = 0; i < count; i++)
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
                Debug.LogError("[Drone_Betan] 자폭 드론을 풀에서 가져오지 못했습니다! 프리팹 설정을 확인하세요.");
            }
        }
    }
}
