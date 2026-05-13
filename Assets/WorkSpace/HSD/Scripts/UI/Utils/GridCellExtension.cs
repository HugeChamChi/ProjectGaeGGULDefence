using UnityEngine;

namespace GaeGGUL.Extension
{
    public static class GridCellExtension
    {
        /// <summary>
        /// 공격력 관련 보너스 수치 계산 (+10, -5 등)
        /// </summary>
        public static string GetAttackBonusText(this GridCell cell, float baseValue)
        {
            if (cell == null || cell.Model == null) return "";

            var model = cell.Model;
            float cellAtkMult = (model.NullifyDamageDebuff ? 1f : model.DamageModifier) * model.TotemAttackModifier;

            if (Mathf.Approximately(cellAtkMult, 1f)) return "";

            int bonusValue = Mathf.RoundToInt(baseValue * (cellAtkMult - 1f));
            if (bonusValue == 0) return "";

            return bonusValue > 0 ? $"+{bonusValue}" : bonusValue.ToString();
        }

        /// <summary>
        /// 스킬 쿨타임 관련 보너스 시간 계산 (+0.5s, -0.2s 등)
        /// </summary>
        public static string GetCooldownBonusText(this GridCell cell, float baseCooldown)
        {
            if (cell == null || cell.Model == null) return "";

            var model = cell.Model;
            if (Mathf.Approximately(model.SpeedModifier, 1f)) return "";

            float bonusVal = baseCooldown * (model.SpeedModifier - 1f);
            if (Mathf.Approximately(bonusVal, 0f)) return "";

            return bonusVal > 0 ? $"+{bonusVal:F1}s" : $"{bonusVal:F1}s";
        }

        /// <summary>
        /// 식량 생산량 관련 보너스 정보 반환
        /// </summary>
        public static string GetFoodBonusText(this GridCell cell)
        {
            if (cell == null || cell.Model == null) return "";

            if (cell.Model.HasFoodBuff)
            {
                // TODO: 기획 수치가 확정되면 (예: +20%) 수치로 변경 가능
                return "Buff";
            }

            return "";
        }
    }
}
