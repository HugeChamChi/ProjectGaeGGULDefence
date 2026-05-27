using UnityEngine;
using UnityEngine.UI;

namespace GaeGGUL.Tutorial
{
    /// <summary>
    /// UI/TutorialHole 쉐이더의 파라미터를 조절하여 화면에 구멍을 뚫어주는 하이라이터입니다.
    /// </summary>
    public class UI_TutorialHighlighter : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private Material _holeMaterial; // UI/TutorialHole 쉐이더가 적용된 재질
        [SerializeField] private Image _maskImage;      // 쉐이더 재질이 입혀진 배경 이미지 (Full Screen Stretch 권장)

        private static readonly int CenterID = Shader.PropertyToID("_Center");
        private static readonly int SizeID = Shader.PropertyToID("_Size");
        private static readonly int SoftnessID = Shader.PropertyToID("_Softness");

        /// <summary>
        /// 타겟 UI의 위치와 크기를 계산하여 쉐이더 구멍을 뚫습니다. (Pivot 위치에 상관없이 기하학적 중앙 계산)
        /// </summary>
        public void SetTarget(RectTransform target, Vector2 sizeOffset, float softness)
        {
            if (_maskImage == null || _holeMaterial == null || target == null) return;

            _maskImage.gameObject.SetActive(true);
            RectTransform maskRect = _maskImage.rectTransform;

            // 1. 타겟의 월드 사각형 코너 정보 가져오기
            Vector3[] corners = new Vector3[4];
            target.GetWorldCorners(corners);
            
            // 2. 기하학적 중앙 계산: Pivot 설정에 상관없이 실제 사각형의 중앙 좌표를 구함
            // corners[0]: Bottom-Left, corners[2]: Top-Right
            Vector3 worldCenter = (corners[0] + corners[2]) * 0.5f;

            // 3. 위치 변환: 타겟의 중앙 월드 좌표를 하이라이트 배경의 로컬 좌표계로 변환
            Vector3 localCenter = maskRect.InverseTransformPoint(worldCenter);

            // 4. 크기 계산: 월드 공간에서의 가로, 세로 길이 측정
            float worldWidth = Vector3.Distance(corners[0], corners[3]);
            float worldHeight = Vector3.Distance(corners[0], corners[1]);

            // 마스크 이미지의 월드 스케일로 나누어 로컬 공간에서의 크기로 보정
            Vector2 localSize = new Vector2(
                worldWidth / maskRect.lossyScale.x, 
                worldHeight / maskRect.lossyScale.y
            );
            
            localSize += sizeOffset;

            // 5. 쉐이더 파라미터 업데이트
            _holeMaterial.SetVector(CenterID, new Vector4(localCenter.x, localCenter.y, 0, 0));
            _holeMaterial.SetVector(SizeID, new Vector4(localSize.x, localSize.y, 0, 0));
            _holeMaterial.SetFloat(SoftnessID, softness);
            
            Debug.Log($"[TutorialHighlighter] Target: {target.name}, Pivot-Adjusted LocalCenter: {localCenter}, LocalSize: {localSize}");
        }

        public void Hide()
        {
            if (_maskImage != null) _maskImage.gameObject.SetActive(false);
        }
    }
}
