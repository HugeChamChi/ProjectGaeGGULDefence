using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace HSD.UI.Setting
{
    /// <summary>
    /// 인게임 전용 세팅 패널 (다시시작, 로비로 이동 추가).
    /// 열어도 게임을 멈추지 않는다 (사용자 10-13 "설정창 확인할 때 일시정지 하지 마").
    /// </summary>
    public class UI_SettingPanel_InGame : UI_SettingPanel_Base
    {
        [Header("InGame Specific")]
        [SerializeField] private bool _allowOpening = true;
        [SerializeField] private Button btn_Restart;
        [SerializeField] private Button btn_GoToLobby;

        /// <summary>Whether this scene permits opening settings, including direct UI calls.</summary>
        public bool AllowOpening => _allowOpening;

        /// <summary>Opens settings only when permitted by the scene.</summary>
        public override void Open()
        {
            if (_allowOpening) base.Open();
        }

        /// <summary>Blocks every asynchronous settings entry point during the tutorial.</summary>
        public override UniTask OpenAsync() => _allowOpening ? base.OpenAsync() : UniTask.CompletedTask;

        protected override void Awake()
        {
            base.Awake();

            if (btn_Restart != null) btn_Restart.onClick.AddListener(_presenter.OnRestartClicked);
            if (btn_GoToLobby != null) btn_GoToLobby.onClick.AddListener(_presenter.OnGoToLobbyClicked);
        }
    }
}
