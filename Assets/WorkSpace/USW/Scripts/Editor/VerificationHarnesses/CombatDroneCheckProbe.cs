#if UNITY_EDITOR
using UnityEngine;

/// <summary>실제 DroneUnit의 등록/해제 및 소환자 선택지 갱신을 풀 없이 검사한다.</summary>
public sealed class CombatDroneCheckProbe : Drone_Betan
{
    protected override void Awake() { }
    protected override bool HasValidData() => unitData != null;
    /// <summary>실제 배치 훅을 호출한다.</summary>
    public void PlaceForCheck() => OnUnitPlaced();
    /// <summary>실제 회수 훅을 호출한다.</summary>
    public void RemoveForCheck() => OnUnitRemoved();
    /// <summary>원위치 좌표를 확인한다.</summary>
    public Vector2[] Formation => SlotOffsets;
    protected override DroneUnit CreateCombatDrone()
    {
        var go = new GameObject("Check combat drone");
        go.SetActive(false);
        go.transform.SetParent(transform, false);
        var drone = go.AddComponent<DroneUnit>();
        typeof(DroneUnit).GetField("_droneManager", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .SetValue(drone, _droneManager);
        go.SetActive(true);
        return drone;
    }
    protected override void ReleaseCombatDrone(DroneUnit drone)
    {
        drone.gameObject.SetActive(false);
        // Preview Scene에서는 MonoBehaviour 비활성 콜백이 실행되지 않을 수 있다.
        typeof(DroneUnit).GetMethod("OnDisable", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Invoke(drone, null);
        DestroyImmediate(drone.gameObject);
    }
}
#endif
