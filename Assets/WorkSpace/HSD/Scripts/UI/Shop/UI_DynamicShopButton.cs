using UnityEngine;
using UnityEngine.UI;
using GaeGGUL.Core;
using System;
using Cysharp.Threading.Tasks;
using TMPro;

public class UI_DynamicShopButton : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private TextMeshProUGUI _timeText;

    private DynamicShopData _data;
    private DateTime _endTime;
    private DynamicShopPopupController _popupManager;

    public void Initialize(DynamicShopData data, DynamicShopPopupController popupManager)
    {
        _data = data;
        _popupManager = popupManager;
        
        DateTime startTime = Player.Shop.Dynamic.GetStartTime(_data.ShopID);
        
        // 일, 시, 분, 초를 합산하여 종료 시간 계산
        _endTime = startTime.AddDays(_data.Day)
                            .AddHours(_data.Hour)
                            .AddMinutes(_data.Minute)
                            .AddSeconds(_data.Second);

        // 기간 만료 체크
        if (Server.GetServerTime() >= _endTime)
        {
            gameObject.SetActive(false);
            return;
        }

        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(OnClick);
        
        UpdateTimerAsync().Forget();
    }

    private async UniTask UpdateTimerAsync()
    {
        while (this != null && gameObject.activeSelf)
        {
            var remaining = _endTime - Server.GetServerTime();
            if (remaining.TotalSeconds <= 0)
            {
                gameObject.SetActive(false);
                break;
            }

            if (remaining.Days > 0)
                _timeText.text = $"{remaining.Days}일 {remaining.Hours}시간 {remaining.Minutes}분";
            else
                _timeText.text = $"{remaining.Hours}시간 {remaining.Minutes}분 {remaining.Seconds}초";

            await UniTask.Delay(TimeSpan.FromSeconds(1), cancellationToken: this.GetCancellationTokenOnDestroy());
        }
    }

    private void OnClick()
    {
        if (_popupManager != null)
        {
            _popupManager.OpenPanel(_data.ShopID);
        }
    }
}
