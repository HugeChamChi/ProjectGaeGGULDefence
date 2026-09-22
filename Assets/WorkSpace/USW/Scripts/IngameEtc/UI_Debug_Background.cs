using UnityEngine;
using UnityEngine.UI;

/// <summary>QA용 — 배경 SpriteRenderer의 스프라이트를 좌/우 버튼으로 순환 교체한다.</summary>
public class UI_Debug_Background : MonoBehaviour
{
    [Header("배경 대상 (비워두면 '/// Map/bg'에서 자동으로 찾음)")]
    [SerializeField] private SpriteRenderer _background;

    [Header("교체할 스프라이트 목록 (순서대로 순환)")]
    [SerializeField] private Sprite[] _sprites;

    [Header("좌/우 전환 버튼 (비워두면 이 오브젝트/형제 오브젝트에서 자동으로 찾음)")]
    [SerializeField] private Button _prevButton;
    [SerializeField] private Button _nextButton;

    private int _index;

    private void Awake()
    {
        if (_background == null)
        {
            // "/// Map"는 이름 자체에 '/'가 포함돼 있어 GameObject.Find 경로 탐색이 불가능하다.
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                if (root.name != "/// Map") continue;
                var bg = root.transform.Find("bg");
                if (bg != null) _background = bg.GetComponent<SpriteRenderer>();
                break;
            }
        }
        if (_nextButton == null) _nextButton = GetComponent<Button>();
        if (_prevButton == null && transform.parent != null)
        {
            var prev = transform.parent.Find("BgPrevButton");
            if (prev != null) _prevButton = prev.GetComponent<Button>();
        }

        _prevButton?.onClick.AddListener(Prev);
        _nextButton?.onClick.AddListener(Next);
    }

    public void Prev() => Apply(_index - 1);
    public void Next() => Apply(_index + 1);

    private void Apply(int index)
    {
        if (_background == null || _sprites == null || _sprites.Length == 0) return;
        _index = ((index % _sprites.Length) + _sprites.Length) % _sprites.Length;
        _background.sprite = _sprites[_index];
    }
}
