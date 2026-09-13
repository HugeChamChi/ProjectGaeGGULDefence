using System.Collections.Generic;
using UnityEditor;

/// <summary>사용자가 선택한 SO에 TD1008~1010의 조정 가능한 초기 구성을 만든다.</summary>
public static class TotemExpansionPresets
{
    /// <summary>안쪽 8칸 공격력 / 바깥 16칸 공격속도. 수치는 제작 시작용 예시다.</summary>
    [MenuItem("Assets/Totems/Set TD1008 Dual Ring")]
    public static void SetDualRing() => Apply(1008);

    /// <summary>인접 8칸 공격속도 감소 / 공격력 증가. 수치는 제작 시작용 예시다.</summary>
    [MenuItem("Assets/Totems/Set TD1009 Attack Tradeoff")]
    public static void SetTradeoff() => Apply(1009);

    /// <summary>식량 생성 기능. 전용 TotemFoodGenerator 프리팹과 함께 사용한다.</summary>
    [MenuItem("Assets/Totems/Set TD1010 Food Generator")]
    public static void SetFood() => Apply(1010);

    private static void Apply(int id)
    {
        foreach (var selected in Selection.objects)
        {
            if (selected is not TotemData data) continue;
            Undo.RecordObject(data, "Set totem expansion preset");
            Configure(data, id);
            EditorUtility.SetDirty(data);
        }
    }

    /// <summary>기능/범위만 초기화한다. ID, 주소, 기존 아트 참조는 변경하지 않는다.</summary>
    public static void Configure(TotemData data, int id)
    {
        if (data == null || id < 1008 || id > 1010) throw new System.ArgumentException("TD1008~1010 SO required.");
        data.UseSheetData = false;
        data.functions = new List<ITotemFunction>();
        data.effectRanges = new List<ITotemRange>();
        data.attackDisabledRanges = new List<ITotemRange>();
        if (id == 1008)
        {
            data.effectRanges.Add(new TotemSquareRingRange { MinRadius = 1, MaxRadius = 2 });
            data.functions.Add(new SquareRingBuffFunction
            {
                Range = new TotemSquareRingRange { MinRadius = 1, MaxRadius = 1 },
                Buff = new SimpleBuffFunction { kind = StatKind.AttackPercent, amount = 0.1f }
            });
            data.functions.Add(new SquareRingBuffFunction
            {
                Range = new TotemSquareRingRange { MinRadius = 2, MaxRadius = 2 },
                Buff = new SimpleBuffFunction { kind = StatKind.Speed, amount = 0.1f }
            });
        }
        else if (id == 1009)
        {
            data.effectRanges.Add(new TotemSquareRingRange());
            data.functions.Add(new SimpleBuffFunction { kind = StatKind.AttackPercent, amount = 0.5f });
            data.functions.Add(new SimpleBuffFunction { kind = StatKind.Speed, amount = -0.2f });
        }
        else
        {
            data.functions.Add(new FoodGeneratorFunction());
            data.isRotatable = false;
        }
    }
}
