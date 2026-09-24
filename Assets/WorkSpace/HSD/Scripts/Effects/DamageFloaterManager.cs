using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 데미지 플로터 소환을 전담하는 매니저
/// ExpEffectController와 동일하게 BossManager의 이벤트를 스스로 구독하여
/// 역방향 의존성을 방지하고 완전한 단일 책임을 가집니다.
/// </summary>
public class DamageFloaterManager : MonoBehaviour, ILoadableAsset
{
    [VContainer.Inject] private BossManager _bossManager;
    [VContainer.Inject] private AssetLifecycleManager _assetLifecycle;

    [Header("프리팹 어드레서블 주소")]
    public string damageTextAddress = "DamageTextPrefab";

    [Header("UI 생성 위치")]
    [Tooltip("데미지 텍스트가 생성될 캔버스(또는 컨테이너) 트랜스폼")]
    public Transform damageTextContainer;

    [Header("데미지 스타일")]
    public DamageFloaterStyle normalStyle;
    public DamageFloaterStyle criticalStyle;

    [Header("모아서 띄우기 (식량 플로터 방식, 보스 옆 한 자리)")]
    [Tooltip("끄면 예전처럼 맞은 위치마다 숫자를 흩뿌린다.")]
    [SerializeField] private bool _useReceipt = true;
    [Tooltip("피해를 이 시간(실제 초) 동안 종류별로 합산했다가 숫자 하나로 띄운다. 식량 플로터 기본값 0.4와 같다.")]
    [SerializeField, Min(0.05f)] private float _receiptFlushInterval = 0.4f;
    [Tooltip("숫자가 떠올라 사라지기까지 (실제 초).")]
    [SerializeField, Min(0.1f)] private float _receiptDuration = 0.9f;
    [Tooltip("떠오르는 거리 = 글자 높이 × 이 값.")]
    [SerializeField, Min(0f)] private float _receiptRiseLines = 2.5f;
    [Tooltip("같은 순간 여러 종류(일반/치명타/화상)가 뜰 때 세로 간격 = 글자 높이 × 이 값.")]
    [SerializeField, Min(0.5f)] private float _receiptLineHeight = 1.1f;
    [Tooltip("글자 크기 배율 (데미지 텍스트 프리팹 기준).")]
    [SerializeField, Range(0.2f, 1.5f)] private float _receiptTextScale = 0.6f;
    [Tooltip("동시에 떠 있을 수 있는 숫자 최대 개수. 넘치면 가장 오래된 것부터 재사용.")]
    [SerializeField, Range(1, 12)] private int _receiptMaxLines = 6;
    [Tooltip("보스 스프라이트 오른쪽 끝 기준 화면 픽셀 오프셋. 보스가 화면보다 크면 화면 오른쪽 안쪽으로 붙는다.")]
    [SerializeField] private Vector2 _receiptScreenOffset = new Vector2(24f, 0f);
    [Tooltip("화면 가장자리 여백 (픽셀).")]
    [SerializeField] private float _receiptEdgePadding = 32f;
    [SerializeField] private Color _normalColor = Color.white;
    [SerializeField] private Color _criticalColor = new Color(1f, 0.55f, 0.1f, 1f);
    [SerializeField] private Color _burnColor = new Color(1f, 0.2f, 0.15f, 1f);
    [SerializeField] private float _criticalScale = 1.25f;
    [SerializeField] private float _burnScale = 0.85f;

    private DamageReceiptFeed _receipt;
    
    private BossBase _subscribedBoss;
    private GameObject _loadedPrefab;

    public bool IsLoaded => _loadedPrefab != null;

    public async UniTask LoadAssetsAsync()
    {
        if (_loadedPrefab == null && !string.IsNullOrEmpty(damageTextAddress))
        {
            _loadedPrefab = await RM.LoadAsync<GameObject>(damageTextAddress);
        }
    }

    public void UnloadAssets()
    {
        if (_loadedPrefab != null)
        {
            RM.Unload(_loadedPrefab);
            _loadedPrefab = null;
        }
    }

    private void Start()
    {
        _assetLifecycle?.LoadAsync(this).Forget();

        // 보스 소환 이벤트 구독
        if (_bossManager != null)
        {
            _bossManager.OnBossEntryed += SubscribeBoss;
            
            // 이미 소환된 보스가 있다면 바로 구독
            if (_bossManager.CurrentBoss != null)
            {
                SubscribeBoss(null, null);
            }
        }
    }

    private void OnDestroy()
    {
        _assetLifecycle?.Unload(this);

        if (_bossManager != null)
        {
            _bossManager.OnBossEntryed -= SubscribeBoss;
        }
        UnsubscribeBoss();
    }

    private void SubscribeBoss(BossEntry _, BossEntry _1)
    {
        UnsubscribeBoss();

        _subscribedBoss = _bossManager.CurrentBoss;
        if (_subscribedBoss != null)
        {
            _subscribedBoss.OnDamageDealt += OnBossDamaged;
            EnsureReceipt()?.SetAnchor(_subscribedBoss.transform);
        }
    }

    private void UnsubscribeBoss()
    {
        if (_subscribedBoss != null)
        {
            _subscribedBoss.OnDamageDealt -= OnBossDamaged;
            _subscribedBoss = null;
        }
    }

    private void OnBossDamaged(decimal damage, Vector3? hitPos, BossDamageKind kind)
    {
        if (_subscribedBoss == null) return;
        if (_useReceipt)
        {
            EnsureReceipt()?.Add(damage, kind);
            return;
        }
        
        Vector3 spawnPos = hitPos ?? (_subscribedBoss.transform.position + Vector3.up * 2.5f);
        
        SpawnDamageText(spawnPos, damage, false);
    }

    // 모아서 띄우는 숫자 묶음을 컨테이너 아래에 한 번 만든다. 프리팹이 아직 로드 전이면 null.
    private DamageReceiptFeed EnsureReceipt()
    {
        if (!_useReceipt) return null;
        if (_receipt != null) return _receipt;
        if (_loadedPrefab == null && !string.IsNullOrEmpty(damageTextAddress)) _loadedPrefab = RM.Load<GameObject>(damageTextAddress);
        if (_loadedPrefab == null || damageTextContainer is not RectTransform container) return null;
        var go = new GameObject("DamageReceipt", typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(container, false);
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        _receipt = go.AddComponent<DamageReceiptFeed>();
        _receipt.Initialize(_loadedPrefab, normalStyle, new DamageReceiptFeed.Settings
        {
            MaxLines = _receiptMaxLines,
            FlushInterval = _receiptFlushInterval,
            Duration = _receiptDuration,
            RiseLines = _receiptRiseLines,
            LineHeightFactor = _receiptLineHeight,
            TextScale = _receiptTextScale,
            ScreenOffset = _receiptScreenOffset,
            EdgePadding = _receiptEdgePadding,
            NormalColor = _normalColor,
            CriticalColor = _criticalColor,
            BurnColor = _burnColor,
            CriticalScale = _criticalScale,
            BurnScale = _burnScale,
        }, rect);
        if (_subscribedBoss != null) _receipt.SetAnchor(_subscribedBoss.transform);
        return _receipt;
    }

    /// <summary>
    /// 월드 좌표를 입력받아 현재 컨테이너(Canvas) 설정에 맞춰 적절히 배치합니다.
    /// </summary>
    public void SpawnDamageText(Vector3 worldPosition, decimal damage, bool isCritical = false)
    {
        if (_loadedPrefab == null)
        {
            if (!string.IsNullOrEmpty(damageTextAddress))
            {
                _loadedPrefab = RM.Load<GameObject>(damageTextAddress);
            }
        }
        if (_loadedPrefab == null) return;

        // 1. 프리팹 생성 (임시 위치)
        var floaterObj = RM.Instantiate(_loadedPrefab, Vector3.zero, Quaternion.identity, damageTextContainer, true);
        if (floaterObj != null)
        {
            var floater = floaterObj.GetComponent<DamageFloater>();
            if (floater != null)
            {
                // 2. 범용 좌표 설정
                RectTransform rect = floater.transform as RectTransform;
                Canvas canvas = damageTextContainer.GetComponentInParent<Canvas>();

                if (rect != null && canvas != null && canvas.renderMode != RenderMode.WorldSpace)
                {
                    // Screen Space (Overlay / Camera) 모드
                    Camera cam = Camera.main;
                    Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, worldPosition);
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(damageTextContainer as RectTransform, screenPoint, canvas.worldCamera, out Vector2 localPoint);
                    rect.anchoredPosition = localPoint;
                    
                    Vector3 localPos = rect.localPosition;
                    localPos.z = 0f;
                    rect.localPosition = localPos;
                }
                else
                {
                    // World Space 또는 일반 Transform
                    floater.transform.position = worldPosition;
                }

                // 3. 스타일 적용 및 재생
                DamageFloaterStyle style = isCritical ? criticalStyle : normalStyle;
                floater.SetupAndPlay(damage.ToString(), style, isCritical);
            }
        }
    }
}
