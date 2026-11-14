using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using GGD.BackendCharts;
using LitJson;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor-only bridge between the authoring workbook <c>Data/Balance/TotemBalance.xlsx</c> and the bundled TotemData assets
/// (user decision 2026-11-11: Excel is the source; bundled values and the BACKND chart are both made from it).
/// The same workbook is uploaded to DEV_TotemBalance, then LIVE_TotemBalance. Never opened by a player build.
/// </summary>
public static class TotemBalanceWorkbook
{
    public const string SourcePath = "Data/Balance/TotemBalance.xlsx";
    public const string SheetName = "TotemBalance";
    // The TD totems moved to Legacy on 2026-11-14 (level totems replaced them); the chart still requires their 36 keys.
    private const string AssetFolder = "Assets/WorkSpace/USW/Data/TotemData/Legacy/Playable";

    [MenuItem("Tools/Balance/TotemBalance.xlsx 검사 (업로드 전)")]
    public static void CheckMenu() => Debug.Log(Check(SourcePath));

    [MenuItem("Tools/Balance/TotemBalance.xlsx 값을 앱 내장 값에 반영")]
    public static void ImportMenu() => Debug.Log(Import(SourcePath));

    /// <summary>Reads and validates the workbook with the rules the game applies to the chart.</summary>
    public static Dictionary<string, float[]> Read(string path)
    {
        var table = BalanceXlsxTable.Read(File.ReadAllBytes(path), SheetName, 1, "Key", "Value");
        var rows = new JsonData(); rows.SetJsonType(JsonType.Array);
        foreach (var row in table.Rows)
        {
            var item = new JsonData();
            item["Key"] = row.Get("Key") ?? "";
            item["Value"] = row.Get("Value") ?? "";
            rows.Add(item);
        }
        return TotemBalance.Parse(rows);
    }

    public static string Check(string path)
    {
        var workbook = Read(path);
        var bundled = Bundled();
        var lines = new List<string> { "TotemBalance.xlsx 형식 통과: 키 " + workbook.Count + "개" };
        foreach (var pair in workbook)
        {
            string excel = Text(pair.Value);
            if (!bundled.TryGetValue(pair.Key, out string app)) lines.Add("앱 내장 값 없음: " + pair.Key);
            else if (app != excel) lines.Add("다름 " + pair.Key + ": 엑셀 " + excel + " / 앱 내장 " + app);
        }
        if (lines.Count == 1) lines.Add("앱 내장 값과 전부 같음");
        return string.Join("\n", lines);
    }

    /// <summary>Bundled value of every chart key, in chart text form. Enabled has no bundled field and reads 1.</summary>
    public static Dictionary<string, string> Bundled()
    {
        var result = new Dictionary<string, string>();
        foreach (var data in Assets().Values)
            foreach (string name in TotemBalance.Names(data.totemId))
            {
                float[] values = data.BundledList(name) ?? new[] { data.BundledNumber(name) };
                if (values.Any(float.IsNaN))
                    throw new InvalidOperationException(data.name + ": 차트 키 " + name + "에 해당하는 값이 에셋에 없다.");
                result.Add(TotemBalance.Key(data.totemId, name), Text(values));
            }
        foreach (string key in TotemBalance.Rules.Keys)
            if (!result.ContainsKey(key)) throw new InvalidOperationException("차트 키 " + key + ": 앱에 TotemData 에셋이 없다.");
        return result;
    }

    /// <summary>Writes workbook numbers into the bundled TotemData assets after the whole workbook passed validation.</summary>
    public static string Import(string path)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode before importing totem balance.");
        var workbook = Read(path);
        var notes = new List<string>();
        foreach (var data in Assets().Values)
        {
            Undo.RecordObject(data, "Import TotemBalance.xlsx");
            foreach (string name in TotemBalance.Names(data.totemId))
            {
                float[] values = workbook[TotemBalance.Key(data.totemId, name)];
                if (name == TotemBalance.Enabled)
                {
                    if (values[0] == 0f) notes.Add("TD" + data.totemId + " 끔");
                    continue;
                }
                Write(data, name, values);
            }
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
        }
        return "TotemBalance.xlsx 값을 앱 내장 값에 반영함: 키 " + workbook.Count + "개"
            + (notes.Count > 0 ? "\n주의: 켜고 끄기는 차트에서만 적용된다(튜토리얼과 에디터 직접 실행은 전부 켜짐). " + string.Join(", ", notes) : "");
    }

    private static void Write(TotemData data, string name, float[] values)
    {
        float value = values[0];
        switch (name)
        {
            case TotemBalance.Chance: data.BonusProjectile.Chance = value; break;
            case TotemBalance.AttackCoefficient: data.BonusProjectile.AttackCoefficient = value; break;
            case TotemBalance.StageSeconds: for (int i = 0; i < values.Length; i++) data.GrowthStages[i].RequiredSeconds = values[i]; break;
            case TotemBalance.StageAttackBonus: for (int i = 0; i < values.Length; i++) data.GrowthStages[i].AttackBonus = values[i]; break;
            case TotemBalance.HarvestSeconds: data.GrowthHarvestSeconds = value; break;
            case TotemBalance.HarvestFood: data.GrowthHarvestFood = value; break;
            case TotemBalance.FireIntervalSeconds: SetPrivate(data, "_debuffFireInterval", value); break;
            case TotemBalance.ImpactDamage: SetPrivate(data, "_debuffImpactDamage", value); break;
            case TotemBalance.SpawnIntervalSeconds: data.WildcardSpawn.IntervalSeconds = value; break;
            case TotemBalance.ShadowDelaySeconds: data.ShadowAttack.DelaySeconds = value; break;
            case TotemBalance.FoodIntervalSeconds: data.GetFoodGenerator().interval = value; break;
            case TotemBalance.Food: data.GetFoodGenerator().amount = value; break;
            case TotemBalance.AttackBonus: SetSimple(data, StatKind.AttackPercent, value); break;
            case TotemBalance.SpeedBonus: SetSimple(data, StatKind.Speed, value); break;
            case TotemBalance.CritChanceBonus: SetSimple(data, StatKind.CritChance, value); break;
            case TotemBalance.CritDamageBonus: SetSimple(data, StatKind.CritDamage, value); break;
            case TotemBalance.WindowSeconds:
                foreach (var function in Functions(data))
                    if (function is ConditionalBuffFunction { condition: BossEncounterWindowCondition window }) window.seconds = value;
                break;
            default: throw new InvalidOperationException("No bundled field for " + name);
        }
    }

    // The chart holds one number per totem and stat kind, so every simple buff of that kind in the asset gets it.
    private static void SetSimple(TotemData data, StatKind kind, float value)
    {
        foreach (var function in Functions(data))
        {
            var simple = function as SimpleBuffFunction ?? (function as ConditionalBuffFunction)?.buff;
            if (simple != null && simple.kind == kind) simple.amount = value;
        }
    }

    private static IEnumerable<ITotemFunction> Functions(TotemData data)
        => data.functions.Concat(data.EffectGroups.Where(group => group != null).SelectMany(group => group.Functions));

    private static void SetPrivate(TotemData data, string field, float value)
    {
        var serialized = new SerializedObject(data);
        serialized.FindProperty(field).doubleValue = (double)(decimal)value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>Playable TotemData assets that the chart has keys for, by totem ID.</summary>
    public static Dictionary<int, TotemData> Assets()
    {
        var result = new Dictionary<int, TotemData>();
        foreach (string guid in AssetDatabase.FindAssets("t:TotemData", new[] { AssetFolder }))
        {
            var data = AssetDatabase.LoadAssetAtPath<TotemData>(AssetDatabase.GUIDToAssetPath(guid));
            if (data != null && TotemBalance.Names(data.totemId).Any()) result.Add(data.totemId, data);
        }
        return result;
    }

    private static string Text(float[] values) => string.Join("|", values.Select(N));
    public static string N(float value) => ((double)(decimal)value).ToString("0.######", CultureInfo.InvariantCulture);
}
