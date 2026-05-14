using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;

namespace HSD.UI.Utils
{
    /// <summary>
    /// 좌우 화살표를 통해 상태를 순환시키는 토글 버튼 (진동, 데미지 플로터 등)
    /// </summary>
    public class UI_StateToggleBtn : MonoBehaviour
    {
        [SerializeField] private Button btn_Prev;
        [SerializeField] private Button btn_Next;
        [SerializeField] private TMP_Text txt_State;

        [Header("Visual Indicators (Optional)")]
        [SerializeField] private List<GameObject> stateIndicators;

        private List<string> _stateNames = new List<string>();
        private int _currentIndex = 0;
        private Action<int> _onValueChanged;

        public int CurrentIndex => _currentIndex;

        public void Init(List<string> stateNames, int startIndex, Action<int> onValueChanged)
        {
            _stateNames = stateNames;
            _currentIndex = startIndex;
            _onValueChanged = onValueChanged;

            if (btn_Prev != null)
            {
                btn_Prev.onClick.RemoveAllListeners();
                btn_Prev.onClick.AddListener(() => ChangeIndex(-1));
            }

            if (btn_Next != null)
            {
                btn_Next.onClick.RemoveAllListeners();
                btn_Next.onClick.AddListener(() => ChangeIndex(1));
            }

            RefreshUI();
        }

        public void SetState(int index)
        {
            if (_stateNames == null || _stateNames.Count == 0) return;
            _currentIndex = Mathf.Clamp(index, 0, _stateNames.Count - 1);
            RefreshUI();
        }

        private void ChangeIndex(int direction)
        {
            if (_stateNames == null || _stateNames.Count == 0) return;

            _currentIndex += direction;
            if (_currentIndex < 0) _currentIndex = _stateNames.Count - 1;
            else if (_currentIndex >= _stateNames.Count) _currentIndex = 0;

            _onValueChanged?.Invoke(_currentIndex);
            RefreshUI();
        }

        private void RefreshUI()
        {
            if (txt_State != null && _stateNames != null && _stateNames.Count > _currentIndex)
                txt_State.text = _stateNames[_currentIndex];

            // 인디케이터 처리 (예: 왼쪽/오른쪽 불 들어오기 등)
            if (stateIndicators != null)
            {
                for (int i = 0; i < stateIndicators.Count; i++)
                {
                    if (stateIndicators[i] != null)
                        stateIndicators[i].SetActive(i == _currentIndex);
                }
            }
        }
    }
}
