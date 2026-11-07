namespace GaeGGUL.Tutorial
{
    /// <summary>First battle completion belongs to the authenticated account, not the device.</summary>
    public sealed class IngameTutorialProgress
    {
        public const string TutorialId = "IngameTutorial.v1";
        public bool IsCompleted => Player.Tutorial.IsCompleted(TutorialId);
        public void Complete() => Player.Tutorial.MarkAsCompleted(TutorialId);
    }
}
