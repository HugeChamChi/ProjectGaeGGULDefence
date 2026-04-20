using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ════════════════════════════════════════════════════════
// BossRewardUI — InGameSingleton 교체 + Manager 접근 통일
// ════════════════════════════════════════════════════════
public class BossRewardUI : InGameSingleton<BossRewardUI>
{
    [Header("자식 패널")]
    [SerializeField] private GameObject rewardPanel;

    [Header("토템 카드 버튼")]
    [SerializeField] private Button[]   btnTotems;
    [SerializeField] private TMP_Text[] btnTotemLabels;

    [Header("빈 셀 없을 때 대체 식량 보상")]
    [SerializeField] private float fallbackFood = 500f;

    private System.Action _onChoiceMade;

    protected override void Awake()
    {
        base.Awake();

        for (int i = 0; i < btnTotems.Length; i++)
        {
            int idx = i;
            btnTotems[i].onClick.AddListener(() => OnTotemSelected(idx));
        }

        if (rewardPanel != null) rewardPanel.SetActive(false);
    }

    public void Show(System.Action onChoiceMade)
    {
        _onChoiceMade = onChoiceMade;
        RefreshCardLabels();
        rewardPanel.SetActive(true);

        Manager.Timer.StopTimer();

        foreach (var cell in Manager.Grid.GetOccupiedCells())
            cell.OccupyingUnit?.OnRemoved();
    }

    private void RefreshCardLabels()
    {
        int prefabCount = Manager.Totem.TotemPrefabCount;

        for (int i = 0; i < btnTotems.Length; i++)
        {
            bool available = i < prefabCount;
            btnTotems[i].gameObject.SetActive(available);
            if (!available) continue;

            var data = Manager.Totem.GetTotemData(i);
            if (data != null && i < btnTotemLabels.Length && btnTotemLabels[i] != null)
                btnTotemLabels[i].text = $"{data.totemName}\n<size=70%>{data.description}</size>";
        }
    }

    private void OnTotemSelected(int index)
    {
        bool placed = Manager.Totem.SpawnTotemByIndex(index);

        if (!placed)
        {
            Debug.Log($"BossRewardUI: 빈 셀 없음 — 식량 {fallbackFood} 지급");
            Manager.Currency.AddCurrency(fallbackFood);
        }

        Hide();
    }

    private void Hide()
    {
        rewardPanel.SetActive(false);
        Manager.Timer.ResumeTimer();

        foreach (var cell in Manager.Grid.GetOccupiedCells())
        {
            if (cell.OccupyingUnit != null)
                cell.OccupyingUnit.OnPlaced(Manager.Currency, Manager.Boss.CurrentBoss);
        }

        _onChoiceMade?.Invoke();
        _onChoiceMade = null;
    }
}
