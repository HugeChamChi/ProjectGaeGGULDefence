using UnityEngine;
using Cysharp.Threading.Tasks;

namespace GaeGGUL.Animation
{
    public interface ITweenEffect
    {
        // 효과 초기화 (대상 객체 전달)
        void Initialize(Transform target);
        
        // 효과 재생
        UniTask Play();
        
        // 효과 중단
        void Stop();
    }
}