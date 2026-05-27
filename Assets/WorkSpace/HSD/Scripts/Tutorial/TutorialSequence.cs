using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GaeGGUL.Tutorial
{
    [CreateAssetMenu(fileName = "TutorialSequence", menuName = "GaeGGUL/Tutorial/Sequence")]
    public class TutorialSequence : ScriptableObject
    {
        public string tutorialID;

        [SerializeReference]
        [SelectableReference]
        public List<TutorialStepBase> steps = new List<TutorialStepBase>();

        public async UniTask PlayAsync(TutorialManager manager)
        {
            Debug.Log($"[Tutorial] Starting Sequence: {tutorialID}");
            
            foreach (var step in steps)
            {
                if (step == null) continue;

                // 스텝 설정에 따라 배경 Dim 처리 (비동기로 실행하여 연출과 병렬 진행)
                manager.SetDimAsync(step.useDim).Forget();
                
                await step.ExecuteAsync(manager);
            }

            // 전체 시퀀스 종료 후 Dim 해제
            await manager.SetDimAsync(false);
            
            Debug.Log($"[Tutorial] Finished Sequence: {tutorialID}");
        }
    }
}
