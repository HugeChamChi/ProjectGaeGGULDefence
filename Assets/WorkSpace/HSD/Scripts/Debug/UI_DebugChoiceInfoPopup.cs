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

        public DebugTabType TabType => DebugTabType.LevelUp;

        private void Awake()
        {
            if (btn_Close != null)
            {
                btn_Close.onClick.AddListener(ClosePopup);
            }
            _presenter = new DebugChoiceInfoPresenter(this);
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
        private IDebugChoiceInfoView _view;

        public DebugChoiceInfoPresenter(IDebugChoiceInfoView view)
        {
            _view = view;
        }

        public void OnOpen()
        {
            var levelUpManager = Object.FindObjectOfType<LevelUpManager>(true);
            if (levelUpManager == null || levelUpManager.LevelUpPool == null)
            {
                _view.UpdateStatsText("LevelUpManager not found.");
                return;
            }

            var chosenIds = levelUpManager.ChosenIds.ToList();
            var pool = levelUpManager.LevelUpPool;

            Dictionary<string, float> accumulatedStats = new Dictionary<string, float>();

            foreach (var id in chosenIds)
            {
                var data = pool.FirstOrDefault(d => d != null && d.chooseId == id);
                if (data != null)
                {
                    AddStat(accumulatedStats, data.primaryEffect.ToString(), data.primaryValue);
                    AddStat(accumulatedStats, data.secondaryEffect.ToString(), data.secondaryValue);
                    AddStat(accumulatedStats, data.specialEffect.ToString(), data.specialValue);
                }
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<color=yellow><b>[글로벌 적용 선택지 버프 누적 스탯]</b></color>\n");

            if (accumulatedStats.Count == 0)
            {
                sb.AppendLine("<color=#bbbbbb>적용된 글로벌 선택지가 없습니다.</color>");
            }
            else
            {
                foreach (var kvp in accumulatedStats)
                {
                    if (kvp.Key == "None" || kvp.Value == 0f) continue;
                    
                    string sign = kvp.Value > 0 ? "<color=#00ff00>+</color>" : "<color=#ff0000>-</color>";
                    sb.AppendLine($"<color=#eeeeee>• {kvp.Key,-20}</color> : {sign}<color=white>{Mathf.Abs(kvp.Value)}</color>");
                }
            }

            _view.UpdateStatsText(sb.ToString());
        }

        private void AddStat(Dictionary<string, float> dict, string effectName, float value)
        {
            if (string.IsNullOrEmpty(effectName) || effectName == "None" || value == 0f) return;

            // 칸마다 적용되는 줄별 스탯은 여기서 제외해야 한다.
            if (effectName.Contains("FrontRow") || effectName.Contains("BackRow")) return;

            if (dict.ContainsKey(effectName))
                dict[effectName] += value;
            else
                dict[effectName] = value;
        }
    }
}
