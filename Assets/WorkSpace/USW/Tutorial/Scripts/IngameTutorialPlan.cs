using System.Collections.Generic;
using UnityEngine;

namespace GaeGGUL.Tutorial
{
    /// <summary>Designer-owned ordered list of reusable lesson assets.</summary>
    [CreateAssetMenu(menuName = "USW/Tutorial/Sequence", fileName = "TutorialSequence")]
    public sealed class IngameTutorialPlan : ScriptableObject
    {
        [Tooltip("위에서 아래 순서로 실행합니다. 같은 단계 SO를 여러 시퀀스에서 재사용할 수 있습니다.")]
        public List<IngameTutorialLesson> Lessons = new List<IngameTutorialLesson>();

        /// <summary>Reports invalid recipes before entering Play, without requiring a fixed number of lessons.</summary>
        public string Validate()
        {
            if (Lessons.Count == 0) return "단계를 하나 이상 추가해 주세요.";
            var seen = new HashSet<IngameTutorialStage>();
            foreach (var lesson in Lessons)
            {
                if (lesson == null) return "비어 있는 단계 슬롯이 있습니다.";
                var error = lesson.Validate();
                if (error != null) return lesson.name + ": " + error;
                if (lesson.Kind != IngameTutorialLesson.LessonKind.GameplayRecipe) continue;
                var stage = lesson.Gameplay.Stage;
                if (!System.Enum.IsDefined(typeof(IngameTutorialStage), stage)) return lesson.name + ": 전투 블록을 선택해 주세요.";
                if (seen.Contains(stage)) return lesson.name + ": 일회성 전투 블록을 중복 사용할 수 없습니다.";
                foreach (var prerequisite in Prerequisites(stage))
                    if (!seen.Contains(prerequisite)) return lesson.name + ": 앞에 " + prerequisite + " 블록이 필요합니다.";
                seen.Add(stage);
            }
            return null;
        }

        private static IngameTutorialStage[] Prerequisites(IngameTutorialStage stage)
        {
            switch (stage)
            {
                case IngameTutorialStage.TimeLimit: return new[] { IngameTutorialStage.BossEntrance };
                case IngameTutorialStage.FirstSummon: return new[] { IngameTutorialStage.TimeLimit };
                case IngameTutorialStage.ObserveCombat: return new[] { IngameTutorialStage.FirstSummon };
                case IngameTutorialStage.ThreeSummons: return new[] { IngameTutorialStage.BossEntrance };
                case IngameTutorialStage.Merge: return new[] { IngameTutorialStage.ObserveCombat };
                case IngameTutorialStage.ExperienceGauge: return new[] { IngameTutorialStage.UpgradeSlots };
                case IngameTutorialStage.LevelUp: return new[] { IngameTutorialStage.ExperienceGauge };
                case IngameTutorialStage.OpenUpgrade: return new[] { IngameTutorialStage.Merge };
                case IngameTutorialStage.UpgradeSlots: return new[] { IngameTutorialStage.OpenUpgrade };
                case IngameTutorialStage.ChiefSkill: return new[] { IngameTutorialStage.LevelUp, IngameTutorialStage.UpgradeSlots };
                case IngameTutorialStage.TotemChoice: return new[] { IngameTutorialStage.ChiefSkill };
                case IngameTutorialStage.OpenInventory: return new[] { IngameTutorialStage.TotemChoice };
                case IngameTutorialStage.PlaceTotem: return new[] { IngameTutorialStage.OpenInventory };
                case IngameTutorialStage.MoveTotem: return new[] { IngameTutorialStage.PlaceTotem };
                case IngameTutorialStage.RotateTotem: return new[] { IngameTutorialStage.PlaceTotem };
                default: return System.Array.Empty<IngameTutorialStage>();
            }
        }
    }
}
