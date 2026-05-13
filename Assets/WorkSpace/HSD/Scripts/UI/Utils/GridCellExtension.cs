using UnityEngine;

namespace GaeGGUL.Extension
{
    public static class GridCellExtension
    {
        // ── 핵심 계산 로직 (내부 재사용) ──────────────────────────

        private static float GetAttackMultiplier(this GridCell cell)
        {
            if (cell == null || cell.Model == null) return 1f;
            var model = cell.Model;
            return (model.NullifyDamageDebuff ? 1f : model.DamageModifier) * model.TotemAttackModifier;
        }

        private static float GetSpeedMultiplier(this GridCell cell)
        {
            if (cell == null || cell.Model == null) return 1f;
            return cell.Model.SpeedModifier;
        }

        // ── 최종 수치 계산 (Presenter에서 사용) ──────────────────

        public static int GetFinalAttack(this GridCell cell, float baseAtk)
        {
            return Mathf.RoundToInt(baseAtk * cell.GetAttackMultiplier());
        }

        public static float GetFinalCooldown(this GridCell cell, float baseCooldown)
        {
            return baseCooldown * cell.GetSpeedMultiplier();
        }

        // ── 보너스 텍스트 생성 (Presenter에서 사용) ────────────────

        /// <summary>
        /// 공격력 관련 보너스 수치 계산 (+10, -5 등)
        /// </summary>
        public static string GetAttackBonusText(this GridCell cell, float baseValue)
        {
            float mult = cell.GetAttackMultiplier();
            if (Mathf.Approximately(mult, 1f)) return "";

            int bonusValue = Mathf.RoundToInt(baseValue * (mult - 1f));
            if (bonusValue == 0) return "";

            return bonusValue > 0 ? $"+{bonusValue}" : bonusValue.ToString();
        }

        /// <summary>
        /// 스킬 쿨타임 관련 보너스 시간 계산 (+0.5s, -0.2s 등)
        /// </summary>
        public static string GetCooldownBonusText(this GridCell cell, float baseCooldown)
        {
            float mult = cell.GetSpeedMultiplier();
            if (Mathf.Approximately(mult, 1f)) return "";

            float bonusVal = baseCooldown * (mult - 1f);
            if (Mathf.Approximately(bonusVal, 0f)) return "";

            return bonusVal > 0 ? $"+{bonusVal:F1}s" : $"{bonusVal:F1}s";
        }

        /// <summary>
        /// 식량 생산량 관련 보너스 정보 반환
        /// </summary>
        public static string GetFoodBonusText(this GridCell cell)
        {
            if (cell != null && cell.Model != null && cell.Model.HasFoodBuff)
            {
                return "Buff";
            }
            return "";
        }
    }
}
