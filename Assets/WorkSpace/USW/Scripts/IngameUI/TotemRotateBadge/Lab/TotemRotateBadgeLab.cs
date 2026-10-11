using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using GaeGGUL.UI.Totem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 토템 회전 가능/불가 배지 실험실(FxLab_TotemRotateBadge). 실제 화면 3곳에 사용자가 고른 A 원형 배지를 붙여 위치를 비교한다.
/// ① 선택지 개요 = 실제 TotemRewardCanvas(사선 띠) ② 선택지 상세 = 같은 화면의 상세(적용 범위 상자) ③ 배치 토템 정보창 = 실제 Totem_Info_Panel.
/// 원본 프리팹·코드는 건드리지 않고, 실험실 위 별도 캔버스에서 실제 UI 요소 위치를 따라가며 그린다. 데이터: TotemData.isRotatable.
/// </summary>
public sealed class TotemRotateBadgeLab : MonoBehaviour
{
    /// <summary>위치 시안 이름. 순서 = TotemRotateBadgeStyle 값.</summary>
    public static readonly string[] Labels = { "없음(현재)", "A 범위 상자 모서리", "B 이름 옆", "C 아이콘 모서리", "D 범위 글자 옆" };

    /// <summary>화면 이름.</summary>
    public static readonly string[] Screens = { "선택지 개요", "선택지 상세", "배치 정보창" };

    private static readonly string[] Descriptions =
    {
        "지금 게임 — 회전 여부 표시 없음",
        "\"적용 범위\" 상자 오른쪽 위 모서리 (선택지 개요는 범위 상자가 없어 아이콘 모서리)",
        "토템 이름 바로 오른쪽",
        "토템 아이콘 모서리 (정보창은 등급 배지와 겹치지 않게 왼쪽 위)",
        "\"적용 범위\" 글자 왼쪽에 작은 배지 (선택지 개요는 이름 옆)",
    };

    private const string RangeLabelKeyword = "범위";
    private const string PanelRangeBox = "Grid_Area";
    private const float SettleSeconds = 1f;
    private static readonly Color ButtonIdle = new Color(1f, 1f, 1f, 0.15f);
    private static readonly Color ButtonSelected = new Color(0.35f, 0.8f, 0.45f, 0.85f);
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [Header("실제 UI (빌더가 씬에 배치)")]
    [SerializeField] private TotemRewardUI _reward;
    [SerializeField] private UI_TotemInfoPanel[] _panels = System.Array.Empty<UI_TotemInfoPanel>();
    [Tooltip("정보창 순서와 같은 토템: 회전 가능 / 불가")]
    [SerializeField] private TotemData[] _panelTotems = System.Array.Empty<TotemData>();
    [SerializeField] private GameObject _panelStage;

    [Header("배지")]
    [SerializeField] private RectTransform _overlay;
    [SerializeField] private Sprite _rotateIcon;
    [SerializeField] private Sprite _circle;
    [SerializeField] private Color _accent = new Color(0.45f, 1f, 0.85f, 1f);
    [SerializeField] private Color _fixed = new Color(0.68f, 0.68f, 0.74f, 1f);
    [SerializeField] private Color _badgeBack = new Color(0.08f, 0.1f, 0.17f, 0.94f);
    [SerializeField] private Color _slash = new Color(1f, 0.32f, 0.3f, 1f);
    [Tooltip("화면별 배지 지름: 개요, 상세, 정보창")]
    [SerializeField] private float[] _sizes = { 78f, 80f, 56f };
    [SerializeField] private float _labelScale = 0.7f;
    [SerializeField] private float _gap = 10f;

    [Header("상태")]
    [SerializeField] private TotemRotateBadgeStyle _style = TotemRotateBadgeStyle.RangeCorner;
    [SerializeField] private int _screen;
    [Tooltip("끄면 회전 가능한 토템에만 배지(불가 11종은 표시 없음)")]
    [SerializeField] private bool _showFixed = true;

    [Header("UI")]
    [SerializeField] private TMP_Text _status;
    [SerializeField] private Image[] _styleButtons = System.Array.Empty<Image>();
    [SerializeField] private Image[] _screenButtons = System.Array.Empty<Image>();

    private enum Anchor { TopRight, TopLeft, AfterText, BeforeText }

    private sealed class Badge
    {
        public RectTransform Root;
        public Transform Target;
        public Anchor Anchor;
        public float Size;
    }

    private readonly List<Badge> _badges = new();
    private bool _rewardOpened;
    private int _detailSlot = -1;
    private bool _switching;

    /// <summary>위치 시안 수.</summary>
    public int VariantCount => Labels.Length;

    private void Start()
    {
        for (int i = 0; i < _panels.Length && i < _panelTotems.Length; i++)
            if (_panels[i] != null && _panelTotems[i] != null) _panels[i].SetData(_panelTotems[i]);
        SelectScreen(_screen);
    }

    private void LateUpdate()
    {
        foreach (var b in _badges) Place(b);
    }

    // ── 버튼 ─────────────────────────────────────────────────────────

    /// <summary>위치 시안 선택.</summary>
    public void SelectStyle(int index)
    {
        _style = (TotemRotateBadgeStyle)Mathf.Clamp(index, 0, Labels.Length - 1);
        Rebuild();
    }

    /// <summary>화면 선택: 0 개요, 1 상세, 2 정보창.</summary>
    public void SelectScreen(int index)
    {
        _screen = Mathf.Clamp(index, 0, Screens.Length - 1);
        if (!_switching) StartCoroutine(SwitchScreen());
    }

    /// <summary>회전 불가 토템도 배지 표시 ↔ 회전 가능만.</summary>
    public void ToggleShowFixed()
    {
        _showFixed = !_showFixed;
        Rebuild();
    }

    /// <summary>선택지 상세에서 다음 토템으로.</summary>
    public void NextDetail()
    {
        if (_screen != 1 || _reward == null) return;
        var choices = Choices();
        if (choices == null || choices.Count == 0) return;
        _detailSlot = (_detailSlot + 1) % choices.Count;
        Call(_reward, "ShowDetail", _detailSlot);
        Rebuild();
    }

    // ── 화면 전환 ────────────────────────────────────────────────────

    /// <summary>화면 전환이 끝났는지(캡처용).</summary>
    public bool Ready => !_switching;

    private IEnumerator SwitchScreen()
    {
        _switching = true;
        ClearBadges();
        bool reward = _screen < 2;
        if (_panelStage != null) _panelStage.SetActive(!reward);
        if (_reward != null)
        {
            if (reward && !_rewardOpened)
            {
                _rewardOpened = true;
                _reward.Show(null);
            }
            yield return WaitIdle();
            bool inDetail = Detail() != null && Detail().gameObject.activeSelf;
            if (_screen == 0 && inDetail) { Call(_reward, "BackToOverview"); yield return WaitIdle(); }
            if (_screen == 1 && !inDetail)
            {
                if (_detailSlot < 0) _detailSlot = FirstSlot(true);
                Call(_reward, "OnBandClicked", _detailSlot);
                yield return WaitIdle();
            }
            var canvas = _reward.GetComponentInParent<Canvas>(true);
            if (canvas != null) canvas.enabled = reward;
        }
        for (int f = 0; f < 3; f++) yield return null;
        _switching = false;
        Rebuild();
    }

    private IEnumerator WaitIdle()
    {
        var busy = typeof(TotemRewardUI).GetField("_busy", Private);
        float until = Time.unscaledTime + 5f;
        yield return null;
        while (busy != null && (bool)busy.GetValue(_reward) && Time.unscaledTime < until) yield return null;
        for (int f = 0; f < 2; f++) yield return null;
    }

    private int FirstSlot(bool rotatable)
    {
        var choices = Choices();
        if (choices == null) return 0;
        for (int i = 0; i < choices.Count; i++) if (choices[i] != null && choices[i].isRotatable == rotatable) return i;
        return 0;
    }

    /// <summary>캡처용: 상세에 회전 가능/불가 토템을 띄운다.</summary>
    public void ShowDetailOf(bool rotatable)
    {
        if (_screen != 1) return;
        _detailSlot = FirstSlot(rotatable);
        Call(_reward, "ShowDetail", _detailSlot);
        Rebuild();
    }

    // ── 배지 구성 ────────────────────────────────────────────────────

    private void Rebuild()
    {
        ClearBadges();
        if (!_switching && _style != TotemRotateBadgeStyle.None)
        {
            if (_screen == 0) BuildOverview();
            else if (_screen == 1) BuildDetail();
            else BuildPanels();
        }
        foreach (var b in _badges) Place(b);
        int s = (int)_style;
        if (_status != null)
            _status.text = $"<b>{Screens[_screen]} · {Labels[s]}</b>  {Descriptions[s]}\n" +
                           $"불가 표시: {(_showFixed ? "회색 + 빨간 사선" : "안 함(회전 가능만 배지)")}";
        for (int i = 0; i < _styleButtons.Length; i++)
            if (_styleButtons[i] != null) _styleButtons[i].color = i == s ? ButtonSelected : ButtonIdle;
        for (int i = 0; i < _screenButtons.Length; i++)
            if (_screenButtons[i] != null) _screenButtons[i].color = i == _screen ? ButtonSelected : ButtonIdle;
    }

    private void BuildOverview()
    {
        var choices = Choices();
        if (choices == null) return;
        var overview = FindDeep(_reward.transform, "Overview");
        float size = _sizes[0];
        for (int i = 0; i < choices.Count; i++)
        {
            var band = overview != null ? FindDeep(overview, $"Band_{i}") : null;
            if (band == null || !band.gameObject.activeInHierarchy) continue;
            var icon = FindDeep(band, "Icon");
            var name = FindDeep(band, "Name");
            switch (_style)
            {
                case TotemRotateBadgeStyle.RangeCorner:
                case TotemRotateBadgeStyle.IconCorner: Add(choices[i], icon, Anchor.TopRight, size); break;
                case TotemRotateBadgeStyle.NameSide: Add(choices[i], name, Anchor.AfterText, size); break;
                case TotemRotateBadgeStyle.RangeLabel: Add(choices[i], name, Anchor.AfterText, size * _labelScale); break;
            }
        }
    }

    private void BuildDetail()
    {
        var choices = Choices();
        var detail = Detail();
        if (choices == null || detail == null || _detailSlot < 0 || _detailSlot >= choices.Count) return;
        var data = choices[_detailSlot];
        var range = detail.GetComponentInChildren<UI_TotemRangeGrid>(true);
        var content = FindDeep(detail, "Content");
        Target(data, content != null ? FindDeep(content, "Icon") : null, content != null ? FindDeep(content, "Name") : null,
            range != null ? range.transform : null, RangeLabel(range), Anchor.TopRight, _sizes[1]);
    }

    private void BuildPanels()
    {
        for (int i = 0; i < _panels.Length && i < _panelTotems.Length; i++)
        {
            var panel = _panels[i];
            if (panel == null || !panel.gameObject.activeInHierarchy) continue;
            // 정보창의 UI_TotemRangeGrid 영역은 설명 칸까지 넓어서, 실제 "적용 범위" 상자(Grid_Area)를 기준으로 잡는다.
            Target(_panelTotems[i], FindDeep(panel.transform, "TotemIcon_Slot"), FindDeep(panel.transform, "TotemName_Text"),
                FindDeep(panel.transform, PanelRangeBox), RangeLabel(panel.GetComponentInChildren<UI_TotemRangeGrid>(true)),
                Anchor.TopLeft, _sizes[2]);
        }
    }

    private void Target(TotemData data, Transform icon, Transform name, Transform rangeBox, Transform rangeLabel, Anchor iconCorner, float size)
    {
        switch (_style)
        {
            case TotemRotateBadgeStyle.RangeCorner: Add(data, rangeBox, Anchor.TopRight, size); break;
            case TotemRotateBadgeStyle.NameSide: Add(data, name, Anchor.AfterText, size); break;
            case TotemRotateBadgeStyle.IconCorner: Add(data, icon, iconCorner, size); break;
            case TotemRotateBadgeStyle.RangeLabel: Add(data, rangeLabel, Anchor.BeforeText, size * _labelScale); break;
        }
    }

    private static Transform RangeLabel(UI_TotemRangeGrid range)
    {
        if (range == null) return null;
        foreach (var t in range.GetComponentsInChildren<TMP_Text>(true))
            if (t.text.Contains(RangeLabelKeyword)) return t.transform;
        return null;
    }

    private void Add(TotemData data, Transform target, Anchor anchor, float size)
    {
        if (data == null || target == null) return;
        bool rotatable = data.isRotatable;
        if (!rotatable && !_showFixed) return;
        var root = NewRect(_overlay, "RotateBadge", Vector2.one * size);
        Color tone = rotatable ? _accent : new Color(_fixed.r, _fixed.g, _fixed.b, 0.6f);
        NewImage(root, "Ring", _circle, tone, Vector2.one * size);
        NewImage(root, "Back", _circle, _badgeBack, Vector2.one * (size - Mathf.Max(3f, size * 0.1f)));
        NewImage(root, "Icon", _rotateIcon, rotatable ? Color.white : new Color(0.62f, 0.62f, 0.68f, 0.85f), Vector2.one * (size * 0.66f));
        if (!rotatable)
        {
            var bar = NewImage(root, "Slash", null, _slash, new Vector2(size * 0.86f, Mathf.Max(3f, size * 0.09f)));
            bar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }
        _badges.Add(new Badge { Root = root, Target = target, Anchor = anchor, Size = size });
    }

    private void Place(Badge b)
    {
        if (b.Root == null || b.Target == null) return;
        Vector3 world;
        Vector2 offset;
        float half = b.Size * 0.5f;
        if (b.Anchor == Anchor.AfterText || b.Anchor == Anchor.BeforeText)
        {
            var text = b.Target.GetComponent<TMP_Text>();
            if (text == null) return;
            var bounds = text.textBounds;
            bool after = b.Anchor == Anchor.AfterText;
            world = text.transform.TransformPoint(new Vector3(after ? bounds.max.x : bounds.min.x, bounds.center.y, 0f));
            offset = new Vector2(after ? _gap + half : -_gap - half, 0f);
        }
        else
        {
            var rect = b.Target as RectTransform;
            if (rect == null) return;
            var corners = new Vector3[4]; // 0 좌하, 1 좌상, 2 우상, 3 우하
            rect.GetWorldCorners(corners);
            bool right = b.Anchor == Anchor.TopRight;
            world = corners[right ? 2 : 1];
            offset = new Vector2(right ? -half * 0.45f : half * 0.45f, -half * 0.45f);
        }
        Vector2 local = (Vector2)_overlay.InverseTransformPoint(world) + offset;
        b.Root.localPosition = local;
        b.Root.gameObject.SetActive(b.Target.gameObject.activeInHierarchy);
    }

    private void ClearBadges()
    {
        foreach (var b in _badges) if (b.Root != null) Destroy(b.Root.gameObject);
        _badges.Clear();
    }

    // ── 실제 UI 접근(실험실 전용 리플렉션) ───────────────────────────

    private List<TotemData> Choices() =>
        _reward == null ? null : typeof(TotemRewardUI).GetField("_choices", Private)?.GetValue(_reward) as List<TotemData>;

    private RectTransform Detail() =>
        _reward == null ? null : typeof(TotemRewardUI).GetField("_detail", Private)?.GetValue(_reward) as RectTransform;

    private static void Call(object target, string method, params object[] args)
    {
        var m = target.GetType().GetMethod(method, Private);
        if (m == null) { Debug.LogWarning($"[TotemRotateBadgeLab] 메서드 없음: {method}"); return; }
        m.Invoke(target, args);
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root == null) return null;
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name && t != root) return t;
        return null;
    }

    private static RectTransform NewRect(RectTransform parent, string name, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        return rect;
    }

    private static Image NewImage(RectTransform parent, string name, Sprite sprite, Color color, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.sizeDelta = size;
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.preserveAspect = sprite != null;
        image.raycastTarget = false;
        return image;
    }

    // ── 캡처 ─────────────────────────────────────────────────────────

    /// <summary>마지막 캡처 결과(진행 중이면 null).</summary>
    public string CaptureResult { get; private set; } = string.Empty;

    /// <summary>화면 × 위치 시안 전체를 PNG로 저장한다. 상세는 회전 가능/불가 토템을 각각 찍는다.</summary>
    public void CaptureAll(string folder, int width, int height)
    {
        CaptureResult = null;
        StartCoroutine(CaptureRoutine(folder, width, height));
    }

    private IEnumerator CaptureRoutine(string folder, int width, int height)
    {
        System.IO.Directory.CreateDirectory(folder);
        // 보상 화면은 열릴 때의 게임 뷰 크기로 배치가 고정되므로 게임 뷰 그대로 찍는다(해상도 = 게임 뷰).
        int count = 0;
        for (int screen = 0; screen < Screens.Length; screen++)
        {
            SelectScreen(screen);
            yield return null;
            while (!Ready) yield return null;
            yield return new WaitForSecondsRealtime(SettleSeconds); // 정보창 여는 애니메이션이 끝난 뒤 찍는다
            SelectStyle((int)_style);
            int passes = screen == 1 ? 2 : 1;
            for (int pass = 0; pass < passes; pass++)
            {
                if (screen == 1) ShowDetailOf(pass == 0);
                for (int s = 0; s < Labels.Length; s++)
                {
                    SelectStyle(s);
                    yield return null;
                    yield return new WaitForEndOfFrame();
                    string suffix = screen == 1 ? (pass == 0 ? "_rot" : "_fixed") : string.Empty;
                    Shot(System.IO.Path.Combine(folder, $"s{screen}{suffix}_v{s}.png"));
                    count++;
                }
            }
        }
        SelectStyle((int)TotemRotateBadgeStyle.RangeCorner);
        CaptureResult = $"{folder} ({count})";
    }

    private static void Shot(string path)
    {
        var tex = ScreenCapture.CaptureScreenshotAsTexture();
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Destroy(tex);
    }
}
