using UnityEngine.SceneManagement;
using VContainer;
using Cysharp.Threading.Tasks;
using System;

public class SceneChangeManager
{
    private readonly GlobalUIManager _globalUIManager;
    private readonly AssetLifecycleManager _assetLifecycle;

    [Inject]
    public SceneChangeManager(GlobalUIManager globalUIManager, AssetLifecycleManager assetLifecycle)
    {
        _globalUIManager = globalUIManager;
        _assetLifecycle = assetLifecycle;
    }

    /// <summary>
    /// 페이드 인 -> beforeLoad -> 씬 로드 -> afterLoad -> 페이드 아웃의 완벽한 흐름을 제어합니다.
    /// </summary>
    public async UniTask TransitionToSceneAsync(string sceneName, Func<UniTask> beforeLoad = null, Func<UniTask> afterLoad = null)
    {
        // 1. 화면 가리기 (Fade In)
        await _globalUIManager.FadeInAsync();

        // 2. 씬 로드 전 추가 작업 (데이터 저장 등)
        if (beforeLoad != null)
        {
            await beforeLoad();
        }

        // 3. 에셋 라이프사이클 정리 (씬 전환 전 로드된 에셋 언로드)
        _assetLifecycle.UnloadAll();
        RM.ReleaseAllHandles();

        // 4. 씬 로드
        await LoadSceneInternalAsync(sceneName);

        // 5. 씬 로드 후 추가 작업 (데이터 바인딩, 초기화 등)
        if (afterLoad != null)
        {
            await afterLoad();
        }

        // 6. 화면 밝히기 (Fade Out)
        await _globalUIManager.FadeOutAsync();
    }

    /// <summary>
    /// 실제 씬 로드 처리 (추후 어드레서블 적용 시 이곳만 수정)
    /// </summary>
    private async UniTask LoadSceneInternalAsync(string sceneName)
    {
        await SceneManager.LoadSceneAsync(sceneName).ToUniTask();
    }

    public void LoadScene(string sceneName)
    {
        TransitionToSceneAsync(sceneName).Forget();
    }

    public UniTask LoadSceneAsync(string sceneName)
    {
        return TransitionToSceneAsync(sceneName);
    }
}
