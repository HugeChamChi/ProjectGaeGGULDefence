using VContainer.Unity;

namespace GaeGGUL.Tutorial
{
    /// <summary>씬의 튜토리얼 대상을 앱 서비스에 등록하고 씬 종료 때 해제한다.</summary>
    public sealed class TutorialSceneBinding : IInitializable, System.IDisposable
    {
        private readonly TutorialManager _manager;
        private readonly SceneComponentCollection _components;

        /// <summary>앱의 매니저와 이 씬의 컴포넌트 제공자를 주입받는다.</summary>
        public TutorialSceneBinding(TutorialManager manager, SceneComponentCollection components)
        {
            _manager = manager;
            _components = components;
        }
        /// <summary>다른 초기화 순서를 바꾸지 않고 튜토리얼 대상을 등록한다.</summary>
        public void Initialize() => _manager.RegisterScene(_components);
        /// <summary>씬 종료 때 오래된 대상 참조를 해제한다.</summary>
        public void Dispose() { if (_manager != null) _manager.UnregisterScene(_components); }
    }
}
