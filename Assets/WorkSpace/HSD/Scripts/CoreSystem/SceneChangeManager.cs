using UnityEngine.SceneManagement;
using UnityEngine.AddressableAssets;
using VContainer;
using VContainer.Unity;
using Cysharp.Threading.Tasks;
using System;

public class SceneChangeManager
{
    private readonly GlobalUIManager _globalUIManager;
    private readonly AssetLifecycleManager _assetLifecycle;
    public bool IsTransitioning { get; private set; }

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
        IsTransitioning = true;
        try
        {
        // 1. 화면을 완전히 가린 뒤 씬을 로드한다.
        await _globalUIManager.FadeInAsync();
        _globalUIManager.ReportLoadingProgress(0.1f, "데이터 확인 중");

        // 2. 씬 로드 전 추가 작업 (데이터 저장 등)
        if (beforeLoad != null)
        {
            await beforeLoad();
        }

        // 3. 씬 로드 (Addressables 비동기 로드)
        await LoadSceneInternalAsync(sceneName);

        // 4. 이전 씬에 종속되었던 비관리 에셋 라이프사이클만 정리
        _assetLifecycle.UnloadAll();

        // Scene activation finishes before its asynchronous gameplay preparation.
        var scope = LifetimeScope.Find<InGameLifetimeScope>(SceneManager.GetActiveScene());
        if (scope != null)
            await scope.Container.Resolve<GameInitializer>().Preparation;

        // 5. 씬 로드 후 추가 작업 (데이터 바인딩, 초기화 등)
        if (afterLoad != null)
        {
            await afterLoad();
        }

        _globalUIManager.ReportLoadingProgress(1f, "준비 완료");

        }
        finally
        {
            // 로그인/저장/씬 로드가 실패해도 가림막을 해제해 재시도할 수 있다.
            try { await _globalUIManager.FadeOutAsync(); }
            finally { IsTransitioning = false; }
        }
    }

    /// <summary>
    /// 실제 씬 로드 처리 (Addressables 비동기 씬 로드)
    /// </summary>
    private async UniTask LoadSceneInternalAsync(string sceneName)
    {
        var progress = Progress.Create<float>(value =>
            _globalUIManager.ReportLoadingProgress(0.15f + value * 0.4f, "전장 불러오는 중"));
        // 시작 씬은 일반 Player 씬으로 포함한다. Addressables 씬과 중복 등록하지 않는다.
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            if (System.IO.Path.GetFileNameWithoutExtension(path) != sceneName) continue;
            await SceneManager.LoadSceneAsync(i).ToUniTask(progress: progress);
            return;
        }
        try
        {
            await Addressables.LoadSceneAsync(sceneName).ToUniTask(progress: progress);
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogWarning($"[SceneChangeManager] Addressables.LoadSceneAsync('{sceneName}') 실패, SceneManager 폴백 시도: {e.Message}");
            await SceneManager.LoadSceneAsync(sceneName).ToUniTask(progress: progress);
        }
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
