using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>프리뷰 시 확정한 카드 정의. 내부 설정은 공개하지 않으며 공유 SO 변경과 독립적이다.</summary>
public sealed class SelectionCardSnapshot
{
    private readonly SelectionCardDefinition _definition;
    /// <summary>저작 카드 식별자.</summary>
    public int CardId { get; }
    /// <summary>확정 시 아이콘 에셋 참조.</summary>
    public Sprite Icon { get; }
    /// <summary>확정 시 카드 이름.</summary>
    public string Name { get; }
    /// <summary>확정 시 설명 템플릿. 실제 문구 생성은 별도 formatter 책임이다.</summary>
    public string DescriptionTemplate { get; }
    /// <summary>동시 활성화가 금지된 기능 그룹의 읽기 전용 목록.</summary>
    public IReadOnlyList<string> ExclusiveGroups { get; }
    /// <summary>지속 효과 정의 수.</summary>
    public int EffectCount => _definition.Effects.Count;
    /// <summary>획득 명령 수. snapshot 자체는 실행하지 않는다.</summary>
    public int CommandCount => _definition.Commands.Count;

    internal SelectionCardSnapshot(LevelUpData card, Func<int, int, int> nextInt)
    {
        _definition = SelectionDefinitionReader.CopyDefinition(card);
        foreach (var effect in _definition.Effects)
        {
            if (!(effect is HackingProductionRollDefinition roll)) continue;
            int min = Mathf.RoundToInt(roll.MinimumRatio * 100f);
            int max = Mathf.RoundToInt(roll.MaximumRatio * 100f);
            int value = min;
            if (min != max)
            {
                if (nextInt == null) throw new ArgumentNullException(nameof(nextInt));
                value = nextInt(min, max + 1);
                if (value < min || value > max) throw new ArgumentException("Random source returned a value outside its requested range.");
            }
            roll.MinimumRatio = roll.MaximumRatio = value / 100f;
        }
        CardId = card.chooseId;
        Icon = card.icon;
        Name = card.chooseName;
        DescriptionTemplate = card.description;
        ExclusiveGroups = _definition.ExclusiveGroups.AsReadOnly();
    }
    /// <summary>명령 타입 존재만 조회한다. 가변 설정이나 실행 권한은 반환하지 않는다.</summary>
    public bool HasCommand<T>() where T : SelectionCommandDefinition
    {
        foreach (var command in _definition.Commands) if (command is T) return true;
        return false;
    }
    internal SelectionCardDefinition CopyDefinition() => SelectionDefinitionReader.CopyAndValidate(_definition);
}
