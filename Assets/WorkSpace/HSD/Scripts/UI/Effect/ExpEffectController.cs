using System.Collections;
using System.Collections.Generic;
using AssetKits.ParticleImage;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 보스 데미지 시 경험치 이펙트를 생성하고, 이펙트가 목적지에 도달하면 실제 경험치를 추가하는 컨트롤러
/// </summary>
public class ExpEffectController : MonoBehaviour
{
    [SerializeField] private ParticleImage particleImage;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform attractorTarget;

    private BossBase _subscribedBoss;

    private void Start()
    {
        // 보스 소환 이벤트 구독
        if (BossManager.Instance != null)
        {
            BossManager.Instance.OnBossEntryed += SubscribeBoss;
            
            // 이미 소환된 보스가 있다면 바로 구독
            if (BossManager.Instance.CurrentBoss != null)
            {
                SubscribeBoss(null); // entry는 사용하지 않으므로 null 전달
            }
        }
    }

    private void OnDestroy()
    {
        if (BossManager.Instance != null)
        {
            BossManager.Instance.OnBossEntryed -= SubscribeBoss;
        }

        UnsubscribeBoss();
    }

    private void SubscribeBoss(BossEntry _)
    {
        UnsubscribeBoss();

        _subscribedBoss = BossManager.Instance.CurrentBoss;
        if (_subscribedBoss != null)
        {
            _subscribedBoss.OnDamaged += OnBossDamaged;
        }
    }

    private void UnsubscribeBoss()
    {
        if (_subscribedBoss != null)
        {
            _subscribedBoss.OnDamaged -= OnBossDamaged;
            _subscribedBoss = null;
        }
    }

    private void OnBossDamaged(int damage)
    {
        if (particleImage == null) return;

        // 획득할 경험치량 미리 계산
        float expAmount = Manager.Exp.CalculateExpFromDamage(damage);
        if (expAmount <= 0) return;

        // 보스 위치(혹은 지정된 위치)에서 파티클 생성
        // UI 좌표계이므로 bossSpawnPoint 근처에서 생성되도록 설정 가능하나 
        // 여기서는 기존처럼 현재 위치 혹은 파티클 자체 설정에 맡김
        var particle = RM.Instantiate(particleImage, spawnPoint.position, Quaternion.identity, transform, true);

        particle.attractorTarget = attractorTarget;

        particle.onAnyParticleFinished.RemoveAllListeners();
        // 파티클이 목적지(ExpBar)에 처음 도달했을 때 실제 경험치 추가 (중복 방지를 위해 flag 사용)
        bool isAdded = false;
        particle.onAnyParticleFinished.AddListener(() => 
        {
            if (isAdded) return;
            isAdded = true;
            Manager.Exp.AddExp(expAmount);
        });

        particle.Play();

        // 파티클 시스템 수명 주기에 맞춰 제거
        RM.Destroy(particle, particle.duration + 0.5f);
    }
}
