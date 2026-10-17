using UnityEngine;

namespace GaeGGUL.Tutorial
{
    /// <summary>기기에 보존하는 첫 전투 튜토리얼 완료 기록. 앱 재실행의 튜토리얼 생략에 사용한다.</summary>
    public sealed class IngameTutorialProgress
    {
        private const string CompletedKey = "IngameTutorial.Completed.v1";
        /// <summary>이 기기에서 전용 인게임 튜토리얼을 끝냈는지.</summary>
        public bool IsCompleted => PlayerPrefs.GetInt(CompletedKey, 0) == 1;
        /// <summary>모든 단계를 끝낸 경우에만 즉시 저장한다.</summary>
        public void Complete() { PlayerPrefs.SetInt(CompletedKey, 1); PlayerPrefs.Save(); }
    }
}
