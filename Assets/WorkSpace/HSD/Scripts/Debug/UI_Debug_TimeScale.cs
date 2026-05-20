using UnityEngine;
using UnityEngine.UI;

public class UI_Debug_TimeScale : MonoBehaviour
{
    [Header("Time Setting")]
    [SerializeField] private Toggle tog_TimeStop;

    private void Awake()
    {
        tog_TimeStop.onValueChanged.AddListener((b) => SetTimeScale(b));
    }

    private void SetTimeScale(bool isTrue)
    {
        float value = isTrue ? 0 : 1;
        Time.timeScale = value;
    }
}
