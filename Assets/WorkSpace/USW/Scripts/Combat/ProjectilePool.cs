using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;

/// <summary>
/// 투사체 오브젝트 풀 — InGameSingleton.
/// 씬에 오브젝트로 배치하면 Screen Space Overlay Canvas를 자동 생성한다.
/// 투사체 시각은 Unity 내장 Knob 스프라이트(원형)를 Image로 사용.
/// </summary>
public class ProjectilePool : InGameSingleton<ProjectilePool>
{
    [Header("투사체 설정")]
    [Tooltip("null이면 원형 Image를 코드로 자동 생성")]
    [SerializeField] private Projectile _prefab;

    [SerializeField] private Color   _projectileColor = new Color(1f, 0.45f, 0.1f, 0.95f);
    [SerializeField] private float   _projectileSize  = 22f;
    [SerializeField] private int     _initialPoolSize = 20;

    private ObjectPool<Projectile> _pool;
    private Transform              _container;

    protected override void Awake()
    {
        base.Awake();
        CreateOverlayCanvas();

        _pool = new ObjectPool<Projectile>(
            createFunc:      CreateProjectile,
            actionOnGet:     p => p.gameObject.SetActive(true),
            actionOnRelease: p => p.gameObject.SetActive(false),
            actionOnDestroy: p => Destroy(p.gameObject),
            collectionCheck: false,
            defaultCapacity: _initialPoolSize
        );
    }

    // ── 외부 API ──────────────────────────────────────────────────

    /// <summary>from → to 로 투사체 발사. 도착 후 자동 풀 반환.</summary>
    public void Launch(Vector3 from, Vector3 to)
    {
        var p = _pool.Get();
        p.Launch(from, to, ReturnToPool);
    }

    // ── 내부 ──────────────────────────────────────────────────────

    private void ReturnToPool(Projectile p)
    {
        _pool.Release(p);
    }

    private void CreateOverlayCanvas()
    {
        var canvasGO = new GameObject("ProjectileCanvas");
        canvasGO.transform.SetParent(transform);

        var canvas           = canvasGO.AddComponent<Canvas>();
        canvas.renderMode    = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder  = 200;

        canvasGO.AddComponent<CanvasScaler>();

        _container = canvasGO.transform;
    }

    private Projectile CreateProjectile()
    {
        if (_prefab != null)
        {
            var inst = Instantiate(_prefab, _container);
            return inst;
        }

        // 내장 Knob 스프라이트로 원형 투사체 자동 생성
        var go  = new GameObject("Projectile");
        go.transform.SetParent(_container, false);

        var img    = go.AddComponent<Image>();
        img.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");
        img.color  = _projectileColor;

        var rt          = go.GetComponent<RectTransform>();
        rt.sizeDelta    = new Vector2(_projectileSize, _projectileSize);
        rt.localScale   = Vector3.one;

        return go.AddComponent<Projectile>();
    }
}
