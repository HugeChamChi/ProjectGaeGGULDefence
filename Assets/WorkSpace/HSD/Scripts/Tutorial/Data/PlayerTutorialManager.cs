using Cysharp.Threading.Tasks;

namespace GaeGGUL.Tutorial
{
    /// <summary>Tutorial completion shares the durable PlayerData snapshot; no extra table/write.</summary>
    public class PlayerTutorialManager : Global.IClearable
    {
        public TutorialData Data { get; private set; } = new();
        public bool IsDirty { get => false; set { } }
        public UniTask InitializeAsync()
        {
            Data = new TutorialData();
            if (Player.PlayerData.IsReady) Data.completedTutorials = Player.PlayerData.Data.CompletedTutorials;
            return UniTask.CompletedTask;
        }
        public bool IsCompleted(string id) => Player.PlayerData.IsReady && Player.PlayerData.Data.CompletedTutorials.Contains(id);
        public void MarkAsCompleted(string id)
        {
            Player.PlayerData.CompleteTutorial(id);
            if (Player.PlayerData.IsReady) Data.completedTutorials = Player.PlayerData.Data.CompletedTutorials;
        }
        public UniTask SaveAsync() => Player.PlayerData.SaveAsync();
        public void Clear() { Data = new TutorialData(); }
    }
}
