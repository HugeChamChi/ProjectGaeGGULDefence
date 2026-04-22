using UnityEngine;
using TMPro;

public class PlayerProfilePopup : MonoBehaviour
{
    [SerializeField] private PlayerDataController _controller;

    [Header("TopUI")]
    [SerializeField] private TMP_Text _playerNameText;

    private bool _isDataBind = false;

    private void Apply(PlayerData data) => Init(data);

    public void Init(PlayerData data)
    {
        if (data == null)
        {
            Debug.Log("PlayerProfilePopup: data == null");
            return;
        }

        _playerNameText.text = $"{data.PlayerName}";
    }

    /// <summary>
    /// DB 데이터를 팝업에 실시간 연동 활성화/비활성화
    /// </summary>
    public void EnableDataBind(bool enable)
    {
        if (_isDataBind == enable) return;
        _isDataBind = enable;

        if (_isDataBind)
            _controller.OnPlayerProfilePopupUpdated += Apply;
        else
            _controller.OnPlayerProfilePopupUpdated -= Apply;
    }

    private void OnDisable()
    {
        if (_isDataBind)
        {
            _controller.OnPlayerProfilePopupUpdated -= Apply;
            _isDataBind = false;
        }
    }
}
