using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 전투 종료 순간 필드 보스의 모습만 투명 배경 텍스처로 한 번 찍는다.
/// 결과 화면은 이 그림을 보스가 서 있던 화면 위치에서 받아 이어 보여 준다.
/// </summary>
public sealed class ResultBossSnapshot : IDisposable
{
    /// <summary>보스 영역만 잘라 낸 텍스처. 보스 밖은 투명.</summary>
    public Texture2D Texture { get; private set; }

    /// <summary>텍스처가 차지하던 화면 픽셀 영역.</summary>
    public Rect ScreenRect { get; private set; }

    /// <summary>보스 렌더러를 빈 레이어로 잠시 옮겨 그 레이어만 찍는다. 실패하면 null.</summary>
    public static ResultBossSnapshot Capture(Component boss, Camera source)
    {
        if (boss == null || source == null || Screen.width <= 0 || Screen.height <= 0) return null;
        var renderers = new List<Renderer>();
        foreach (var renderer in boss.GetComponentsInChildren<Renderer>())
            if (renderer.enabled && renderer.gameObject.activeInHierarchy && !(renderer is ParticleSystemRenderer))
                renderers.Add(renderer);
        int layer = FindFreeLayer();
        if (renderers.Count == 0 || layer < 0) return null;

        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        var screenRect = ProjectBounds(source, bounds);
        if (screenRect.width < 1f || screenRect.height < 1f) return null;

        var layers = new int[renderers.Count];
        for (int i = 0; i < renderers.Count; i++)
        {
            layers[i] = renderers[i].gameObject.layer;
            renderers[i].gameObject.layer = layer;
        }
        // 렌더 텍스처 내용은 다음 프레임까지 보장되지 않으므로 그린 직후 보스 영역만 일반 텍스처로 옮긴다.
        int x = Mathf.FloorToInt(screenRect.xMin), y = Mathf.FloorToInt(screenRect.yMin);
        int width = Mathf.Min(Screen.width, Mathf.CeilToInt(screenRect.xMax)) - x;
        int height = Mathf.Min(Screen.height, Mathf.CeilToInt(screenRect.yMax)) - y;
        var target = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32);
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            { name = "ResultBossSnapshot", wrapMode = TextureWrapMode.Clamp };
        var go = new GameObject("ResultBossSnapshotCamera");
        try
        {
            var camera = go.AddComponent<Camera>();
            camera.CopyFrom(source);
            camera.cullingMask = 1 << layer;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.targetTexture = target;
            camera.enabled = false;
            camera.Render();
            if ((SystemInfo.copyTextureSupport & CopyTextureSupport.RTToTexture) != 0)
                Graphics.CopyTexture(target, 0, 0, x, y, width, height, texture, 0, 0, 0, 0);
            else
            {
                var previous = RenderTexture.active;
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(x, y, width, height), 0, 0, false);
                texture.Apply(false, false);
                RenderTexture.active = previous;
            }
            camera.targetTexture = null;
        }
        finally
        {
            target.Release();
            UnityEngine.Object.Destroy(target);
            for (int i = 0; i < renderers.Count; i++)
                if (renderers[i] != null) renderers[i].gameObject.layer = layers[i];
            UnityEngine.Object.Destroy(go);
        }
        return new ResultBossSnapshot { Texture = texture, ScreenRect = new Rect(x, y, width, height) };
    }

    /// <summary>텍스처를 해제한다.</summary>
    public void Dispose()
    {
        if (Texture == null) return;
        UnityEngine.Object.Destroy(Texture);
        Texture = null;
    }

    private static Rect ProjectBounds(Camera camera, Bounds bounds)
    {
        var min = new Vector2(float.MaxValue, float.MaxValue);
        var max = new Vector2(float.MinValue, float.MinValue);
        for (int i = 0; i < 8; i++)
        {
            var corner = new Vector3(
                (i & 1) == 0 ? bounds.min.x : bounds.max.x,
                (i & 2) == 0 ? bounds.min.y : bounds.max.y,
                (i & 4) == 0 ? bounds.min.z : bounds.max.z);
            Vector2 point = camera.WorldToScreenPoint(corner);
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }
        min = Vector2.Max(min, Vector2.zero);
        max = Vector2.Min(max, new Vector2(Screen.width, Screen.height));
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    // 이름 없는 사용자 레이어(31부터 역순)를 촬영 전용으로 잠시 빌린다.
    private static int FindFreeLayer()
    {
        for (int layer = 31; layer >= 8; layer--)
            if (string.IsNullOrEmpty(LayerMask.LayerToName(layer))) return layer;
        return -1;
    }
}
