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
    [VContainer.Inject] private BossManager _bossManager;
    [VContainer.Inject] private CurrencyManager _currencyManager;
    [VContainer.Inject] private TotemBuffManager _totemBuffManager;
    [VContainer.Inject] private GridManager _gridManager;
    [VContainer.Inject] private LevelUpManager _levelUpManager;
    [VContainer.Inject] private GameManager _gameManager;
    private float _betanNormalTimer, _betanEpicTimer;
    private DroneSelectionEffect _normalEffect, _epicEffect;
    private readonly List<Drone_Betan> _betans = new();

    private void Update()
    {
        if (_gameManager != null && _gameManager.CurrentState != GameManager.GameState.Playing) return;
        TickSelections(Time.deltaTime);
    }

    /// <summary>전투 시간에 따라 베탕 추가 소환을 실행한다. 에픽은 필드 합계 상한이다.</summary>
    public void TickSelections(float deltaTime)
    {
        var state = _levelUpManager?.DroneSelections;
        var normal = state?.Get(DroneSelectionKind.BetanPeriodicBomb);
        var epic = state?.Get(DroneSelectionKind.BetanFleetBomb);
        if (_normalEffect != normal) { _normalEffect = normal; _betanNormalTimer = 0; }
        if (_epicEffect != epic) { _epicEffect = epic; _betanEpicTimer = 0; }
        if (deltaTime <= 0 || (normal == null && epic == null)) return;
        _betans.Clear();
        int fleetCount = 0;
        foreach (var drone in _drones)
        {
            if (drone == null || !drone.isActiveAndEnabled || drone.Owner is not Drone_Betan betan
                || !betan.isActiveAndEnabled || betan.currentCell == null) continue;
            fleetCount++;
            if (!_betans.Contains(betan)) _betans.Add(betan);
        }
        if (fleetCount == 0) { _betanNormalTimer = _betanEpicTimer = 0; return; }
        if (normal != null && normal.Interval > 0)
        {
            _betanNormalTimer += deltaTime;
            while (_betanNormalTimer >= normal.Interval)
            {
                _betanNormalTimer -= normal.Interval;
                foreach (var betan in _betans) betan.SpawnSelfDestructDrones(normal.Count);
            }
        }
        if (epic != null && epic.Interval > 0)
        {
            _betanEpicTimer += deltaTime;
            while (_betanEpicTimer >= epic.Interval)
            {
                _betanEpicTimer -= epic.Interval;
                int remaining = Mathf.Min(fleetCount, epic.Count);
                foreach (var drone in _drones)
                {
                    if (remaining == 0) break;
                    if (drone != null && drone.isActiveAndEnabled && drone.Owner is Drone_Betan betan && _betans.Contains(betan))
                    { betan.SpawnSelfDestructDrones(1); remaining--; }
                }
            }
        }
    }

    /// <summary>알팡 액티브에서 배치된 감망의 버프를 각 한 번 발동한다.</summary>
    public virtual void ApplyEmergencyBuffs()
    {
        if (_levelUpManager?.DroneSelections.Get(DroneSelectionKind.GammanEmergency) == null) return;
        var owners = new HashSet<Drone_Gamman>();
        foreach (var drone in _drones)
            if (drone != null && drone.Owner is Drone_Gamman gamman && gamman.currentCell != null && gamman.isActiveAndEnabled)
                owners.Add(gamman);
        foreach (var gamman in owners) gamman.ApplyDroneBuff();
    }

    private readonly List<DroneUnit> _drones = new();

    // ── 식량 ────────────────────────────────────────────────────────
    [Header("식량 (재화)")]
    [Tooltip("드론 1기당 기본 식량 획득량")]
    [SerializeField] private float _defaultFoodPerDrone = 0.15f;
    private float _baseFoodPerDrone;

    public event System.Action OnDroneCountChanged;

    public int DroneCount => _drones.Count;
    public IReadOnlyList<DroneUnit> Drones => _drones;
    private readonly List<Drone_Zeltan> _foodProducers = new();
    /// <summary>가장 최근 배치된 유효 젤탕의 패시브를 사용하고, 없으면 기본 생산량을 사용한다.</summary>
    public float BaseFoodPerDrone
    {
        get
        {
            for (int i = _foodProducers.Count - 1; i >= 0; i--)
                if (_foodProducers[i] != null && _foodProducers[i].isActiveAndEnabled
                    && _foodProducers[i].currentCell != null)
                    return _foodProducers[i].FoodPerDronePerSecond;
            return _baseFoodPerDrone;
        }
    }

    /// <summary>젤탕을 군단 식량 생산자로 등록한다.</summary>
    public void RegisterFoodProducer(Drone_Zeltan producer)
    {
        if (producer != null && !_foodProducers.Contains(producer)) _foodProducers.Add(producer);
    }

    /// <summary>제거된 젤탕만 해제하며 다른 젤탕의 패시브는 유지한다.</summary>
    public void UnregisterFoodProducer(Drone_Zeltan producer) => _foodProducers.Remove(producer);

    /// <summary>실제 자폭마다 배치된 베탕의 충전을 앞당긴다. 완충을 넘는 시간은 저장하지 않는다.</summary>
    public void NotifySelfDestructExplosion()
    {
        float seconds = _levelUpManager?.DroneSelections.Get(DroneSelectionKind.BetanRepairKit)?.Value ?? 0f;
        if (seconds <= 0 || _gridManager == null) return;
        foreach (var cell in _gridManager.GetOccupiedCells())
            if (cell.OccupyingUnit is Drone_Betan betan && betan.isActiveAndEnabled && betan.Combat != null)
                betan.Combat.SkillTimer = Mathf.Min(betan.GetCurrentSkillInterval(), betan.Combat.SkillTimer + seconds);
    }

    /// <summary>황금 모노클은 알팡 액티브에 확정 치명타 배율을 적용한다.</summary>
    public decimal GetRallyDamage(int droneCount, float damagePerDrone)
    {
        decimal damage = Mathf.RoundToInt(droneCount * damagePerDrone);
        var monocle = _levelUpManager?.DroneSelections.Get(DroneSelectionKind.AlphanGoldenMonocle);
        return monocle == null ? damage : damage * (decimal)monocle.Value;
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

    [Header("드론 스폰 배치 간격 (늘어진 V자)")]
    [Tooltip("하단 드론의 X 간격 (좁게)")]
    public float droneSpreadXLower = 0.25f; 
    [Tooltip("하단 드론의 Y 위치 (아래로 깊게)")]
    public float droneSpreadYLower = 0.2f;   // (수정) 발밑으로 꺼지지 않게 살짝 위로 올림
    [Tooltip("상단 드론의 X 간격 (넓게)")]
    public float droneSpreadXUpper = 0.8f; 
    [Tooltip("상단 드론의 Y 위치 (위로)")]
    public float droneSpreadYUpper = 0.7f;   // (수정) 본체 머리 부근에 위치하도록 올림

    [Header("집결 대형 설정")]
    [Tooltip("드론 간 기본 가로 간격 (Unit)")]
    [SerializeField] private float _rallySpacing   = 0.2f;
    [Tooltip("드론 간 기본 세로 간격 (Unit)")]
    [SerializeField] private float _rallyVSpacing   = 0.2f;
    [Tooltip("집결 대형의 최대 가로 폭 (Unit) (넘으면 간격 자동 축소)")]
    [SerializeField] private float _rallyMaxWidth   = 2.0f;
    [Tooltip("집결 대형의 최대 세로 폭 (Unit) (넘으면 간격 자동 축소)")]
    [SerializeField] private float _rallyMaxHeight  = 1.5f;

    [Header("집결 연출")]
    [Tooltip("집결 이동 시간 (초)")]
    [SerializeField] private float _rallyMoveDuration = 0.5f;
    [Tooltip("집결 후 대기(와인드업) 시간 (밀리초)")]
    [SerializeField] private int _rallyWindupMs = 200;
    [Tooltip("원위치 귀환 이동 시간 (초)")]
    [SerializeField] private float _rallyReturnDuration = 0.4f;

    [Header("전기 이펙트")]
    [Tooltip("전기 선 프리팹 (옵션, LineRenderer 포함된 GameObject)")]
    [SerializeField] private LineRenderer _electricLinePrefab;
    [Tooltip("레이저 빔 프리팹 (옵션, LineRenderer 포함된 GameObject)")]
    [SerializeField] private LineRenderer _laserBeamPrefab;

    [Tooltip("전기 선 렌더러 머티리얼 (프리팹 미사용 시)")]
    [SerializeField] private Material _electricLineMaterial;
    [Tooltip("전기 선 굵기 (Unit)")]
    [SerializeField] private float _electricLineWidth = 0.02f;
    [SerializeField] private Color _electricColorA = Color.cyan;
    [SerializeField] private Color _electricColorB = Color.white;

    [Header("테스트")]
    [SerializeField] private float _testRallyDamage = 50f;

    // ── 드론 등록 ───────────────────────────────────────────────────

    public void RegisterDrone(DroneUnit drone)
    {
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
    /// 1) 모든 드론을 보스 아래 V자 대형으로 집결
    /// 2) 드론 간 전기 효과(LineRenderer) 연결
    /// 3) 중앙 드론만 사격 + 전체 데미지
    /// 4) 원래 위치 귀환 후 궤도 재개
    /// </summary>
    public virtual async UniTask ExecuteRallyAsync(float damagePerDrone, CancellationToken token, DroneSelectionEffect doubleShot = null)
    {
        if (_isRallying || _drones.Count == 0) return;
        _isRallying = true;

        GameObject lrObj = null;
        CancellationTokenSource lrCts = null;

        try
        {
            var snapshot = _drones.ToArray();

            // 집결 위치 계산 — 그리드 기하학적 정중앙 기준
            Vector3 rallyCenter = Vector3.zero;
            if (_gridManager != null)
            {
                rallyCenter = _gridManager.GetAbsoluteCenterPosition();
            }

            // 전기 효과(LineRenderer) 생성
            lrObj = new GameObject("DroneElectricLines");
            lrObj.transform.position = Vector3.zero;
            
            int lineCount = Mathf.Max(0, snapshot.Length - 1);
            var lineRenderers = new LineRenderer[lineCount];
            for (int i = 0; i < lineCount; i++)
            {
                LineRenderer lr;
                if (_electricLinePrefab != null)
                {
                    lr = Instantiate(_electricLinePrefab, lrObj.transform);
                    lr.useWorldSpace = true;
                    lr.positionCount = 2;
                }
                else
                {
                    var lineGo = new GameObject($"Line_{i}");
                    lineGo.transform.SetParent(lrObj.transform, false);
                    lr = lineGo.AddComponent<LineRenderer>();
                    lr.material = _electricLineMaterial;
                    lr.useWorldSpace = true;
                    lr.positionCount = 2;
                    lr.startWidth = _electricLineWidth;
                    lr.endWidth = _electricLineWidth;
                    lr.sortingOrder = 50; // Grid나 유닛보다 위에 노출
                }
                lineRenderers[i] = lr;
            }

            lrCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            UpdateLineRendererAsync(lrObj, lineRenderers, snapshot, lrCts.Token).Forget();

            // ① 집결 이동 (전체 병렬) - V자 배치
            var moveTasks = new List<UniTask>();
            
            float actualSpacing = _rallySpacing;
            float actualVSpacing = _rallyVSpacing;
            int count = snapshot.Length;

            if (count > 1)
            {
                // 최대 폭 제한에 따른 간격 압축
                float totalW = (count - 1) * _rallySpacing;
                if (totalW > _rallyMaxWidth) actualSpacing = _rallyMaxWidth / (count - 1);
                
                float totalH = ((count - 1) / 2f) * _rallyVSpacing;
                if (totalH > _rallyMaxHeight) actualVSpacing = _rallyMaxHeight / ((count - 1) / 2f);
            }

            // V자 대형 전체를 수직으로 '정중앙'에 맞추기 위해, 대형 전체 높이의 절반만큼 아래로 오프셋
            float vHeight = ((count - 1) / 2f) * actualVSpacing;
            float verticalOffset = -vHeight * 0.5f;

            for (int i = 0; i < count; i++)
            {
                if (snapshot[i] == null) continue;
                float distFromCenter = Mathf.Abs(i - (count - 1) / 2f);
                float x = rallyCenter.x + (i - (count - 1) / 2f) * actualSpacing;
                float y = rallyCenter.y + verticalOffset + distFromCenter * actualVSpacing;
                var pos = new Vector3(x, y, 0f);
                moveTasks.Add(snapshot[i].MoveToAsync(pos, _rallyMoveDuration, token));
            }
            if (moveTasks.Count > 0)
                await UniTask.WhenAll(moveTasks);

            // ② 짧은 와인드업
            if (await UniTask.Delay(_rallyWindupMs, cancellationToken: token).SuppressCancellationThrow())
                return;

            // ③ 중앙 드론 사격 + 피해
            var boss = _bossManager?.CurrentBoss;
            if (boss != null && !boss.IsDead)
            {
                int centerIndex = snapshot.Length / 2;
                if (snapshot[centerIndex] != null)
                {
                    snapshot[centerIndex].FireRallyShot();
                    
                    // 중앙 빔 연출 (전기 선 부모 활용)
                    Transform beamParent = snapshot[0] != null ? snapshot[0].transform.parent : transform;
                    SpawnLaserBeamAsync(rallyCenter, boss.transform.position, beamParent).Forget();
                }

                decimal baseDamage = GetRallyDamage(snapshot.Length, damagePerDrone);
                if (doubleShot == null) boss.TakeDamage(baseDamage);
                else
                {
                    long finalUnits = boss.CalculateFinalDamageUnits(baseDamage);
                    long shotUnits = (long)System.Math.Round(finalUnits * (decimal)doubleShot.Value, System.MidpointRounding.AwayFromZero);
                    boss.ApplyRecordedDamage(shotUnits);
                    for (int shot = 1; shot < doubleShot.Count; shot++)
                    {
                        if (await UniTask.Delay(System.TimeSpan.FromSeconds(doubleShot.Interval), cancellationToken: token).SuppressCancellationThrow()) return;
                        if (boss == null || boss.IsDead) break;
                        if (snapshot[centerIndex] != null) snapshot[centerIndex].FireRallyShot();
                        SpawnLaserBeamAsync(rallyCenter, boss.transform.position, transform).Forget();
                        boss.ApplyRecordedDamage(shotUnits);
                    }
                }
            }

            // ④ 귀환 전에 전기 이펙트(라인) 제거
            lrCts?.Cancel();
            if (lrObj != null)
            {
                Destroy(lrObj);
                lrObj = null;
            }

            // ⑤ 귀환 (전체 병렬)
            var returnTasks = new List<UniTask>();
            for (int i = 0; i < snapshot.Length; i++)
            {
                if (snapshot[i] != null)
                    returnTasks.Add(snapshot[i].ReturnToHomeAsync(_rallyReturnDuration, token));
            }
            if (returnTasks.Count > 0)
                await UniTask.WhenAll(returnTasks);

            // ⑥ 궤도 재개
            foreach (var d in snapshot)
            {
                if (d != null)
                    d.StartOrbit();
            }
        }
        finally
        {
            _isRallying = false;
            
            lrCts?.Cancel();
            lrCts?.Dispose();
            if (lrObj != null)
                Destroy(lrObj);
        }
    }

    private async UniTaskVoid UpdateLineRendererAsync(GameObject container, LineRenderer[] lineRenderers, DroneUnit[] drones, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested && container != null)
            {
                for (int i = 0; i < lineRenderers.Length; i++)
                {
                    if (drones[i] != null && drones[i+1] != null)
                    {
                        if (!lineRenderers[i].enabled) lineRenderers[i].enabled = true;
                        
                        Vector3 posA = drones[i].transform.position;
                        Vector3 posB = drones[i+1].transform.position;
                        
                        lineRenderers[i].SetPosition(0, posA);
                        lineRenderers[i].SetPosition(1, posB);

                        Color c = UnityEngine.Random.value > 0.5f ? _electricColorA : _electricColorB;
                        lineRenderers[i].startColor = c;
                        lineRenderers[i].endColor = c;
                    }
                    else
                    {
                        lineRenderers[i].enabled = false;
                    }
                }
                
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }
        catch (System.OperationCanceledException) { }
    }

    private async UniTaskVoid SpawnLaserBeamAsync(Vector3 startPos, Vector3 endPos, Transform parent)
    {
        LineRenderer lr;
        GameObject go;
        Color baseColor = Color.yellow;
        
        if (_laserBeamPrefab != null)
        {
            lr = Instantiate(_laserBeamPrefab, parent);
            go = lr.gameObject;
            baseColor = lr.startColor;
            lr.useWorldSpace = true;
            lr.positionCount = 2;
        }
        else
        {
            go = new GameObject("RallyLaserBeam");
            go.transform.SetParent(parent, false);
            lr = go.AddComponent<LineRenderer>();
            lr.material = _electricLineMaterial; 
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.startWidth = _electricLineWidth * 2f;
            lr.endWidth = _electricLineWidth * 2f;
            lr.sortingOrder = 55;
            
            lr.startColor = baseColor;
            lr.endColor = baseColor;
        }
        
        lr.SetPosition(0, startPos);
        lr.SetPosition(1, endPos);

        // 0.25초 동안 서서히 페이드아웃
        float duration = 0.25f;
        float elapsed = 0f;
        while (elapsed < duration && lr != null)
        {
            elapsed += Time.deltaTime;
            float alpha = 1f - (elapsed / duration);
            Color fadeColor = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
            lr.startColor = fadeColor;
            lr.endColor = fadeColor;
            await UniTask.Yield(PlayerLoopTiming.Update);
        }
        
        if (go != null) Destroy(go);
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
        FoodTickAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    // ── 식량 틱 ─────────────────────────────────────────────────────

    private async UniTaskVoid FoodTickAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await UniTask.Delay(1000, cancellationToken: token);

            int count = _drones.Count;
            if (count > 0 && _currencyManager != null)
            {
                float chieftainFood = 0f;
                var lu = Object.FindFirstObjectByType<LevelUpManager>();
                if (lu != null) chieftainFood = lu.ChieftainFoodProductionBonus;

                float food = count * BaseFoodPerDrone * ((_totemBuffManager?.FoodAmountMultiplier ?? 1f) + chieftainFood);
                _currencyManager.AddCurrency(food);
            }
        }
    }
}
