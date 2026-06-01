using UnityEngine;

/// <summary>
/// 데미지 플로터 소환을 전담하는 매니저
/// ExpEffectController와 동일하게 BossManager의 이벤트를 스스로 구독하여
/// 역방향 의존성을 방지하고 완전한 단일 책임을 가집니다.
/// </summary>
public class DamageFloaterManager : MonoBehaviour
{
    [VContainer.Inject] private BossManager _bossManager;

    [Header("프리팹 어드레서블 주소")]
    public string damageTextAddress = "DamageTextPrefab";

    [Header("UI 생성 위치")]
    [Tooltip("데미지 텍스트가 생성될 캔버스(또는 컨테이너) 트랜스폼")]
    public Transform damageTextContainer;

    [Header("데미지 스타일")]
    public DamageFloaterStyle normalStyle;
    public DamageFloaterStyle criticalStyle;
    
    private BossBase _subscribedBoss;

    private void Start()
    {
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

    private void OnBossDamaged(int damage)
    {
        if (_subscribedBoss == null) return;
        
        // 보스 머리 위 월드 좌표 계산
        Vector3 spawnPos = _subscribedBoss.transform.position + Vector3.up * 2.5f;
        
        SpawnDamageText(spawnPos, damage, false);
    }

    /// <summary>
    /// 월드 좌표를 입력받아 현재 컨테이너(Canvas) 설정에 맞춰 적절히 배치합니다.
    /// </summary>
    public void SpawnDamageText(Vector3 worldPosition, int damage, bool isCritical = false)
    {
        // 1. 프리팹 생성 (임시 위치)
        var floater = RM.Instantiate<DamageFloater>(damageTextAddress, Vector3.zero, damageTextContainer, true);

        if (floater != null)
        {
            // 2. 범용 좌표 설정
            RectTransform rect = floater.transform as RectTransform;
            Canvas canvas = damageTextContainer.GetComponentInParent<Canvas>();

            if (rect != null && canvas != null && canvas.renderMode != RenderMode.WorldSpace)
            {
                // Screen Space (Overlay / Camera) 모드
                Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, worldPosition);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(damageTextContainer as RectTransform, screenPoint, canvas.worldCamera, out Vector2 localPoint);
                rect.anchoredPosition = localPoint;
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
