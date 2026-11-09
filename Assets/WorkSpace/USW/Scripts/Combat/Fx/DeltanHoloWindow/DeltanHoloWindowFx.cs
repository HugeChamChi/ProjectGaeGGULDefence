using UnityEngine;

/// <summary>
/// 델탕 skill 홀로그램 창 연출. 유닛 외형 아래에 두고 skill이 시작될 때 <see cref="Play"/> 한 번만 부른다.
/// 창이 하나씩 튀어나오고 글줄이 타자 치듯 길이가 바뀌다가 함께 닫힌다. Spine 애니메이션은 건드리지 않는다.
/// </summary>
public class DeltanHoloWindowFx : MonoBehaviour
{
    // 창/글줄마다 타자 리듬이 겹치지 않게 하는 위상 차이와, 닫힐 때 줄어드는 비율.
    private const float SeedPerWindow = 1.9f;
    private const float SeedPerLine = 1.7f;
    private const float CloseShrink = 0.85f;

    [SerializeField] private DeltanHoloWindowSettings _settings;

    private sealed class WindowView
    {
        public Transform Root;
        public SpriteRenderer Frame;
        public SpriteRenderer[] Lines;
        public float[] Seeds;
    }

    private WindowView[] _views;
    private float _time;
    private bool _playing;

    /// <summary>재생 중인지 여부.</summary>
    public bool IsPlaying => _playing;

    /// <summary>처음부터 재생한다. 재생 중에 다시 부르면 처음부터 다시 시작한다.</summary>
    public void Play()
    {
        if (_settings == null || _settings.FrameSprite == null) return;
        // The live field also scales its map parent. Match the lab's world-unit
        // attachment after placement, preserving the unit's existing facing.
        if (transform.parent != null)
        {
            var scale = transform.parent.lossyScale;
            if (Mathf.Abs(scale.x) > Mathf.Epsilon && Mathf.Abs(scale.y) > Mathf.Epsilon)
                transform.localScale = new Vector3(1f / Mathf.Abs(scale.x), 1f / Mathf.Abs(scale.y), 1f);
        }
        EnsureViews();
        _time = 0f;
        _playing = true;
        Apply();
    }

    /// <summary>즉시 숨긴다.</summary>
    public void Stop()
    {
        _playing = false;
        if (_views == null) return;
        foreach (var view in _views) view.Root.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!_playing) return;
        _time += Time.deltaTime;
        if (_time >= _settings.CloseTime + _settings.CloseDuration) { Stop(); return; }
        Apply();
    }

    private void OnDisable() => Stop();

    private void EnsureViews()
    {
        if (_views != null && _views.Length == _settings.Windows.Length) return;
        if (_views != null) foreach (var view in _views) if (view.Root != null) Destroy(view.Root.gameObject);
        _views = new WindowView[_settings.Windows.Length];
        for (int i = 0; i < _views.Length; i++) _views[i] = CreateView(i);
    }

    private WindowView CreateView(int index)
    {
        var root = new GameObject("HoloWindow_" + index).transform;
        root.SetParent(transform, false);
        var view = new WindowView
        {
            Root = root,
            Frame = CreateRenderer("Frame", root, _settings.FrameSprite, _settings.SortingOrder),
            Lines = new SpriteRenderer[Mathf.Max(0, _settings.LinesPerWindow)],
            Seeds = new float[Mathf.Max(0, _settings.LinesPerWindow)],
        };
        for (int i = 0; i < view.Lines.Length; i++)
        {
            view.Lines[i] = CreateRenderer("Line_" + i, root, _settings.LineSprite, _settings.SortingOrder + 1);
            view.Seeds[i] = index * SeedPerWindow + i * SeedPerLine;
        }
        root.gameObject.SetActive(false);
        return view;
    }

    private SpriteRenderer CreateRenderer(string name, Transform parent, Sprite sprite, int order)
    {
        var renderer = new GameObject(name).AddComponent<SpriteRenderer>();
        renderer.transform.SetParent(parent, false);
        renderer.sprite = sprite;
        renderer.sortingLayerName = _settings.SortingLayer;
        renderer.sortingOrder = order;
        return renderer;
    }

    private void Apply()
    {
        float close = _settings.CloseDuration > 0f ? Mathf.Clamp01((_time - _settings.CloseTime) / _settings.CloseDuration) : (_time >= _settings.CloseTime ? 1f : 0f);
        float alpha = 1f - close;
        for (int i = 0; i < _views.Length; i++)
        {
            var data = _settings.Windows[i];
            var view = _views[i];
            float local = _time - data.Delay;
            bool visible = local >= 0f && alpha > 0f;
            view.Root.gameObject.SetActive(visible);
            if (!visible) continue;

            float pop = _settings.PopDuration > 0f ? BackOut(Mathf.Clamp01(local / _settings.PopDuration), _settings.PopOvershoot) : 1f;
            float scale = pop * Mathf.Lerp(1f, CloseShrink, close);
            view.Root.localPosition = new Vector3(data.Offset.x, data.Offset.y, 0f);
            view.Root.localRotation = Quaternion.Euler(0f, 0f, data.Rotation);
            view.Root.localScale = new Vector3(scale, scale, 1f);
            Layout(view, data.Width, local, alpha);
        }
    }

    private void Layout(WindowView view, float width, float time, float alpha)
    {
        Vector2 frameSize = _settings.FrameSprite.bounds.size;
        float frameScale = width / (frameSize.x * _settings.ContentRatio.x);
        view.Frame.transform.localScale = new Vector3(frameScale, frameScale, 1f);
        view.Frame.color = new Color(1f, 1f, 1f, alpha);

        if (_settings.LineSprite == null) return;
        Vector2 lineSize = _settings.LineSprite.bounds.size;
        float contentHeight = frameSize.y * _settings.ContentRatio.y * frameScale;
        float top = contentHeight * (0.5f - _settings.TitleBarRatio);
        float left = -width * 0.5f + width * _settings.LineLeftPadRatio;
        float thickness = contentHeight * _settings.LineThicknessRatio;
        var color = _settings.LineColor;
        color.a *= alpha;
        for (int i = 0; i < view.Lines.Length; i++)
        {
            float wave = 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(view.Seeds[i] + time * _settings.LineJitterSpeed));
            float lineWidth = width * Mathf.Lerp(_settings.LineWidthRange.x, _settings.LineWidthRange.y, wave);
            var line = view.Lines[i].transform;
            line.localPosition = new Vector3(left + lineWidth * 0.5f, top - contentHeight * _settings.LineSpacingRatio * (i + 1), 0f);
            line.localScale = new Vector3(lineWidth / lineSize.x, thickness / lineSize.y, 1f);
            view.Lines[i].color = color;
        }
    }

    private static float BackOut(float t, float overshoot)
    {
        float u = t - 1f;
        return 1f + (overshoot + 1f) * u * u * u + overshoot * u * u;
    }
}
