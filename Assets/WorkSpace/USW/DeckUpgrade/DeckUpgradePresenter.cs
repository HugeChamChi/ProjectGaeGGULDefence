using System;

namespace GaeGGUL.DeckUpgrade
{
    /// <summary>Maps authored character data and preview state to display data and commands.</summary>
    public sealed class DeckUpgradePresenter : IDisposable
    {
        private readonly DeckUpgradeService _service;
        private readonly UnitData[] _units;
        private readonly ChieftainData _chief;

        /// <summary>Receives the scoped preview service and this screen's authored catalog.</summary>
        public DeckUpgradePresenter(DeckUpgradeService service, UnitData[] units, ChieftainData chief)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _units = units ?? throw new ArgumentNullException(nameof(units));
            _chief = chief ?? throw new ArgumentNullException(nameof(chief));
            _service.OnChanged += HandleChanged;
        }

        /// <summary>Raised when displayed preview state needs refreshing.</summary>
        public event Action OnChanged;

        /// <summary>Builds display state directly from data, never from TMP labels.</summary>
        public DeckUpgradeItemViewData GetItem(int index)
        {
            string id = GetId(index);
            var data = new DeckUpgradeItemViewData
            {
                Index = index, Name = index < 0 ? _chief.DisplayName : _units[index].unitName,
                Level = _service.GetLevel(id), Gold = _service.GetBalance(id), Currency = _service.GetCurrency(id), Cost = _service.GetCost(id),
                IsOwned = _service.IsOwned(id), IsBusy = _service.IsBusy || (_service.UsesAccountData && !Player.PlayerData.IsReady),
                UpgradeStatus = _service.UsesAccountData && Player.PlayerData.HasSaveConflict ? "재접속 필요" : _service.IsBusy ? "저장 중…" : "",
                ChiefBonus = _service.GetChiefBonus(GetId(-1))
            };
            if (data.IsChief) PopulateChief(data);
            else PopulateUnit(data, UnitBalanceRuntime.Resolve(_units[index]), id);
            return data;
        }

        /// <summary>Attempts the selected character's preview upgrade.</summary>
        public bool TryUpgrade(int index) => _service.TryUpgrade(GetId(index));
        public Cysharp.Threading.Tasks.UniTask<bool> TryUpgradeAsync(int index) => _service.TryUpgradeAsync(GetId(index));

        /// <inheritdoc />
        public void Dispose() => _service.OnChanged -= HandleChanged;

        private void HandleChanged() => OnChanged?.Invoke();

        private string GetId(int index)
        {
            if (index < -1 || index >= _units.Length) throw new ArgumentOutOfRangeException(nameof(index));
            if (!_service.UsesAccountData) return index < 0 ? DeckUpgradeProgress.ChiefId : _units[index].name;
            return index < 0 ? PlayerCharacterManager.ChiefId(_chief.ChieftainId) : PlayerCharacterManager.UnitId(_units[index].characterId.Get(Tier.Normal));
        }

        private void PopulateChief(DeckUpgradeItemViewData data)
        {
            data.SkillName = "액티브 스킬";
            data.Description = $"버튼을 누르면 전투 드론들을 중앙에 모아 보스에게 일제 사격합니다.\n\n기본 피해: 참가 드론 공격력 합 × {_chief.AlphanSkill.CurrentRallyAttackCoefficient:0.##}\n기본 재사용 대기시간: {_chief.AlphanSkill.CurrentCooldownSeconds:0.#}초";
            string next = data.IsMaxLevel ? "MAX" : "+" + ((data.ChiefBonus + DeckUpgradeProgress.ChiefBonusPerLevel) * 100).ToString("0") + "%";
            data.CurrentStat = $"전체 공격력   +{data.ChiefBonus * 100:0}%";
            data.CurrentSpeed = $"전체 능력치   +{data.ChiefBonus * 100:0}%";
            data.StatComparison = data.CurrentStat + $" → <color=#91E6A6>{next}</color>";
            data.SpeedComparison = data.CurrentSpeed + $" → <color=#91E6A6>{next}</color>";
        }

        private void PopulateUnit(DeckUpgradeItemViewData data, UnitData unit, string id)
        {
            data.Name = unit.unitName;
            data.SkillName = string.IsNullOrWhiteSpace(unit.SkillDisplayName) ? "유닛 특수 능력" : unit.SkillDisplayName;
            data.Description = unit.GetFormattedDescription(Tier.Normal);
            bool producer = unit.foodProduction.Get(Tier.Normal) > 0;
            float basis = producer ? unit.foodProduction.Get(Tier.Normal) : unit.atk.Get(Tier.Normal);
            float multiplier = _service.GetMultiplier(id) * (1 + data.ChiefBonus);
            float nextMultiplier = (_service.GetMultiplier(id) + DeckUpgradeProgress.CurrentMultiplierPerLevel) * (1 + data.ChiefBonus);
            string next = data.IsMaxLevel ? "MAX" : (basis * nextMultiplier).ToString("0.#");
            data.CurrentStat = (producer ? "식량 생산   " : "공격력   ") + $"{basis * multiplier:0.#}";
            data.StatComparison = data.CurrentStat + $"  ›  <color=#91E6A6>{next}</color>";
            float frequency = unit.attackSpeed.Get(Tier.Normal);
            float rate = frequency * multiplier;
            string nextRate = data.IsMaxLevel ? "MAX" : (frequency * nextMultiplier).ToString("0.0");
            data.CurrentSpeed = $"공격속도   {rate:0.0} 회/초";
            data.SpeedComparison = $"공격속도   {rate:0.0} → <color=#91E6A6>{nextRate}</color> <size=23>회/초</size>";
        }
    }
}
