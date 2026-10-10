using UnityEngine;

/// <summary>
/// 부착된 RectTransform을 Screen.safeArea에 맞게 자동 조정.
/// SafeAreaRoot 오브젝트에 단 하나만 부착한다.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SafeAreaApplier : MonoBehaviour
{
    private RectTransform _rect;
    private Rect _lastSafeArea = Rect.zero;
    private Vector2Int _lastScreenSize = Vector2Int.zero;

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();

        Apply();
    }

    private void Update()
    {
        // SafeArea 또는 화면 크기 변경 시만 재적용 (기기 회전 대응)
        if (Screen.safeArea != _lastSafeArea ||
            Screen.width != _lastScreenSize.x ||
            Screen.height != _lastScreenSize.y)
        {
            Apply();
        }
    }

    private void Apply()
    {
        // 편집 중에는 작성된 앵커를 변경하지 않는다.
        if (!Application.isPlaying) return;

        if (_rect == null)
            _rect = GetComponent<RectTransform>();

        if (_rect == null) return;

        // 에디터 초기화 전 Screen 값이 0인 경우 스킵
        if (Screen.width == 0 || Screen.height == 0) return;

        _lastSafeArea = Screen.safeArea;
        _lastScreenSize = new Vector2Int(Screen.width, Screen.height);

        var anchorMin = _lastSafeArea.position;
        var anchorMax = _lastSafeArea.position + _lastSafeArea.size;

        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;

        _rect.anchorMin = anchorMin;
        _rect.anchorMax = anchorMax;
        _rect.offsetMin = Vector2.zero;
        _rect.offsetMax = Vector2.zero;
    }
}
