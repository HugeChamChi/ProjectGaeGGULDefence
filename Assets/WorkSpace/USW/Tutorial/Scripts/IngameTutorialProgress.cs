using System.Threading;
using Cysharp.Threading.Tasks;
using VContainer;

namespace GaeGGUL.Tutorial
{
    /// <summary>현재 인증 계정의 첫 전투 튜토리얼 완료 기록과 저장 재시도.</summary>
    public sealed class IngameTutorialProgress
    {
        /// <summary>기존 화면별 튜토리얼과 구분하는 첫 전투 완료 ID.</summary>
        public const string CompletionId = "ingame-first-battle.v1";
        private readonly PlayerDataController _player;

        /// <summary>독립 검증 도구에서도 기존 Player 진입점을 사용할 수 있다.</summary>
        public IngameTutorialProgress() : this(Player.PlayerData) { }

        /// <summary>루트에 등록된 계정 저장 컨트롤러를 사용한다.</summary>
        [Inject]
        public IngameTutorialProgress(PlayerDataController player) { _player = player; }

        /// <summary>로드한 현재 계정이 전용 인게임 튜토리얼을 끝냈는지.</summary>
        public bool IsCompleted => _player.IsReady && _player.Data.CompletedTutorials.Contains(CompletionId);

        /// <summary>모든 단계를 끝낸 계정의 영속 저널에 중복 없이 기록한다.</summary>
        public void Complete() { _player.CompleteTutorial(CompletionId); }

        /// <summary>로비 이동 전에 계정 완료 기록 저장을 확인한다. 실패 시 기존 저널은 재시도를 위해 남긴다.</summary>
        public async UniTask<bool> SaveCompletionAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (!_player.IsReady) return false;
            Complete();
            return await _player.TrySaveAsync().AttachExternalCancellation(token);
        }
    }
}
