using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class DynamicShopPopupController : MonoBehaviour
{
    [SerializeField] private Transform _uiRoot; // 패널이 생성될 Canvas 또는 UI 루트
    private Dictionary<string, UI_Base> _panelDict = new();
    private Queue<string> _popupQueueIDs = new Queue<string>();
    private bool _isRoutineRunning = false;

    private void Start()
    {
        // 상점 활성화 이벤트 구독
        Player.Shop.Dynamic.OnShopActivated += OnShopActivated;
    }

    private void OnDestroy()
    {
        if (Player.Shop.Dynamic != null)
            Player.Shop.Dynamic.OnShopActivated -= OnShopActivated;
    }

    private void OnEnable()
    {
        if (!_isRoutineRunning && _popupQueueIDs.Count > 0)
        {
            DynamicPopupRoutine().Forget();
        }
    }

    private async UniTaskVoid DynamicPopupRoutine()
    {
        if (_isRoutineRunning) return;
        _isRoutineRunning = true;

        var ct = this.GetCancellationTokenOnDestroy();

        while (_popupQueueIDs.Count > 0)
        {
            string shopID = _popupQueueIDs.Dequeue();
            
            // SuppressCancellationThrow를 사용하여 예외 비용 없이 취소 처리
            bool isCanceled = await ShowPopup(shopID, ct).SuppressCancellationThrow();
            if (isCanceled) break;
        }

        _isRoutineRunning = false;
    }

    private async UniTask ShowPopup(string shopID, System.Threading.CancellationToken ct)
    {
        UI_Base panel = GetOrOpenPanel(shopID);
        if (panel == null) return;

        var tcs = new UniTaskCompletionSource();
        System.Action onClosedAction = null;
        onClosedAction = () =>
        {
            panel.OnClosed -= onClosedAction;
            tcs.TrySetResult();
        };
        panel.OnClosed += onClosedAction;

        panel.Open();

        await tcs.Task.AttachExternalCancellation(ct);
    }

    private void OnShopActivated(string shopID, bool isFirstTime)
    {
        // 처음 활성화되는 경우(이벤트 발생 직후)에만 자동으로 패널을 띄움
        if (isFirstTime)
        {
            OpenPanel(shopID);
        }
    }

    public void OpenPanel(string shopID)
    {
        if (_popupQueueIDs.Contains(shopID)) return;

        // 이미 열려있는 경우 중복해서 띄우지 않음
        if (_panelDict.TryGetValue(shopID, out var panel) && panel != null && panel.gameObject.activeSelf)
        {
            return;
        }

        _popupQueueIDs.Enqueue(shopID);
        
        // 현재 활성화 상태이고 루틴이 실행 중이 아니라면 루틴 시작
        if (gameObject.activeInHierarchy && !_isRoutineRunning)
        {
            DynamicPopupRoutine().Forget();
        }
    }

    private UI_Base GetOrOpenPanel(string shopID)
    {
        if (_panelDict.TryGetValue(shopID, out var existingPanel) && existingPanel != null)
        {
            return existingPanel;
        }

        var data = Table.Shop.Dynamic.Get(shopID);

        if (data != null && data.PanelPrefab != null)
        {
            // 지정된 _uiRoot 하위에 패널 생성
            GameObject panelObj = RM.Instantiate(data.PanelPrefab, _uiRoot);
            panelObj.SetActive(false);

            // RectTransform을 초기화하여 부모에 꽉 차도록 설정
            if (panelObj.TryGetComponent<RectTransform>(out var rect))
            {
                SetFull(rect);

                if (panelObj.TryGetComponent<UI_Base>(out var uiBase))
                {
                    _panelDict[shopID] = uiBase;
                    return uiBase;
                }
            }
        }
        return null;
    }

    private void SetFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }
}
