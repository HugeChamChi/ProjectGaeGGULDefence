using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// 레벨업 연출 공용 이펙트 도구 (MonoBehaviour 아님 — 각 연출 컴포넌트가 하나씩 소유).
/// 이펙트 슬롯 프리팹이 있으면 그것을, 없으면 DOTween 임시 이펙트(섬광 원 + 퍼지는 링)를 재생한다.
/// 생성한 이펙트는 수명이 다하면 스스로 사라지고, 소유 컴포넌트 파괴 시 DestroyAll로 일괄 정리한다.
/// 모든 트윈은 레벨업 일시정지(timeScale=0) 중에도 돌도록 unscaled다.
/// </summary>
public sealed class LevelUpFxKit
{
    private const int PlaceholderTextureSize = 128;
    private static Sprite _softCircle;
    private static Sprite _ring;

    private readonly List<GameObject> _spawned = new List<GameObject>();
    private readonly float _prefabLifetime;
    private readonly Color _placeholderColor;

    /// <param name="prefabLifetime">슬롯 프리팹 인스턴스 자동 제거 시간(초, unscaled).</param>
    /// <param name="placeholderColor">임시 이펙트 링 색.</param>
    public LevelUpFxKit(float prefabLifetime, Color placeholderColor)
    {
        _prefabLifetime = prefabLifetime;
        _placeholderColor = placeholderColor;
    }

    /// <summary>부드러운 원(글로우) 임시 스프라이트.</summary>
    public static Sprite SoftCircle { get { EnsureSprites(); return _softCircle; } }

    /// <summary>링 임시 스프라이트.</summary>
    public static Sprite Ring { get { EnsureSprites(); return _ring; } }

    /// <summary>슬롯 프리팹 또는 임시 버스트를 worldPos에 재생한다.</summary>
    public void Spawn(GameObject prefab, RectTransform root, Vector3 worldPos, float placeholderSize)
    {
        if (root == null) return;
        if (prefab != null)
        {
            var fx = Object.Instantiate(prefab, root);
            fx.transform.position = worldPos;
            fx.transform.SetAsLastSibling();
            TrackAndExpire(fx, _prefabLifetime);
            return;
        }
        SpawnPlaceholderBurst(root, worldPos, placeholderSize);
    }

    /// <summary>외부에서 만든 연출 오브젝트를 추적 목록에 등록한다 (DestroyAll 대상).</summary>
    public void Track(GameObject go)
    {
        if (go != null) _spawned.Add(go);
    }

    /// <summary>추적 해제 후 즉시 파괴한다.</summary>
    public void Release(GameObject go)
    {
        _spawned.Remove(go);
        if (go != null) Object.Destroy(go);
    }

    /// <summary>수명이 지나면 파괴되도록 등록한다.</summary>
    public void TrackAndExpire(GameObject go, float lifetime)
    {
        Track(go);
        ExpireAsync(go, lifetime, go.GetCancellationTokenOnDestroy()).Forget();
    }

    /// <summary>추적 중인 모든 연출 오브젝트를 파괴한다.</summary>
    public void DestroyAll()
    {
        foreach (var go in _spawned.ToArray())
            if (go != null) Object.Destroy(go);
        _spawned.Clear();
    }

    /// <summary>레이캐스트를 막지 않는 단순 이미지 하나를 만든다.</summary>
    public static Image CreateImage(RectTransform parent, Sprite sprite, float size, Color color, string name = null)
    {
        var go = new GameObject(name ?? (sprite != null ? sprite.name : "FxImage"), typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.sizeDelta = new Vector2(size, size);
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    /// <summary>임시 이펙트: 부드러운 섬광 원 + 바깥으로 퍼지는 링.</summary>
    private void SpawnPlaceholderBurst(RectTransform root, Vector3 worldPos, float size)
    {
        var group = new GameObject("PlaceholderBurst", typeof(RectTransform));
        var groupRt = (RectTransform)group.transform;
        groupRt.SetParent(root, false);
        groupRt.position = worldPos;
        groupRt.SetAsLastSibling();

        var flash = CreateImage(groupRt, SoftCircle, size * 0.6f, Color.white);
        flash.rectTransform.localScale = Vector3.one * 0.3f;
        _ = DOTween.Sequence().SetUpdate(true).SetLink(group)
            .Join(flash.rectTransform.DOScale(1f, 0.18f).SetEase(Ease.OutQuad))
            .Join(flash.DOFade(0f, 0.3f).SetEase(Ease.InQuad));

        var ring = CreateImage(groupRt, Ring, size, _placeholderColor);
        ring.rectTransform.localScale = Vector3.one * 0.2f;
        _ = DOTween.Sequence().SetUpdate(true).SetLink(group)
            .Join(ring.rectTransform.DOScale(1f, 0.4f).SetEase(Ease.OutCubic))
            .Join(ring.DOFade(0f, 0.4f).SetEase(Ease.InQuad));

        TrackAndExpire(group, 0.45f);
    }

    private async UniTaskVoid ExpireAsync(GameObject go, float lifetime, CancellationToken token)
    {
        bool canceled = await UniTask.Delay(LevelUpUiSpace.Ms(lifetime), DelayType.Realtime, cancellationToken: token)
                                     .SuppressCancellationThrow();
        _spawned.Remove(go);
        if (!canceled && go != null) Object.Destroy(go);
    }

    private static void EnsureSprites()
    {
        if (_softCircle == null) _softCircle = CreateRadialSprite("SoftCircle", r => Mathf.Pow(1f - r, 2f));
        if (_ring == null) _ring = CreateRadialSprite("Ring", r => Mathf.Clamp01(1f - Mathf.Abs(r - 0.85f) / 0.12f));
    }

    private static Sprite CreateRadialSprite(string name, Func<float, float> alphaByRadius)
    {
        const int size = PlaceholderTextureSize;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[size * size];
        float half = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float r = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) / half;
            float a = r >= 1f ? 0f : Mathf.Clamp01(alphaByRadius(r));
            pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
        }
        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        sprite.name = name;
        return sprite;
    }
}
