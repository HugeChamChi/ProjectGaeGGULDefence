using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 이펙트 실험실(FxLab_StatBuff 씬) 전용 — 버프 연출 시안(StatBuffFxStyle)을 버튼으로 골라 반복 재생한다.
///   위 패널: 시안 목록(이름 = 어울리는 토템) · 아래 패널: 색 바꾸기, 처음부터, 다시 걸기(지속 연장 확인), 3기 모두 켜기.
///   기본은 가운데 유닛에만 건다. "3기 모두"를 켜면 양옆에도 같은 시안을 걸어 여러 유닛이 붙어 있을 때 복잡함을 본다.
/// 걸리는 순간 유닛이 살짝 튀어 오른다(실험실 연출 — 게임 유닛 쪽은 별도). 연출 본체(StatBuffFx)는 게임에서도 그대로 쓴다.
/// 빌더: StatBuffFxLabBuilder (Tools/USW/Fx/Build Stat Buff Lab). 시안 전체 캡처: Tools/USW/Fx/Stat Buff Lab - Capture All Styles.
/// </summary>
public sealed class StatBuffFxLab : MonoBehaviour, IFxLabPlayable
{
    [Header("장면 (빌더가 연결) — 순서: 왼쪽 / 가운데 / 오른쪽")]
    [SerializeField] private StatBuffFx[] _fx = System.Array.Empty<StatBuffFx>();
    [SerializeField] private Transform[] _units = System.Array.Empty<Transform>();
    [SerializeField] private StatBuffFxStyle[] _styles = System.Array.Empty<StatBuffFxStyle>();

    [Header("반복")]
    [SerializeField, Min(0)] private int _selected;
    [SerializeField] private bool _allUnits;
    [SerializeField, Min(0.2f)] private float _buffSeconds = 3f;
    [SerializeField, Min(0f)] private float _rest = 1f;
    [Tooltip("유닛마다 시작을 이만큼 늦춘다")]
    [SerializeField, Min(0f)] private float _stagger = 0.15f;
    [SerializeField] private bool _loop = true;
    [SerializeField] private bool _playOnStart = true;
    [SerializeField, Min(0.05f)] private float _fxScale = 1.2f;
    [SerializeField] private float[] _scaleSteps = { 0.9f, 1.2f, 1.5f, 1.8f };
    [SerializeField] private string[] _scaleNames = { "작게", "보통", "크게", "아주 크게" };

    [Header("색 버튼")]
    [SerializeField] private string[] _presetNames = { "민트", "초록", "빨강", "주황", "노랑", "파랑", "보라", "분홍" };
    [SerializeField] private Color[] _presetColors =
    {
        new Color(0.36f, 0.92f, 0.68f, 1f),
        new Color(0.42f, 0.85f, 0.25f, 1f),
        new Color(1f, 0.3f, 0.32f, 1f),
        new Color(1f, 0.58f, 0.2f, 1f),
        new Color(1f, 0.85f, 0.2f, 1f),
        new Color(0.3f, 0.65f, 1f, 1f),
        new Color(0.72f, 0.45f, 1f, 1f),
        new Color(1f, 0.5f, 0.78f, 1f),
    };
    [Tooltip("-1 = 시안 원래 색")]
    [SerializeField] private int _tintIndex = -1;

    [Header("유닛 튀어오름")]
    [SerializeField, Min(0f)] private float _punch = 0.12f;
    [SerializeField, Min(0.05f)] private float _punchSeconds = 0.3f;

    [Header("시안 전체 캡처")]
    [SerializeField] private string _captureFolder = "Temp/FxCapture/StatBuffStyles";
    [SerializeField] private Vector2Int _captureSize = new Vector2Int(540, 1168);
    [SerializeField] private float[] _captureTimes = { 0.55f, 1.6f };

    private Vector3[] _unitScales;
    private bool[] _fired;
    private float _t = -1f;
    private bool _unscaled;
    private bool _capturing;
    private Vector2 _listScroll;
    private GUIStyle _centeredLabel, _wrapLabel;

    /// <inheritdoc/>
    public bool UseUnscaledTime
    {
        get => _unscaled;
        set
        {
            _unscaled = value;
            foreach (var fx in _fx) if (fx != null) fx.UseUnscaledTime = value;
        }
    }

    private StatBuffFxStyle Current => _styles.Length > 0 ? _styles[Mathf.Clamp(_selected, 0, _styles.Length - 1)] : null;

    private void Start()
    {
        _unitScales = new Vector3[_units.Length];
        for (int i = 0; i < _units.Length; i++) _unitScales[i] = _units[i].localScale;
        _fired = new bool[_fx.Length];
        ApplyTint();
        if (_playOnStart) Play();
    }

    /// <summary>처음부터 한 번 재생한다 (캡처 시작점).</summary>
    [ContextMenu("Play")]
    public void Play()
    {
        foreach (var fx in _fx) if (fx != null) fx.Clear();
        _t = 0f;
        if (_fired != null) System.Array.Clear(_fired, 0, _fired.Length);
    }

    /// <summary>표시 중인 버프를 한 번 더 건다 (지속시간 갱신·배지 재등장 확인용).</summary>
    [ContextMenu("Refresh Buffs")]
    public void Refresh()
    {
        for (int i = 0; i < _fx.Length; i++) if (Targeted(i)) Fire(i);
    }

    /// <summary>시안을 고르고 처음부터 재생한다.</summary>
    public void Select(int index)
    {
        if (_styles.Length == 0) return;
        _selected = (index % _styles.Length + _styles.Length) % _styles.Length;
        Play();
    }

    private bool Targeted(int i) => _allUnits || i == _fx.Length / 2;

    private void Update()
    {
        if (_t < 0f) return;
        _t += _unscaled ? Time.unscaledDeltaTime : Time.deltaTime;

        for (int i = 0; i < _fx.Length; i++)
        {
            if (!Targeted(i)) continue;
            float local = _t - (_allUnits ? i * _stagger : 0f);
            if (!_fired[i] && local >= 0f)
            {
                _fired[i] = true;
                Fire(i);
            }
            if (i < _units.Length && _units[i] != null)
            {
                float k = Mathf.Clamp01(local / _punchSeconds);
                float bump = k > 0f && k < 1f ? Mathf.Sin(k * Mathf.PI) * (1f - k) : 0f;
                _units[i].localScale = new Vector3(_unitScales[i].x * (1f - bump * _punch * 0.5f), _unitScales[i].y * (1f + bump * _punch), _unitScales[i].z);
            }
        }

        if (_t >= _buffSeconds + _fx.Length * _stagger + _rest)
        {
            if (_loop) Play();
            else _t = -1f;
        }
    }

    private void Fire(int i)
    {
        if (_fx[i] == null || i >= _units.Length) return;
        _fx[i].Play(_units[i], Current, _buffSeconds, _fxScale);
    }

    private void ApplyTint()
    {
        Color? tint = _tintIndex >= 0 && _tintIndex < _presetColors.Length ? _presetColors[_tintIndex] : (Color?)null;
        foreach (var fx in _fx) if (fx != null) fx.SetTint(tint);
    }

    // ── 버튼 (다른 FxLab과 같은 IMGUI, 540 폭 기준으로 늘린다) ──────

    private void OnGUI()
    {
        if (_capturing) return;
        float scale = Mathf.Max(0.6f, Screen.width / 540f);
        var old = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        float width = Screen.width / scale;
        float height = Screen.height / scale;

        // 위: 현재 시안 + 목록
        var style = Current;
        const float topHeight = 300f;
        GUI.Box(new Rect(10f, 10f, width - 20f, topHeight), $"버프 시안 {_selected + 1}/{_styles.Length} (임시)");
        GUI.Label(new Rect(20f, 32f, width - 40f, 44f), style != null ? $"<b>{style.DisplayName}</b>\n{style.Usage}" : "시안 없음", WrapLabel);
        const int columns = 3;
        float cellWidth = (width - 40f - (columns - 1) * 4f) / columns;
        int rows = Mathf.CeilToInt(_styles.Length / (float)columns);
        var view = new Rect(20f, 80f, width - 40f, topHeight - 76f);
        var content = new Rect(0f, 0f, view.width - 16f, rows * 36f);
        _listScroll = GUI.BeginScrollView(view, _listScroll, content);
        for (int i = 0; i < _styles.Length; i++)
        {
            var r = new Rect((i % columns) * (cellWidth + 4f), (i / columns) * 36f, cellWidth - 6f, 32f);
            var oldBg = GUI.backgroundColor;
            if (i == _selected) GUI.backgroundColor = new Color(1f, 0.9f, 0.4f, 1f);
            string label = _styles[i] != null ? $"{(char)('A' + i)}. {_styles[i].DisplayName}" : "-";
            if (GUI.Button(r, label)) Select(i);
            GUI.backgroundColor = oldBg;
        }
        GUI.EndScrollView();

        // 아래: 색 + 조작
        const float bottomHeight = 196f;
        float top = height - bottomHeight - 10f;
        GUI.Box(new Rect(10f, top, width - 20f, bottomHeight), "색 바꾸기 (조각·오라·기둥·눈금)");
        SwatchRow(new Rect(20f, top + 30f, width - 40f, 50f));
        int steps = Mathf.Min(_scaleSteps.Length, _scaleNames.Length);
        float stepWidth = steps > 0 ? (width - 40f - 64f - (steps - 1) * 4f) / steps : 0f;
        GUI.Label(new Rect(20f, top + 96f, 64f, 34f), "크기");
        for (int i = 0; i < steps; i++)
        {
            var oldBg = GUI.backgroundColor;
            if (Mathf.Approximately(_fxScale, _scaleSteps[i])) GUI.backgroundColor = new Color(1f, 0.9f, 0.4f, 1f);
            if (GUI.Button(new Rect(84f + i * (stepWidth + 4f), top + 92f, stepWidth, 34f), _scaleNames[i]))
            {
                _fxScale = _scaleSteps[i];
                Play();
            }
            GUI.backgroundColor = oldBg;
        }
        float buttonWidth = (width - 56f) / 3f;
        if (GUI.Button(new Rect(20f, top + 140f, buttonWidth, 44f), "처음부터")) Play();
        if (GUI.Button(new Rect(28f + buttonWidth, top + 140f, buttonWidth, 44f), "다시 걸기")) Refresh();
        if (GUI.Button(new Rect(36f + buttonWidth * 2f, top + 140f, buttonWidth, 44f), _allUnits ? "3기 모두: ON" : "3기 모두: OFF"))
        {
            _allUnits = !_allUnits;
            Play();
        }
        GUI.matrix = old;
    }

    private void SwatchRow(Rect area)
    {
        int count = Mathf.Min(_presetColors.Length, _presetNames.Length);
        int cells = count + 1; // 맨 앞 = 시안 원래 색
        const float gap = 4f;
        float w = (area.width - gap * (cells - 1)) / cells;
        var oldColor = GUI.color;
        for (int i = -1; i < count; i++)
        {
            var r = new Rect(area.x + (i + 1) * (w + gap), area.y, w, area.height);
            if (i == _tintIndex)
            {
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(r.x - 3f, r.y - 3f, r.width + 6f, r.height + 6f), Texture2D.whiteTexture);
            }
            GUI.color = i < 0 ? new Color(0.85f, 0.85f, 0.85f, 1f) : _presetColors[i];
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = oldColor;
            GUI.Label(r, i < 0 ? "원래" : _presetNames[i], CenteredLabel);
            if (GUI.Button(r, GUIContent.none, GUIStyle.none))
            {
                _tintIndex = i;
                ApplyTint();
            }
        }
        GUI.color = oldColor;
    }

    private GUIStyle CenteredLabel => _centeredLabel ??= new GUIStyle(GUI.skin.label)
    {
        alignment = TextAnchor.MiddleCenter,
        fontStyle = FontStyle.Bold,
        normal = { textColor = new Color(0.1f, 0.1f, 0.12f, 1f) },
    };

    private GUIStyle WrapLabel => _wrapLabel ??= new GUIStyle(GUI.skin.label) { wordWrap = true, richText = true };

    // ── 시안 전체 캡처 (검토용 PNG) ─────────────────────

    /// <summary>시안마다 처음부터 재생하며 _captureTimes 시점의 카메라 화면을 PNG로 저장한다.</summary>
    [ContextMenu("Capture All Styles")]
    public void CaptureAllStyles() => CaptureAllAsync(this.GetCancellationTokenOnDestroy()).Forget();

    private async UniTaskVoid CaptureAllAsync(CancellationToken token)
    {
        var cam = Camera.main;
        if (cam == null || _capturing) return;
        string dir = Path.Combine(Directory.GetCurrentDirectory(), _captureFolder);
        if (Directory.Exists(dir)) foreach (var f in Directory.GetFiles(dir, "*.png")) File.Delete(f);
        Directory.CreateDirectory(dir);

        int oldRate = Time.captureFramerate;
        bool oldUnscaled = UseUnscaledTime;
        int oldSelected = _selected;
        _capturing = true;
        Time.captureFramerate = 30;
        UseUnscaledTime = false;
        try
        {
            for (int i = 0; i < _styles.Length; i++)
            {
                Select(i);
                foreach (float at in _captureTimes)
                {
                    await UniTask.WaitUntil(() => _t >= at, cancellationToken: token);
                    Save(cam, Path.Combine(dir, $"{i:00}_{(char)('A' + i)}_{at:0.00}.png"));
                }
            }
            Debug.Log($"[StatBuffLab] 시안 {_styles.Length}종 캡처: {dir}");
        }
        finally
        {
            Time.captureFramerate = oldRate;
            UseUnscaledTime = oldUnscaled;
            _capturing = false;
            Select(oldSelected);
        }
    }

    private void Save(Camera cam, string path)
    {
        var rt = RenderTexture.GetTemporary(_captureSize.x, _captureSize.y, 24);
        var prevTarget = cam.targetTexture;
        var prevActive = RenderTexture.active;
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        cam.targetTexture = prevTarget;
        RenderTexture.active = prevActive;
        RenderTexture.ReleaseTemporary(rt);
        Destroy(tex);
    }
}
