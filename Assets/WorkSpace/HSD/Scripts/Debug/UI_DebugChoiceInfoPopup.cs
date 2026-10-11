using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using System.Collections.Generic;
using System.Text;

namespace HSD.InGameDebug
{
    public interface IDebugChoiceInfoView
    {
        void UpdateStatsText(string text);
    }

    public class UI_DebugChoiceInfoPopup : MonoBehaviour, IDebugInfoPopup, IDebugChoiceInfoView
    {
        [Header("UI Elements")]
        [SerializeField] private Button btn_Close;
        [SerializeField] private TextMeshProUGUI txt_AccumulatedStats;
        
        private DebugChoiceInfoPresenter _presenter;
        [VContainer.Inject] private SelectionEffectReader _effects;

        public DebugTabType TabType => DebugTabType.LevelUp;

        private void Awake()
        {
            if (btn_Close != null)
            {
                btn_Close.onClick.AddListener(ClosePopup);
            }
            _presenter = new DebugChoiceInfoPresenter(this, () => _effects);
        }

        public void OpenPopup()
        {
            gameObject.SetActive(true);
            _presenter.OnOpen();
        }

        public void ClosePopup()
        {
            gameObject.SetActive(false);
        }

        public void UpdateStatsText(string text)
        {
            if (txt_AccumulatedStats != null)
                txt_AccumulatedStats.text = text;
        }

        private void OnDestroy()
        {
            if (btn_Close != null)
            {
                btn_Close.onClick.RemoveListener(ClosePopup);
            }
        }
    }

    public class DebugChoiceInfoPresenter
    {
        private readonly IDebugChoiceInfoView _view;
        private readonly System.Func<SelectionEffectReader> _reader;
        /// <summary>씬의 읽기 전용 상태를 표시한다. 제거된 카드 이력을 다시 합산하지 않는다.</summary>
        public DebugChoiceInfoPresenter(IDebugChoiceInfoView view, System.Func<SelectionEffectReader> reader)
        { _view = view; _reader = reader; }
        public void OnOpen()
        {
            var effects = _reader();
            if (effects == null) { _view.UpdateStatsText("Selection reader not available."); return; }
            var text = new StringBuilder("<color=yellow><b>[현재 선택지 효과]</b></color>\n");
            text.AppendLine($"공격 +{effects.AttackBonus:P0} / 공속 +{effects.AttackSpeedBonus:P0}");
            text.AppendLine($"치명 확률 {effects.CritChance:P0} / 피해 ×{effects.CritDamageMultiplier:0.##}");
            text.AppendLine($"식량 속도 +{effects.FoodSpeedBonus:P0} / 경험치 ×{effects.ExpGainMultiplier:0.##}");
            text.AppendLine($"투사체 크기 +{effects.ProjectileSizeBonus:P0} / 게이지 속도 +{effects.GaugeSpeedBonus:P0}");
            text.AppendLine($"소환 할인 {effects.SummonDiscountRate:P0} + {effects.SummonFixedDiscountAmount:0.##}");
            text.AppendLine($"추가 드론 {effects.ExtraCombatDroneCount} / 해킹 생산 +{effects.HackingProductionFlat}, +{effects.HackingProductionBonus:P0}");
            text.AppendLine($"해킹 자폭 {effects.HasBombHacking} / 스택 {effects.HackingStacksPerBomb}");
            _view.UpdateStatsText(text.ToString());
        }
    }
}
