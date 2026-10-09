using UnityEngine;
using UnityEngine.UI;

/// <summary>HUD 자리에서만 한 번 조립되거나 접히는 원호·눈금 연출. 반복 회전은 하지 않는다.</summary>
public sealed class HackTemporalTransition
{
    private const float Diameter = 208f;
    private const int TickCount = 12;
    private static readonly Color Cyan = new Color(.46f, .9f, 1f);
    private readonly RectTransform _root;
    private readonly Image _outer, _inner, _sweep;
    private readonly Image[] _ticks = new Image[TickCount];

    /// <summary>진입·퇴장에 재사용할 도형을 한 번 만든다.</summary>
    public HackTemporalTransition(RectTransform parent, string name)
    {
        _root = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        _root.SetParent(parent, false);
        _root.anchorMin = _root.anchorMax = _root.pivot = Vector2.one * .5f;
        _root.sizeDelta = Vector2.one * Diameter;
        _outer = Image("OuterArc", Vector2.one * Diameter, HackGaugeSprites.ThinRing);
        _inner = Image("InnerArc", Vector2.one * (Diameter * .78f), HackGaugeSprites.ThinRing);
        foreach (var arc in new[] { _outer, _inner })
        {
            arc.type = UnityEngine.UI.Image.Type.Filled;
            arc.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360;
            arc.fillOrigin = (int)UnityEngine.UI.Image.Origin360.Top;
        }
        _sweep = Image("LockLine", new Vector2(Diameter * .7f, 4f), null);
        for (int i = 0; i < TickCount; i++)
        {
            _ticks[i] = Image("Tick" + i, new Vector2(i % 3 == 0 ? 5f : 3f, i % 3 == 0 ? 16f : 9f), null);
            _ticks[i].rectTransform.localEulerAngles = new Vector3(0f, 0f, -i * 360f / TickCount);
        }
        Hide();
    }

    /// <summary>선택되지 않은 시안이나 연출 종료 상태에서 숨긴다.</summary>
    public void Hide() => _root.gameObject.SetActive(false);

    /// <summary>정규화 시간0~1로 한 번 재생한다. 퇴장 때는 중심으로 접힌다.</summary>
    public void Render(Vector2 position, float progress, bool exiting)
    {
        float q = Mathf.Clamp01(progress);
        _root.gameObject.SetActive(q < 1f);
        _root.anchoredPosition = position;
        float envelope = Mathf.Sin(q * Mathf.PI);
        float radius = exiting ? Mathf.Lerp(1f, .15f, q * q) : Mathf.Lerp(.48f, 1.12f, 1f - Mathf.Pow(1f - q, 3f));
        _root.localScale = Vector3.one * radius;
        _outer.fillAmount = exiting ? 1f - q : Mathf.Clamp01(q * 2.6f);
        _inner.fillAmount = exiting ? (1f - q) * .75f : Mathf.Clamp01(q * 2f) * .75f;
        _outer.rectTransform.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(-75f, 0f, q));
        _inner.rectTransform.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(95f, 180f, q));
        _outer.color = new Color(Cyan.r, Cyan.g, Cyan.b, envelope);
        _inner.color = new Color(.85f, .97f, 1f, envelope * .8f);
        _sweep.rectTransform.sizeDelta = new Vector2(Diameter * .7f * Mathf.Sin(q * Mathf.PI), 4f);
        _sweep.color = new Color(.85f, .97f, 1f, envelope * .85f);
        for (int i = 0; i < TickCount; i++)
        {
            float angle = i * 2f * Mathf.PI / TickCount;
            _ticks[i].rectTransform.anchoredPosition = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * Diameter * .52f;
            float stagger = Mathf.Clamp01((q - i * .018f) * 7f);
            _ticks[i].color = new Color(Cyan.r, Cyan.g, Cyan.b, envelope * stagger);
        }
    }

    private Image Image(string name, Vector2 size, Sprite sprite)
    {
        var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        image.rectTransform.SetParent(_root, false);
        image.rectTransform.anchorMin = image.rectTransform.anchorMax = image.rectTransform.pivot = Vector2.one * .5f;
        image.rectTransform.sizeDelta = size;
        image.sprite = sprite; image.color = Color.clear; image.raycastTarget = false;
        return image;
    }
}
