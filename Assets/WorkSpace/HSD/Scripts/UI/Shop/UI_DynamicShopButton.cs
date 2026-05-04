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

    private DynamicShopTable _table;
    private DateTime _endTime;

    public void Initialize(DynamicShopTable table)
    {
        _table = table;
        DateTime startTime = Player.Shop.Dynamic.GetStartTime(_table.ShopID);
        _endTime = startTime.AddSeconds(_table.Duration);

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

            // 남은 시간 표시 (포맷은 필요에 따라 수정 가능)
            if (remaining.Days > 0)
                _timeText.text = $"{remaining.Days}일 {remaining.Hours}시간 {remaining.Minutes}분";
            else
                _timeText.text = $"{remaining.Hours}시간 {remaining.Minutes}분 {remaining.Seconds}초";

            await UniTask.Delay(TimeSpan.FromSeconds(1), cancellationToken: this.GetCancellationTokenOnDestroy());
        }
    }

    private void OnClick()
    {
        // 상점 패널 생성
        RM.Instantiate(_table.PanelPrefab);
    }
}
