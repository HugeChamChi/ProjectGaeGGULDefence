using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 합성 연출 (씬 스코프 서비스).
/// 1) 재료 유닛의 스프라이트 잔상이 합성 칸으로 빨려들며 작아진다.
///    실제 유닛은 호출 직후 로직상 즉시 제거되므로 잔상은 공격·식량 생산 등에 영향이 없다.
/// 2) 합성 칸에서 만화풍 먼지구름과 충격선이 펑 터진다.
/// 3) 먼지가 걷힐 무렵 소환 줄기(SpawnLine)가 합성 칸 제자리에서 솟았다 내리꽂힌다 (UnitSpawner가 PlayMerge 반환 시간 뒤 GetLineOrigin에서 발사).
/// 모든 연출은 게임 시간(scaled) 기준이라 일시정지·슬로우와 함께 멈춘다.
/// </summary>
public sealed class MergeEffectPlayer : IDisposable
{
    private readonly MergeEffectSettings _settings;
    private readonly CancellationTokenSource _cts = new CancellationTokenSource();
    private readonly Stack<SpriteRenderer> _pool = new Stack<SpriteRenderer>();
    private Transform _root;
    private bool _disposed;

    /// <summary>씬 스코프 설정을 받는다.</summary>
    public MergeEffectPlayer(MergeEffectSettings settings)
    {
        _settings = settings;
    }

    /// <summary>합성 소환 줄기의 포물선 높이(월드).</summary>
    public float LineCurveHeight => _settings != null ? _settings.LineCurveHeight : 5f;

    /// <summary>
    /// 소환 줄기 출발점 (합성 칸 기준 오프셋 적용).
    /// 가로 오프셋은 화면 중앙 쪽으로 뒤집어 가장자리 칸에서도 줄기가 보드 밖으로 나가지 않게 한다.
    /// </summary>
    public Vector3 GetLineOrigin(Vector3 center)
    {
        if (_settings == null) return center;
        var o = _settings.LineStartOffset;
        var cam = Camera.main;
        float midX = cam != null ? cam.transform.position.x : 0f;
        float x = Mathf.Abs(o.x) * (center.x < midX ? 1f : -1f);
        return center + new Vector3(x, o.y, 0f);
    }

    /// <summary>
    /// 합성 연출을 시작한다. 재료 유닛이 파괴되기 전에 호출해야 잔상을 복사할 수 있다.
    /// </summary>
    /// <param name="materials">사라질 재료 유닛들</param>
    /// <param name="center">합성 결과가 놓일 칸의 월드 위치</param>
    /// <returns>소환 줄기 출발까지 기다릴 시간(게임 시간, 초)</returns>
    public float PlayMerge(IReadOnlyList<UnitBase> materials, Vector3 center)
    {
        if (_disposed || _settings == null) return 0f;

        var ghosts = new List<Transform>(materials.Count);
        for (int i = 0; i < materials.Count; i++)
        {
            var ghost = CreateGhost(materials[i]);
            if (ghost != null) ghosts.Add(ghost);
        }

        center.z = 0f;
        PlayAsync(ghosts, center, _cts.Token).Forget();
        return _settings.GatherSeconds + _settings.LineDelaySeconds;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts.Cancel();
        _cts.Dispose();
        if (_root != null) UnityEngine.Object.Destroy(_root.gameObject);
        _pool.Clear();
    }

    // ── 연출 흐름 ────────────────────────────────────────────────

    private async UniTaskVoid PlayAsync(List<Transform> ghosts, Vector3 center, CancellationToken token)
    {
        try
        {
            await GatherAsync(ghosts, center, token);
            PlayPuff(center, token);
        }
        catch (OperationCanceledException) { }
        finally
        {
            for (int i = 0; i < ghosts.Count; i++)
                if (ghosts[i] != null) UnityEngine.Object.Destroy(ghosts[i].gameObject);
        }
    }

    private async UniTask GatherAsync(List<Transform> ghosts, Vector3 center, CancellationToken token)
    {
        float duration = _settings.GatherSeconds;
        if (ghosts.Count == 0 || duration <= 0f) return;

        var starts = new Vector3[ghosts.Count];
        var scales = new Vector3[ghosts.Count];
        for (int i = 0; i < ghosts.Count; i++)
        {
            starts[i] = ghosts[i].position;
            scales[i] = ghosts[i].localScale;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            await UniTask.Yield(PlayerLoopTiming.Update, token);
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float move = t * t; // 빨려드는 가속감
            float scale = Mathf.Lerp(1f, _settings.GatherEndScale, t);
            for (int i = 0; i < ghosts.Count; i++)
            {
                if (ghosts[i] == null) continue;
                ghosts[i].position = Vector3.Lerp(starts[i], center, move);
                ghosts[i].localScale = scales[i] * scale;
            }
        }
    }

    private void PlayPuff(Vector3 center, CancellationToken token)
    {
        var sprites = _settings.DustSprites;
        if (sprites != null && sprites.Length > 0)
        {
            int count = _settings.PuffCount;
            float baseAngle = UnityEngine.Random.Range(0f, 360f);
            for (int i = 0; i < count; i++)
            {
                float angle = (baseAngle + 360f * i / count + UnityEngine.Random.Range(-18f, 18f)) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle) * 0.7f, 0f); // 바닥 원근감으로 세로를 눌러준다
                AnimatePuffAsync(PickSprite(sprites), center, center + dir * _settings.PuffSpread,
                    _settings.SidePuffSize, _settings.SortingOrder, token).Forget();
            }
            AnimatePuffAsync(PickSprite(sprites), center, center, _settings.CenterPuffSize,
                _settings.SortingOrder + 1, token).Forget();
        }

        if (_settings.TickSprite != null)
        {
            int ticks = _settings.TickCount;
            float baseAngle = UnityEngine.Random.Range(0f, 360f);
            for (int i = 0; i < ticks; i++)
            {
                float angle = baseAngle + 360f * i / ticks + UnityEngine.Random.Range(-12f, 12f);
                AnimateTickAsync(center, angle, token).Forget();
            }
        }
    }

    private async UniTaskVoid AnimatePuffAsync(Sprite sprite, Vector3 from, Vector3 to, float worldSize,
        int order, CancellationToken token)
    {
        var sr = Rent(sprite, order);
        float fit = FitScale(sprite, worldSize);
        float spin = UnityEngine.Random.Range(-25f, 25f);
        bool flip = UnityEngine.Random.value < 0.5f;
        sr.flipX = flip;
        try
        {
            float duration = _settings.PuffSeconds;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                float popT = Mathf.Clamp01(t / 0.35f);
                float pop = Mathf.LerpUnclamped(0.3f, 1f, EaseOutBack(popT)); // 흡입 직후 빈 프레임 없이 30%에서 시작
                float shrink = t < 0.35f ? 1f : Mathf.Lerp(1f, 0.8f, (t - 0.35f) / 0.65f);
                float alpha = t < 0.5f ? 1f : 1f - (t - 0.5f) / 0.5f;

                sr.transform.position = Vector3.Lerp(from, to, EaseOutCubic(t));
                sr.transform.localScale = Vector3.one * (fit * pop * shrink);
                sr.transform.rotation = Quaternion.Euler(0f, 0f, spin * t);
                sr.color = new Color(1f, 1f, 1f, alpha);

                await UniTask.Yield(PlayerLoopTiming.Update, token);
                elapsed += Time.deltaTime;
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            Return(sr);
        }
    }

    private async UniTaskVoid AnimateTickAsync(Vector3 center, float angleDeg, CancellationToken token)
    {
        var sprite = _settings.TickSprite;
        var sr = Rent(sprite, _settings.SortingOrder + 2);
        float fit = FitScale(sprite, _settings.TickSize, useHeight: true);
        float rad = angleDeg * Mathf.Deg2Rad;
        var dir = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f);
        sr.transform.rotation = Quaternion.Euler(0f, 0f, angleDeg - 90f); // 스프라이트 세로축을 바깥 방향으로
        try
        {
            float duration = _settings.PuffSeconds * 0.7f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                float dist = Mathf.Lerp(_settings.TickDistance * 0.55f, _settings.TickDistance, EaseOutCubic(t));
                float length = t < 0.3f ? t / 0.3f : Mathf.Lerp(1f, 0.3f, (t - 0.3f) / 0.7f);

                sr.transform.position = center + dir * dist;
                sr.transform.localScale = new Vector3(fit, fit * length, 1f);
                sr.color = new Color(1f, 1f, 1f, t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f);

                await UniTask.Yield(PlayerLoopTiming.Update, token);
                elapsed += Time.deltaTime;
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            Return(sr);
        }
    }

    // ── 잔상 / 풀 ────────────────────────────────────────────────

    /// <summary>유닛의 현재 스프라이트들을 그대로 복사한 잔상을 만든다.</summary>
    private Transform CreateGhost(UnitBase unit)
    {
        if (unit == null) return null;
        var renderers = unit.GetComponentsInChildren<SpriteRenderer>(false);
        if (renderers.Length == 0) return null;

        var root = new GameObject("MergeGhost").transform;
        root.SetParent(GetRoot(), false);
        root.position = unit.transform.position;

        for (int i = 0; i < renderers.Length; i++)
        {
            var src = renderers[i];
            if (!src.enabled || src.sprite == null) continue;
            var go = new GameObject(src.name);
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(src.transform.position, src.transform.rotation);
            go.transform.localScale = src.transform.lossyScale;
            var dst = go.AddComponent<SpriteRenderer>();
            dst.sprite = src.sprite;
            dst.color = src.color;
            dst.flipX = src.flipX;
            dst.flipY = src.flipY;
            dst.sharedMaterial = src.sharedMaterial;
            dst.sortingLayerID = src.sortingLayerID;
            dst.sortingOrder = src.sortingOrder;
        }
        return root;
    }

    private SpriteRenderer Rent(Sprite sprite, int order)
    {
        SpriteRenderer sr = null;
        while (_pool.Count > 0 && sr == null) sr = _pool.Pop();
        if (sr == null)
        {
            var go = new GameObject("MergeDust");
            go.transform.SetParent(GetRoot(), false);
            sr = go.AddComponent<SpriteRenderer>();
        }
        sr.sprite = sprite;
        sr.flipX = false;
        sr.color = Color.white;
        sr.sortingLayerName = _settings.SortingLayerName;
        sr.sortingOrder = order;
        sr.transform.localScale = Vector3.zero;
        sr.gameObject.SetActive(true);
        return sr;
    }

    private void Return(SpriteRenderer sr)
    {
        if (sr == null) return;
        sr.gameObject.SetActive(false);
        if (!_disposed) _pool.Push(sr);
    }

    private Transform GetRoot()
    {
        if (_root == null) _root = new GameObject("[MergeEffects]").transform;
        return _root;
    }

    private static Sprite PickSprite(Sprite[] sprites) => sprites[UnityEngine.Random.Range(0, sprites.Length)];

    private static float FitScale(Sprite sprite, float worldSize, bool useHeight = false)
    {
        if (sprite == null) return 0f;
        float size = useHeight ? sprite.bounds.size.y : sprite.bounds.size.x;
        return size > 0f ? worldSize / size : 0f;
    }

    private static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
}
