#if UNITY_EDITOR
/// <summary>선택지 검사에서 리소스를 생성하지 않고 자폭 드론 요청 수를 기록한다. 플레이어 빌드 제외.</summary>
public sealed class DroneSelectionCheckBetan : Drone_Betan
{
    /// <summary>누적 소환 요청 수.</summary>
    public int Spawned { get; private set; }
    protected override void Awake() { }
    /// <inheritdoc />
    public override void SpawnSelfDestructDrones(int count) => Spawned += count;
}
#endif
