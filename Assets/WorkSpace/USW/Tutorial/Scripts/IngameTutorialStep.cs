using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace GaeGGUL.Tutorial
{
    /// <summary>Gameplay lesson stored in the existing TutorialSequence asset format.</summary>
    [Serializable]
    public sealed class IngameTutorialStep : TutorialStepBase
    {
        /// <summary>Gameplay condition and interaction to teach.</summary>
        public IngameTutorialStage Stage = IngameTutorialStage.FirstSummon;
        /// <summary>Instruction displayed until this lesson's interaction completes.</summary>
        [UnityEngine.TextArea(2, 4)] public string Instruction;

        /// <summary>Runs with the scene's injected gameplay director.</summary>
        public UniTask ExecuteAsync(IngameTutorialDirector director, CancellationToken token) =>
            director.ExecuteStageAsync(Stage, token);

        /// <summary>Gameplay sequences require a scene-bound gameplay director.</summary>
        public override UniTask ExecuteAsync(TutorialManager manager) =>
            throw new InvalidOperationException("Use IngameTutorialDirector for gameplay tutorial steps.");
    }
}
