using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace HSD.UI.Setting
{
    /// <summary>
    /// 인게임 전용 세팅 패널 (다시시작, 로비로 이동 추가)
    /// </summary>
    public class UI_SettingPanel_InGame : UI_SettingPanel_Base
    {
        [Header("InGame Specific")]
        [SerializeField] private Button btn_Restart;
        [SerializeField] private Button btn_GoToLobby;

        // 게임 속도는 TimeScaleService가 단일 소유 — 다른 정지(레벨업 등)와 겹쳐도 서로 풀어버리지 않는다.
        [VContainer.Inject] private TimeScaleService _timeScale;

        protected override void Awake()
        {
            base.Awake();
            
            if (btn_Restart != null) btn_Restart.onClick.AddListener(_presenter.OnRestartClicked);
            if (btn_GoToLobby != null) btn_GoToLobby.onClick.AddListener(_presenter.OnGoToLobbyClicked);
        }

        private void OnEnable()
        {
            if (_timeScale != null) _timeScale.Pause(this);
            else Debug.LogWarning("[UI_SettingPanel_InGame] TimeScaleService 미주입 — 일시정지 불가", this);
        }

        private void OnDisable()
        {
            _timeScale?.Release(this);
        }
    }
}
