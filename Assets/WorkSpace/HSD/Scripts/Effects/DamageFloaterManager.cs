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
            _subscribedBoss.OnDamaged += OnBossDamaged;
        }
    }

    private void UnsubscribeBoss()
    {
        if (_subscribedBoss != null)
        {
            _subscribedBoss.OnDamaged -= OnBossDamaged;
            _subscribedBoss = null;
        }
    }

    private void OnBossDamaged(decimal damage, Vector3? hitPos)
    {
        if (_subscribedBoss == null) return;
        
        Vector3 spawnPos = hitPos ?? (_subscribedBoss.transform.position + Vector3.up * 2.5f);
        
        SpawnDamageText(spawnPos, damage, false);
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
