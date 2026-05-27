using UnityEngine;

namespace GaeGGUL.Tutorial
{
    public class UI_TutorialRaycastFilter : MonoBehaviour, ICanvasRaycastFilter
    {
        private RectTransform _holeRect;
        private bool _isHighlighting = false;

        public void SetTarget(RectTransform target)
        {
            _holeRect = target;
            _isHighlighting = true;
        }

        public void Clear()
        {
            _isHighlighting = false;
            _holeRect = null;
        }

        public bool IsRaycastLocationValid(Vector2 sp, Camera eventCamera)
        {
            if (!_isHighlighting || _holeRect == null) return true;

            // 타겟 UI가 속한 캔버스의 카메라를 정확히 참조 (Overlay면 null)
            var canvas = _holeRect.GetComponentInParent<Canvas>();
            var cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;

            // 클릭 지점이 구멍 안에 있는지 체크 (true면 통과시키기 위해 false 반환)
            return !RectTransformUtility.RectangleContainsScreenPoint(_holeRect, sp, cam);
        }
    }
}
