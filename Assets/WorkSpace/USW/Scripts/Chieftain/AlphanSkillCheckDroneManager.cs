#if UNITY_EDITOR
using System.Threading;
using Cysharp.Threading.Tasks;

/// <summary>에디터 검사에서 VFX 없이 발동/선택지 전달/취소를 기록한다. 플레이어 빌드 제외.</summary>
public sealed class AlphanSkillCheckDroneManager : DroneManager
{
    public int CastCount;
    public int EmergencyCount;
    public float Damage;
    public DroneSelectionEffect Protocol;
    public bool HoldCast;
    public CancellationToken LastToken;
    /// <inheritdoc />
    public override void ApplyEmergencyBuffs() => EmergencyCount++;
    /// <inheritdoc />
    public override async UniTask ExecuteRallyAsync(float damagePerDrone, CancellationToken token, DroneSelectionEffect doubleShot = null)
    {
        CastCount++;Damage=damagePerDrone;Protocol=doubleShot;LastToken=token;
        if (HoldCast) await UniTask.WaitUntil(()=>!HoldCast,cancellationToken:token);
    }
}
#endif
