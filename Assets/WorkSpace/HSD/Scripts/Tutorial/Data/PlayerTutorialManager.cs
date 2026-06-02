using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GaeGGUL.Tutorial
{
    /// <summary>
    /// 플레이어의 튜토리얼 진행 상태를 관리하고 뒤끝 서버와 동기화합니다.
    /// </summary>
    public class PlayerTutorialManager : Global.IClearable
    {
        public TutorialData Data { get; private set; } = new();
        public bool IsDirty { get; set; }

        public async UniTask InitializeAsync()
        {
            // TODO: 뒤끝 서버에서 튜토리얼 데이터를 로드하는 로직 구현
            // BackendGameData.Instance.GetGameData<TutorialData>() 등을 활용
            await UniTask.CompletedTask;
            Debug.Log("[PlayerTutorialManager] Initialized");
        }

        public bool IsCompleted(string tutorialID)
        {
            return Data.IsCompleted(tutorialID);
        }

        public void MarkAsCompleted(string tutorialID)
        {
            if (Data.IsCompleted(tutorialID)) return;

            Data.Complete(tutorialID);
            IsDirty = true;
        }

        public async UniTask SaveAsync()
        {
            if (!IsDirty) return;
            
            // TODO: 뒤끝 서버에 데이터 업데이트
            // BackendGameData.Instance.GameDataUpdate(Data);
            Debug.Log($"[PlayerTutorialManager] Saved Tutorial Completion: {Data.completedTutorials.Count} total.");
            IsDirty = false;
            await UniTask.CompletedTask;
        }

        public void Clear()
        {
            Data = new TutorialData();
        }
    }
}
