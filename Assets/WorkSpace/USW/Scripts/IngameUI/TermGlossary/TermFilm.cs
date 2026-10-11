using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// 용어 팝업: 움직임이 핵심인 용어의 시연 영상을 필름 프레임 안에 띄운다 (레퍼런스 design/스킬예시.gif).
/// 영상 준비 시간 동안 TV 노이즈를 보여 주고, 준비되면 노이즈가 걷히며 반복 재생된다.
/// 레벨업 중에는 게임 시간이 멈출 수 있으므로 영상은 unscaled 시간으로 돈다.
/// </summary>
public sealed class TermFilm : MonoBehaviour
{
    private static readonly Color FrameColor = new Color(0.09f, 0.09f, 0.12f, 1f);
    private static readonly Color HoleColor = new Color(0.32f, 0.3f, 0.27f, 1f);

    [Tooltip("필름 위아래 구멍 띠 두께 / 좌우 테두리 두께")]
    [SerializeField, Min(0f)] private float _stripHeight = 40f;
    [SerializeField, Min(0f)] private float _sideBorder = 22f;
    [SerializeField, Min(2)] private int _holesPerRow = 9;
    [Tooltip("이보다 자리가 좁으면 영상을 띄우지 않는다 (캔버스 기준 단위)")]
    [SerializeField, Min(0f)] private float _minHeight = 260f;
    [Tooltip("노이즈를 최소 이만큼은 보여 준다 (영상이 바로 준비돼도 전환 맛을 살린다)")]
    [SerializeField, Min(0f)] private float _minNoiseSeconds = 0.3f;
    [SerializeField, Min(0.01f)] private float _noiseFadeOut = 0.25f;
    [SerializeField, Min(0.01f)] private float _popDuration = 0.28f;
    [Tooltip("화면 안쪽(영상) 가로:세로 비율. 클립 비율이 달라도 이 비율로 가운데를 잘라 채운다 (사용자 예시 2026-10-11, 4:5)")]
    [SerializeField, Min(0.1f)] private float _frameAspect = 0.8f;

    private RectTransform _rect;
    private RectTransform _screen;
    private RawImage _video;
    private RawImage _noise;
    private VideoPlayer _player;
    private RenderTexture _target;
    private Texture2D _noiseTexture;
    private float _shownAt;
    private bool _prepared;
    private bool _noiseFading;

    /// <summary>영상이 실제로 재생 중인지 (검증용).</summary>
    public bool IsPlaying => _player != null && _player.isPlaying;
    /// <summary>노이즈가 걷혔는지 (검증용).</summary>
    public bool NoiseCleared => _noise != null && !_noise.gameObject.activeSelf;
    /// <summary>필름 사각형 (검증용).</summary>
    public RectTransform Rect => _rect;

    /// <summary>팝업 층 아래에 필름 프레임을 만든다. 처음에는 숨겨 둔다.</summary>
    public static TermFilm Create(RectTransform parent, TMP_FontAsset font, Material fontMaterial)
    {
        var rect = NewRect("Film", parent);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        var film = rect.gameObject.AddComponent<TermFilm>();
        film._rect = rect;
        film.Build(font, fontMaterial);
        // 필름은 보기만 한다 — 누름은 뒤의 방패로 가서 닫기가 된다.
        foreach (var graphic in rect.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
        rect.gameObject.SetActive(false);
        return film;
    }

    /// <summary>
    /// 캔버스 기준 top(필름 위 끝)에 붙여 최대 maxWidth 폭으로 띄운다. 아래 끝이 bottomLimit보다 내려가면 그만큼 줄인다.
    /// 화면 비율은 _frameAspect로 고정하고 클립은 가운데를 잘라 채운다. 자리가 좁으면 띄우지 않고 false.
    /// </summary>
    public bool Show(VideoClip clip, float top, float bottomLimit, float maxWidth, Vector2 rootCenter)
    {
        if (clip == null) { Hide(); return false; }
        if (!Relayout(top, bottomLimit, maxWidth, rootCenter)) { Hide(); return false; }

        // 가운데 맞춰 자르기 (cover)
        float clipAspect = clip.height > 0 ? (float)clip.width / clip.height : _frameAspect;
        _video.uvRect = clipAspect > _frameAspect
            ? new Rect((1f - _frameAspect / clipAspect) * 0.5f, 0f, _frameAspect / clipAspect, 1f)
            : new Rect(0f, (1f - clipAspect / _frameAspect) * 0.5f, 1f, clipAspect / _frameAspect);

        EnsureTarget((int)clip.width, (int)clip.height);
        ClearTarget();

        _rect.gameObject.SetActive(true);
        _noise.gameObject.SetActive(true);
        _noise.color = Color.white;
        _noiseFading = false;
        _prepared = false;
        _shownAt = Time.unscaledTime;

        _player.Stop();
        _player.clip = clip;
        _player.Prepare();

        _rect.DOKill();
        _rect.localScale = Vector3.one * 0.85f;
        _rect.DOScale(1f, _popDuration).SetEase(Ease.OutBack, 1.6f).SetUpdate(true).SetLink(gameObject);
        return true;
    }

    /// <summary>Fits the existing film above a resized sheet without preparing or restarting its video.</summary>
    public bool Relayout(float top, float bottomLimit, float maxWidth, Vector2 rootCenter)
    {
        float innerW = maxWidth - _sideBorder * 2f;
        float innerH = Mathf.Min(innerW / _frameAspect, top - bottomLimit - _stripHeight * 2f);
        if (innerH + _stripHeight * 2f < _minHeight) return false;
        innerW = innerH * _frameAspect;
        _rect.sizeDelta = new Vector2(innerW + _sideBorder * 2f, innerH + _stripHeight * 2f);
        _rect.pivot = new Vector2(0.5f, 1f);
        _rect.anchoredPosition = new Vector2(0f, top - rootCenter.y);
        return true;
    }

    /// <summary>영상을 멈추고 숨긴다.</summary>
    public void Hide()
    {
        if (_player != null) _player.Stop();
        if (_rect != null) { _rect.DOKill(); _rect.gameObject.SetActive(false); }
        if (_noise != null) _noise.DOKill();
    }

    private void OnPrepared(VideoPlayer player)
    {
        _prepared = true;
        player.Play();
    }

    private void Update()
    {
        if (_noise == null || !_noise.gameObject.activeSelf) return;
        // 지지직 — 매 프레임 노이즈 무늬 위치를 무작위로 옮긴다.
        _noise.uvRect = new Rect(Random.value, Random.value, 1.6f, 1.6f);
        if (!_noiseFading && _prepared && _player.frame > 0 && Time.unscaledTime - _shownAt >= _minNoiseSeconds)
        {
            _noiseFading = true;
            _noise.DOFade(0f, _noiseFadeOut).SetUpdate(true).SetLink(gameObject)
                  .OnComplete(() => _noise.gameObject.SetActive(false));
        }
    }

    private void OnDestroy()
    {
        if (_player != null) _player.prepareCompleted -= OnPrepared;
        if (_target != null) { _target.Release(); Destroy(_target); }
        if (_noiseTexture != null) Destroy(_noiseTexture);
    }

    private void EnsureTarget(int width, int height)
    {
        width = Mathf.Max(width, 16);
        height = Mathf.Max(height, 16);
        if (_target != null && _target.width == width && _target.height == height) return;
        if (_target != null) { _target.Release(); Destroy(_target); }
        _target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
        _player.targetTexture = _target;
        _video.texture = _target;
    }

    // 이전 영상의 마지막 장면이 노이즈 뒤로 비치지 않게 지운다.
    private void ClearTarget()
    {
        var previous = RenderTexture.active;
        RenderTexture.active = _target;
        GL.Clear(false, true, Color.black);
        RenderTexture.active = previous;
    }

    private void Build(TMP_FontAsset font, Material fontMaterial)
    {
        _rect.gameObject.AddComponent<Image>().color = FrameColor;

        // 위아래 필름 구멍 띠
        for (int row = 0; row < 2; row++)
        {
            for (int i = 0; i < _holesPerRow; i++)
            {
                var hole = NewRect("Hole", _rect);
                float x = (i + 0.5f) / _holesPerRow;
                hole.anchorMin = hole.anchorMax = new Vector2(x, row == 0 ? 1f : 0f);
                hole.sizeDelta = new Vector2(_stripHeight * 0.55f, _stripHeight * 0.42f);
                hole.anchoredPosition = new Vector2(0f, (row == 0 ? -1f : 1f) * _stripHeight * 0.5f);
                hole.gameObject.AddComponent<Image>().color = HoleColor;
            }
        }

        _screen = NewRect("Screen", _rect);
        _screen.anchorMin = Vector2.zero;
        _screen.anchorMax = Vector2.one;
        _screen.offsetMin = new Vector2(_sideBorder, _stripHeight);
        _screen.offsetMax = new Vector2(-_sideBorder, -_stripHeight);
        _video = _screen.gameObject.AddComponent<RawImage>();
        _video.color = Color.white;
        _video.raycastTarget = false;

        var noiseRect = NewRect("Noise", _screen);
        noiseRect.anchorMin = Vector2.zero;
        noiseRect.anchorMax = Vector2.one;
        noiseRect.offsetMin = noiseRect.offsetMax = Vector2.zero;
        _noise = noiseRect.gameObject.AddComponent<RawImage>();
        _noise.texture = _noiseTexture = MakeNoise(96);
        _noise.raycastTarget = false;

        _player = _rect.gameObject.AddComponent<VideoPlayer>();
        _player.playOnAwake = false;
        _player.isLooping = true;
        _player.renderMode = VideoRenderMode.RenderTexture;
        _player.audioOutputMode = VideoAudioOutputMode.None;
        _player.skipOnDrop = true;
        _player.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
        _player.prepareCompleted += OnPrepared;
    }

    private static Texture2D MakeNoise(int size)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Repeat,
        };
        var pixels = new Color32[size * size];
        for (int i = 0; i < pixels.Length; i++)
        {
            byte v = (byte)Random.Range(20, 200);
            pixels[i] = new Color32(v, v, v, 255);
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(parent, false);
        return rect;
    }
}
