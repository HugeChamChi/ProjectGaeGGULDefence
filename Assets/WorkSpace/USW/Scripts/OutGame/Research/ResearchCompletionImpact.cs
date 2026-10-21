using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>영구 강화 MAX 도달 시 노드 폭발·탄성 또는 배경 경계의 가로 섬광.</summary>
public sealed class ResearchCompletionImpact : MonoBehaviour
{
    private RectTransform _target, _effect;
    private readonly Image[] _lights = new Image[16];
    private bool _horizontal;
    private Color _tint;
    private Tween _motion;

    public void Initialize(ResearchViewSettings settings, bool horizontal = false)
    {
        _target = (RectTransform)transform; _horizontal = horizontal;
        _tint = settings.BackgroundBandColor;
        _tint = Color.Lerp(_tint, new Color(1f, .95f, .65f), .55f);
        _effect = ResearchUi.NewRect(transform, "MaxCompletionImpact");
        _effect.anchorMin = _effect.anchorMax = new Vector2(.5f, .5f);
        _effect.sizeDelta = Vector2.zero;
        for (int i = 0; i < _lights.Length; i++)
        {
            _lights[i] = ResearchUi.NewImage(_effect, "CompletionLight_" + i,
                !horizontal && i % 3 == 0 ? settings.CircleFill : null, Vector2.zero, Vector2.zero);
            _lights[i].rectTransform.anchorMin = _lights[i].rectTransform.anchorMax = Vector2.one * .5f;
        }
        Stop();
    }

    public void Play()
    {
        Stop();
        Sample(0);
        _motion = DOVirtual.Float(0, 1, .56f, Sample).SetEase(Ease.Linear)
            .SetUpdate(true).SetLink(gameObject).OnComplete(Stop);
    }

    private void Sample(float t)
    {
        _effect.gameObject.SetActive(true);
        float travel = 1 - Mathf.Pow(1 - t, 3);
        float fade = Mathf.Pow(1 - t, 2);
        if (!_horizontal)
        {
            Vector2 scale = t < .12f ? Vector2.Lerp(Vector2.one, new Vector2(1.13f, .82f), t / .12f)
                : t < .3f ? Vector2.Lerp(new Vector2(1.13f, .82f), new Vector2(.91f, 1.22f), (t - .12f) / .18f)
                : t < .55f ? Vector2.Lerp(new Vector2(.91f, 1.22f), new Vector2(1.07f, .96f), (t - .3f) / .25f)
                : Vector2.Lerp(new Vector2(1.07f, .96f), Vector2.one, 1 - Mathf.Pow((1 - t) / .45f, 3));
            _target.localScale = new Vector3(scale.x, scale.y, 1);
        }
        float size = Mathf.Min(_target.rect.width, _target.rect.height);
        for (int i = 0; i < _lights.Length; i++)
        {
            var rect = _lights[i].rectTransform;
            var tint = i == 0 ? Color.white : _tint; tint.a = fade * (_horizontal && i > 0 ? .25f / i : 1);
            _lights[i].color = tint;
            if (_horizontal)
            {
                rect.sizeDelta = new Vector2(_target.rect.width * Mathf.Lerp(.15f, 1, Mathf.Min(1, travel * 3)) * (1 - i * .018f), 3 + i * 5);
                rect.anchoredPosition = Vector2.zero;
            }
            else
            {
                float angle = i * Mathf.PI * 2 / _lights.Length;
                rect.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * size * Mathf.Lerp(.25f, .92f, travel);
                rect.localEulerAngles = new Vector3(0, 0, angle * Mathf.Rad2Deg);
                rect.sizeDelta = (i % 3 == 0 ? Vector2.one * .055f : new Vector2(.19f, .025f)) * size * (1 - t * .8f);
            }
        }
    }

    public void Stop()
    {
        _motion?.Kill(); _motion = null;
        if (!_horizontal && _target != null) _target.localScale = Vector3.one;
        if (_effect != null) _effect.gameObject.SetActive(false);
    }
    private void OnDisable() => Stop();
    private void OnDestroy() => Stop();
}
