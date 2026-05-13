using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ShaderCompiler : MonoBehaviour
{
    public ShaderVariantCollection globalSVC;
    public Slider loadingBar; // UI 게이지용

    void Start()
    {
        StartCoroutine(WarmupRoutine());
    }

    IEnumerator WarmupRoutine()
    {
        if (globalSVC != null)
        {
            // 1. 셰이더 컴파일 시작
            Debug.Log("셰이더 컴파일 시작...");

            // 실제 명조처럼 게이지를 움직이고 싶다면 분할해서 처리해야 하지만,
            // Prewarm() 자체는 동기 방식이라 호출 시 화면이 잠시 멈출 수 있습니다.
            globalSVC.WarmUp();

            // 2. 컴파일 완료 후 다음 씬으로 이동
            Debug.Log("셰이더 컴파일 완료!");
            loadingBar.value = 1.0f;

            yield return new WaitForSeconds(0.5f); // 연출용 대기
            // SceneManager.LoadScene("MainLobby");
        }
    }
}
