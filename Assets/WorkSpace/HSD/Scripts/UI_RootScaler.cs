using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 가로 폭이 1080보다 작으면 Match 0 (Width)으로, 
/// 1080 이상이면 Match 1 (Height)로 설정합니다.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(CanvasScaler))]
public class UI_RootScaler : MonoBehaviour
{
    private CanvasScaler _canvasScaler;

    // 기준 가로 폭
    private const int TargetWidth = 1080;

    private void Awake()
    {
        _canvasScaler = GetComponent<CanvasScaler>();
        Refresh();
    }

#if UNITY_EDITOR
    private void Update()
    {
        // 에디터에서 Game 뷰 크기를 바꿀 때마다 즉시 반영되도록 합니다.
        if (!Application.isPlaying)
        {
            Refresh();
        }
    }
#endif

    public void Refresh()
    {
        if (_canvasScaler == null) return;

        float currentWidth = Screen.width;
        
        // 현재 기기(또는 게임 뷰)의 가로 폭 확인
        if (currentWidth < TargetWidth)
        {
            // 폴드 외부 화면처럼 가로가 좁은 경우
            _canvasScaler.matchWidthOrHeight = currentWidth / TargetWidth; // Width에 맞춤
        }
        else
        {
            // S24, 일반 폰처럼 가로가 1080 이상인 경우
            _canvasScaler.matchWidthOrHeight = 1f; // Height에 맞춤
        }
    }
}
