using UnityEngine;
using UnityEngine.UI;

namespace HSD.InGameDebug
{
    public class UI_DebugTotemInfoPopup : MonoBehaviour, IDebugInfoPopup
    {
        [Header("UI Elements")]
        [SerializeField] private Button btn_Close;
        [SerializeField] private Transform gridContainer;
        [SerializeField] private UI_DebugTotemPopup detailPopup;
        
        private UI_DebugTotemGridView _totemGridView;

        public DebugTabType TabType => DebugTabType.Totem;

        private void Awake()
        {
            if (btn_Close != null)
            {
                btn_Close.onClick.AddListener(ClosePopup);
            }
        }

        public void OpenPopup()
        {
            gameObject.SetActive(true);

            if (_totemGridView == null)
            {
                Transform targetTransform = gridContainer != null ? gridContainer : transform;
                _totemGridView = targetTransform.GetComponent<UI_DebugTotemGridView>();
                if (_totemGridView == null)
                {
                    _totemGridView = targetTransform.gameObject.AddComponent<UI_DebugTotemGridView>();
                }
            }

            _totemGridView.gameObject.SetActive(true);
            _totemGridView.InitOrRefresh(detailPopup);
        }

        public void ClosePopup()
        {
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (btn_Close != null)
            {
                btn_Close.onClick.RemoveListener(ClosePopup);
            }
        }
    }
}
