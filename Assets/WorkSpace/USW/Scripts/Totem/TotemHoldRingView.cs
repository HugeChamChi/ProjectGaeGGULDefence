using UnityEngine;

/// <summary>
/// 토템 홀드 원형 게이지의 표시 전용 (TotemHoldFeedback이 소유).
/// 런타임에 SpriteRenderer 쿼드 하나를 만들어 GaeGGUL/Totem Hold Ring 셰이더로 그린다 — 씬·프리팹 편집 없음.
/// 판정·시간 계산은 하지 않고, 받은 진행도/확정 상태를 등장·사라짐·번쩍 연출과 함께 그리기만 한다.
/// </summary>
public sealed class TotemHoldRingView
{
    private const float FadeInSeconds = 0.08f;
    private const float FadeOutSeconds = 0.12f;
    private const float AppearScale = 0.8f;
    private const float ArmedPopScale = 0.08f;
    private const float ArmedPopSeconds = 0.2f;
    private const float ArmedFlashSeconds = 0.25f;
    private const int SortingOffsetBelowTotem = 1;

    private static readonly int ProgressId = Shader.PropertyToID("_Progress");
    private static readonly int ArmedId = Shader.PropertyToID("_Armed");
    private static readonly int VisibilityId = Shader.PropertyToID("_Visibility");
    private static readonly int FlashId = Shader.PropertyToID("_Flash");
    private static readonly int TimeId = Shader.PropertyToID("_HoldRingTime");

    private readonly Material _material;
    private readonly float _worldSize;
    private float _visibility;
    private GameObject _object;
    private SpriteRenderer _renderer;
    private MaterialPropertyBlock _block;
    private Texture2D _quadTexture;
    private Sprite _quadSprite;

    /// <summary>머티리얼이 없으면 아무것도 그리지 않는다.</summary>
    public TotemHoldRingView(Material material, float worldSize)
    {
        _material = material;
        _worldSize = worldSize;
    }

    /// <summary>새로 누를 때 등장 연출을 처음부터 다시 시작한다.</summary>
    public void ResetAppear() => _visibility = 0f;

    /// <summary>
    /// 매 프레임 호출. visible이 false면 서서히 사라진다.
    /// armedAge: 가득 찬 뒤 지난 실제 초 (가득 차지 않았으면 음수).
    /// </summary>
    public void Tick(bool visible, float progress, float armedAge, SpriteRenderer anchor, Transform fallbackAnchor, float now, float dt)
    {
        float fade = visible ? dt / FadeInSeconds : -dt / FadeOutSeconds;
        _visibility = Mathf.Clamp01(_visibility + fade);
        if (_visibility <= 0f)
        {
            if (_object != null && _object.activeSelf) _object.SetActive(false);
            return;
        }
        if (!EnsureCreated()) return;
        if (!_object.activeSelf) _object.SetActive(true);

        if (anchor != null)
        {
            _object.transform.position = anchor.bounds.center;
            _renderer.sortingLayerID = anchor.sortingLayerID;
            _renderer.sortingOrder = anchor.sortingOrder - SortingOffsetBelowTotem;
        }
        else if (fallbackAnchor != null)
        {
            _object.transform.position = fallbackAnchor.position;
        }

        bool armed = armedAge >= 0f;
        float flash = armed ? Mathf.Clamp01(1f - armedAge / ArmedFlashSeconds) : 0f;
        float pop = armed ? Mathf.Sin(Mathf.Clamp01(armedAge / ArmedPopSeconds) * Mathf.PI) * ArmedPopScale : 0f;
        float eased = 1f - (1f - _visibility) * (1f - _visibility);
        float scale = _worldSize * (Mathf.Lerp(AppearScale, 1f, eased) + pop);
        _object.transform.localScale = new Vector3(scale, scale, 1f);

        _renderer.GetPropertyBlock(_block);
        _block.SetFloat(ProgressId, progress);
        _block.SetFloat(ArmedId, armed ? 1f : 0f);
        _block.SetFloat(VisibilityId, _visibility);
        _block.SetFloat(FlashId, flash);
        _block.SetFloat(TimeId, now);
        _renderer.SetPropertyBlock(_block);
    }

    private bool EnsureCreated()
    {
        if (_renderer != null) return true;
        if (_material == null) return false;
        _quadTexture = new Texture2D(4, 4, TextureFormat.RGBA32, false) { name = "TotemHoldRingQuad", wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[16];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
        _quadTexture.SetPixels32(pixels);
        _quadTexture.Apply(false, true);
        // PPU 4 → 1 월드 단위 쿼드. 실제 크기는 localScale(worldSize)로 맞춘다.
        _quadSprite = Sprite.Create(_quadTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        _object = new GameObject("TotemHoldRing");
        _renderer = _object.AddComponent<SpriteRenderer>();
        _renderer.sprite = _quadSprite;
        _renderer.sharedMaterial = _material;
        _block = new MaterialPropertyBlock();
        return true;
    }

    /// <summary>생성한 오브젝트와 텍스처를 정리한다.</summary>
    public void Destroy()
    {
        if (_object != null) Object.Destroy(_object);
        if (_quadSprite != null) Object.Destroy(_quadSprite);
        if (_quadTexture != null) Object.Destroy(_quadTexture);
        _object = null; _renderer = null; _quadSprite = null; _quadTexture = null;
    }
}
