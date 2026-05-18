using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 합성 버튼 뷰.
/// 합성 가능 여부 색상 표시, 클릭 시 OnMergeRequested 이벤트 발행.
/// Manager 직접 참조 없음 — InGameInstaller가 이벤트를 MergeManager에 연결.
/// </summary>
public class MergeButtonUI : MonoBehaviour
{
    [SerializeField] private Image buttonImage;
    [SerializeField] private Color colorActive   = Color.white;
    [SerializeField] private Color colorInactive = new Color(0.369f, 0.369f, 0.369f, 1f);

    public event Action OnMergeRequested;

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(() => OnMergeRequested?.Invoke());
    }

    /// <summary>UnitActionPopupUI.Show()에서 호출 — 합성 가능 여부에 따라 색상 변경</summary>
    public void SetState(bool canMerge)
    {
        buttonImage.color = canMerge ? colorActive : colorInactive;
    }
}
