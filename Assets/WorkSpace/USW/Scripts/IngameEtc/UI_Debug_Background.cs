using UnityEngine;
using UnityEngine.UI;

/// <summary>QA용 — 배경 SpriteRenderer의 스프라이트를 버튼 3개로 즉시 교체한다.</summary>
public class UI_Debug_Background : MonoBehaviour
{
    [Header("배경 대상")]
    [SerializeField] private SpriteRenderer _background;

    [Header("교체할 스프라이트 (버튼 순서와 동일)")]
    [SerializeField] private Sprite _sprite1;
    [SerializeField] private Sprite _sprite2;
    [SerializeField] private Sprite _sprite3;

    [Header("배경 선택 버튼")]
    [SerializeField] private Button _btn1;
    [SerializeField] private Button _btn2;
    [SerializeField] private Button _btn3;

    private void Awake()
    {
        _btn1?.onClick.AddListener(() => SetBackground(_sprite1));
        _btn2?.onClick.AddListener(() => SetBackground(_sprite2));
        _btn3?.onClick.AddListener(() => SetBackground(_sprite3));
    }

    private void SetBackground(Sprite sprite)
    {
        if (_background == null || sprite == null) return;
        _background.sprite = sprite;
    }
}
