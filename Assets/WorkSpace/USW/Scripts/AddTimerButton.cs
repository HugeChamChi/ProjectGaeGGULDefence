using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ⚠️ 디버그 전용 — 출시 전 반드시 제거하거나 비활성화
/// </summary>
public class DebugTimerButton : MonoBehaviour
{
    [Header("디버그 전용 ⚠️")]
    [SerializeField] private Button btnAddTime;
    [SerializeField] private float  addSeconds = 30f;

    private void Start()
    {
        if (btnAddTime != null)
            btnAddTime.onClick.AddListener(OnAddTimePressed);
        else
            Debug.LogWarning("DebugTimerButton: btnAddTime 미연결");
    }

    private void OnAddTimePressed()
    {
        Manager.Timer.AddTime(addSeconds);
    }
}
