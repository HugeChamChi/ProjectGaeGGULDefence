using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 전역적인 UI 사운드 및 화면 터치 사운드를 관리하는 컨트롤러
/// </summary>
public class UISoundController : MonoBehaviour
{
    private const string BUTTON_SFX = "01.Button_Touch(max vol)";
    private const string TOUCH_SFX  = "01.Screen_touch(max vol)";

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            PlayClickSound();
        }
    }

    private void PlayClickSound()
    {
        if (EventSystem.current == null) return;

        // 마우스 위치에서 UI 레이캐스트 실행
        PointerEventData eventData = new PointerEventData(EventSystem.current);
        eventData.position = Input.mousePosition;

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        if (results.Count > 0)
        {
            // UI를 클릭함
            GameObject hitObj = results[0].gameObject;
            
            // 클릭한 대상이나 그 부모 중 Button 컴포넌트가 있는지 확인
            Button btn = hitObj.GetComponentInParent<Button>();
            
            if (btn != null && btn.interactable)
            {
                // 상호작용 가능한 버튼인 경우 버튼 소리 재생
                Manager.Audio.PlaySFX(BUTTON_SFX);
            }
            else
            {
                // 상호작용 없는 UI 영역인 경우 일반 터치 소리 재생
                Manager.Audio.PlaySFX(TOUCH_SFX);
            }
        }
        else
        {
            // UI가 없는 배경 화면을 클릭한 경우 일반 터치 소리 재생
            Manager.Audio.PlaySFX(TOUCH_SFX);
        }
    }
}
