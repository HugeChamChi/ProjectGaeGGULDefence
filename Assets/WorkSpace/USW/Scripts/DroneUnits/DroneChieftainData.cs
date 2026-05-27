using UnityEngine;

/// <summary>
/// 족장(집결 폭발) 등급별 설정.
/// 피해 = 현재 드론 수 × damagePerDrone
/// </summary>
[CreateAssetMenu(fileName = "DroneChieftainData", menuName = "Game/DroneUnits/DroneChieftainData")]
public class DroneChieftainData : ScriptableObject
{
    [Tooltip("드론 1마리당 폭발 피해")]
    public float damagePerDrone;
}
