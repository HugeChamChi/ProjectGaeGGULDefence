using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HSD.UI.Setting
{
    /// <summary>
    /// 설정창 "프레임 ◀ 60 ▶" 줄. 좌우 버튼으로 후보 값을 고르면 Application.targetFrameRate에 즉시 적용한다.
    /// 저장(다음 판 유지)은 아직 없다 — 인게임 시작 시 InGameInstaller가 기본값으로 다시 맞춘다(2026-10-12 사용자 결정: 선택 동작까지만).
    /// </summary>
    public class UI_FrameRateSelector : MonoBehaviour
    {
        [SerializeField] private Button btn_Prev;
        [SerializeField] private Button btn_Next;
        [SerializeField] private TMP_Text txt_Value;
        [Tooltip("고를 수 있는 프레임 값(오름차순)")]
        [SerializeField] private int[] _options = { 30, 60 };
        [Tooltip("양 끝에서 버튼을 흐리게 할 투명도")]
        [SerializeField, Range(0f, 1f)] private float _disabledAlpha = 0.35f;

        private int _index;

        private void Awake()
        {
            if (btn_Prev != null) btn_Prev.onClick.AddListener(() => Step(-1));
            if (btn_Next != null) btn_Next.onClick.AddListener(() => Step(1));
        }

        private void OnEnable()
        {
            _index = ClosestIndex(Application.targetFrameRate);
            Refresh();
        }

        /// <summary>후보 값을 한 칸 옮기고 즉시 적용한다.</summary>
        public void Step(int direction)
        {
            if (_options == null || _options.Length == 0) return;
            int next = Mathf.Clamp(_index + direction, 0, _options.Length - 1);
            if (next == _index) return;
            _index = next;
            Application.targetFrameRate = _options[_index];
            Refresh();
        }

        private int ClosestIndex(int value)
        {
            if (_options == null || _options.Length == 0) return 0;
            int best = 0;
            for (int i = 1; i < _options.Length; i++)
                if (Mathf.Abs(_options[i] - value) < Mathf.Abs(_options[best] - value)) best = i;
            return best;
        }

        private void Refresh()
        {
            if (_options == null || _options.Length == 0) return;
            if (txt_Value != null) txt_Value.text = _options[_index].ToString();
            SetEnabled(btn_Prev, _index > 0);
            SetEnabled(btn_Next, _index < _options.Length - 1);
        }

        private void SetEnabled(Button button, bool enabled)
        {
            if (button == null) return;
            button.interactable = enabled;
            if (button.targetGraphic != null)
            {
                var color = button.targetGraphic.color;
                color.a = enabled ? 1f : _disabledAlpha;
                button.targetGraphic.color = color;
            }
        }
    }
}
