using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;

/// <summary>영구 강화의 저장 진행을 한 전투의 변경 불가능한 수치로 읽는다. 튜토리얼에서는 0.</summary>
public sealed class ResearchRunBonuses
{
    private readonly ResearchSaveService _saves;
    private readonly ResearchAccountContext _account;
    private readonly ResearchTreeData _tree;
    private readonly float[] _values = new float[Enum.GetValues(typeof(ResearchStat)).Length];
    private readonly SceneComponentCollection _scene;
    /// <summary>강화 로드가 끝나기 전에는 전투 시작을 허용하지 않는다.</summary>
    public bool IsReady { get; private set; }
    private string _rewardAccount;
    private long _rewardGeneration;
    public bool CanGrantBattleRewards(string account) => IsReady && _rewardAccount != null
        && _rewardAccount == account && _account.AccountKey == account && _account.Generation == _rewardGeneration;

#if UNITY_EDITOR
    /// <summary>Direct Editor previews use zero permanent bonuses and never load a player save session.</summary>
    public void PrepareEditorPreview()
    {
        Array.Clear(_values, 0, _values.Length);
        IsReady = true;
    }
#endif

    /// <summary>화면과 같은 SO/저장 세션을 사용하며 튜토리얼 씬은 명시적으로 제외한다.</summary>
    public ResearchRunBonuses(ResearchSaveService saves, ResearchAccountContext account,
        ResearchTreeData tree, SceneComponentCollection scene)
    {
        _saves = saves; _account = account; _tree = tree;
        _scene = scene;
    }

    /// <summary>게임 시작 전에 모든 합계를 복사한다. 이후 화면/저장 변경이 현재 전투를 바꾸지 않는다.</summary>
    public async UniTask LoadAsync(CancellationToken cancellationToken)
    {
        if (IsReady) return;
        // Scope construction runs before scene.isLoaded. Detect the tutorial after loading,
        // when scene components can actually be enumerated, rather than caching a false negative.
        if (!_scene.Enumerate<GaeGGUL.Tutorial.IngameTutorialDirector>().Any())
        {
            if (_tree == null) throw new InvalidOperationException("영구 강화 트리 미연결");
            string accountKey = _account.AccountKey;
            long accountGeneration = _account.Generation;
            var tree = await _saves.ResolveTreeAsync(_tree, cancellationToken);
            if (accountKey != _account.AccountKey) throw new InvalidOperationException("Research account changed during load");
            var session = _saves.GetSession(accountKey, tree, "OutgameUpgrade.");
            await session.LoadAsync(cancellationToken);
            await session.FlushAsync(cancellationToken);
            if (accountKey != ResearchSaveAddress.LocalAccount && Player.PlayerData.IsDirty)
                await Player.PlayerData.TrySaveAsync();
            if (session.SaveError != null || Player.PlayerData.IsDirty) throw new InvalidOperationException("Upgrade progress is not confirmed by server.");
            if (accountKey != _account.AccountKey) throw new InvalidOperationException("Research account changed during load");
            foreach (ResearchStat stat in Enum.GetValues(typeof(ResearchStat)))
                _values[(int)stat] = session.Progress.GetTotal(stat);
            if (accountGeneration != _account.Generation) throw new InvalidOperationException("Research account session changed during load");
            _rewardAccount = accountKey;
            _rewardGeneration = accountGeneration;
        }
        cancellationToken.ThrowIfCancellationRequested();
        IsReady = true;
    }

    /// <summary>SO의 비율 값은 그대로, 시작 식량은 정수량 그대로 반환한다.</summary>
    public float Get(ResearchStat stat) => _values[(int)stat];
}
