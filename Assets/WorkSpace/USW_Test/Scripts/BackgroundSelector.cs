using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 미리보기 배경을 Inspector에서 등록된 옵션으로 교체합니다.
/// texture가 null인 옵션은 solidColor를 단색 배경으로 사용합니다.
/// </summary>
public class BackgroundSelector : MonoBehaviour
{
    [Serializable]
    public struct BackgroundOption
    {
        public string    label;
        public Texture2D texture;     // null이면 solidColor 단색 표시
        public Color     solidColor;
    }

    [Header("배경 표시 대상")]
    [SerializeField] private RawImage _backgroundTarget;

    [Header("배경 옵션 (순서대로 버튼에 대응)")]
    [SerializeField] private BackgroundOption[] _options = new BackgroundOption[]
    {
        new BackgroundOption { label = "검정",  texture = null, solidColor = Color.black },
        new BackgroundOption { label = "흰색",  texture = null, solidColor = Color.white },
        new BackgroundOption { label = "회색",  texture = null, solidColor = new Color(0.47f, 0.47f, 0.47f) },
        new BackgroundOption { label = "체커",  texture = null, solidColor = new Color(0.2f, 0.2f, 0.2f) },
    };

    [Header("배경 선택 버튼 (옵션 개수와 동일하게)")]
    [SerializeField] private Button[] _buttons;

    private int _currentIndex;

    private void Awake()
    {
        for (int i = 0; i < _buttons.Length && i < _options.Length; i++)
        {
            int index = i;
            _buttons[i].onClick.AddListener(() => ApplyBackground(index));

            var label = _buttons[i].GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.text = _options[i].label;
        }
    }

    private void Start()
    {
        if (_options.Length > 0) ApplyBackground(0);
    }

    /// <summary>버튼 클릭 또는 외부에서 직접 배경 인덱스를 지정합니다.</summary>
    public void ApplyBackground(int index)
    {
        if (index < 0 || index >= _options.Length) return;
        _currentIndex = index;

        BackgroundOption opt = _options[index];
        if (opt.texture != null)
        {
            _backgroundTarget.texture = opt.texture;
            _backgroundTarget.color   = Color.white;
        }
        else
        {
            _backgroundTarget.texture = null;
            _backgroundTarget.color   = opt.solidColor;
        }
    }

    public int CurrentIndex => _currentIndex;
}
