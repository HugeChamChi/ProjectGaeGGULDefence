using UnityEngine;

namespace GaeGGUL.UI.Unit
{
    /// <summary>SO와 현재 전투 보정을 표시한다. 표시용 난수 추첨이나 시트 조회는 하지 않는다.</summary>
    public class UI_UnitInfoPresenter
    {
        private readonly UI_UnitInfoPanel _view;
        private UnitBase _unit;
        private UnitData _displayedData;
        private Tier _displayedTier;
        private bool _wasPlaced, _trackingUnit, _hasStats;
        private float _attack, _interval, _cooldown;

        /// <summary>기존 정보 패널을 표시 대상으로 사용한다.</summary>
        public UI_UnitInfoPresenter(UI_UnitInfoPanel view) => _view = view;

        /// <summary>추적 유닛을 교체하고 현재 수치를 즉시 표시한다.</summary>
        public void SetUnitData(UnitBase unit)
        {
            Clear();
            _unit = unit;
            _trackingUnit = true;
            _wasPlaced = unit != null && unit.currentCell != null;
            Refresh();
        }

        /// <summary>기존 정적 SO 표시 호출을 지원하며 이전 유닛 추적은 해제한다.</summary>
        public void SetUnitData(UnitData data)
        {
            Clear();
            if (data == null) return;
            Show(data, Tier.Normal, data.atk.Get(Tier.Normal),
                1f / Mathf.Max(data.attackSpeed.Get(Tier.Normal), 0.01f), data.skillCooldown.Get(Tier.Normal));
        }

        /// <summary>열린 동안 현재 값을 갱신한다. 유닛 소멸/제거 시 false를 반환한다.</summary>
        public bool Refresh()
        {
            if (!_trackingUnit) return _displayedData != null;
            if (_unit == null || _unit.unitData == null || (_wasPlaced && _unit.currentCell == null)) return false;
            Show(_unit.unitData, _unit.currentTier, _unit.GetDisplayAttackDamage(),
                _unit.GetDisplayAttackInterval(), _unit.CanUseSkill ? _unit.GetCurrentSkillInterval() : 0f);
            return true;
        }

        /// <summary>닫힌 창이 이전 유닛을 계속 참조하지 않도록 정리한다.</summary>
        public void Clear()
        {
            _unit = null; _displayedData = null;
            _hasStats = _wasPlaced = _trackingUnit = false;
        }

        private void Show(UnitData data, Tier tier, float attack, float interval, float cooldown)
        {
            bool identityChanged = _displayedData != data || _displayedTier != tier;
            if (identityChanged) _view.UpdateBasicInfo(data.unitName, data.icon, tier);
            if (identityChanged || !_hasStats || _cooldown != cooldown)
                _view.UpdateSkillInfo(data.skillData != null ? data.skillData.skillName : string.Empty,
                    data.skillData != null ? data.skillData.description : data.description,
                    cooldown > 0f ? $"{cooldown:F1}초" : string.Empty);
            if (!_hasStats || _attack != attack || _interval != interval)
                _view.UpdateStats(attack.ToString("0.##"), $"{interval:F2}초");
            _displayedData = data; _displayedTier = tier;
            _attack = attack; _interval = interval; _cooldown = cooldown; _hasStats = true;
        }
    }
}