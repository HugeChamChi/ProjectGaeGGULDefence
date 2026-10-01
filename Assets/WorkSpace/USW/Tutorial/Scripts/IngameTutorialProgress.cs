using UnityEngine;

namespace GaeGGUL.Tutorial
{
    /// <summary>내부 테스트용 기기 저장. 서버의 로비 튜토리얼 진행과 별도로 관리한다.</summary>
    public sealed class IngameTutorialProgress
    {
        private const string CompletedKey = "IngameTutorial.Completed.v1";
        /// <summary>이 기기에서 전용 인게임 튜토리얼을 끝냈는지.</summary>
        public bool IsCompleted => PlayerPrefs.GetInt(CompletedKey, 0) == 1;
        /// <summary>모든 단계를 끝낸 경우에만 즉시 저장한다.</summary>
        public void Complete() { PlayerPrefs.SetInt(CompletedKey, 1); PlayerPrefs.Save(); }
    }
}
