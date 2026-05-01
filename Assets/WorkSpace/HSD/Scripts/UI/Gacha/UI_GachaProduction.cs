using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 가챠의 시각적 연출(애니메이션, 이펙트 등)을 담당하는 클래스입니다.
/// </summary>
public class UI_GachaProduction : MonoBehaviour
{
    [Header("Production Objects")]
    [SerializeField] private RectTransform productionRoot; // 연출용 루트 오브젝트
    [SerializeField] private Image gachaBoxImage;         // 흔들릴 상자 이미지
    [SerializeField] private GameObject effectLight;      // 연출 중 활성화될 빛 이펙트

    [Header("Animation Settings")]
    [SerializeField] private float shakeDuration = 0.5f;
    [SerializeField] private float shakeStrength = 10f;
    [SerializeField] private int shakeVibrato = 10;

    /// <summary>
    /// 가챠 연출을 실행하고 완료될 때까지 대기합니다.
    /// </summary>
    public async UniTask PlayAsync(ICharacterData[] results)
    {
        if (productionRoot != null) productionRoot.gameObject.SetActive(true);
        if (effectLight != null) effectLight.SetActive(false);

        // 1. 상자 흔들림 연출 (DOTween)
        if (gachaBoxImage != null)
        {
            await gachaBoxImage.rectTransform
                .DOShakeAnchorPos(shakeDuration, shakeStrength, shakeVibrato)
                .SetEase(Ease.OutQuad)
                .ToUniTask();
        }

        // 2. 강렬한 빛 연출 등 추가 가능
        if (effectLight != null)
        {
            effectLight.SetActive(true);
            await UniTask.Delay(TimeSpan.FromSeconds(0.3f));
        }

        // 3. 연출 종료 및 정리 (필요 시)
        // if (productionRoot != null) productionRoot.gameObject.SetActive(false);
    }

    public void ResetProduction()
    {
        if (productionRoot != null) productionRoot.gameObject.SetActive(false);
        if (effectLight != null) effectLight.SetActive(false);
    }
}
