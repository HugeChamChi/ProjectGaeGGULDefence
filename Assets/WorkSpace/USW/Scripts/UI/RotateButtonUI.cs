using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 토템 회전 버튼. 클릭 시 현재 토템을 직접 회전 (순수 UI 동작).
/// </summary>
public class RotateButtonUI : MonoBehaviour
{
    private TotemBase _targetTotem;

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(() => _targetTotem?.Rotate());
    }

    public void SetTotem(TotemBase totem) => _targetTotem = totem;
}
