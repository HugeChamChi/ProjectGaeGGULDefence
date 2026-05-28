using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GaeGGUL.Tutorial
{
    /// <summary>
    /// 씬에 배치되어 실제 연출 로직(DOTween, 파티클 등)을 담당하는 베이스 클래스입니다.
    /// 연출 자체가 하나의 Mono가 됩니다.
    /// </summary>
    public abstract class TutorialActor : MonoBehaviour
    {
        [SerializeField] private string _actorID;
        public string ActorID => _actorID;

        /// <summary>
        /// 이 Mono가 담당하는 연출을 실행합니다.
        /// </summary>
        public abstract UniTask PlayAsync();
    }
}
