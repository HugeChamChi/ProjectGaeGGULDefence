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
            float attackSpeed = data.attackSpeed.Get(Tier.Normal);
            Show(data, Tier.Normal, data.atk.Get(Tier.Normal),
                attackSpeed > 0f ? 1f / Mathf.Max(attackSpeed, 0.01f) : 0f, data.skillCooldown.Get(Tier.Normal));
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
            bool betan = _unit is Drone_Betan || data.prefabAddress == "DroneUnit_Betan";
            if (betan)
            {
                var effects = _unit?.DroneEffects;
                bool conversion = effects?.HasBombHacking == true;
                _view.UpdateSkillInfo(conversion ? "해킹 자폭" : data.skillData?.skillName ?? string.Empty,
                    conversion ? $"자폭 드론이 폭발 피해 대신 보스 적중 시 해킹 스택 {effects.HackingStacksPerBomb}개를 추가합니다. 기본 스킬·추가 자폭 모두 전환되며 수리 키트가 발동합니다."
                    : data.skillData?.description ?? data.GetFormattedDescription(tier),
                    cooldown > 0f ? $"{cooldown:F1}초" : string.Empty);
            }
            else if (data.Disigman != null)
            {
                int attacks = _unit is Disigman live ? live.RequiredAttacks
                    : Mathf.Max(1, data.Disigman.AttacksToCharge.Get(tier));
                float seconds = _unit is Disigman current ? current.SecondsRecovered
                    : Mathf.Max(0f, data.Disigman.SecondsRecovered.Get(tier));
                var damage = data.skillData?.hitEffects?.Find(effect => effect is AtkCoefficientDamage) as AtkCoefficientDamage;
                float coefficient = damage?.coefficient?.Get(tier) ?? 0f;
                _view.UpdateSkillInfo(data.skillData?.skillName ?? string.Empty,
                    $"본체 일반공격 {attacks}회마다 공격력 ×{coefficient:0.##}의 한 발을 발사하고 즉시 남은 시간을 {seconds:0.##}초 회복합니다. 드론을 생성하지 않습니다.",
                    $"공격 {attacks}회 충전");
            }
            else if (data.Hacking != null)
            {
                bool producer = _unit is Drone_Deltan || data.prefabAddress == "DroneUnit_Deltan";
                var hacking = data.Hacking;
                int attacks = _unit is Drone_Deltan delta ? delta.RequiredAttacks : hacking.AttacksToCharge.Get(tier);
                int production = _unit is Drone_Deltan liveDelta ? liveDelta.NextProduction : hacking.StacksProduced.Get(tier);
                float perStack = hacking.CoefficientPerStack.Get(tier) * (_unit is Drone_Gamman gamma ? gamma.StackDamageMultiplier : 1f);
                string description = producer
                    ? $"소유 드론 일반공격 {attacks}회마다 해킹 스킬을 사용합니다. 다음 생산 {production}스택. 보스 공유 상한 {hacking.Capacity}."
                    : $"기본 피해 계수 {hacking.BaseCoefficient.Get(tier):0.##} + 소비 스택당 {perStack:0.##}. 최대 {hacking.MaxStacksConsumed.Get(tier)}스택 소비. 부족하면 남은 만큼만 소비하며 스택0에서도 공격합니다.";
                _view.UpdateSkillInfo(producer ? "해킹" : "해킹 기폭", description,
                    producer ? $"공격 {attacks}회 충전" : $"{cooldown:F1}초");
            }
            else if (identityChanged || !_hasStats || _cooldown != cooldown)
                _view.UpdateSkillInfo(data.skillData != null ? data.skillData.skillName : string.Empty,
                    data.skillData != null ? data.skillData.description : data.GetFormattedDescription(tier),
                    cooldown > 0f ? $"{cooldown:F1}초" : string.Empty, data.DebuffBindings.Get(tier));
            if (!_hasStats || _attack != attack || _interval != interval)
                _view.UpdateStats(attack.ToString("0.##"), $"{interval:F2}초");
            _displayedData = data; _displayedTier = tier;
            _attack = attack; _interval = interval; _cooldown = cooldown; _hasStats = true;
        }
    }
}
