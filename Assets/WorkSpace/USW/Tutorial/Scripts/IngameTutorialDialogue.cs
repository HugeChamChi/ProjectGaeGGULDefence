using TMPro;
using UnityEngine;

namespace GaeGGUL.Tutorial
{
    /// <summary>Displays the current lesson without an independent next/skip action.</summary>
    public sealed class IngameTutorialDialogue : MonoBehaviour
    {
        [SerializeField] private TMP_Text _progress;
        [SerializeField] private TMP_Text _instruction;
        [SerializeField] private TMP_Text _hint;

        /// <summary>Changes dialogue at the same time as the authored gameplay stage.</summary>
        public void Show(IngameTutorialStep step, int total, int index = 0)
        {
            gameObject.SetActive(true);
            _progress.text = $"{(index > 0 ? index : (int)step.Stage)} / {total}";
            _instruction.text = step.Instruction;
            bool awareness = step.Stage == IngameTutorialStage.LevelUp || step.Stage == IngameTutorialStage.UpgradeSlots ||
                step.Stage == IngameTutorialStage.ChiefSkill || step.Stage == IngameTutorialStage.TotemChoice;
            _hint.text = awareness ? "하이라이트를 확인하고 화면을 터치하세요" :
                step.Stage == IngameTutorialStage.BossEntrance ? string.Empty : "안내된 조작을 완료하면 다음으로 넘어갑니다";
        }

        /// <summary>Displays a designer-composed UI lesson with its actual sequence position.</summary>
        public void ShowText(string instruction, int index, int total, bool awareness)
        {
            gameObject.SetActive(true);
            _progress.text = $"{index} / {total}";
            _instruction.text = instruction;
            _hint.text = awareness ? "하이라이트를 확인하고 화면을 터치하세요" : "안내된 조작을 완료하면 다음으로 넘어갑니다";
        }

        /// <summary>Dismisses the text together with an awareness guide or the completed sequence.</summary>
        public void Hide() => gameObject.SetActive(false);
    }
}
