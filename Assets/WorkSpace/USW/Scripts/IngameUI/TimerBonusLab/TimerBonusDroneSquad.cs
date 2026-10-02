using System;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 처치 전 공격 — 실제 드론 유닛 프리팹(Normal/Buffer/Debuffer)을 보스 주위에 띄우고, 번갈아 실제 레이저 투사체(DroneLaser_*)를 쏜다 (사용자 요청 2026-10-02).
/// 레이저는 각 프리팹의 ProjectileData를 실행 중에 복제해 피격 이펙트(hitEffect)만 비운 채 발사한다 → 보스 쪽 피격 이펙트 없음. 원본 에셋은 바꾸지 않는다.
/// 드론·레이저는 월드 스프라이트(Unit/FX 정렬 레이어)라 캔버스 위에 그려진다.
/// 발사 시점은 실험실 시계를 따르지만, 레이저 비행·드론 부유는 게임 시간으로 움직인다 (슬로모션·히트스톱 미적용).
/// 자리·크기·간격은 TimerBonusLabSettings에서 매 프레임 읽는다 → 플레이 중 인스펙터로 조정하면 바로 반영되고 에셋에 남는다.
/// </summary>
public sealed class TimerBonusDroneSquad : IDisposable
{
    /// <summary>드론 한 종. 빌더가 드론 프리팹에서 레이저·공격 프레임을 읽어 채운다.</summary>
    [Serializable]
    public class Entry
    {
        public GameObject DronePrefab;
        public Projectile LaserPrefab;
        [Tooltip("레이저 원본 데이터 — 실행 중 복제본에서 hitEffect만 비워 쓴다")]
        public ProjectileData LaserData;
        [Tooltip("발사 순간 잠깐 바뀌는 스프라이트 (날개 펼침)")]
        public Sprite AttackSprite;
    }

    private const float AttackHoldSeconds = 0.18f;   // DroneUnit 기본값과 같게
    private const float PunchScale = 0.18f;
    private const float PunchSeconds = 0.2f;
    private const float AimRadiusPx = 40f;

    private sealed class Drone
    {
        public Entry Entry;
        public RectTransform Slot;
        public Transform Anchor;
        public Transform Body;
        public SpriteRenderer Renderer;
        public Sprite NormalSprite;
        public Vector3 PrefabScale;
        public ProjectileData NoHitData;
        public float LastFire = float.NegativeInfinity;
    }

    private readonly Drone[] _drones;
    private readonly RectTransform _target;
    private readonly TimerBonusLabSettings _settings;
    private readonly Vector2 _bossCenter;
    private int _lastShot = -1;
    private float _lastT;

    /// <summary>캔버스 slotParent 위 자리를 따라 월드에 드론을 띄운다 (흔들림·줌도 같이 따라간다).</summary>
    public TimerBonusDroneSquad(Entry[] entries, RectTransform slotParent, Vector2 bossCenter, TimerBonusLabSettings settings)
    {
        _settings = settings;
        _bossCenter = bossCenter;
        _target = TimerBonusLab.NewRect("LaserTarget", slotParent, Vector2.zero);
        _target.anchoredPosition = bossCenter;
        int count = entries != null ? entries.Length : 0;
        _drones = new Drone[count];
        for (int i = 0; i < count; i++)
        {
            var e = entries[i];
            if (e == null || e.DronePrefab == null) continue;
            var slot = TimerBonusLab.NewRect("DroneSlot" + i, slotParent, Vector2.zero);
            // 부유 애니메이션(DroneHoverAnimation)이 localPosition을 움직이므로, 우리는 부모(Anchor)만 옮긴다.
            var anchor = new GameObject("DroneAnchor" + i).transform;
            var body = Object.Instantiate(e.DronePrefab, anchor, false).transform;
            NoHitData(e, out var noHit);
            var sr = body.GetComponent<SpriteRenderer>();
            _drones[i] = new Drone
            {
                Entry = e,
                Slot = slot,
                Anchor = anchor,
                Body = body,
                Renderer = sr,
                NormalSprite = sr != null ? sr.sprite : null,
                PrefabScale = body.localScale,
                NoHitData = noHit,
            };
        }
    }

    /// <summary>t초 화면. firing이면 설정 간격마다 드론이 번갈아 한 발씩 쏜다.</summary>
    public void Render(float t, bool firing)
    {
        if (t < _lastT) _lastShot = -1;   // 반복 재시작
        _lastT = t;
        float px = _settings.PxScale;
        float scale = _settings.DroneScale;
        var slots = _settings.DroneSlots;
        for (int i = 0; i < _drones.Length; i++)
        {
            var d = _drones[i];
            if (d == null) continue;
            if (t < d.LastFire) d.LastFire = float.NegativeInfinity;
            Vector2 slotPx = slots != null && i < slots.Length ? slots[i] : Vector2.zero;
            d.Slot.anchoredPosition = _bossCenter + slotPx * px;
            Vector3 p = d.Slot.position;
            d.Anchor.position = new Vector3(p.x, p.y, 0f);
            float since = t - d.LastFire;
            if (d.Renderer != null && d.Entry.AttackSprite != null)
                d.Renderer.sprite = since >= 0f && since < AttackHoldSeconds ? d.Entry.AttackSprite : d.NormalSprite;
            float punch = since >= 0f && since < PunchSeconds ? Mathf.Sin(since / PunchSeconds * Mathf.PI) * (1f - since / PunchSeconds) : 0f;
            d.Body.localScale = d.PrefabScale * (scale * (1f + PunchScale * punch));
        }

        int shot = Mathf.FloorToInt(t / Mathf.Max(0.02f, _settings.DroneFireInterval));
        if (!firing || shot <= 0 || shot == _lastShot || _drones.Length == 0) return;
        _lastShot = shot;
        var shooter = _drones[shot % _drones.Length];
        if (shooter != null) Fire(shooter, t);
    }

    private void Fire(Drone d, float t)
    {
        d.LastFire = t;
        if (d.Entry.LaserPrefab == null) return;
        Vector3 origin = d.Body.position;
        origin.z = 0f;
        float worldPerPx = _target.lossyScale.x;
        Vector3 target = _target.position + (Vector3)(UnityEngine.Random.insideUnitCircle * (AimRadiusPx * _settings.PxScale * worldPerPx));
        target.z = 0f;
        // 풀(RM)을 쓰지 않는다: 이 인스턴스에만 피격 이펙트 없는 데이터를 넣고 끝나면 버린다.
        var laser = Object.Instantiate(d.Entry.LaserPrefab, origin, Quaternion.identity);
        laser.Launch(origin, target, p => Object.Destroy(p.gameObject), d.NoHitData, null, _settings.DroneScale);
    }

    private static void NoHitData(Entry e, out ProjectileData noHit)
    {
        noHit = null;
        if (e.LaserData == null) return;
        noHit = Object.Instantiate(e.LaserData);
        noHit.name = e.LaserData.name + "_NoHit (Lab)";
        noHit.hitEffect = null;
    }

    /// <summary>드론과 복제 데이터를 정리한다 (날아가는 레이저는 도착 시 스스로 사라진다).</summary>
    public void Dispose()
    {
        foreach (var d in _drones)
        {
            if (d == null) continue;
            if (d.Anchor != null) Object.Destroy(d.Anchor.gameObject);
            if (d.NoHitData != null) Object.Destroy(d.NoHitData);
        }
    }
}
