using UnityEngine;

namespace GaeGGUL.Tutorial
{
    /// <summary>A reusable recipe or a configurable UI guide, authored independently of scene objects.</summary>
    [CreateAssetMenu(menuName = "USW/Tutorial/Lesson", fileName = "TutorialLesson")]
    public sealed class IngameTutorialLesson : ScriptableObject
    {
        /// <summary>Supported building blocks.</summary>
        public enum LessonKind { GameplayRecipe, Awareness, ForceButton, WaitForSignal }
        /// <summary>When a custom lesson starts displaying its guidance.</summary>
        public enum StartCondition { Immediately, TargetVisible, Signal }

        [Tooltip("GameplayRecipe: 전투 전용 블록 / Awareness: 인지 표시 / ForceButton: 버튼 선택 강제 / WaitForSignal: 게임 이벤트 완료 대기")]
        public LessonKind Kind;
        [Tooltip("기존 전투 조작 블록. 대사와 지연 시간도 여기서 편집합니다.")]
        public IngameTutorialStep Gameplay = new IngameTutorialStep();
        [TextArea(2, 5)] public string Instruction;
        public StartCondition StartWhen;
        [Tooltip("Scene의 TutorialTargetBinding.Key와 일치하는 이름. SO에 씬 오브젝트를 직접 넣지 않습니다.")]
        public string TargetKey;
        public string StartSignal;
        public string CompletionSignal;
        [Min(0)] public float DelaySeconds;
        public bool PauseGameplay = true;
        public bool Dim = true;
        public bool ShowHand = true;
        [Tooltip("조건/신호 대기 중 월드 조작을 허용합니다. 인지 표시와 버튼 강제 표시 중에는 오버레이가 입력을 제한합니다.")]
        public bool AllowWorldInput;

        /// <summary>Checks authoring errors before a sequence starts.</summary>
        public string Validate()
        {
            if (Kind == LessonKind.GameplayRecipe) return Gameplay == null ? "전투 블록이 비어 있습니다." : null;
            if ((Kind == LessonKind.ForceButton || StartWhen == StartCondition.TargetVisible) && string.IsNullOrWhiteSpace(TargetKey))
                return "Target Key를 지정해 주세요.";
            if (StartWhen == StartCondition.Signal && string.IsNullOrWhiteSpace(StartSignal)) return "Start Signal을 지정해 주세요.";
            if (Kind == LessonKind.WaitForSignal && string.IsNullOrWhiteSpace(CompletionSignal)) return "Completion Signal을 지정해 주세요.";
            if (Kind == LessonKind.WaitForSignal && PauseGameplay) return "게임 이벤트 대기 단계는 Pause Gameplay를 꺼 주세요.";
            return null;
        }
    }
}
