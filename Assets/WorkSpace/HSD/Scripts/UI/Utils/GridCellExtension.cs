using UnityEngine;

namespace GaeGGUL.Extension
{
    public sealed class GridCellExtension
    {
        private readonly TotemBuffManager _totemManager;
        private readonly ISelectionCombatReader _selectionCombat;
        private readonly GridManager _grid;
        private const int DefaultRows = 4;

        /// <summary>씬의 버프 및 레벨업 서비스를 주입받는다. 기존 계산식은 유지한다.</summary>
        public GridCellExtension(TotemBuffManager totemManager, ISelectionCombatReader selectionCombat, GridManager grid = null)
        {
            _totemManager = totemManager;
            _selectionCombat = selectionCombat;
            _grid = grid;
        }

        private float GetAttackMultiplier(GridCell cell)
        {
            if (cell == null || cell.Model == null) return 1f;
            var model = cell.Model;
            float globalMult = _totemManager != null ? _totemManager.AttackMultiplier : 1f;
            float cellBonus = model.GetTotemCellBonus(StatKind.AttackPercent);
            return (model.NullifyDamageDebuff ? 1f : model.DamageModifier) * model.TotemAttackModifier * (globalMult + cellBonus);
        }

        private float GetSpeedMultiplier(GridCell cell)
        {
            if (cell == null || cell.Model == null) return 1f;
            var model = cell.Model;
            float globalMult = _totemManager != null ? _totemManager.SpeedMultiplier : 1f;
            float cellBonusMult = Mathf.Max(0.1f, 1f - model.GetTotemCellBonus(StatKind.Speed));
            return model.SpeedModifier * model.TotemSpeedModifier * globalMult * cellBonusMult;
        }

        private float GetSkillCooldownMultiplier(GridCell cell)
        {
            if (cell == null || cell.Model == null) return 1f;
            
            int row = cell.GridPosition.y;
            float rowSpeedMult = Mathf.Max(_selectionCombat != null ? _selectionCombat.GetRowSpeedMultiplier(row, _grid != null ? _grid.Rows : DefaultRows) : 1f, 0.01f);
            
            float gaugeSpeedMult = _totemManager != null ? _totemManager.GaugeSpeedMultiplier : 1f;
            float cellSpeedModifier = cell.Model.SpeedModifier;
            
            return (gaugeSpeedMult * cellSpeedModifier) / rowSpeedMult;
        }

        // ── 최종 수치 계산 (Presenter에서 사용) ──────────────────

        public int GetFinalAttack(GridCell cell, float baseAtk)
        {
            if (cell != null && cell.OccupyingUnit != null) return cell.OccupyingUnit.GetNonCriticalAttackDamage();
            return Mathf.RoundToInt(baseAtk * GetAttackMultiplier(cell));
        }

        public float GetFinalCooldown(GridCell cell, float baseCooldown)
        {
            if (cell != null && cell.OccupyingUnit != null) return cell.OccupyingUnit.GetCurrentSkillInterval();
            return baseCooldown * GetSkillCooldownMultiplier(cell);
        }

        // ── 보너스 텍스트 생성 (Presenter에서 사용) ────────────────

        /// <summary>
        /// 공격력 관련 보너스 수치 계산 (+10, -5 등)
        /// </summary>
        public string GetAttackBonusText(GridCell cell, float baseValue)
        {
            int diff;
            if (cell != null && cell.OccupyingUnit != null)
            {
                diff = cell.OccupyingUnit.GetAttackDamage() - Mathf.RoundToInt(baseValue);
            }
            else
            {
                float mult = GetAttackMultiplier(cell);
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
        public string GetCooldownBonusText(GridCell cell, float baseCooldown)
        {
            float diff;
            if (cell != null && cell.OccupyingUnit != null)
            {
                diff = cell.OccupyingUnit.GetCurrentSkillInterval() - baseCooldown;
            }
            else
            {
                float mult = GetSkillCooldownMultiplier(cell);
                if (Mathf.Approximately(mult, 1f)) return "";
                diff = baseCooldown * (mult - 1f);
            }

            if (Mathf.Approximately(diff, 0f)) return "";
            string color = diff < 0 ? "green" : "red";
            return $"<color={color}>{(diff > 0 ? $"+{diff:F1}s" : $"{diff:F1}s")}</color>";
        }

        public float GetFinalAttackSpeed(GridCell cell, float baseSpeed)
        {
            if (cell != null && cell.OccupyingUnit != null) return cell.OccupyingUnit.GetCurrentAttackInterval();
            return baseSpeed * GetSpeedMultiplier(cell);
        }

        public string GetAttackSpeedBonusText(GridCell cell, float baseSpeed)
        {
            float diff;
            if (cell != null && cell.OccupyingUnit != null)
            {
                diff = cell.OccupyingUnit.GetCurrentAttackInterval() - baseSpeed;
            }
            else
            {
                float mult = GetSpeedMultiplier(cell);
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
        public string GetFoodBonusText(GridCell cell)
        {
            if (cell != null && cell.Model != null && cell.Model.HasFoodBuff)
            {
                return "Buff";
            }
            return "";
        }
    }
}
