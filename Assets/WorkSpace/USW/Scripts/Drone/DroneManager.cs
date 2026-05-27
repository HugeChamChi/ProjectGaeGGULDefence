using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 드론 군단 관리 매니저.
/// 드론 등록/해제, 초당 식량 틱, 드론 버프/보스 디버프 상태,
/// 족장 집결 폭발(ExecuteRallyAsync)을 담당한다.
/// </summary>
public class DroneManager : InGameSingleton<DroneManager>
{
    private readonly List<DroneUnit> _drones = new();

    // ── 식량 ────────────────────────────────────────────────────────
    private float _baseFoodPerDrone = 0.15f;

    public int DroneCount => _drones.Count;

    // ── 드론 버프 ───────────────────────────────────────────────────
    private float _droneAtkMult   = 1f;
    private float _droneSpeedMult = 1f;
    private float _buffEndTime;

    public float DroneAtkMultiplier   => Time.time < _buffEndTime ? _droneAtkMult   : 1f;
    public float DroneSpeedMultiplier => Time.time < _buffEndTime ? _droneSpeedMult : 1f;

    // ── 보스 디버프 ─────────────────────────────────────────────────
    private float _bossDebuffMult = 1f;
    private float _debuffEndTime;

    public float BossDebuffMultiplier => Time.time < _debuffEndTime ? _bossDebuffMult : 1f;

    // ── 집결 상태 ───────────────────────────────────────────────────
    private bool _isRallying;

    [Header("집결 대형")]
    [Tooltip("드론 간 수평 간격 (px)")]
    [SerializeField] private float _rallySpacing    = 50f;
    [Tooltip("보스 중심 기준 아래 방향 오프셋 (px)")]
    [SerializeField] private float _rallyBossOffset = 200f;

    // ── 드론 등록 ───────────────────────────────────────────────────

    public void RegisterDrone(DroneUnit drone)
    {
        if (!_drones.Contains(drone))
            _drones.Add(drone);
    }

    public void UnregisterDrone(DroneUnit drone) => _drones.Remove(drone);

    // ── 식량 설정 ───────────────────────────────────────────────────

    public void SetPerDroneFood(float rate) => _baseFoodPerDrone = rate;
    public void ResetPerDroneFood()         => _baseFoodPerDrone = 0.15f;

    // ── 버프/디버프 ─────────────────────────────────────────────────

    public void ApplyDroneBuff(float atkMult, float speedMult, float duration)
    {
        _droneAtkMult   = atkMult;
        _droneSpeedMult = speedMult;
        _buffEndTime    = Time.time + duration;
    }

    public void ApplyBossDebuff(float multiplier, float duration)
    {
        _bossDebuffMult = multiplier;
        _debuffEndTime  = Time.time + duration;
    }

    // ── 족장 집결 폭발 ──────────────────────────────────────────────

    /// <summary>
    /// 1) 모든 드론을 보스 앞 가로 일렬로 집결
    /// 2) 일제 사격 + 데미지
    /// 3) 원래 위치 귀환 후 궤도 재개
    /// </summary>
    public async UniTask ExecuteRallyAsync(float damagePerDrone, CancellationToken token)
    {
        if (_isRallying || _drones.Count == 0) return;
        _isRallying = true;

        try
        {
            var snapshot = _drones.ToArray();

            // 집결 위치 계산 — 보스 아래 가로 일렬
            var boss = Manager.Boss?.CurrentBoss;
            Vector3 rallyCenter = boss != null
                ? boss.transform.position + new Vector3(0f, -_rallyBossOffset, 0f)
                : new Vector3(Screen.width * 0.5f, Screen.height * 0.65f, 0f);

            float totalW = (snapshot.Length - 1) * _rallySpacing;

            // ① 집결 이동 (전체 병렬)
            var moveTasks = new UniTask[snapshot.Length];
            for (int i = 0; i < snapshot.Length; i++)
            {
                float x   = rallyCenter.x - totalW * 0.5f + i * _rallySpacing;
                var   pos = new Vector3(x, rallyCenter.y, 0f);
                moveTasks[i] = snapshot[i].MoveToAsync(pos, 0.5f, token);
            }
            await UniTask.WhenAll(moveTasks);

            // ② 짧은 와인드업
            if (await UniTask.Delay(200, cancellationToken: token).SuppressCancellationThrow())
                return;

            // ③ 일제 사격 + 피해
            boss = Manager.Boss?.CurrentBoss;
            if (boss != null && !boss.IsDead)
            {
                foreach (var d in snapshot)
                    d.FireRallyShot();

                boss.TakeDamage(Mathf.RoundToInt(snapshot.Length * damagePerDrone));
            }

            // ④ 귀환 (전체 병렬)
            var returnTasks = new UniTask[snapshot.Length];
            for (int i = 0; i < snapshot.Length; i++)
                returnTasks[i] = snapshot[i].ReturnToHomeAsync(0.4f, token);
            await UniTask.WhenAll(returnTasks);

            // ⑤ 궤도 재개
            foreach (var d in snapshot)
                d.StartOrbit();
        }
        finally
        {
            _isRallying = false;
        }
    }

    // ── 초기화 ──────────────────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();
        FoodTickAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    // ── 식량 틱 ─────────────────────────────────────────────────────

    private async UniTaskVoid FoodTickAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await UniTask.Delay(1000, cancellationToken: token);

            int count = _drones.Count;
            if (count > 0 && Manager.Currency != null)
            {
                float food = count * _baseFoodPerDrone * (Manager.Buff?.FoodAmountMultiplier ?? 1f);
                Manager.Currency.AddCurrency(food);
            }
        }
    }
}
