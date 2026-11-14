using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Copied in-game scene only: switch between A/E motion concepts and the existing renderer.</summary>
[DefaultExecutionOrder(-10000)]
public sealed class DamageMotionAEPreview : MonoBehaviour
{
    public enum PreviewMode { NormalA_CriticalE, AllA, AllE, Original }
    public PreviewMode Mode;
    public TMP_FontAsset Font;
    public Material FontMaterial;
    public float NormalFontSize = 60;
    public float CriticalFontSize = 78;
    public float MotionScale = 1.5f;
    public Vector2 Offset = new Vector2(0, 45);
    public bool AutoSample = true;
    public float SampleInterval = 1.3f;
    public float SampleDamage = 1280;
    private sealed class Popup
    {
        public TextMeshProUGUI Text;
        public Vector2 Origin;
        public float Born;
        public bool Heavy, Active;
    }
    private readonly List<Popup> _pool = new();
    private BossDamageNumbers[] _oldViews;
    private DamageFloaterManager _original;
    private BossManager _manager;
    private BossBase _boss;
    private RectTransform _root;
    private PreviewMode _appliedMode = (PreviewMode)(-1);
    private float _nextSample;
    private int _sample;
    public int NormalHits { get; private set; }
    public int CriticalHits { get; private set; }
    public BossBase Boss => _boss;
    public int ActiveCount { get { int n = 0; foreach (var p in _pool) if (p.Active) n++; return n; } }
    public bool LastHeavy { get; private set; }

    private void Awake()
    {
        _oldViews = FindObjectsByType<BossDamageNumbers>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        _original = FindFirstObjectByType<DamageFloaterManager>();
        _manager = FindFirstObjectByType<BossManager>();
        ApplyMode();
        var go = new GameObject("AE Damage Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        go.transform.SetParent(transform, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 125;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0;
        _root = (RectTransform)go.transform;
    }

    public void SetMode(PreviewMode mode) { Mode = mode; ApplyMode(); }
    private void ApplyMode()
    {
        if (_appliedMode == Mode) return;
        _appliedMode = Mode;
        Clear();
        bool original = Mode == PreviewMode.Original;
        foreach (var view in _oldViews) if (view.gameObject.scene == gameObject.scene) view.enabled = original;
        // Enabling the existing view also suppresses the legacy manager. When switching
        // back, set this after OnDisable has restored its previous suppression value.
        _original.SuppressOutput = !original || _oldViews.Length > 0;
    }

    private void Update()
    {
        ApplyMode();
        if (_manager.CurrentBoss != _boss)
        {
            if (_boss != null) _boss.OnDamageDealt -= OnDamage;
            _boss = _manager.CurrentBoss;
            if (_boss != null) _boss.OnDamageDealt += OnDamage;
            Clear();
        }
        if (AutoSample && _boss != null && Time.unscaledTime >= _nextSample)
        {
            _nextSample = Time.unscaledTime + SampleInterval;
            Attack(_sample++ % 2 == 1);
        }
        foreach (var p in _pool)
        {
            if (!p.Active) continue;
            float age = Time.unscaledTime - p.Born;
            if (age >= (p.Heavy ? 1f : .72f)) { p.Active = false; p.Text.gameObject.SetActive(false); continue; }
            SampleMotion(age, p.Heavy, out var delta, out var scale, out float rotation, out float alpha);
            p.Text.rectTransform.anchoredPosition = p.Origin + delta * MotionScale;
            p.Text.transform.localScale = scale;
            p.Text.transform.localRotation = Quaternion.Euler(0, 0, rotation);
            var color = p.Text.color; color.a = alpha; p.Text.color = color;
        }
    }

    public static void SampleMotion(float t, bool heavy, out Vector2 delta, out Vector3 scale, out float rotation, out float alpha)
    {
        rotation = 0;
        if (!heavy)
        {
            float s = t < .1f ? .4f + t * 10 : t < .24f ? 1.4f - (t - .1f) * (.4f / .14f) : 1;
            scale = new Vector3(s * (t < .07f ? 1.25f : 1), s * (t < .07f ? .65f : 1), 1);
            delta = new Vector2(0, 58 * (1 - Mathf.Exp(-t * 3)));
            alpha = 1 - Mathf.Clamp01((t - .44f) / .28f);
        }
        else
        {
            float s = t < .065f ? .3f + t * 22 : t < .19f ? 1.73f - (t - .065f) * (.73f / .125f) : 1;
            scale = new Vector3(s, s, 1);
            delta = new Vector2(Mathf.Sin(t * 130) * Mathf.Exp(-t * 15) * 9, t < .4f ? 8 * t : 3.2f + (t - .4f) * 45);
            rotation = Mathf.Sin(t * 85) * Mathf.Exp(-t * 16) * .1f * Mathf.Rad2Deg;
            alpha = 1 - Mathf.Clamp01((t - .7f) / .3f);
        }
    }

    private Vector3 Center()
    {
        var mesh = _boss.GetComponentInChildren<MeshRenderer>();
        if (mesh != null) return mesh.bounds.center;
        var sprite = _boss.GetComponentInChildren<SpriteRenderer>();
        return sprite != null ? sprite.bounds.center : _boss.transform.position + Vector3.up * 2.5f;
    }

    private void OnDamage(decimal damage, Vector3? hit, BossDamageKind kind)
    {
        if (damage <= 0) return;
        bool critical = kind == BossDamageKind.Critical;
        if (critical) CriticalHits++; else NormalHits++;
        if (Mode == PreviewMode.Original) return;
        bool heavy = Mode == PreviewMode.AllE || Mode == PreviewMode.NormalA_CriticalE && critical;
        LastHeavy = heavy;
        var p = _pool.Find(item => !item.Active);
        if (p == null && _pool.Count >= 64)
        {
            p = _pool[0];
            foreach (var item in _pool) if (item.Born < p.Born) p = item;
        }
        if (p == null)
        {
            var go = new GameObject("AE Damage", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(_root, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.raycastTarget = false;
            text.alignment = TextAlignmentOptions.Center;
            text.fontStyle = FontStyles.Bold;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            if (Font != null) text.font = Font;
            if (FontMaterial != null) text.fontSharedMaterial = FontMaterial;
            text.rectTransform.sizeDelta = new Vector2(500, 150);
            p = new Popup { Text = text };
            _pool.Add(p);
        }
        var screen = Camera.main.WorldToScreenPoint(hit ?? Center());
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, null, out var local);
        local += Offset + new Vector2(((NormalHits + CriticalHits) % 3 - 1) * 18, 0);
        local.x = Mathf.Clamp(local.x, _root.rect.xMin + 180, _root.rect.xMax - 180);
        local.y = Mathf.Clamp(local.y, _root.rect.yMin + 100, _root.rect.yMax - 200);
        p.Origin = local; p.Born = Time.unscaledTime; p.Heavy = heavy; p.Active = true;
        p.Text.text = damage.ToString("N0");
        p.Text.fontSize = critical ? CriticalFontSize : NormalFontSize;
        p.Text.color = kind == BossDamageKind.Burn ? new Color(1, .4f, .22f) : kind == BossDamageKind.Rally ?
            new Color(.45f, .9f, 1) : critical ? new Color(1, .82f, .46f) : new Color(.94f, .95f, .85f);
        p.Text.gameObject.SetActive(true);
        p.Text.rectTransform.anchoredPosition = local;
        p.Text.transform.localScale = Vector3.zero;
    }

    public void Clear() { foreach (var p in _pool) { p.Active = false; p.Text.gameObject.SetActive(false); } }
    public void Attack(bool critical)
    {
        if (_boss == null) return;
        // Sample controls must remain usable without killing the comparison target.
        // Ordinary unit attacks still use their unchanged real combat path.
        _boss.Heal(_boss.MaxHp - _boss.CurrentHp);
        decimal damage = System.Math.Min((decimal)SampleDamage, _boss.MaxHp * .1m) * (critical ? 3 : 1);
        _boss.TakeDamage(damage, Center(), critical ? BossDamageKind.Critical : BossDamageKind.Normal);
    }
    public void Burst() => StartCoroutine(BurstHits());
    private IEnumerator BurstHits()
    {
        for (int i = 0; i < 8; i++) { Attack(i % 4 == 3); yield return new WaitForSecondsRealtime(.115f); }
    }

    private void OnGUI()
    {
        float scale = Mathf.Clamp(Screen.width / 750f, .7f, 1.5f);
        var saved = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
        float width = Mathf.Min(480, Screen.width / scale - 16);
        GUILayout.BeginArea(new Rect(8, Screen.height / scale - 178, width, 170), GUI.skin.box);
        GUILayout.Label("DAMAGE MOTION  |  A: Spring Pop / E: Heavy Impact");
        var mode = (PreviewMode)GUILayout.Toolbar((int)Mode, new[] { "A + E", "A only", "E only", "Existing" }, GUILayout.Height(35));
        if (mode != Mode) SetMode(mode);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Normal Hit", GUILayout.Height(30))) Attack(false);
        if (GUILayout.Button("Critical Hit", GUILayout.Height(30))) Attack(true);
        if (GUILayout.Button("8 Hits", GUILayout.Height(30))) Burst();
        GUILayout.EndHorizontal();
        AutoSample = GUILayout.Toggle(AutoSample, "Auto sample (heals target before hit)");
        GUILayout.Label($"Mode: {Mode}  |  Normal {NormalHits}  Critical {CriticalHits}");
        GUILayout.EndArea();
        GUI.matrix = saved;
    }

    private void OnDestroy() { if (_boss != null) _boss.OnDamageDealt -= OnDamage; }
}
