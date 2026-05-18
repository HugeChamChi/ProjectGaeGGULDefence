using UnityEngine;

/// <summary>
/// 토템 회전 버튼. 클릭 시 현재 토템을 직접 회전 (순수 UI 동작).
/// </summary>
public class RotateButtonUI : MonoBehaviour
{
    private TotemBase _targetTotem;

    public void SetTotem(TotemBase totem) => _targetTotem = totem;

    public void OnRotateButtonClicked() => _targetTotem?.Rotate();
}
