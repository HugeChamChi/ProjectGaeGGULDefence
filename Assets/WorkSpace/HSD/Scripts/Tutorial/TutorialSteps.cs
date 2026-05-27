using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GaeGGUL.Tutorial
{
    // --- [Data Structures] ---
    [Serializable]
    public class DialogueData
    {
        [TextArea(3, 5)]
        public string dialogue;
        public Sprite characterIcon;
        public string speakerName;
    }

    // --- [Action Steps] ---

    [Serializable]
    public class DialogueStep : TutorialStepBase
    {
        public DialogueData dialogueData;
        public bool waitForClick = true;
        public string waitTargetID; // 이 값이 있으면 해당 ID의 버튼 클릭 시에만 넘어감

        public override async UniTask ExecuteAsync(TutorialManager manager)
        {
            if (delayBeforeExecute > 0)
                await UniTask.Delay(TimeSpan.FromSeconds(delayBeforeExecute));

            Debug.Log($"[Tutorial] Show Dialogue: {dialogueData.dialogue}");
            // manager.DialogueUI.Show(dialogueData);
            
            if (waitForClick)
            {
                if (string.IsNullOrEmpty(waitTargetID))
                    await manager.WaitAnyClick();
                else
                    await manager.WaitTargetClick(waitTargetID);
            }
        }
    }

    [Serializable]
    public class AnimationStep : TutorialStepBase
    {
        public string targetID;
        public bool waitForFinish = true;

        public override async UniTask ExecuteAsync(TutorialManager manager)
        {
            if (delayBeforeExecute > 0)
                await UniTask.Delay(TimeSpan.FromSeconds(delayBeforeExecute));

            var actor = TutorialRegistry.GetActor(targetID);
            if (actor != null)
            {
                if (waitForFinish) await actor.PlayAsync();
                else actor.PlayAsync().Forget();
            }
            else
            {
                Debug.LogWarning($"[Tutorial] Animation Actor not found: {targetID}");
            }
        }
    }

    [Serializable]
    public class ButtonGuideStep : TutorialStepBase
    {
        public string targetID;
        public bool waitForClick = true;

        public override async UniTask ExecuteAsync(TutorialManager manager)
        {
            if (delayBeforeExecute > 0) await UniTask.Delay(TimeSpan.FromSeconds(delayBeforeExecute));

            var target = TutorialRegistry.GetUI(targetID);
            if (target != null)
            {
                // UI를 최상단으로 올리고 하이라이트 표시
                manager.ShowHighlight(targetID);

                if (waitForClick)
                {
                    await manager.WaitTargetClick(targetID);
                    manager.HideHighlight();
                }
            }
        }
    }

    [Serializable]
    public class WaitButtonClickStep : TutorialStepBase
    {
        public string targetID;

        public override async UniTask ExecuteAsync(TutorialManager manager)
        {
            if (delayBeforeExecute > 0) await UniTask.Delay(TimeSpan.FromSeconds(delayBeforeExecute));

            Debug.Log($"[Tutorial] Waiting for button click: {targetID}");
            await manager.WaitTargetClick(targetID);
        }
    }

    [Serializable]
    public class HideHighlightStep : TutorialStepBase
    {
        public override async UniTask ExecuteAsync(TutorialManager manager)
        {
            Debug.Log("[Tutorial] Hide Highlight");
            manager.HideHighlight();
            await UniTask.CompletedTask;
        }
    }
}
