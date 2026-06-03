using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 드론 군단 관리 매니저.
/// 드론 등록/해제, 초당 식량 틱, 드론 버프/보스 디버프 상태,
/// 족장 집결 폭발(ExecuteRallyAsync)을 담당한다.
/// </summary>
public class DroneManager : MonoBehaviour
{
    [VContainer.Inject] private BossManager _bossManager;
    [VContainer.Inject] private CurrencyManager _currencyManager;
    [VContainer.Inject] private TotemBuffManager _totemBuffManager;
    [VContainer.Inject] private GridManager _gridManager;

    private readonly List<DroneUnit> _drones = new();

    // ── 식량 ────────────────────────────────────────────────────────
    [Header("식량 (재화)")]
    [Tooltip("드론 1기당 기본 식량 획득량")]
    [SerializeField] private float _defaultFoodPerDrone = 0.15f;
    private float _baseFoodPerDrone;

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
    [SerializeField] private float _rallySpacing    = 100f;
    [Tooltip("V자 대형 수직 간격 (px)")]
    [SerializeField] private float _rallyVSpacing   = 100f;
    [Tooltip("보스 중심 기준 아래 방향 오프셋 (px)")]
    [SerializeField] private float _rallyBossOffset = 200f;

    [Header("집결 연출")]
    [Tooltip("집결 이동 시간 (초)")]
    [SerializeField] private float _rallyMoveDuration = 0.5f;
    [Tooltip("집결 후 대기(와인드업) 시간 (밀리초)")]
    [SerializeField] private int _rallyWindupMs = 200;
    [Tooltip("원위치 귀환 이동 시간 (초)")]
    [SerializeField] private float _rallyReturnDuration = 0.4f;

    [Header("전기 이펙트")]
    [Tooltip("전기 선 렌더러 머티리얼")]
    [SerializeField] private Material _electricLineMaterial;
    [Tooltip("전기 선 굵기")]
    [SerializeField] private float _electricLineWidth = 5f;
    [Tooltip("지터링 세기 (픽셀)")]
    [SerializeField] private float _electricJitterAmount = 2f;
    [SerializeField] private Color _electricColorA = Color.cyan;
    [SerializeField] private Color _electricColorB = Color.white;

    [Header("테스트")]
    [SerializeField] private float _testRallyDamage = 50f;

    // ── 드론 등록 ───────────────────────────────────────────────────

    public void RegisterDrone(DroneUnit drone)
    {
        if (!_drones.Contains(drone))
            _drones.Add(drone);
    }

    public void UnregisterDrone(DroneUnit drone) => _drones.Remove(drone);

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

    public void ApplyBossDebuff(float multiplier, float duration)
    {
        _bossDebuffMult = multiplier;
        _debuffEndTime  = Time.time + duration;
    }

    // ── 족장 집결 폭발 ──────────────────────────────────────────────

    /// <summary>
    /// 1) 모든 드론을 보스 아래 V자 대형으로 집결
    /// 2) 드론 간 전기 효과(LineRenderer) 연결
    /// 3) 중앙 드론만 사격 + 전체 데미지
    /// 4) 원래 위치 귀환 후 궤도 재개
    /// </summary>
    public async UniTask ExecuteRallyAsync(float damagePerDrone, CancellationToken token)
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
            else
            {
                rallyCenter = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
            }

            // 전기 효과(UI Image) 생성
            lrObj = new GameObject("DroneElectricLines");
            Transform parentTransform = snapshot[0] != null ? snapshot[0].transform.parent : transform;
            lrObj.transform.SetParent(parentTransform, false);
            lrObj.transform.SetAsLastSibling();
            
            int lineCount = Mathf.Max(0, snapshot.Length - 1);
            var lineImages = new UnityEngine.UI.Image[lineCount];
            for (int i = 0; i < lineCount; i++)
            {
                var lineGo = new GameObject($"Line_{i}");
                lineGo.transform.SetParent(lrObj.transform, false);
                var img = lineGo.AddComponent<UnityEngine.UI.Image>();
                img.material = _electricLineMaterial;
                img.raycastTarget = false;
                var rt = img.rectTransform;
                rt.pivot = new Vector2(0f, 0.5f);
                lineImages[i] = img;
            }

            lrCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            UpdateLineRendererAsync(lrObj, lineImages, snapshot, lrCts.Token).Forget();

            // ① 집결 이동 (전체 병렬) - V자 배치
            var moveTasks = new List<UniTask>();
            // V자 대형 전체를 수직으로 '정중앙'에 맞추기 위해, 대형 전체 높이의 절반만큼 아래로 오프셋
            float vHeight = ((snapshot.Length - 1) / 2f) * _rallyVSpacing;
            float verticalOffset = -vHeight * 0.5f;

            for (int i = 0; i < snapshot.Length; i++)
            {
                if (snapshot[i] == null) continue;
                float distFromCenter = Mathf.Abs(i - (snapshot.Length - 1) / 2f);
                float x = rallyCenter.x + (i - (snapshot.Length - 1) / 2f) * _rallySpacing;
                float y = rallyCenter.y + verticalOffset + distFromCenter * _rallyVSpacing;
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
                    SpawnLaserBeamAsync(snapshot[centerIndex].transform.position, boss.transform.position, beamParent).Forget();
                }

                boss.TakeDamage(Mathf.RoundToInt(snapshot.Length * damagePerDrone));
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

    private async UniTaskVoid UpdateLineRendererAsync(GameObject container, UnityEngine.UI.Image[] lineImages, DroneUnit[] drones, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested && container != null)
            {
                for (int i = 0; i < lineImages.Length; i++)
                {
                    if (drones[i] != null && drones[i+1] != null)
                    {
                        if (!lineImages[i].enabled) lineImages[i].enabled = true;
                        
                        Vector3 posA = drones[i].transform.position;
                        Vector3 posB = drones[i+1].transform.position;
                        
                        if (UnityEngine.Random.value > 0.3f)
                        {
                            posA += new Vector3(UnityEngine.Random.Range(-_electricJitterAmount, _electricJitterAmount), 
                                               UnityEngine.Random.Range(-_electricJitterAmount, _electricJitterAmount), 0f);
                            posB += new Vector3(UnityEngine.Random.Range(-_electricJitterAmount, _electricJitterAmount), 
                                               UnityEngine.Random.Range(-_electricJitterAmount, _electricJitterAmount), 0f);
                        }
                        
                        var rt = lineImages[i].rectTransform;
                        Vector3 localA = rt.parent.InverseTransformPoint(posA);
                        Vector3 localB = rt.parent.InverseTransformPoint(posB);
                        
                        rt.localPosition = localA;
                        Vector3 localDir = localB - localA;
                        rt.sizeDelta = new Vector2(localDir.magnitude, _electricLineWidth);
                        float angle = Mathf.Atan2(localDir.y, localDir.x) * Mathf.Rad2Deg;
                        rt.localRotation = Quaternion.Euler(0, 0, angle);

                        lineImages[i].color = UnityEngine.Random.value > 0.5f ? _electricColorA : _electricColorB;
                    }
                    else
                    {
                        lineImages[i].enabled = false;
                    }
                }
                
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }
        catch (System.OperationCanceledException) { }
    }

    private async UniTaskVoid SpawnLaserBeamAsync(Vector3 startPos, Vector3 endPos, Transform parent)
    {
        var go = new GameObject("RallyLaserBeam");
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<UnityEngine.UI.Image>();
        img.material = _electricLineMaterial; 
        img.color = Color.yellow; // 강렬한 빔 색상
        img.raycastTarget = false;
        
        var rt = img.rectTransform;
        rt.pivot = new Vector2(0f, 0.5f);
        
        Vector3 localA = rt.parent.InverseTransformPoint(startPos);
        Vector3 localB = rt.parent.InverseTransformPoint(endPos);
        rt.localPosition = localA;
        Vector3 localDir = localB - localA;
        
        // 빔은 전기 이펙트보다 두껍게 연출
        rt.sizeDelta = new Vector2(localDir.magnitude, _electricLineWidth * 4f); 
        float angle = Mathf.Atan2(localDir.y, localDir.x) * Mathf.Rad2Deg;
        rt.localRotation = Quaternion.Euler(0, 0, angle);

        // 0.25초 동안 서서히 페이드아웃
        float duration = 0.25f;
        float elapsed = 0f;
        while (elapsed < duration && img != null)
        {
            elapsed += Time.deltaTime;
            float alpha = 1f - (elapsed / duration);
            img.color = new Color(Color.yellow.r, Color.yellow.g, Color.yellow.b, alpha);
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
                float food = count * _baseFoodPerDrone * (_totemBuffManager?.FoodAmountMultiplier ?? 1f);
                _currencyManager.AddCurrency(food);
            }
        }
    }
}
