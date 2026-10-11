using System;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 실험실용 보스 피격 글리치 (디시그망 시안 D) — 타이머 해킹 글리치와 같은 언어(가로 띠 어긋남·하늘색/분홍 잔상·툭툭 끊기는 떨림)로
/// 보스를 짧게 찢는다. AllIn1SpriteShader가 있으면 GLITCH_ON 재질로 띠 어긋남을 더하고, 없으면 잔상·떨림만 쓴다.
/// 끝나면 원래 재질·색·위치로 되돌린다.
/// </summary>
public sealed class BossGlitchFx : IDisposable
{
    private const string GlitchShaderName = "AllIn1SpriteShader/AllIn1SpriteShader";
    private const string GlitchKeyword = "GLITCH_ON";
    private const float TickSeconds = 0.04f;
    private const float GlitchSize = 1.6f;
    private const float GlitchSpeed = 25f;
    private const float CyanTint = 0f;          // 노란 보스에 하늘색을 섞으면 탁한 올리브색이 돼서 끔 (2026-10-12 캡처) — 잔상·띠 어긋남만
    private const float GhostAlpha = 0.6f;
    private static readonly int GlitchAmountId = Shader.PropertyToID("_GlitchAmount");
    private static readonly int GlitchSizeId = Shader.PropertyToID("_GlitchSize");
    private static readonly int GlitchSpeedId = Shader.PropertyToID("_GlitchSpeed");

    private readonly SpriteRenderer _boss;
    private readonly SpriteRenderer _ghostCyan;
    private readonly SpriteRenderer _ghostHot;
    private readonly Material _original;
    private readonly Material _glitch;
    private readonly Vector3 _home;
    private readonly Color _baseColor;
    private readonly Color _cyan;
    private readonly Color _hot;
    private float _t = -1f;
    private float _duration;
    private float _jitter;
    private float _ghostOffset;
    private float _amount;
    private int _seed;

    /// <summary>AllIn1 글리치 재질을 쓸 수 있으면 true (아니면 잔상·떨림만).</summary>
    public bool HasShaderGlitch => _glitch != null;

    /// <summary>boss 스프라이트에 붙는다. 잔상 2장은 보스 바로 뒤에 그린다.</summary>
    public BossGlitchFx(SpriteRenderer boss, Color cyan, Color hot)
    {
        _boss = boss;
        _cyan = cyan;
        _hot = hot;
        _home = boss.transform.localPosition;
        _baseColor = boss.color;
        _original = boss.sharedMaterial;
        _ghostCyan = Ghost("GhostCyan");
        _ghostHot = Ghost("GhostHot");
        var shader = Shader.Find(GlitchShaderName);
        if (shader == null || !shader.isSupported) return;
        _glitch = new Material(shader) { name = "BossGlitch (lab)" };
        _glitch.EnableKeyword(GlitchKeyword);
        _glitch.SetFloat(GlitchSizeId, GlitchSize);
        _glitch.SetFloat(GlitchSpeedId, GlitchSpeed);
        _glitch.SetFloat(GlitchAmountId, 0f);
    }

    /// <summary>글리치 시작 (진행 중이면 처음부터). jitter·ghostOffset은 월드 단위, amount는 AllIn1 _GlitchAmount 최대치.</summary>
    public void Trigger(float duration, float jitter, float ghostOffset, float amount)
    {
        _t = 0f;
        _duration = Mathf.Max(0.01f, duration);
        _jitter = jitter;
        _ghostOffset = ghostOffset;
        _amount = amount;
        _seed++;
    }

    /// <summary>dt초 진행 후 그린다.</summary>
    public void Tick(float dt)
    {
        if (_t < 0f || _boss == null) return;
        _t += dt;
        if (_t >= _duration) { Restore(); return; }

        float k = 1f - _t / _duration;
        int tick = Mathf.FloorToInt(_t / TickSeconds);
        float dx = (WorldGlitchBlocks.Hash(_seed * 31, tick) - 0.5f) * 2f * _jitter * k;
        _boss.transform.localPosition = _home + new Vector3(dx, 0f, 0f);
        var tint = Color.Lerp(_baseColor, _cyan, CyanTint * k);
        tint.a = _baseColor.a;
        _boss.color = tint;

        // 잔상은 보스 자식이라 보스 크기로 나눠 월드 간격을 맞춘다.
        float scale = Mathf.Max(0.0001f, _boss.transform.lossyScale.x);
        float spread = _ghostOffset * k / scale;
        float h1 = Mathf.Lerp(0.5f, 1.3f, WorldGlitchBlocks.Hash(_seed * 31 + 7, tick));
        float h2 = Mathf.Lerp(0.5f, 1.3f, WorldGlitchBlocks.Hash(_seed * 31 + 13, tick));
        Show(_ghostCyan, new Vector3(-spread * h1, 0f, 0f), _cyan, GhostAlpha * k);
        Show(_ghostHot, new Vector3(spread * h2, 0f, 0f), _hot, GhostAlpha * k);

        if (_glitch == null) return;
        if (_boss.sharedMaterial != _glitch) _boss.sharedMaterial = _glitch;
        _glitch.SetFloat(GlitchAmountId, _amount * k);
    }

    /// <summary>즉시 원래 모습으로.</summary>
    public void Restore()
    {
        _t = -1f;
        if (_boss == null) return;
        _boss.transform.localPosition = _home;
        _boss.color = _baseColor;
        _boss.sharedMaterial = _original;
        if (_ghostCyan != null) _ghostCyan.enabled = false;
        if (_ghostHot != null) _ghostHot.enabled = false;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Restore();
        if (_glitch != null) Object.Destroy(_glitch);
    }

    private SpriteRenderer Ghost(string name)
    {
        var go = new GameObject(name, typeof(SpriteRenderer));
        go.transform.SetParent(_boss.transform, false);
        var sr = go.GetComponent<SpriteRenderer>();
        sr.sprite = _boss.sprite;
        sr.sortingLayerID = _boss.sortingLayerID;
        sr.sortingOrder = _boss.sortingOrder - 1;
        sr.enabled = false;
        return sr;
    }

    private static void Show(SpriteRenderer sr, Vector3 localPos, Color color, float alpha)
    {
        sr.enabled = true;
        sr.transform.localPosition = localPos;
        sr.color = new Color(color.r, color.g, color.b, alpha);
    }
}
