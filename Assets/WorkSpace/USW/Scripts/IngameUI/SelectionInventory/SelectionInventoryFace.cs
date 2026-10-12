using UnityEngine;
using UnityEngine.UI;

/// <summary>흰 테두리 원 안에 캐릭터 상반신을 보여 주는 작은 얼굴 (획득 선택지 목록의 대상 유닛 표시).</summary>
public sealed class SelectionInventoryFace : MonoBehaviour
{
    [SerializeField] private Image _art;

    /// <summary>얼굴 그림을 바꾼다. 그림이 없으면 얼굴 전체를 숨긴다.</summary>
    public void Set(Sprite sprite)
    {
        gameObject.SetActive(sprite != null);
        if (_art != null) _art.sprite = sprite;
    }
}
