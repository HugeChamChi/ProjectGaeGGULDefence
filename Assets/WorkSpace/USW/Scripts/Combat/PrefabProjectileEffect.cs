using System;
using UnityEngine;

/// <summary>
/// 프리팹 스폰 + 일정 시간 후 파괴하는 기본 이펙트. 기존 Projectile._hitEffectPrefab 동작과 동일하며,
/// 사운드 이름을 지정하면 AudioManager로 함께 재생한다.
/// </summary>
[Serializable]
[DisplayName("프리팹 이펙트")]
public class PrefabProjectileEffect : IEffectSpawner
{
    [Tooltip("비워두면 프리팹 스폰 없이 사운드만 재생")]
    [KoreanLabel("이펙트 프리팹")]
    public GameObject effectPrefab;
    [KoreanLabel("지속 시간")]
    public float duration = 1.0f;
    [Tooltip("비워두면 사운드 재생 안 함")]
    [KoreanLabel("효과음 이름")]
    public string sfxName;

    public void Play(Vector3 position, Transform parent, AudioManager audioManager)
    {
        if (!string.IsNullOrEmpty(sfxName))
            audioManager?.PlaySFX(sfxName);

        if (effectPrefab != null)
        {
            var effect = RM.Instantiate(effectPrefab, position, Quaternion.identity, parent, true);
            if (effect != null)
            {
                RM.Destroy(effect, duration);
            }
        }
    }
}
