using Cysharp.Threading.Tasks;

/// <summary>
/// 런타임 에셋(Sprite, Prefab 등)을 Address 기반으로 로드/언로드하는 SO의 공통 계약.
/// AssetLifecycleManager가 이 인터페이스를 통해 일괄 관리합니다.
/// </summary>
public interface ILoadableAsset
{
    UniTask LoadAssetsAsync();
    void UnloadAssets();
    bool IsLoaded { get; }
}
