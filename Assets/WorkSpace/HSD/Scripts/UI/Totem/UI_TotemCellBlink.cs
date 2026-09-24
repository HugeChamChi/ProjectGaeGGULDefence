using UnityEngine;

namespace GaeGGUL.UI.Totem
{
    /// <summary>
    /// 범위 그리드의 적용 범위 칸을 부드럽게 깜빡여 범위를 강조한다.
    /// 칸 전체(채움 + 테두리)를 CanvasGroup 알파로 1 ↔ minAlpha 사이에서 천천히 오간다.
    /// 게임이 멈춘 보상 화면에서도 돌도록 실제 시간(unscaled) 기준. 꺼지면 알파 1로 돌아간다.
    /// </summary>
    [DisallowMultipleComponent]
    public class UI_TotemCellBlink : MonoBehaviour
    {
        [Tooltip("한 번 어두워졌다 밝아지는 데 걸리는 시간(실제 초)")]
        [SerializeField, Min(0.1f)] private float _periodSeconds = 1.2f;
        [Tooltip("가장 어두울 때 알파 (1 = 깜빡임 없음)")]
        [SerializeField, Range(0f, 1f)] private float _minAlpha = 0.25f;

        private CanvasGroup _group;

        private void OnEnable()
        {
            if (_group == null) _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;
        }

        private void OnDisable()
        {
            if (_group != null) _group.alpha = 1f;
        }

        private void Update()
        {
            // 모든 범위 칸이 같은 박자로 깜빡이도록 공통 시계를 쓴다 (0 → 1 → 0 코사인)
            float dim = 0.5f - 0.5f * Mathf.Cos(Time.unscaledTime / _periodSeconds * Mathf.PI * 2f);
            _group.alpha = Mathf.Lerp(1f, _minAlpha, dim);
        }
    }
}
