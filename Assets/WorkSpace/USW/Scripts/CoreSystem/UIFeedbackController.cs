using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;
using AssetKits.ParticleImage;

/// <summary>
/// 전역적인 UI 사운드 및 ParticleImage 기반 클릭 이펙트를 관리하는 컨트롤러
/// </summary>
public class UIFeedbackController : MonoBehaviour
{
    [Header("Sound Settings")]
    [SerializeField] private string buttonSFX = "01.Button_Touch(max vol)";
    [SerializeField] private string touchSFX  = "01.Screen_touch(max vol)";

    [Header("Effect Settings")]
    [SerializeField] private ParticleImage clickEffectPrefab; // 클릭 시 생성될 ParticleImage 프리팹
    [SerializeField] private RectTransform effectParent;      // 이펙트가 생성될 UI 부모 (최상위 Canvas 권장)

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            HandleClickFeedback();
        }
    }

    private void HandleClickFeedback()
    {
        if (EventSystem.current == null) return;

        Vector2 mousePos = Input.mousePosition;

        // 1. 클릭 대상 판별 (Raycast)
        PointerEventData eventData = new PointerEventData(EventSystem.current);
        eventData.position = mousePos;

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        bool isButton = false;
        if (results.Count > 0)
        {
            GameObject hitObj = results[0].gameObject;
            Button btn = hitObj.GetComponentInParent<Button>();
            
            if (btn != null && btn.interactable)
            {
                isButton = true;
            }
        }

        // 2. 사운드 재생
        Manager.Audio.PlaySFX(isButton ? buttonSFX : touchSFX);

        // 3. ParticleImage 이펙트 재생
        PlayClickEffect(mousePos);
    }

    private void PlayClickEffect(Vector2 screenPos)
    {
        if (clickEffectPrefab == null || effectParent == null) return;

        // RM을 사용하여 풀링 기반으로 생성
        var effect = RM.Instantiate(clickEffectPrefab, screenPos, Quaternion.identity, effectParent, true);
        
        // RectTransform 위치 설정 (Screen Space Overlay/Camera 대응)
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(effectParent, screenPos, null, out Vector2 localPos))
        {
            effect.rectTransform.anchoredPosition = localPos;
        }

        // ParticleImage 재생
        effect.Play();
        
        // 재생 완료 후 자동 반환 (duration + 여유시간)
        RM.Destroy(effect, effect.duration + 0.5f);
    }
}
