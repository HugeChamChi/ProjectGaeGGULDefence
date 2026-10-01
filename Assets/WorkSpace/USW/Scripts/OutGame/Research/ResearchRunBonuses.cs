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
    private readonly bool _enabled;
    /// <summary>강화 로드가 끝나기 전에는 전투 시작을 허용하지 않는다.</summary>
    public bool IsReady { get; private set; }

    /// <summary>화면과 같은 SO/저장 세션을 사용하며 튜토리얼 씬은 명시적으로 제외한다.</summary>
    public ResearchRunBonuses(ResearchSaveService saves, ResearchAccountContext account,
        ResearchTreeData tree, SceneComponentCollection scene)
    {
        _saves = saves; _account = account; _tree = tree;
        _enabled = !scene.Enumerate<GaeGGUL.Tutorial.IngameTutorialDirector>().Any();
    }

    /// <summary>게임 시작 전에 모든 합계를 복사한다. 이후 화면/저장 변경이 현재 전투를 바꾸지 않는다.</summary>
    public async UniTask LoadAsync(CancellationToken cancellationToken)
    {
        if (IsReady) return;
        if (_enabled)
        {
            if (_tree == null) throw new InvalidOperationException("영구 강화 트리 미연결");
            var session = _saves.GetSession(_account.AccountKey, _tree, "OutgameUpgrade.");
            await session.LoadAsync(cancellationToken);
            foreach (ResearchStat stat in Enum.GetValues(typeof(ResearchStat)))
                _values[(int)stat] = session.Progress.GetTotal(stat);
        }
        cancellationToken.ThrowIfCancellationRequested();
        IsReady = true;
    }

    /// <summary>SO의 비율 값은 그대로, 시작 식량은 정수량 그대로 반환한다.</summary>
    public float Get(ResearchStat stat) => _values[(int)stat];
}
