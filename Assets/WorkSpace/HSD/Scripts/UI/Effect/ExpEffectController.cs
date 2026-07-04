using System.Collections;
using VContainer;
using System.Collections.Generic;
using AssetKits.ParticleImage;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 보스 데미지 시 경험치 이펙트를 생성하고, 이펙트가 목적지에 도달하면 실제 경험치를 추가하는 컨트롤러
/// </summary>
public class ExpEffectController : MonoBehaviour
{
    [Inject] private BossManager _bossManager;

    [Inject] private ExpManager _expManager;

    [SerializeField] private ParticleImage particleImage;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform attractorTarget;

    private BossBase _subscribedBoss;

    private void Start()
    {
        // 보스 소환 이벤트 구독
        if (_bossManager != null)
        {
            _bossManager.OnBossEntryed += SubscribeBoss;
            
            // 이미 소환된 보스가 있다면 바로 구독
            if (_bossManager.CurrentBoss != null)
            {
                SubscribeBoss(null, null); // entry는 사용하지 않으므로 null 전달
            }
        }
    }

    private void OnDestroy()
    {
        if (_bossManager != null)
        {
            _bossManager.OnBossEntryed -= SubscribeBoss;
        }

        UnsubscribeBoss();
    }

    private void SubscribeBoss(BossEntry _, BossEntry _1)
    {
        UnsubscribeBoss();

        _subscribedBoss = _bossManager.CurrentBoss;
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

    private float _accumulatedExp = 0f;
    private float _effectCooldown = 0.15f; 
    private float _lastEffectTime = 0f;

    private void Update()
    {
        if (_accumulatedExp > 0f && Time.unscaledTime - _lastEffectTime >= _effectCooldown)
        {
            float expToApply = _accumulatedExp;
            _accumulatedExp = 0f;
            _lastEffectTime = Time.unscaledTime;

            SpawnParticleAndApplyExp(expToApply);
        }
    }

    private void OnBossDamaged(int damage, Vector3? hitPos)
    {
        if (particleImage == null) return;

        float expAmount = _expManager.CalculateExpFromDamage(damage);
        if (expAmount <= 0) return;

        _accumulatedExp += expAmount;
    }

    private void SpawnParticleAndApplyExp(float expAmount)
    {
        // 타임스케일이 0이거나(토템 선택창 등), 파티클 오브젝트가 없으면 에러를 막기 위해 파티클 생성 생략
        if (Time.timeScale == 0f || particleImage == null)
        {
            _expManager.AddExp(expAmount);
            return;
        }

        var particle = RM.Instantiate(particleImage, spawnPoint.position, Quaternion.identity, spawnPoint, true);
        particle.attractorTarget = attractorTarget;

        particle.onAnyParticleFinished.RemoveAllListeners();
        bool isAdded = false;
        particle.onAnyParticleFinished.AddListener(() => 
        {
            if (isAdded) return;
            isAdded = true;
            _expManager.AddExp(expAmount);
        });

        particle.Play();

        RM.Destroy(particle, particle.duration + 0.5f);
    }
}
