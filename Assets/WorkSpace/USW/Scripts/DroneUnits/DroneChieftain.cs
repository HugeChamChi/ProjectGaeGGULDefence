using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 족장 드론 지휘관 — Tier.Chieftain 등급.
/// 스킬 발동 시 DroneManager.ExecuteRallyAsync를 통해:
/// 모든 드론이 보스 앞 가로 일렬로 집결 → 일제 사격 → 귀환 → 궤도 재개.
///
/// 족장 등급 규칙: 합성/판매/등급 변경 불가 (MergeManager, UnitSpawner에서 차단).
/// 드론이 없으면 발동해도 아무 일 없음.
/// </summary>
public class DroneChieftain : UnitBase
{
    [SerializeField] private DroneChieftainData _data;

    protected override void OnSkillFull()
    {
        onSkillFull?.Invoke();

        if (_data == null || Manager.Drone == null) return;

        Manager.Drone
            .ExecuteRallyAsync(_data.damagePerDrone, this.GetCancellationTokenOnDestroy())
            .Forget(Debug.LogException);
    }
}
