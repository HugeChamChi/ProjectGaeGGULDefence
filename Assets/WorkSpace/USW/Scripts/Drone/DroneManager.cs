using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 드론 군단 관리 매니저.
/// 드론 등록/해제, 초당 식량 틱, 드론 버프 상태,
/// 족장 집결 폭발(ExecuteRallyAsync)을 담당한다.
/// </summary>
public class DroneManager : MonoBehaviour
{
    private DroneHackingRuntime _hacking;
    /// <summary>이 씬의 생산자/소비자가 공유하는 보스 해킹 자원.</summary>
    public DroneHackingRuntime Hacking
    {
        get
        {
            if (_hacking == null)
            {
                _hacking = gameObject.AddComponent<DroneHackingRuntime>();
                _hacking.Initialize(_bossManager, _gameManager, _effects);
            }
            return _hacking;
        }
    }
    [VContainer.Inject] private BossManager _bossManager;
    [VContainer.Inject] private CurrencyManager _currencyManager;
    [VContainer.Inject] private TotemBuffManager _totemBuffManager;
    [VContainer.Inject] private GridManager _gridManager;
    [VContainer.Inject] private IDroneEffectReader _effects;
    [VContainer.Inject] private IChiefSelectionReader _chiefEffects;
    [VContainer.Inject] private GameManager _gameManager;
    [VContainer.Inject] private FieldPauseVisuals _fieldPause;
    private float _betanNormalTimer, _betanEpicTimer;
    private (float Interval, int Count) _normalEffect, _epicEffect;
    private readonly List<Drone_Betan> _betans = new();
    private readonly HashSet<Drone_Betan> _betanMembership = new();

    private void Update()
    {
        if (_gameManager != null && _gameManager.CurrentState != GameManager.GameState.Playing) return;
        TickSelections(Time.deltaTime);
        TickFood(Time.deltaTime);
    }

    /// <summary>전투 시간에 따라 베탕 추가 소환을 실행한다. 에픽은 필드 합계 상한이다.</summary>
    public void TickSelections(float deltaTime)
    {
        if (_fieldPause?.AttacksHeld == true) return;
        var normal = (Interval: _effects?.PeriodicBombInterval ?? 0f, Count: _effects?.PeriodicBombCount ?? 0);
        var epic = (Interval: _effects?.FleetBombInterval ?? 0f, Count: _effects?.FleetBombCount ?? 0);
        if (_normalEffect != normal) { _normalEffect = normal; _betanNormalTimer = 0; }
        if (_epicEffect != epic) { _epicEffect = epic; _betanEpicTimer = 0; }
        if (deltaTime <= 0 || (normal.Count == 0 && epic.Count == 0)) return;
        _betans.Clear();
        _betanMembership.Clear();
        int fleetCount = 0;
        foreach (var drone in _drones)
        {
            if (drone == null || !drone.isActiveAndEnabled || drone.Owner is not Drone_Betan betan
                || !betan.isActiveAndEnabled || betan.currentCell == null) continue;
            fleetCount++;
            if (_betanMembership.Add(betan)) _betans.Add(betan);
        }
        if (fleetCount == 0) { _betanNormalTimer = _betanEpicTimer = 0; return; }
        if (normal.Count > 0 && normal.Interval > 0)
        {
            _betanNormalTimer += deltaTime;
            while (_betanNormalTimer >= normal.Interval)
            {
                _betanNormalTimer -= normal.Interval;
                foreach (var betan in _betans) betan.SpawnSelfDestructDrones(normal.Count);
            }
        }
        if (epic.Count > 0 && epic.Interval > 0)
        {
            _betanEpicTimer += deltaTime;
            while (_betanEpicTimer >= epic.Interval)
            {
                _betanEpicTimer -= epic.Interval;
                int remaining = Mathf.Min(fleetCount, epic.Count), volley = 0;
                foreach (var drone in _drones)
                {
                    if (remaining == 0) break;
                    // 9103 일제 출격: 순번을 붙여 한 기씩 이어 출발시킨다(FxLab_BombHack 다수 발사 표현).
                    if (drone != null && drone.isActiveAndEnabled && drone.Owner is Drone_Betan betan && _betanMembership.Contains(betan))
                    { betan.SpawnFleetBomb(volley++); remaining--; }
                }
            }
        }
    }

    /// <summary>알팡 액티브에서 배치된 감망의 버프를 각 한 번 발동한다.</summary>
    public virtual void ApplyEmergencyBuffs()
    {
        if (_effects?.HasEmptyStackDamage != true) return;
        var owners = new HashSet<Drone_Gamman>();
        foreach (var drone in _drones)
            if (drone != null && drone.Owner is Drone_Gamman gamman && gamman.currentCell != null && gamman.isActiveAndEnabled)
                owners.Add(gamman);
        foreach (var gamman in owners) gamman.ApplyDroneBuff();
    }

    private readonly List<DroneUnit> _drones = new();
    private readonly Dictionary<DroneUnit, float> _foodProgress = new();

    // ── 식량 ────────────────────────────────────────────────────────
    [Header("식량 (재화)")]
    [Tooltip("드론 1기당 기본 식량 획득량")]
    [SerializeField] private float _defaultFoodPerDrone = 0.15f;
    private float _baseFoodPerDrone;

    public event System.Action OnDroneCountChanged;

    public int DroneCount => _drones.Count;
    /// <summary>현재 실제 생산 가능한 전투 드론들의 초당 식량 합계. 식량 지급과 같은 필터·보정을 사용한다.</summary>
    public float CurrentFoodProductionPerSecond => GetFleetFoodPerSecond(BaseFoodPerDrone);

    private static bool CanProduceFood(DroneUnit drone) => drone != null && drone.isActiveAndEnabled
        && drone.Owner != null && drone.Owner.currentCell != null && !drone.Owner.IsStunned && !drone.Owner.IsCellSealed;
    /// <summary>새 집결 사격에 참가 가능한 드론 수. 이미 진행 중인 집결은 다시 필터링하지 않는다.</summary>
    public int RallyAvailableDroneCount
    {
        get
        {
            int count = 0;
            foreach (var drone in _drones)
                if (drone != null && (drone.Owner == null || (!drone.Owner.IsStunned && !drone.Owner.IsCellSealed))) count++;
            return count;
        }
    }
    public IReadOnlyList<DroneUnit> Drones => _drones;
    private readonly List<Drone_Zeltan> _foodProducers = new();
    /// <summary>유효한 젤탕들의 드론 1기당 계수를 모두 더한다. 유효한 젤탕이 없으면 기본 생산량을 사용한다.</summary>
    public float BaseFoodPerDrone
    {
        get
        {
            bool any = false;
            float sum = 0f;
            foreach (var producer in _foodProducers)
            {
                if (!IsActiveFoodProducer(producer)) continue;
                any = true;
                sum += producer.FoodPerDronePerSecond;
            }
            return any ? sum : _baseFoodPerDrone;
        }
    }

    private static bool IsActiveFoodProducer(Drone_Zeltan producer) => producer != null && producer.isActiveAndEnabled
        && producer.currentCell != null && !producer.IsCellSealed;

    /// <summary>주어진 드론 1기당 계수로 현재 생산 가능한 드론들이 버는 초당 식량. 지급과 같은 필터·토템 보정을 쓴다.</summary>
    public float GetFleetFoodPerSecond(float foodPerDrone)
    {
        int count = 0;
        foreach (var drone in _drones) if (CanProduceFood(drone)) count++;
        return count * foodPerDrone * (_totemBuffManager?.FoodAmountMultiplier ?? 1f);
    }

    /// <summary>젤탕을 군단 식량 생산자로 등록한다.</summary>
    public void RegisterFoodProducer(Drone_Zeltan producer)
    {
        if (producer != null && !_foodProducers.Contains(producer)) _foodProducers.Add(producer);
    }

    /// <summary>제거된 젤탕만 해제하며 다른 젤탕의 패시브는 유지한다.</summary>
    public void UnregisterFoodProducer(Drone_Zeltan producer) => _foodProducers.Remove(producer);

    private long _dividendRunId;
    private int _dividendRound;

    /// <summary>클리어가 확정된 런·라운드의 현재 잔고를 한 번 정산한다. 자격 없는 완료도 재처리하지 않는다.</summary>
    public float SettleWaveFoodDividend(long runId, int round)
    {
        if (round <= 0 || (_dividendRunId == runId && round <= _dividendRound)) return 0f;
        _dividendRunId = runId;
        _dividendRound = round;
        float ratio = _effects?.WaveFoodDividendRatio ?? 0f;
        if (ratio <= 0f || _currencyManager == null || _gridManager == null) return 0f;
        bool placed = false;
        foreach (var cell in _gridManager.GetOccupiedCells())
            if (cell.OccupyingUnit is Drone_Zeltan producer && producer.isActiveAndEnabled && producer.currentCell == cell)
            { placed = true; break; }
        if (!placed) return 0f;
        float amount = _currencyManager.Currency * ratio;
        if (amount <= 0f) return 0f;
        _currencyManager.AddCurrency(amount);
        return amount;
    }

    /// <summary>실제 자폭의 발사자가 같은 개체 수명으로 배치되어 있을 때 충전을 앞당긴다.</summary>
    public void NotifySelfDestructExplosion(Drone_Betan owner, int lifetime)
    {
        float seconds = _effects?.ExplosionSkillRecoverySeconds ?? 0f;
        if (seconds <= 0f || owner == null || !owner.IsBombOwnerCurrent(lifetime) || owner.IsCellSealed || owner.Combat == null) return;
        owner.Combat.SkillTimer = Mathf.Min(owner.GetCurrentSkillInterval(), owner.Combat.SkillTimer + seconds);
    }

    /// <summary>황금 모노클은 알팡 액티브에 확정 치명타 배율을 적용한다.</summary>
    public decimal GetRallyDamage(int droneCount, float damagePerDrone)
    {
        decimal damage = Mathf.RoundToInt(droneCount * damagePerDrone);
        return _chiefEffects?.HasRallyDamage == true ? damage * (decimal)_chiefEffects.RallyDamageBonus : damage;
    }

    /// <summary>참가 드론의 현재 비치명 사격 공격력을 합산하고 집결 계수·황금 모노클을 한 번 적용한다.</summary>
    public decimal GetChiefRallyDamage(IReadOnlyList<DroneUnit> drones, float attackCoefficient)
    {
        if (drones == null) throw new System.ArgumentNullException(nameof(drones));
        if (float.IsNaN(attackCoefficient) || float.IsInfinity(attackCoefficient) || attackCoefficient < 0f)
            throw new System.ArgumentOutOfRangeException(nameof(attackCoefficient));
        decimal attack = 0m;
        foreach (var drone in drones)
            if (drone != null) attack += Mathf.Max(0, drone.NonCriticalAttackDamage);
        decimal damage = attack * (decimal)attackCoefficient;
        return _chiefEffects?.HasRallyDamage == true ? damage * (decimal)_chiefEffects.RallyDamageBonus : damage;
    }

    // ── 드론 버프 ───────────────────────────────────────────────────
    private float _droneAtkMult   = 1f;
    private float _droneSpeedMult = 1f;
    private float _buffEndTime;

    public float DroneAtkMultiplier   => Time.time < _buffEndTime ? _droneAtkMult   : 1f;
    public float DroneSpeedMultiplier => Time.time < _buffEndTime ? _droneSpeedMult : 1f;


    // ── 집결 상태 ───────────────────────────────────────────────────
    private bool _isRallying;
    /// <summary>액티브 버튼의 중복 실행 방지에 사용한다.</summary>
    public bool IsRallying => _isRallying;

    /// <summary>집결 사격 한 발이 실제 보스 체력을 감소시킨 직후 알린다. 충전·빗나감·무적에는 발행하지 않는다.</summary>
    public event System.Action<BossBase> OnRallyImpact;

    [Header("드론 스폰 배치 간격 (늘어진 V자)")]
    [Tooltip("하단 드론의 X 간격 (좁게)")]
    public float droneSpreadXLower = 0.25f; 
    [Tooltip("하단 드론의 Y 위치 (아래로 깊게)")]
    public float droneSpreadYLower = 0.2f;   // (수정) 발밑으로 꺼지지 않게 살짝 위로 올림
    [Tooltip("상단 드론의 X 간격 (넓게)")]
    public float droneSpreadXUpper = 0.8f; 
    [Tooltip("상단 드론의 Y 위치 (위로)")]
    public float droneSpreadYUpper = 0.7f;   // (수정) 본체 머리 부근에 위치하도록 올림

    [Header("집결 대형 (보스 반대쪽으로 열린 부채꼴 — FxLab_ChiefConvergeBeam 기준)")]
    [Tooltip("부채꼴 반지름 (Unit) — 드론은 합류점 뒤쪽으로 퍼진다")]
    [SerializeField, Min(0f)] private float _rallyFanRadius = 2.4f;
    [Tooltip("부채꼴 펼침 각도 (도)")]
    [SerializeField, Range(30f, 220f)] private float _rallyFanDegrees = 160f;
    [Tooltip("이 수를 넘으면 안쪽·바깥쪽 두 줄 부채꼴로 나눈다")]
    [SerializeField, Min(1)] private int _rallySingleRowMax = 10;
    [Tooltip("두 줄일 때 안쪽 줄 반지름 비율")]
    [SerializeField, Range(0.3f, 0.95f)] private float _rallyInnerRowRatio = 0.68f;

    [Header("집결 연출")]
    [Tooltip("집결 이동 시간 (초)")]
    [SerializeField] private float _rallyMoveDuration = 0.5f;
    [Tooltip("집결 후 대기(와인드업) 시간 (밀리초)")]
    [SerializeField] private int _rallyWindupMs = 200;
    [Tooltip("원위치 귀환 이동 시간 (초)")]
    [SerializeField] private float _rallyReturnDuration = 0.4f;
    private ChiefConvergeBeamFx _convergeBeamPrefab;
    private ChiefConvergeBeamFx _convergeBeam;

    /// <summary>집결 사격 수렴 빔 연출 프리팹 — 족장 스킬 데이터(AlphanSkillData.BeamFx)가 넣어 준다. 비어 있으면 연출 없이 피해만 준다.</summary>
    public ChiefConvergeBeamFx RallyBeamPrefab
    {
        get => _convergeBeamPrefab;
        set
        {
            if (_convergeBeamPrefab == value) return;
            _convergeBeamPrefab = value;
            if (_convergeBeam != null) { Destroy(_convergeBeam.gameObject); _convergeBeam = null; }
        }
    }

    [Header("테스트")]
    [SerializeField] private float _testRallyDamage = 50f;

    // ── 드론 등록 ───────────────────────────────────────────────────

    public void RegisterDrone(DroneUnit drone)
    {
        _fieldPause?.RegisterDrone(drone);
        if (!_drones.Contains(drone))
        {
            _drones.Add(drone);
            OnDroneCountChanged?.Invoke();
        }
    }

    public void UnregisterDrone(DroneUnit drone)
    {
        if (_drones.Remove(drone))
        {
            _foodProgress.Remove(drone);
            OnDroneCountChanged?.Invoke();
        }
    }

    // ── 식량 설정 ───────────────────────────────────────────────────

    public void SetPerDroneFood(float rate) => _baseFoodPerDrone = rate;
    public void ResetPerDroneFood()         => _baseFoodPerDrone = _defaultFoodPerDrone;

    // ── 버프/디버프 ─────────────────────────────────────────────────

    public void ApplyDroneBuff(float atkMult, float speedMult, float duration)
    {
        _droneAtkMult   = atkMult;
        _droneSpeedMult = speedMult;
        _buffEndTime    = Time.time + duration;
    }


    // ── 족장 집결 폭발 ──────────────────────────────────────────────

    /// <summary>
    /// 1) 모든 드론을 그리드 중앙에서 보스 반대쪽으로 열린 부채꼴로 집결
    /// 2) 수렴 빔 충전 — 드론마다 빛이 차오르고 조준선이 합류점으로 모인다 (ChiefConvergeBeamFx)
    /// 3) 빔이 터지는 순간 전체 피해 (연속 사격은 빔 유지 중 Pulse와 함께 추가 피해)
    /// 4) 빔이 사그라든 뒤 원래 위치 귀환 후 궤도 재개
    /// </summary>
    public virtual UniTask ExecuteRallyAsync(float damagePerDrone, CancellationToken token, ChiefVolleySettings doubleShot = default, ChieftainData source = null)
        => ExecuteRallyCoreAsync(damagePerDrone, token, doubleShot, source, null);

    /// <summary>족장 SO의 공격력 연동 집결과 공유 해킹 스택 전량소모를 같은 발사 수명으로 실행한다.</summary>
    public virtual UniTask ExecuteChiefRallyAsync(AlphanSkillData data, CancellationToken token, ChieftainData source = null)
    {
        if (data == null) throw new System.ArgumentNullException(nameof(data));
        if (_chiefEffects?.ChiefHackingDamagePerStack > 0f && data.Hacking == null)
            throw new System.InvalidOperationException("Emergency detonation requires authored shared hacking settings on the chief skill.");
        return ExecuteRallyCoreAsync(0f, token, default, source, data);
    }

    private async UniTask ExecuteRallyCoreAsync(float damagePerDrone, CancellationToken token,
        ChiefVolleySettings doubleShot, ChieftainData source, AlphanSkillData chiefData)
    {
        if (_isRallying || RallyAvailableDroneCount == 0) return;
        _isRallying = true;
        ChiefConvergeBeamFx beam = null;
        DroneUnit[] snapshot = System.Array.Empty<DroneUnit>();
        int[] lifetimes = System.Array.Empty<int>();
        BossBase chiefTarget = null;
        DroneHackingRuntime chiefHacking = null;
        HackingStackLedger.Reservation chiefReservation = null;
        int chiefGeneration = 0;
        bool chiefCommitted = false;
        decimal chiefDamage = 0m;
        float hackingDamageRatio = 0f;
        bool IsCurrent(int index) => snapshot[index] != null && snapshot[index].isActiveAndEnabled
            && snapshot[index].PlacementLifetime == lifetimes[index];
        bool IsChiefTargetCurrent(BossBase boss) => chiefData == null ||
            (boss != null && ReferenceEquals(boss, chiefTarget) && ReferenceEquals(_bossManager?.CurrentBoss, chiefTarget)
                && (chiefHacking == null || ReferenceEquals(chiefHacking.Target, boss) && chiefHacking.Ledger.Generation == chiefGeneration));

        try
        {
            snapshot = _drones.FindAll(d => d != null && (d.Owner == null || (!d.Owner.IsStunned && !d.Owner.IsCellSealed))).ToArray();
            lifetimes = new int[snapshot.Length];
            for (int i = 0; i < snapshot.Length; i++) lifetimes[i] = snapshot[i].PlacementLifetime;
            if (chiefData != null)
            {
                chiefTarget = _bossManager?.CurrentBoss;
                chiefDamage = GetChiefRallyDamage(snapshot, chiefData.RallyAttackCoefficient);
                hackingDamageRatio = _chiefEffects?.ChiefHackingDamagePerStack ?? 0f;
                if (chiefData.Hacking != null)
                {
                    chiefHacking = Hacking;
                    chiefHacking.Configure(chiefData.Hacking);
                    if (ReferenceEquals(chiefHacking.Target, chiefTarget))
                    {
                        chiefGeneration = chiefHacking.Ledger.Generation;
                        if (chiefTarget != null && chiefTarget.IsTargetable && hackingDamageRatio > 0f && chiefDamage > 0m)
                            chiefReservation = chiefHacking.Reserve(chiefTarget, int.MaxValue);
                    }
                }
            }

            // 집결 위치 — 그리드 기하학적 정중앙, 부채꼴은 보스 반대쪽으로 열린다
            Vector3 rallyCenter = _gridManager != null ? _gridManager.GetAbsoluteCenterPosition() : Vector3.zero;
            rallyCenter.z = 0f;
            var currentBoss = _bossManager?.CurrentBoss;
            Vector3 toBoss = currentBoss != null ? currentBoss.transform.position - rallyCenter : Vector3.up;
            toBoss.z = 0f;
            Vector3 dir = toBoss.sqrMagnitude > 1e-6f ? toBoss.normalized : Vector3.up;

            // ① 집결 이동 (전체 병렬)
            var moveTasks = new List<UniTask>();
            for (int i = 0; i < snapshot.Length; i++)
            {
                if (!IsCurrent(i)) continue;
                moveTasks.Add(snapshot[i].MoveToAsync(RallySlot(i, snapshot.Length, rallyCenter, dir), _rallyMoveDuration, token));
            }
            if (moveTasks.Count > 0)
                await UniTask.WhenAll(moveTasks);

            // ② 짧은 와인드업
            if (await UniTask.Delay(_rallyWindupMs, cancellationToken: token).SuppressCancellationThrow())
                return;

            // ③ 수렴 빔 충전 → 발사 순간 피해
            if (_fieldPause != null) await _fieldPause.WaitForAttacksAsync(token);
            var boss = chiefData != null ? chiefTarget : _bossManager?.CurrentBoss;
            if (boss != null && !boss.IsDead && IsChiefTargetCurrent(boss))
            {
                beam = PlayConvergeBeam(snapshot, boss.transform, lifetimes);
                if (beam != null)
                {
                    bool fired = false;
                    void OnFired() => fired = true;
                    beam.Fired += OnFired;
                    try { await UniTask.WaitUntil(() => fired || !beam.IsPlaying, cancellationToken: token); }
                    finally { beam.Fired -= OnFired; }
                    if (_fieldPause != null) await _fieldPause.WaitForAttacksAsync(token);
                }
                for (int i = 0; i < snapshot.Length; i++) if (IsCurrent(i)) snapshot[i].PlayActionFlash();

                if (boss != null && !boss.IsDead && IsChiefTargetCurrent(boss))
                {
                    decimal baseDamage = chiefData != null ? chiefDamage : GetRallyDamage(snapshot.Length, damagePerDrone);
                    if (chiefData != null)
                    {
                        decimal multiplier = 1m + (chiefReservation?.Amount ?? 0) * (decimal)hackingDamageRatio;
                        long units = boss.CalculateFinalDamageUnits(baseDamage * multiplier);
                        // Do not consume stacks if the original target cannot accept this impact.
                        if (units > 0)
                        {
                            if (chiefReservation?.Amount > 0)
                            {
                                chiefCommitted = chiefHacking.Commit(boss, chiefReservation);
                                if (!chiefCommitted) units = boss.CalculateFinalDamageUnits(baseDamage);
                            }
                            decimal previousHp = boss.CurrentHp;
                            boss.ApplyRecordedDamage(units, source: source);
                            NotifyRallyImpact(boss, previousHp);
                        }
                    }
                    else if (doubleShot.Count == 0)
                    {
                        decimal previousHp = boss.CurrentHp;
                        boss.TakeDamage(baseDamage, source: source);
                        NotifyRallyImpact(boss, previousHp);
                    }
                    else
                    {
                        long finalUnits = boss.CalculateFinalDamageUnits(baseDamage);
                        long shotUnits = (long)System.Math.Round(finalUnits * (decimal)doubleShot.DamageRatio, System.MidpointRounding.AwayFromZero);
                        decimal previousHp = boss.CurrentHp;
                        boss.ApplyRecordedDamage(shotUnits, source: source);
                        NotifyRallyImpact(boss, previousHp);
                        for (int shot = 1; shot < doubleShot.Count; shot++)
                        {
                            if (await UniTask.Delay(System.TimeSpan.FromSeconds(doubleShot.Interval), cancellationToken: token).SuppressCancellationThrow()) return;
                            if (_fieldPause != null) await _fieldPause.WaitForAttacksAsync(token);
                            if (boss == null || boss.IsDead) break;
                            if (beam != null) beam.Pulse();
                            previousHp = boss.CurrentHp;
                            boss.ApplyRecordedDamage(shotUnits, source: source);
                            NotifyRallyImpact(boss, previousHp);
                        }
                    }
                }

                // 빔이 사그라들 때까지 대형 유지
                if (beam != null && beam.IsPlaying)
                {
                    if (await UniTask.WaitUntil(() => !beam.IsPlaying, cancellationToken: token).SuppressCancellationThrow())
                        return;
                }
            }

            // ④ 귀환 (전체 병렬)
            var returnTasks = new List<UniTask>();
            for (int i = 0; i < snapshot.Length; i++)
            {
                if (IsCurrent(i))
                    returnTasks.Add(snapshot[i].ReturnToHomeAsync(_rallyReturnDuration, token));
            }
            if (returnTasks.Count > 0)
                await UniTask.WhenAll(returnTasks);

            // ⑤ 궤도 재개
            for (int i = 0; i < snapshot.Length; i++) if (IsCurrent(i)) snapshot[i].StartOrbit();
        }
        finally
        {
            if (chiefReservation != null && !chiefCommitted && chiefHacking != null)
                chiefHacking.Refund(chiefTarget, chiefReservation);
            _isRallying = false;
            if (beam != null && token.IsCancellationRequested) beam.Stop();
            if (token.IsCancellationRequested)
                for (int i = 0; i < snapshot.Length; i++) if (IsCurrent(i)) snapshot[i].ResumeHomeOrbit();
        }
    }

    private void NotifyRallyImpact(BossBase boss, decimal previousHp)
    {
        if (boss != null && boss.CurrentHp < previousHp) OnRallyImpact?.Invoke(boss);
    }

    // 보스 반대쪽으로 열린 부채꼴 — 드론이 합류점 뒤에서 앞으로 빔을 모은다.
    // _rallySingleRowMax를 넘으면 안쪽·바깥쪽 두 줄로 나누고, 바깥 줄은 반 칸 엇갈려 겹치지 않게 한다.
    private Vector3 RallySlot(int index, int count, Vector3 center, Vector3 dir)
    {
        bool twoRows = count > _rallySingleRowMax;
        int inner = twoRows ? count / 2 : count;
        int row = twoRows && index >= inner ? 1 : 0;
        int i = row == 0 ? index : index - inner;
        int n = row == 0 ? inner : count - inner;
        float step = n <= 1 ? 0f : 1f / (n - 1);
        float u = n <= 1 ? 0.5f : i * step;
        if (row == 1) u = Mathf.Clamp01(u + step * 0.25f);
        float angle = Mathf.Lerp(-_rallyFanDegrees * 0.5f, _rallyFanDegrees * 0.5f, u);
        float radius = _rallyFanRadius * (twoRows && row == 0 ? _rallyInnerRowRatio : 1f);
        return center + Quaternion.Euler(0f, 0f, angle) * -dir * radius;
    }

    private ChiefConvergeBeamFx PlayConvergeBeam(DroneUnit[] drones, Transform target, int[] lifetimes)
    {
        if (_convergeBeamPrefab == null) return null;
        if (_convergeBeam == null)
        {
            // 부모 스케일 영향을 받지 않게 씬 루트에 둔다 (씬이 끝나면 함께 사라진다)
            _convergeBeam = Instantiate(_convergeBeamPrefab);
            _convergeBeam.UseUnscaledTime = false; // 선택지·일시정지 중에는 함께 멈춘다
        }
        var muzzles = new List<Transform>(drones.Length);
        for (int i = 0; i < drones.Length; i++)
            if (drones[i] != null && drones[i].isActiveAndEnabled && drones[i].PlacementLifetime == lifetimes[i])
                muzzles.Add(drones[i].transform);
        _convergeBeam.Play(muzzles, target);
        return _convergeBeam;
    }

    // ── 테스트 ──────────────────────────────────────────────────────

    [ContextMenu("테스트: 집결 발동")]
    [Button]
    public void TriggerTestRally()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("DroneManager: 플레이 모드에서만 테스트 가능합니다.");
            return;
        }
        ExecuteRallyAsync(_testRallyDamage, this.GetCancellationTokenOnDestroy()).Forget(e => { if (e is not System.OperationCanceledException) UnityEngine.Debug.LogException(e); });
    }

    // ── 초기화 ──────────────────────────────────────────────────────

    protected void Awake()
    {
        _baseFoodPerDrone = _defaultFoodPerDrone;
    }

    // ── 식량 틱 ─────────────────────────────────────────────────────

    private void TickFood(float deltaTime)
    {
        if (deltaTime <= 0f || _currencyManager == null || _fieldPause?.AttacksHeld == true) return;
        bool hasProducer = false;
        bool hasIndividualPayout = false;
        float individualFoodPerDrone = 0f;
        foreach (var producer in _foodProducers)
        {
            if (!IsActiveFoodProducer(producer)) continue;
            producer.TickBatchedFoodPayout(deltaTime);
            hasProducer = true;
            if (!producer.UsesExternalFoodPayout)
            {
                hasIndividualPayout = true;
                individualFoodPerDrone += producer.FoodPerDronePerSecond;
            }
        }
        // Exclude only shares already paid by a producer's batch account.
        // With no active producer, retain the existing default fleet production.
        if (hasProducer && !hasIndividualPayout) return;
        if (!hasProducer) individualFoodPerDrone = _baseFoodPerDrone;
        // 개별 드론의 생산 진행도를 보존한다. 스턴 중 시간은 지급 주기에 포함하지 않는다.
        foreach (var drone in _drones)
        {
            if (!CanProduceFood(drone)) continue;
            _foodProgress.TryGetValue(drone, out float progress);
            progress += deltaTime;
            int ticks = Mathf.FloorToInt(progress);
            _foodProgress[drone] = progress - ticks;
            if (ticks > 0)
            {
                float food = ticks * individualFoodPerDrone * (_totemBuffManager?.FoodAmountMultiplier ?? 1f);
                _currencyManager.AddCurrency(food);
            }
        }
    }
}
