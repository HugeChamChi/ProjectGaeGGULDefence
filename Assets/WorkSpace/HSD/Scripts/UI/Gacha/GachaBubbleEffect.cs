using AssetKits.ParticleImage;
using UnityEngine;

/// <summary>
/// 가챠 연출 중 물속에서 올라오는 부글거림을 표현하는 이펙트입니다.
/// ParticleImage 파티클 시스템을 재생/정지합니다.
/// </summary>
public class GachaBubbleEffect : MonoBehaviour
{
    [SerializeField] private ParticleImage particleImage;

    public void Play()
    {
        if (particleImage == null) return;
        particleImage.Play();
    }

    public void Stop()
    {
        if (particleImage == null) return;
        particleImage.Stop(true);
    }

    private void OnDisable()
    {
        Stop();
    }
}
