using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 족장 — 집결 폭발.
/// 스킬 발동 시 DroneManager.ExecuteRallyAsync를 통해:
/// 모든 드론이 보스 앞 가로 일렬로 집결 → 일제 사격 → 귀환 → 궤도 재개.
/// 드론이 없으면 발동해도 아무 일 없음.
/// </summary>
public class DroneChieftain : UnitBase
{
    [SerializeField] private DroneChieftainData[] _dataByTier; // 0=Normal 1=Rare 2=Epic 3=Legend

    private DroneChieftainData Data =>
        unitData != null && _dataByTier != null && (int)unitData.unitTier < _dataByTier.Length
            ? _dataByTier[(int)unitData.unitTier] : null;

    protected override void OnSkillFull()
    {
        onSkillFull?.Invoke();

        if (Data == null || Manager.Drone == null) return;

        Manager.Drone
            .ExecuteRallyAsync(Data.damagePerDrone, this.GetCancellationTokenOnDestroy())
            .Forget(Debug.LogException);
    }
}
