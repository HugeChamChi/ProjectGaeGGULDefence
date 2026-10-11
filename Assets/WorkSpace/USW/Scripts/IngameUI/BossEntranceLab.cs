using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Fast boss warning with two seamless, opposing text conveyors.</summary>
public sealed class BossEntranceLab : MonoBehaviour
{
    public CanvasGroup BorderGlow;
    public CanvasGroup Warning;
    public CanvasGroup TitleAlpha;
    public RectTransform Headline;
    public RectTransform[] Tapes;
    public RectTransform[] TapeContents;
    public RectTransform Boss;
    public CanvasGroup BossAlpha;
    public Image Progress;
    public TMP_Text Playback;
    public bool Repeat = true;
    [Min(0)] public float TapeSpeed = 840f;
    public const float TileWidth = 360f;
    public const float Duration = 1.95f;
    private float _elapsed;
    private bool _playing;

    private void Start() => Replay();
    public void Replay() { _elapsed = 0; _playing = true; RenderAt(0); }

    public void ToggleRepeat()
    {
        Repeat = !Repeat;
        Playback.text = Repeat ? "자동 반복 ON" : "자동 반복 OFF";
    }

    private void Update()
    {
        if (!_playing) return;
        _elapsed += Time.unscaledDeltaTime;
        RenderAt(_elapsed);
        if (_elapsed < Duration + .7f) return;
        if (Repeat) Replay();
        else _playing = false;
    }

    /// <summary>Absolute-time sampling keeps the modulo seam deterministic, including on replay.</summary>
    public void RenderAt(float time)
    {
        time = Mathf.Max(0, time);
        float enter = 1 - Mathf.Pow(1 - Mathf.Clamp01(time / .18f), 3);
        float exit = Mathf.Clamp01((time - 1.48f) / .22f);
        float visible = enter * (1 - exit);
        Warning.alpha = visible;
        float siren = .5f + .5f * Mathf.Sin(time * Mathf.PI * 3.6f);
        BorderGlow.alpha = visible * (.12f + .78f * siren);
        Progress.fillAmount = Mathf.Clamp01(time / Duration);
        float titleIn = 1 - Mathf.Pow(1 - Mathf.Clamp01((time - .08f) / .17f), 3);
        float titleOut = Mathf.Clamp01((time - 1.23f) / .4f);
        TitleAlpha.alpha = titleIn;
        // Keep drifting through the readable center, then accelerate offscreen before the band closes.
        float titleX = 65 + 1050 * (1 - titleIn) - 85 * Mathf.Max(0, time - .08f)
            - 1250 * titleOut * titleOut * titleOut;
        Headline.anchoredPosition = new Vector2(titleX, 0);
        Headline.localScale = Vector3.one * Mathf.Lerp(1.14f, 1, titleIn);
        float offset = Mathf.Repeat(time * TapeSpeed, TileWidth);
        for (int i = 0; i < Tapes.Length; i++)
        {
            float direction = i == 0 ? -1 : 1;
            Tapes[i].anchoredPosition = new Vector2(
                direction * (-1700 * (1 - enter) + 1700 * exit * exit), i == 0 ? 185 : -185);
            // Identical cells are exactly TileWidth apart, so wrapping cannot reveal a gap.
            TapeContents[i].anchoredPosition = new Vector2(direction * offset, 0);
        }
        float arrival = Mathf.SmoothStep(0, 1, Mathf.Clamp01((time - 1.6f) / .3f));
        BossAlpha.alpha = arrival;
        Boss.anchoredPosition = new Vector2(0, Mathf.Lerp(590, 500, arrival));
        Boss.localScale = Vector3.one * Mathf.Lerp(.85f, 1, arrival);
    }
}
