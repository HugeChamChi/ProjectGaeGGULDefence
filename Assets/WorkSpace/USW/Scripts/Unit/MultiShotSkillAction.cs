using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>hitEffects를 shotCount번 발사하는 스킬 액션. 적중 시 결과(데미지 등)는 hitEffects가 담당한다.</summary>
[Serializable]
public class MultiShotSkillAction : ISkillAction
{
    [Tooltip("동시/연속 발사 횟수")]
    public int shotCount = 1;
    [Tooltip("발사 사이 간격(초). 0이면 shotCount발을 동시에 발사합니다(예: 표창 동시 투척). " +
             "0보다 크면 그 간격만큼 텀을 두고 순서대로 발사합니다(예: 연발 사격).")]
    public float shotInterval = 0f;
    [Tooltip("이 스킬 투사체에만 적용되는 추가 크기 배율 (1 = 기본 투사체와 동일, 1.5 = 50% 증가)")]
    public float sizeMultiplier = 1f;

    [Tooltip("이 스킬 투사체의 이동/이펙트/프리팹 구성 (비워두면 ProjectilePool 기본 구성 사용)")]
    public ProjectileData projectileData;

    public void Execute(UnitBase caster, UnitCombatComponent combat, List<IEffect> hitEffects, List<IAdditionalEffect> additionalEffects)
    {
        if (shotInterval <= 0f)
        {
            for (int i = 0; i < shotCount; i++)
                combat.LaunchProjectile(hitEffects, additionalEffects, sizeMultiplier, projectileData);
            return;
        }

        FireShotsAsync(combat, hitEffects, additionalEffects).Forget(e =>
        {
            if (e is not OperationCanceledException) Debug.LogException(e);
        });
    }

    private async UniTask FireShotsAsync(UnitCombatComponent combat, List<IEffect> hitEffects, List<IAdditionalEffect> additionalEffects)
    {
        var token = combat.GetCancellationTokenOnDestroy();
        for (int i = 0; i < shotCount; i++)
        {
            if (token.IsCancellationRequested) return;
            combat.LaunchProjectile(hitEffects, additionalEffects, sizeMultiplier, projectileData);

            if (i < shotCount - 1)
                await UniTask.Delay(TimeSpan.FromSeconds(shotInterval), cancellationToken: token);
        }
    }
}
