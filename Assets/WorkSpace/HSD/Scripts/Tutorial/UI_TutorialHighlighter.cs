using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace GaeGGUL.Tutorial
{
    /// <summary>
    /// UI/TutorialHole 쉐이더의 파라미터를 애니메이션하여 타겟을 강조하는 하이라이터입니다.
    /// 최대 Alpha 값을 고정하여 항상 일정한 어둡기를 유지합니다.
    /// </summary>
    public class UI_TutorialHighlighter : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private Material _holeMaterial; 
        [SerializeField] private Image _maskImage;      

        [Header("Focus Animation")]
        [SerializeField] private float _focusDuration = 0.4f;      
        [SerializeField] private float _startSizeMultiplier = 3.0f; 
        
        [Header("Alpha Settings")]
        [Range(0f, 1f)]
        [SerializeField] private float _targetAlpha = 0.67f; // 170/255 = 약 0.67

        private static readonly int CenterID = Shader.PropertyToID("_Center");
        private static readonly int SizeID = Shader.PropertyToID("_Size");
        private static readonly int SoftnessID = Shader.PropertyToID("_Softness");
        private static readonly int ColorID = Shader.PropertyToID("_Color");

        private Tween _focusTween;

        public void SetTarget(RectTransform target, Vector2 sizeOffset, float softness)
        {
            if (_maskImage == null || _holeMaterial == null || target == null) return;

            _focusTween?.Kill();
            _maskImage.gameObject.SetActive(true);

            RectTransform maskRect = _maskImage.rectTransform;

            // 1. 최종 목표값 계산
            Vector3[] corners = new Vector3[4];
            target.GetWorldCorners(corners);
            Vector3 worldCenter = (corners[0] + corners[2]) * 0.5f;
            Vector3 localCenter = maskRect.InverseTransformPoint(worldCenter);

            float worldWidth = Vector3.Distance(corners[0], corners[3]);
            float worldHeight = Vector3.Distance(corners[0], corners[1]);
            Vector2 finalLocalSize = new Vector2(
                worldWidth / maskRect.lossyScale.x, 
                worldHeight / maskRect.lossyScale.y
            ) + sizeOffset;

            // 2. 초기 상태 설정
            _holeMaterial.SetVector(CenterID, new Vector4(localCenter.x, localCenter.y, 0, 0));
            _holeMaterial.SetFloat(SoftnessID, softness);
            
            // RGB는 Image의 값을 유지
            Color baseColor = _maskImage.color;
            _holeMaterial.SetColor(ColorID, new Color(baseColor.r, baseColor.g, baseColor.b, 0f));

            // 시작 크기
            Vector2 startLocalSize = finalLocalSize * _startSizeMultiplier;
            _holeMaterial.SetVector(SizeID, new Vector4(startLocalSize.x, startLocalSize.y, 0, 0));

            // 3. 애니메이션 실행
            _focusTween = DOTween.To(() => 0f, lerp => {
                // Alpha를 0에서 _targetAlpha(170 수준)까지 페이드
                float currentAlpha = lerp * _targetAlpha;
                _holeMaterial.SetColor(ColorID, new Color(baseColor.r, baseColor.g, baseColor.b, currentAlpha));

                // Size 수축
                Vector2 currentSize = Vector2.Lerp(startLocalSize, finalLocalSize, lerp);
                _holeMaterial.SetVector(SizeID, new Vector4(currentSize.x, currentSize.y, 0, 0));
            }, 1f, _focusDuration)
            .SetEase(Ease.OutQuart)
            .SetUpdate(true);

            Debug.Log($"[Highlighter] Focus Animation: TargetAlpha={_targetAlpha}");
        }

        public void Hide()
        {
            _focusTween?.Kill();
            if (_maskImage != null) _maskImage.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            _focusTween?.Kill();
        }
    }
}
