using UnityEngine;

namespace GaeGGUL.Extension
{
    public static class GridCellExtension
    {
        private static TotemBuffManager _totemBuffManager;
        private static TotemBuffManager TotemManager
        {
            get
            {
                if (_totemBuffManager == null)
                    _totemBuffManager = UnityEngine.Object.FindFirstObjectByType<TotemBuffManager>();
                return _totemBuffManager;
            }
        }

        private static LevelUpManager _levelUpManager;
        private static LevelUpManager LevelUpManager
        {
            get
            {
                if (_levelUpManager == null)
                    _levelUpManager = UnityEngine.Object.FindFirstObjectByType<LevelUpManager>();
                return _levelUpManager;
            }
        }

        private static float GetAttackMultiplier(this GridCell cell)
        {
            if (cell == null || cell.Model == null) return 1f;
            var model = cell.Model;
            float globalMult = TotemManager != null ? TotemManager.AttackMultiplier : 1f;
            float cellBonus = model.TotemCellAttackBonus;
            return (model.NullifyDamageDebuff ? 1f : model.DamageModifier) * model.TotemAttackModifier * (globalMult + cellBonus);
        }

        private static float GetSpeedMultiplier(this GridCell cell)
        {
            if (cell == null || cell.Model == null) return 1f;
            var model = cell.Model;
            float globalMult = TotemManager != null ? TotemManager.SpeedMultiplier : 1f;
            float cellBonusMult = Mathf.Max(0.1f, 1f - model.TotemCellSpeedBonus);
            return model.SpeedModifier * model.TotemSpeedModifier * globalMult * cellBonusMult;
        }

        private static float GetSkillCooldownMultiplier(this GridCell cell)
        {
            if (cell == null || cell.Model == null) return 1f;
            
            int row = cell.GridPosition.y;
            float rowSpeedMult = Mathf.Max(LevelUpManager != null ? LevelUpManager.GetRowSpeedMultiplier(row) : 1f, 0.01f);
            
            float gaugeSpeedMult = TotemManager != null ? TotemManager.GaugeSpeedMultiplier : 1f;
            float cellSpeedModifier = cell.Model.SpeedModifier;
            
            return (gaugeSpeedMult * cellSpeedModifier) / rowSpeedMult;
        }

        // ── 최종 수치 계산 (Presenter에서 사용) ──────────────────

        public static int GetFinalAttack(this GridCell cell, float baseAtk)
        {
            if (cell != null && cell.OccupyingUnit != null) return cell.OccupyingUnit.GetAttackDamage();
            return Mathf.RoundToInt(baseAtk * cell.GetAttackMultiplier());
        }

        public static float GetFinalCooldown(this GridCell cell, float baseCooldown)
        {
            if (cell != null && cell.OccupyingUnit != null) return cell.OccupyingUnit.GetCurrentSkillInterval();
            return baseCooldown * cell.GetSkillCooldownMultiplier();
        }

        // ── 보너스 텍스트 생성 (Presenter에서 사용) ────────────────

        /// <summary>
        /// 공격력 관련 보너스 수치 계산 (+10, -5 등)
        /// </summary>
        public static string GetAttackBonusText(this GridCell cell, float baseValue)
        {
            int diff;
            if (cell != null && cell.OccupyingUnit != null)
            {
                diff = cell.OccupyingUnit.GetAttackDamage() - Mathf.RoundToInt(baseValue);
            }
            else
            {
                float mult = cell.GetAttackMultiplier();
                if (Mathf.Approximately(mult, 1f)) return "";
                diff = Mathf.RoundToInt(baseValue * (mult - 1f));
            }

            if (diff == 0) return "";
            string color = diff > 0 ? "green" : "red";
            return $"<color={color}>{(diff > 0 ? $"+{diff}" : diff.ToString())}</color>";
        }

        /// <summary>
        /// 스킬 쿨타임 관련 보너스 시간 계산 (+0.5s, -0.2s 등)
        /// </summary>
        public static string GetCooldownBonusText(this GridCell cell, float baseCooldown)
        {
            float diff;
            if (cell != null && cell.OccupyingUnit != null)
            {
                diff = cell.OccupyingUnit.GetCurrentSkillInterval() - baseCooldown;
            }
            else
            {
                float mult = cell.GetSkillCooldownMultiplier();
                if (Mathf.Approximately(mult, 1f)) return "";
                diff = baseCooldown * (mult - 1f);
            }

            if (Mathf.Approximately(diff, 0f)) return "";
            string color = diff < 0 ? "green" : "red";
            return $"<color={color}>{(diff > 0 ? $"+{diff:F1}s" : $"{diff:F1}s")}</color>";
        }

        public static float GetFinalAttackSpeed(this GridCell cell, float baseSpeed)
        {
            if (cell != null && cell.OccupyingUnit != null) return cell.OccupyingUnit.GetCurrentAttackInterval();
            return baseSpeed * cell.GetSpeedMultiplier();
        }

        public static string GetAttackSpeedBonusText(this GridCell cell, float baseSpeed)
        {
            float diff;
            if (cell != null && cell.OccupyingUnit != null)
            {
                diff = cell.OccupyingUnit.GetCurrentAttackInterval() - baseSpeed;
            }
            else
            {
                float mult = cell.GetSpeedMultiplier();
                if (Mathf.Approximately(mult, 1f)) return "";
                diff = baseSpeed * (mult - 1f);
            }

            if (Mathf.Approximately(diff, 0f)) return "";
            string color = diff < 0 ? "green" : "red";
            return $"<color={color}>{(diff > 0 ? $"+{diff:F2}s" : $"{diff:F2}s")}</color>";
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
