using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 유닛의 등급(Tier)에 맞춰 UI 외곽선(Outline)을 제어하는 로직 클래스입니다.
/// 등급별 머티리얼 캐싱을 통해 드로우 콜과 메모리를 최적화합니다.
/// </summary>
public class UnitVisualController
{
    private readonly Image _targetImage;
    private static UIOutlineSettings _settings;
    
    // 등급별 머티리얼 캐시 (최적화 핵심)
    private static readonly Dictionary<Tier, Material> _materialCache = new Dictionary<Tier, Material>();

    public UnitVisualController(Image targetImage)
    {
        _targetImage = targetImage;
        LoadSettings();
    }

    private void LoadSettings()
    {
        if (_settings == null)
        {
            _settings = Resources.Load<UIOutlineSettings>("Data/UIOutlineSettings");
        }
    }

    /// <summary>
    /// 등급에 맞춰 외곽선 비주얼을 업데이트합니다. (캐싱된 머티리얼 재사용)
    /// </summary>
    public void UpdateVisual(Tier tier)
    {
        if (_targetImage == null) return;
        if (_settings == null) LoadSettings();
        if (_settings == null) return;

        // 1. 캐시에서 해당 등급의 머티리얼이 있는지 확인
        if (!_materialCache.TryGetValue(tier, out Material sharedMat) || sharedMat == null)
        {
            sharedMat = CreateTierMaterial(tier);
            if (sharedMat != null)
                _materialCache[tier] = sharedMat;
        }

        // 2. 공유 머티리얼 적용
        if (sharedMat != null)
        {
            _targetImage.material = sharedMat;
        }
    }

    private Material CreateTierMaterial(Tier tier)
    {
        Material baseMat = _targetImage.material;
        
        if (baseMat == null)
        {
            // 폴백: 셰이더를 직접 찾아서 생성
            var shader = Shader.Find("GaeGGUL/UI/Outline");
            if (shader == null) return null;
            baseMat = new Material(shader);
        }

        // 새로운 공유 머티리얼 생성 및 설정 적용
        Material newMat = new Material(baseMat);
        newMat.name = $"Outline_{tier}";
        
        var config = _settings.GetSettings(tier);
        newMat.SetColor("_OutlineColor", config.color);
        newMat.SetFloat("_OuterWidth", config.outerWidth);
        newMat.SetFloat("_InnerWidth", config.innerWidth);

        return newMat;
    }

    /// <summary>
    /// 개별 유닛 파괴 시에는 공유 머티리얼을 파괴하지 않습니다.
    /// 게임 종료 시나 씬 전환 시 캐시를 비워야 할 때 호출할 수 있습니다.
    /// </summary>
    public static void ClearCache()
    {
        foreach (var mat in _materialCache.Values)
        {
            if (mat != null) UnityEngine.Object.Destroy(mat);
        }
        _materialCache.Clear();
    }

    public void Cleanup()
    {
        // 공유 머티리얼을 사용하므로 개별 Cleanup에서는 아무것도 하지 않음 (메모리 최적화)
    }
}
