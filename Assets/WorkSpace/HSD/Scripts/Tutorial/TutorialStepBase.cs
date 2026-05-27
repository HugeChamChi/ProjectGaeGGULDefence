using System;
using Cysharp.Threading.Tasks;

namespace GaeGGUL.Tutorial
{
    [Serializable]
    public abstract class TutorialStepBase
    {
        public float delayBeforeExecute = 0f;
        public bool useDim = false; // 이 스텝에서 배경 Dim 효과를 사용할지 여부

        public abstract UniTask ExecuteAsync(TutorialManager manager);
    }
}
