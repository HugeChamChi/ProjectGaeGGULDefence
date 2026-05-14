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

        protected override void Awake()
        {
            base.Awake();
            
            if (btn_Restart != null) btn_Restart.onClick.AddListener(_presenter.OnRestartClicked);
            if (btn_GoToLobby != null) btn_GoToLobby.onClick.AddListener(_presenter.OnGoToLobbyClicked);
        }
    }
}
