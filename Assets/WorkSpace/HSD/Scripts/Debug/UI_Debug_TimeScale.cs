using UnityEngine;
using UnityEngine.UI;

public class UI_Debug_TimeScale : MonoBehaviour
{
    [Header("Time Setting")]
    [SerializeField] private Toggle tog_TimeStop;

    [VContainer.Inject] private TimeScaleService _timeScale;

    private void Awake()
    {
        tog_TimeStop.onValueChanged.AddListener((b) => SetTimeScale(b));
    }

    private void SetTimeScale(bool isTrue)
    {
        if (_timeScale == null) { Debug.LogWarning("[UI_Debug_TimeScale] TimeScaleService 미주입", this); return; }
        if (isTrue) _timeScale.Pause(this);
        else _timeScale.Release(this);
    }
}
