using UnityEngine;
using UnityEngine.UI;

namespace GaeGGUL.Tutorial
{
    /// <summary>Resolves reusable lesson keys to actual scene UI objects.</summary>
    public sealed class TutorialTargetBinding : MonoBehaviour
    {
        public string Key;
        public RectTransform Highlight;
        public Button Button;
        private Canvas[] _canvases;
        private CanvasGroup[] _groups;

        private void Awake()
        {
            var target = Highlight != null ? Highlight : transform;
            _canvases = target.GetComponentsInParent<Canvas>(true);
            _groups = target.GetComponentsInParent<CanvasGroup>(true);
        }

        /// <summary>Checks UI_Base Canvas visibility as well as GameObject and CanvasGroup state.</summary>
        public bool IsVisible
        {
            get
            {
                if (Highlight == null || !Highlight.gameObject.activeInHierarchy) return false;
                if (_canvases != null) foreach (var canvas in _canvases) if (!canvas.isActiveAndEnabled) return false;
                if (_groups != null) foreach (var group in _groups) if (group.alpha <= 0 || !group.interactable) return false;
                return Button == null || Button.IsInteractable();
            }
        }
    }
}
