using UnityEngine;

/// <summary>
/// 재화 생산 시 플로터 소환을 전담하는 매니저.
/// DamageFloaterManager의 로직을 참고하여 독립적으로 구현되었습니다.
/// </summary>
public class CurrencyFloaterManager : MonoBehaviour
{ 
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

    /// <summary>
    /// 월드 좌표를 입력받아 현재 컨테이너(Canvas) 설정에 맞춰 재화 플로터를 배치합니다.
    /// </summary>
    /// <param name="worldPosition">생성할 월드 좌표</param>
    /// <param name="amount">표시할 재화 양</param>
    public void SpawnCurrencyText(Vector3 worldPosition, float amount)
    {
        if (amount <= 0) return;
        if (currencyTextContainer == null)
        {
            Debug.LogWarning("[CurrencyFloaterManager] currencyTextContainer가 할당되지 않았습니다.");
            return;
        }

        // 1. 프리팹 생성 (임시 위치)
        var floater = RM.Instantiate<CurrencyFloater>(currencyTextAddress, Vector3.zero, currencyTextContainer, true);

        if (floater != null)
        {
            // 2. 범용 좌표 설정 (DamageFloaterManager와 동일한 로직)
            RectTransform rect = floater.transform as RectTransform;
            Canvas canvas = currencyTextContainer.GetComponentInParent<Canvas>();

            if (rect != null && canvas != null && canvas.renderMode != RenderMode.WorldSpace)
            {
                // Screen Space (Overlay / Camera) 모드
                Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, worldPosition);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(currencyTextContainer as RectTransform, screenPoint, canvas.worldCamera, out Vector2 localPoint);
                rect.anchoredPosition = localPoint;
            }
            else
            {
                // World Space 또는 일반 Transform
                floater.transform.position = worldPosition;
            }

            // 3. 스타일 적용 및 재생
            // 재화 양이 작을 경우(1 미만) 소수점 첫째 자리까지 표시, 그 외에는 정수로 표시
            string text = amount < 1f ? $"+{amount:F1}" : $"+{Mathf.FloorToInt(amount)}";
            floater.SetupAndPlay(text, currencyStyle, false);
        }
    }
}
