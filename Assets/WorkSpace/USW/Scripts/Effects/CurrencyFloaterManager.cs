using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 재화 생산 시 플로터 소환을 전담하는 매니저.
/// DamageFloaterManager의 로직을 참고하여 독립적으로 구현되었습니다.
/// </summary>
public class CurrencyFloaterManager : MonoBehaviour, ILoadableAsset
{ 
    [VContainer.Inject] private AssetLifecycleManager _assetLifecycle;

    public void Init()
    {
        
    }

    [Header("프리팹 어드레서블 주소")]
    public string currencyTextAddress = "DamageTextPrefab"; // 기본 데미지 프리팹으로 우선 복구

    [Header("UI 생성 위치")]
    [Tooltip("재화 텍스트가 생성될 캔버스(또는 컨테이너) 트랜스폼")]
    public Transform currencyTextContainer;

    [Header("재화 스타일")]
    public DamageFloaterStyle currencyStyle;

    private GameObject _loadedPrefab;

    public bool IsLoaded => _loadedPrefab != null;

    public async UniTask LoadAssetsAsync()
    {
        if (_loadedPrefab == null && !string.IsNullOrEmpty(currencyTextAddress))
        {
            _loadedPrefab = await RM.LoadAsync<GameObject>(currencyTextAddress);
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
    }

    private void OnDestroy()
    {
        _assetLifecycle?.Unload(this);
    }

    /// <summary>
    /// 월드 좌표를 입력받아 현재 컨테이너(Canvas) 설정에 맞춰 재화 플로터를 배치합니다.
    /// </summary>
    /// <param name="worldPosition">생성할 월드 좌표</param>
    /// <param name="floatAmount">표시할 재화 양</param>
    public void SpawnCurrencyText(Vector3 worldPosition, float floatAmount)
    {
        if (floatAmount <= 0) return;
        if (currencyTextContainer == null)
        {
            Debug.LogWarning("[CurrencyFloaterManager] currencyTextContainer가 할당되지 않았습니다.");
            return;
        }

        if (_loadedPrefab == null)
        {
            if (!string.IsNullOrEmpty(currencyTextAddress))
            {
                _loadedPrefab = RM.Load<GameObject>(currencyTextAddress);
            }
        }
        if (_loadedPrefab == null) return;

        // 1. 프리팹 생성 (임시 위치)
        var floaterObj = RM.Instantiate(_loadedPrefab, Vector3.zero, Quaternion.identity, currencyTextContainer, true);
        if (floaterObj != null)
        {
            var floater = floaterObj.GetComponent<CurrencyFloater>();
            if (floater != null)
            {
                // 2. 범용 좌표 설정 (DamageFloaterManager와 동일한 로직)
            RectTransform rect = floater.transform as RectTransform;
            Canvas canvas = currencyTextContainer.GetComponentInParent<Canvas>();

                if (rect != null && canvas != null && canvas.renderMode != RenderMode.WorldSpace)
                {
                    // Screen Space (Overlay / Camera) 모드
                    Camera cam = Camera.main;
                    Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, worldPosition);
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(currencyTextContainer as RectTransform, screenPoint, canvas.worldCamera, out Vector2 localPoint);
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
            // 소수점 아래 값이 있으면 1자리까지 표시, 딱 떨어지는 정수면 정수로 표시
            string text = (floatAmount % 1 == 0) ? $"+{Mathf.FloorToInt(floatAmount)}" : $"+{floatAmount:F1}";
            floater.SetupAndPlay(text, currencyStyle, false);
            }
        }
    }
}
