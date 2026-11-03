using System;
using Cysharp.Threading.Tasks;
namespace GaeGGUL.Tutorial
{
    /// <summary>All tutorial completions are part of authenticated lobby progress.</summary>
    public class PlayerTutorialManager : Global.IClearable
    {
        public TutorialData Data => new TutorialData { completedTutorials = Player.LobbyProgress.Data?.CompletedTutorials ?? new() };
        public bool IsDirty { get => Player.LobbyProgress.IsDirty; set => Player.LobbyProgress.IsDirty = value; }
        public UniTask InitializeAsync()
        { if (!Player.LobbyProgress.IsReady) throw new InvalidOperationException("Tutorial progress is not loaded."); return UniTask.CompletedTask; }
        public bool IsCompleted(string id) => Player.LobbyProgress.IsReady && Player.LobbyProgress.Data.CompletedTutorials.Contains(id);
        public void MarkAsCompleted(string id)
        {
            if (string.IsNullOrEmpty(id) || !Player.LobbyProgress.IsReady || IsCompleted(id)) return;
            Player.LobbyProgress.Data.CompletedTutorials.Add(id); Player.LobbyProgress.Changed();
        }
        public UniTask SaveAsync() => Player.LobbyProgress.SaveAsync();
        public void Clear() { }
    }
}
