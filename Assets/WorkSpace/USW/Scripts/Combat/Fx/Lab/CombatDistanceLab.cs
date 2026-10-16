using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

/// <summary>인게임 복사 씬에서 배치와 카메라를 유지하며 보스와 발판의 거리만 비교한다.</summary>
[DefaultExecutionOrder(1000)]
public sealed class CombatDistanceLab : MonoBehaviour
{
    [SerializeField] private InGameLifetimeScope _scope;
    [SerializeField] private CombatDistanceSettings _settings;
    [SerializeField] private SpriteRenderer _background;
    [SerializeField] private TMP_Text _status;
    [SerializeField] private TMP_Text _pauseLabel;
    [SerializeField] private Image[] _buttons;

    [Inject] private GameInitializer _initializer;
    [Inject] private GameManager _game;
    [Inject] private BossManager _bossManager;
    [Inject] private GridManager _grid;
    [Inject] private UnitFactory _factory;
    [Inject] private UnitSpawner _spawner;
    [Inject] private TimeScaleService _timeScale;

    private BossBase _boss;
    private Vector3 _bossOrigin;
    private Mesh _backdropMesh;
    private Vector3[] _originalVertices;
    private Vector3[] _movedVertices;
    private float[] _backgroundWeights;
    private bool _paused;
    private int _preset;
    private static readonly string[] Labels = { "현재", "조금 가까움", "많이 가까움" };

    /// <summary>전투 및 비교 UI 준비 여부.</summary>
    public bool IsReady { get; private set; }
    /// <summary>현재 비교 단계.</summary>
    public int Preset => _preset;
    /// <summary>실제 전투와 피격 영역이 사용하는 보스.</summary>
    public BossBase Boss => _boss;
    /// <summary>원본 씬에서의 보스 위치.</summary>
    public Vector3 OriginalBossPosition => _bossOrigin;

    private void Start() => InitializeAsync().Forget();

    private async UniTaskVoid InitializeAsync()
    {
        var token = this.GetCancellationTokenOnDestroy();
        try
        {
            if (_scope == null || _settings == null) throw new InvalidOperationException("Distance lab references missing");
            // This standalone lab is its own composition entry, with the copied scene's existing scope.
            await UniTask.WaitUntil(() => _scope.Container != null, cancellationToken: token);
            _scope.Container.Inject(this);
            await UniTask.WaitUntil(() => _initializer.IsReady, cancellationToken: token);
            ValidateSettings();
            UnityEngine.Random.InitState(_settings.Seed);
            _game.StartGame(false);
            if (_game.CurrentState != GameManager.GameState.Playing) throw new InvalidOperationException("Combat preview did not start");
            _bossManager.SpawnSingleBoss(new BossEntry { Data = _settings.Boss }, null);
            _boss = _bossManager.CurrentBoss;
            if (_boss == null) throw new InvalidOperationException("Boss failed to spawn");
            _boss.Init(_settings.PreviewHealth);
            _boss.PreventDeath = true;
            _boss.ExpMultiplier = 0f;
            _bossOrigin = _boss.transform.position;
            BuildBackdrop();
            for (int i = 0; i < _settings.Cells.Length; i++)
            {
                var cell = _grid.GetCell(_settings.Cells[i]);
                if (cell == null || !cell.IsAvailable) throw new InvalidOperationException("Invalid lab cell " + _settings.Cells[i]);
                var unit = _factory.CreateUnitFromData(_settings.Units[i % _settings.Units.Length], _settings.SpawnTier);
                if (unit == null) throw new InvalidOperationException("Unit failed to load");
                _spawner.PlaceUnitWithEffect(unit, cell);
            }
            IsReady = true;
            SelectPreset(0);
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            if (_status != null) _status.text = "거리 비교를 준비하지 못했습니다";
            Debug.LogException(error, this);
        }
    }

    private void ValidateSettings()
    {
        if (_settings.Boss == null || _settings.Units == null || _settings.Units.Length == 0
            || _settings.Cells == null || _settings.Approach == null || _settings.Approach.Length != Labels.Length)
            throw new InvalidOperationException("Incomplete distance settings");
        foreach (var data in _settings.Units) if (data == null) throw new InvalidOperationException("Missing unit data");
        float gap = _settings.BackgroundGap.y - _settings.BackgroundGap.x;
        if (gap <= 0f || _settings.PreviewHealth <= 0) throw new InvalidOperationException("Invalid gap or health");
        foreach (float distance in _settings.Approach)
            if (distance < 0f || distance >= gap) throw new InvalidOperationException("Background gap would fold");
    }

    /// <summary>보스 본체와 위 발판을 같은 거리만큼 이동하며 실제 공격 대상도 함께 옮긴다.</summary>
    public void SelectPreset(int index)
    {
        if (!IsReady || _boss == null) return;
        _preset = Mathf.Clamp(index, 0, Labels.Length - 1);
        float approach = _settings.Approach[_preset];
        _boss.transform.position = _bossOrigin + Vector3.down * approach;
        for (int i = 0; i < _movedVertices.Length; i++)
        {
            _movedVertices[i] = _originalVertices[i]
                + _background.transform.InverseTransformVector(Vector3.down * approach * _backgroundWeights[i]);
        }
        _backdropMesh.vertices = _movedVertices;
        _backdropMesh.RecalculateBounds();
        for (int i = 0; i < _buttons.Length; i++)
            _buttons[i].color = i == _preset ? new Color(.12f,.55f,.53f,.97f) : new Color(.12f,.16f,.23f,.96f);
        _status.text = "거리 비교 · " + Labels[_preset];
    }

    /// <summary>동일한 자세로 비교할 수 있도록 실험실 소유의 정지 요청을 켜거나 끈다.</summary>
    public void TogglePause()
    {
        if (!IsReady) return;
        _paused = !_paused;
        if (_paused) _timeScale.Pause(this);
        else _timeScale.Release(this);
        _pauseLabel.text = _paused ? "전투 재생" : "일시정지";
    }

    private void BuildBackdrop()
    {
        if (_background == null || _background.sprite == null) throw new InvalidOperationException("Background missing");
        var sprite = _background.sprite;
        var bounds = sprite.bounds;
        float bottom = bounds.min.y;
        float top = bounds.max.y;
        float lower = _background.transform.InverseTransformPoint(new Vector3(0,_settings.BackgroundGap.x,0)).y;
        float upper = _background.transform.InverseTransformPoint(new Vector3(0,_settings.BackgroundGap.y,0)).y;
        float[] rows = { bottom, lower, upper, top };
        _originalVertices = new Vector3[8];
        _movedVertices = new Vector3[8];
        _backgroundWeights = new float[8];
        var uv = new Vector2[8];
        var colors = new Color[8];
        var normals = new Vector3[8];
        var rect = sprite.textureRect;
        for (int r = 0; r < 4; r++)
        for (int c = 0; c < 2; c++)
        {
            int i = r * 2 + c;
            _originalVertices[i] = new Vector3(c == 0 ? bounds.min.x : bounds.max.x, rows[r], 0);
            _backgroundWeights[i] = r < 2 ? 0f : 1f;
            uv[i] = new Vector2((rect.x + c * rect.width) / sprite.texture.width,
                (rect.y + Mathf.InverseLerp(bottom,top,rows[r]) * rect.height) / sprite.texture.height);
            colors[i] = _background.color;
            normals[i] = Vector3.back;
        }
        var triangles = new int[18];
        for (int r = 0; r < 3; r++)
        {
            int v = r * 2, t = r * 6;
            triangles[t]=v; triangles[t+1]=v+2; triangles[t+2]=v+1;
            triangles[t+3]=v+1; triangles[t+4]=v+2; triangles[t+5]=v+3;
        }
        _backdropMesh = new Mesh { name = "Distance comparison backdrop" };
        _backdropMesh.vertices = _originalVertices;
        _backdropMesh.uv = uv; _backdropMesh.colors=colors; _backdropMesh.normals=normals; _backdropMesh.triangles=triangles;
        _backdropMesh.RecalculateBounds();
        var go = new GameObject("Distance backdrop",typeof(MeshFilter),typeof(MeshRenderer));
        go.transform.SetParent(_background.transform,false);
        go.GetComponent<MeshFilter>().sharedMesh = _backdropMesh;
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = _background.sharedMaterial;
        renderer.sortingLayerID=_background.sortingLayerID; renderer.sortingOrder=_background.sortingOrder;
        var properties = new MaterialPropertyBlock(); _background.GetPropertyBlock(properties);
        properties.SetTexture("_MainTex",sprite.texture); renderer.SetPropertyBlock(properties);
        _background.enabled = false;
    }

    private void OnDestroy()
    {
        _timeScale?.Release(this);
        if (_backdropMesh != null) Destroy(_backdropMesh);
    }
}
