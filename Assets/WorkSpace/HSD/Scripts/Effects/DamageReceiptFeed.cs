using TMPro;
using UnityEngine;

/// <summary>
/// 식량 플로터와 같은 "모아서 한 자리에서 띄우기" 방식의 데미지 표시.
/// 피해를 FlushInterval 동안 종류별(일반/치명타/화상)로 합산했다가, 보스 옆 고정 위치에서 숫자 하나로 띄워
/// 위로 떠오르며 사라지게 한다. 맞은 위치마다 흩뿌리지 않아 보스 이미지를 가리지 않는다.
/// - 숫자 오브젝트는 MaxLines개를 미리 만들어 재사용한다 (피격마다 생성/파괴 없음). 모자라면 가장 오래된 것을 재사용.
/// - 같은 묶음에 여러 종류가 있으면 세로로 살짝 어긋나게 띄워 겹치지 않게 한다.
/// - 실제 시간(unscaled) 기준 — 토템 홀드 슬로우 중에도 같은 속도로 읽힌다.
/// DamageFloaterManager가 생성·설정한다.
/// </summary>
public sealed class DamageReceiptFeed : MonoBehaviour
{
    /// <summary>생성 시 한 번 넘기는 설정 (DamageFloaterManager 인스펙터 값).</summary>
    public struct Settings
    {
        public int MaxLines;
        /// <summary>피해를 모았다가 한 번에 띄우는 주기 (실제 초).</summary>
        public float FlushInterval;
        /// <summary>숫자 하나가 떠올라 사라지기까지 (실제 초).</summary>
        public float Duration;
        /// <summary>떠오르는 거리 = 글자 높이 × 이 값.</summary>
        public float RiseLines;
        /// <summary>같은 묶음 안 종류별 세로 간격 = 글자 높이 × 이 값.</summary>
        public float LineHeightFactor;
        /// <summary>글자 크기 배율 (데미지 텍스트 프리팹 기준).</summary>
        public float TextScale;
        /// <summary>보스 스프라이트 오른쪽 끝에서 떨어지는 화면 픽셀.</summary>
        public Vector2 ScreenOffset;
        /// <summary>화면 가장자리 여백 (픽셀).</summary>
        public float EdgePadding;
        public Color NormalColor;
        public Color CriticalColor;
        public Color BurnColor;
        public float CriticalScale;
        public float BurnScale;
    }

    private const float PopSeconds = 0.12f;
    private const float PopOvershoot = 0.3f;
    private const float FadeStart = 0.45f; // 수명 비율 — 이후부터 흐려진다
    private static readonly BossDamageKind[] Kinds = { BossDamageKind.Normal, BossDamageKind.Critical, BossDamageKind.Burn };

    private sealed class Popup
    {
        public RectTransform Rect;
        public TextMeshProUGUI Text;
        public bool Active;
        public BossDamageKind Kind;
        public float BornAt;
        public float BaseScale;
        public Vector2 Start;
    }

    private Popup[] _popups;
    private Settings _settings;
    private RectTransform _container;
    private Canvas _canvas;
    private Transform _anchorTarget;
    private SpriteRenderer _anchorRenderer;
    private float _lineHeight;             // 컨테이너 로컬 단위의 글자 높이
    private readonly decimal[] _pending = new decimal[3];
    private float _nextFlushAt;

    /// <summary>템플릿(TMP를 가진 데미지 텍스트 프리팹)을 복제해 숫자 오브젝트를 미리 만든다.</summary>
    public void Initialize(GameObject template, DamageFloaterStyle style, Settings settings, RectTransform container)
    {
        _settings = settings;
        _container = container;
        _canvas = container != null ? container.GetComponentInParent<Canvas>() : null;
        int count = Mathf.Max(1, settings.MaxLines);
        _popups = new Popup[count];
        for (int i = 0; i < count; i++)
        {
            var go = Instantiate(template, container, false);
            go.name = "DamagePopup" + i;
            // 프리팹의 FloaterBase 애니메이션(랜덤 오프셋·파괴)은 쓰지 않는다 — 직접 제어하고 재사용한다.
            var floater = go.GetComponent<FloaterBase>();
            if (floater != null) floater.enabled = false;
            var text = go.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                text.raycastTarget = false;
                if (style != null)
                {
                    if (style.fontAsset != null) text.font = style.fontAsset;
                    if (style.fontMaterial != null) text.fontSharedMaterial = style.fontMaterial;
                    text.fontSize = style.fontSize;
                }
                text.alignment = TextAlignmentOptions.MidlineLeft;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                _lineHeight = text.fontSize * TextScale;
            }
            var rect = (RectTransform)go.transform;
            // 위치는 컨테이너 중심 기준 로컬 좌표로 계산하므로 앵커를 중앙에 고정한다. 왼쪽 정렬로 보스 옆에 붙인다.
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            go.SetActive(false);
            _popups[i] = new Popup { Rect = rect, Text = text };
        }
    }

    /// <summary>숫자를 띄울 기준 보스. 스프라이트 경계 오른쪽 옆에 띄운다.</summary>
    public void SetAnchor(Transform target)
    {
        _anchorTarget = target;
        _anchorRenderer = target != null ? target.GetComponentInChildren<SpriteRenderer>() : null;
        for (int i = 0; i < _pending.Length; i++) _pending[i] = 0;
        if (_popups != null) foreach (var p in _popups) Deactivate(p);
    }

    /// <summary>피해 한 건을 합산 대기열에 넣는다. 실제 표시는 FlushInterval마다.</summary>
    public void Add(decimal amount, BossDamageKind kind)
    {
        if (_popups == null || amount <= 0) return;
        int k = Mathf.Clamp((int)kind, 0, _pending.Length - 1);
        bool idle = _pending[0] == 0 && _pending[1] == 0 && _pending[2] == 0;
        if (idle && Time.unscaledTime >= _nextFlushAt)
            _nextFlushAt = Time.unscaledTime + _settings.FlushInterval; // 조용하다가 첫 피해가 오면 주기를 새로 시작
        _pending[k] += amount;
    }

    private void LateUpdate()
    {
        if (_popups == null) return;
        float now = Time.unscaledTime;
        if (now >= _nextFlushAt) Flush(now);
        Animate(now);
    }

    private void Flush(float now)
    {
        _nextFlushAt = now + _settings.FlushInterval;
        if (_pending[0] == 0 && _pending[1] == 0 && _pending[2] == 0) return;
        Vector2 basePos = ComputeBase();
        int row = 0;
        foreach (var kind in Kinds)
        {
            int k = (int)kind;
            if (_pending[k] <= 0) continue;
            Spawn(kind, _pending[k], basePos + new Vector2(0f, -row * _lineHeight * _settings.LineHeightFactor), now);
            _pending[k] = 0;
            row++;
        }
    }

    private void Spawn(BossDamageKind kind, decimal amount, Vector2 start, float now)
    {
        Popup target = null;
        foreach (var p in _popups)
        {
            if (!p.Active) { target = p; break; }
            if (target == null || p.BornAt < target.BornAt) target = p; // 모두 사용 중이면 가장 오래된 것
        }
        if (target == null) return;

        target.Active = true;
        target.Kind = kind;
        target.BornAt = now;
        target.Start = start;
        target.BaseScale = TextScale * (kind == BossDamageKind.Critical ? _settings.CriticalScale
            : kind == BossDamageKind.Burn ? _settings.BurnScale : 1f);
        if (target.Text != null)
        {
            target.Text.fontStyle = kind == BossDamageKind.Critical ? FontStyles.Bold : FontStyles.Normal;
            target.Text.text = amount < 10m ? amount.ToString("0.#") : amount.ToString("N0");
            target.Text.color = ColorOf(kind);
        }
        target.Rect.anchoredPosition = start;
        target.Rect.localScale = Vector3.zero;
        target.Rect.gameObject.SetActive(true);
        target.Rect.SetAsLastSibling();
    }

    private void Animate(float now)
    {
        float duration = Mathf.Max(_settings.Duration, 0.05f);
        float rise = _lineHeight * _settings.RiseLines;
        foreach (var p in _popups)
        {
            if (!p.Active) continue;
            float age = now - p.BornAt;
            float t = age / duration;
            if (t >= 1f) { Deactivate(p); continue; }

            // 떠오름: 처음에 빠르고 끝에 느리게.
            float eased = 1f - (1f - t) * (1f - t);
            p.Rect.anchoredPosition = p.Start + new Vector2(0f, rise * eased);

            // 등장 팝: 0 → 1.3 → 1
            float pop = age < PopSeconds
                ? Mathf.Lerp(0f, 1f + PopOvershoot, age / PopSeconds)
                : 1f + PopOvershoot * Mathf.Max(0f, 1f - (age - PopSeconds) / PopSeconds);
            float scale = p.BaseScale * pop;
            p.Rect.localScale = new Vector3(scale, scale, 1f);

            if (p.Text != null)
            {
                var c = ColorOf(p.Kind);
                c.a *= t <= FadeStart ? 1f : 1f - (t - FadeStart) / (1f - FadeStart);
                p.Text.color = c;
            }
        }
    }

    private void Deactivate(Popup p)
    {
        p.Active = false;
        if (p.Rect != null) p.Rect.gameObject.SetActive(false);
    }

    private float TextScale => _settings.TextScale > 0f ? _settings.TextScale : 1f;

    private Color ColorOf(BossDamageKind kind) =>
        kind == BossDamageKind.Critical ? _settings.CriticalColor
        : kind == BossDamageKind.Burn ? _settings.BurnColor
        : _settings.NormalColor;

    // 보스 스프라이트 오른쪽 끝 옆(화면 픽셀 기준). 화면 공간·월드 공간 캔버스 모두 화면 좌표를 거쳐 컨테이너 로컬로 바꾼다.
    // 떠오르는 높이까지 포함해 숫자가 화면 안에 머물도록 제한한다.
    private Vector2 ComputeBase()
    {
        if (_container == null || _anchorTarget == null) return Vector2.zero;
        var worldCam = Camera.main;
        if (worldCam == null) return Vector2.zero;
        Camera uiCam = _canvas == null || _canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null : (_canvas.worldCamera != null ? _canvas.worldCamera : worldCam);

        Vector3 world = _anchorRenderer != null
            ? new Vector3(_anchorRenderer.bounds.max.x, _anchorRenderer.bounds.center.y, _anchorRenderer.bounds.center.z)
            : _anchorTarget.position;
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(worldCam, world) + _settings.ScreenOffset;

        // 로컬 글자 높이를 화면 픽셀로 환산해 숫자 폭·떠오르는 높이를 추정한다.
        Vector3 w0 = _container.TransformPoint(Vector3.zero);
        Vector3 w1 = _container.TransformPoint(new Vector3(0f, _lineHeight, 0f));
        Vector2 p0 = uiCam == null ? (Vector2)w0 : RectTransformUtility.WorldToScreenPoint(uiCam, w0);
        Vector2 p1 = uiCam == null ? (Vector2)w1 : RectTransformUtility.WorldToScreenPoint(uiCam, w1);
        float linePx = Mathf.Abs(p1.y - p0.y);
        float risePx = linePx * (_settings.RiseLines + 1f);
        float belowPx = linePx * _settings.LineHeightFactor * (Kinds.Length - 1);
        float widthPx = linePx * 4f;
        float pad = _settings.EdgePadding;
        screen.x = Mathf.Clamp(screen.x, pad, Mathf.Max(pad, Screen.width - pad - widthPx));
        screen.y = Mathf.Clamp(screen.y, pad + belowPx + linePx * 0.5f, Mathf.Max(pad, Screen.height - pad - risePx));

        RectTransformUtility.ScreenPointToLocalPointInRectangle(_container, screen, uiCam, out var local);
        return local;
    }
}
