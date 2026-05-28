using System;
using System.Collections.Generic;
using UnityEngine;

namespace GaeGGUL.Tutorial
{
    /// <summary>
    /// 뒤끝(TheBackend)에 저장될 튜토리얼 진행 데이터 모델입니다.
    /// </summary>
    [Serializable]
    public class TutorialData
    {
        // 완료된 튜토리얼 ID 리스트
        public List<string> completedTutorials = new List<string>();

        public bool IsCompleted(string tutorialID)
        {
            return completedTutorials.Contains(tutorialID);
        }

        public void Complete(string tutorialID)
        {
            if (!completedTutorials.Contains(tutorialID))
            {
                completedTutorials.Add(tutorialID);
            }
        }
    }
}
