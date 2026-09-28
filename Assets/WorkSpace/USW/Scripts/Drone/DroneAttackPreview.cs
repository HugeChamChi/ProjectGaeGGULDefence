using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 테스트 씬 전용 — 오너 유닛 없이 배치된 드론들이 타겟을 향해 번갈아 연출용 투사체를 쏘게 한다.
/// 데미지는 주지 않으며, 드론 공격 프레임/레이저/히트 이펙트 확인용이다.
/// </summary>
public class DroneAttackPreview : MonoBehaviour
{
    [SerializeField] private DroneUnit[] _drones;
    [Tooltip("투사체가 날아갈 중심 위치.")]
    [SerializeField] private Transform _target;
    [Tooltip("타겟 주변 랜덤 착탄 반경(월드 단위).")]
    [SerializeField, Min(0f)] private float _targetRadius = 0.4f;
    [Tooltip("드론 한 마리당 발사 간격(초).")]
    [SerializeField, Min(0.05f)] private float _fireInterval = 0.9f;
    [Tooltip("드론 사이 발사 시차(초).")]
    [SerializeField, Min(0f)] private float _stagger = 0.3f;

    [Header("자폭 드론 (베탕 스킬 연출)")]
    [Tooltip("비워두면 자폭 드론 프리뷰를 끈다.")]
    [SerializeField] private SelfDestructDrone _selfDestructPrefab;
    [Tooltip("자폭 드론을 내보낼 위치 (베탕 본체 자리).")]
    [SerializeField] private Transform _selfDestructOrigin;
    [Tooltip("한 번에 내보낼 자폭 드론 수.")]
    [SerializeField, Min(1)] private int _selfDestructCount = 4;
    [Tooltip("자폭 드론 발사 주기(초).")]
    [SerializeField, Min(0.5f)] private float _selfDestructInterval = 3f;
    [Tooltip("켜져 있으면 주기마다 자동 발사. 꺼두면 버튼으로만 발사.")]
    [SerializeField] private bool _autoSelfDestruct = true;

    [Header("폭발 버전 비교 (화면 버튼)")]
    [SerializeField] private GameObject _explosionWithSmoke;
    [SerializeField] private GameObject _explosionNoSmoke;
    [SerializeField] private bool _showButtons = true;

    private bool _useSmoke = true;

    private void Start()
    {
        var token = this.GetCancellationTokenOnDestroy();
        for (int i = 0; i < _drones.Length; i++)
            FireLoopAsync(_drones[i], i * _stagger, token).Forget();
        if (_selfDestructPrefab != null && _target != null)
            SelfDestructLoopAsync(token).Forget();
    }

    private async UniTaskVoid SelfDestructLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (await UniTask.Delay(Mathf.RoundToInt(_selfDestructInterval * 1000f), cancellationToken: token).SuppressCancellationThrow())
                return;

            if (_autoSelfDestruct) FireSelfDestructNow();
        }
    }

    /// <summary>자폭 드론 묶음을 즉시 한 번 내보낸다.</summary>
    public void FireSelfDestructNow()
    {
        if (_selfDestructPrefab == null || _target == null) return;
        Vector3 origin = _selfDestructOrigin != null ? _selfDestructOrigin.position : transform.position;
        for (int i = 0; i < _selfDestructCount; i++)
        {
            var obj = RM.Instantiate(_selfDestructPrefab.gameObject, origin + (Vector3)(Random.insideUnitCircle * 0.3f), Quaternion.identity, null, true);
            var bomb = obj != null ? obj.GetComponent<SelfDestructDrone>() : null;
            if (bomb != null)
                bomb.InitializePreview(_target.position + (Vector3)(Random.insideUnitCircle * _targetRadius),
                    _useSmoke ? _explosionWithSmoke : _explosionNoSmoke);
        }
    }

    private async UniTaskVoid FireLoopAsync(DroneUnit drone, float startDelay, CancellationToken token)
    {
        if (drone == null || _target == null) return;
        if (await UniTask.Delay(Mathf.RoundToInt(startDelay * 1000f), cancellationToken: token).SuppressCancellationThrow())
            return;

        while (!token.IsCancellationRequested && drone != null)
        {
            Vector3 targetPos = _target.position + (Vector3)(Random.insideUnitCircle * _targetRadius);
            drone.FireVisualShot(targetPos);

            if (await UniTask.Delay(Mathf.RoundToInt(_fireInterval * 1000f), cancellationToken: token).SuppressCancellationThrow())
                return;
        }
    }

    // 테스트 전용 화면 버튼 — 연기 있는/없는 폭발을 골라 바로 발사한다.
    private void OnGUI()
    {
        if (!_showButtons || _selfDestructPrefab == null) return;

        float scale = Screen.width / 540f;
        var style = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(16 * scale) };
        float w = 150f * scale, h = 44f * scale, pad = 8f * scale;
        float y = Screen.height * 0.42f;

        if (GUI.Button(new Rect(pad, y, w, h), "연기 O 자폭", style)) { _useSmoke = true; FireSelfDestructNow(); }
        if (GUI.Button(new Rect(pad, y + h + pad, w, h), "연기 X 자폭", style)) { _useSmoke = false; FireSelfDestructNow(); }
        string auto = _autoSelfDestruct ? "자동 반복 ON" : "자동 반복 OFF";
        if (GUI.Button(new Rect(pad, y + (h + pad) * 2f, w, h), auto, style)) _autoSelfDestruct = !_autoSelfDestruct;
        if (GUI.Button(new Rect(pad, y + (h + pad) * 3f, w, h), "버튼 숨기기", style)) _showButtons = false;
    }
}
