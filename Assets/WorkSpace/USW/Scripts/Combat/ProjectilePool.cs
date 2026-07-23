using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;

/// <summary>
/// 투사체 오브젝트 풀 — InGameSingleton.
/// 투사체 시각은 Unity 내장 Knob 스프라이트(원형)를 SpriteRenderer로 사용.
/// 2D 월드 공간에서 동작합니다.
/// </summary>
public class ProjectilePool : MonoBehaviour
{
    [Header("투사체 설정")]
    [Tooltip("null이면 원형 Image를 코드로 자동 생성")]
    [SerializeField] private Projectile _prefab;

    [Tooltip("Launch()에 ProjectileData를 지정하지 않았을 때 사용할 기본 이동/이펙트 구성")]
    [SerializeField] private ProjectileData _defaultData;

    [SerializeField] private Color   _projectileColor = new Color(1f, 0.45f, 0.1f, 0.95f);
    [SerializeField] private float   _projectileSize  = 22f;
    [SerializeField] private int     _initialPoolSize = 20;

    private ObjectPool<Projectile> _pool;
    private Transform              _container;

    protected void Awake()
    {
        CreateWorldContainer();

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

    /// <summary>from → to 로 투사체 발사(기본 구성 사용). 도착 후 onHitCallback 호출 및 자동 풀 반환.
    /// sizeMultiplier: 이 발사 1회에만 적용되는 추가 크기 배율(예: 스킬 데이터의 투사체 크기 증가치). 기본 1(변화 없음).</summary>
    public void Launch(Vector3 from, Vector3 to, System.Action onHitCallback = null, UnitBase sourceUnit = null, float sizeMultiplier = 1f)
        => Launch(from, to, _defaultData, onHitCallback, sourceUnit, sizeMultiplier);

    /// <summary>from → to 로 투사체 발사(지정 ProjectileData 사용). 도착 후 onHitCallback 호출 및 자동 풀 반환.
    /// sourceUnit을 전달하면 발사 유닛의 BuffController(예: "용기" 버프)로 인한 투사체 크기 증가분도 함께 반영된다.
    /// sizeMultiplier: 이 발사 1회에만 적용되는 추가 크기 배율(예: 스킬 데이터의 투사체 크기 증가치). 기본 1(변화 없음).</summary>
    public void Launch(Vector3 from, Vector3 to, ProjectileData data, System.Action onHitCallback = null, UnitBase sourceUnit = null, float sizeMultiplier = 1f)
    {
        // ProjectileData가 자체 프리팹/주소를 지정한 경우, 공용 풀 대신 그때그때 생성/파괴한다.
        // 스킬 전용 투사체처럼 드물게 쓰이는 비주얼까지 별도 풀을 만들 필요는 없다.
        Projectile custom = data?.projectilePrefab != null
            ? RM.Instantiate(data.projectilePrefab, from, Quaternion.identity, _container, false)
            : (!string.IsNullOrEmpty(data?.projectileAddress)
                ? RM.Instantiate<Projectile>(data.projectileAddress, from, Quaternion.identity, _container, false)
                : null);

        if (custom != null)
        {
            custom.gameObject.SetActive(true);
            custom.Launch(from, to, proj =>
            {
                onHitCallback?.Invoke();
                RM.Destroy(proj);
            }, data, sourceUnit, sizeMultiplier);
            return;
        }

        var p = _pool.Get();
        p.Launch(from, to, proj =>
        {
            onHitCallback?.Invoke();
            ReturnToPool(proj);
        }, data, sourceUnit, sizeMultiplier);
    }

    // ── 내부 ──────────────────────────────────────────────────────

    private void ReturnToPool(Projectile p)
    {
        _pool.Release(p);
    }

    private void CreateWorldContainer()
    {
        var containerGO = new GameObject("ProjectileContainer");
        // 부모를 Canvas 등이 아닌 씬의 최상단(null)으로 설정하여 WorldSpace 좌표계를 온전히 사용하도록 합니다.
        containerGO.transform.SetParent(null);
        containerGO.transform.position = Vector3.zero;
        containerGO.transform.localScale = Vector3.one;
        
        _container = containerGO.transform;
    }

    private Projectile CreateProjectile()
    {
        if (_prefab != null)
        {
            var inst = Instantiate(_prefab, _container);
            if (inst.TryGetComponent<SpriteRenderer>(out var srInst))
            {
                srInst.sortingOrder = 100;
            }
            return inst;
        }

        // 원형 투사체 자동 생성
        var go  = new GameObject("Projectile");
        go.transform.SetParent(_container, false);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CreateCircleSprite(64);
        sr.color  = _projectileColor;
        sr.sortingOrder = 100; // 그리드나 유닛, 배경보다 상위에 확실히 노출

        go.transform.localScale = new Vector3(_projectileSize / 100f, _projectileSize / 100f, 1f);

        return go.AddComponent<Projectile>();
    }

    private static Sprite CreateCircleSprite(int size)
    {
        var tex    = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];
        float r    = size * 0.5f;
        var center = new Vector2(r, r);

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
            float alpha = Mathf.Clamp01((r - dist) / 1.5f);
            pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}
