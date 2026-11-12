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
/// Editor-only bridge between the authoring workbook <c>Data/Balance/SkillBalance.xlsx</c> and the bundled skill values
/// (user decision 2026-11-11: Excel is the source; bundled values and the BACKND chart are both made from it).
/// The same workbook is uploaded to DEV_SkillBalance, then LIVE_SkillBalance. Never opened by a player build.
/// </summary>
public static class SkillBalanceWorkbook
{
    public const string SourcePath = "Data/Balance/SkillBalance.xlsx";
    public const string SheetName = "SkillBalance";
    private const string Folder = "Assets/WorkSpace/USW/Data/DroneUnits/";
    private static readonly string[] Columns = { "Value", "Normal", "Rare", "Epic", "Legend" };
    private static readonly Tier[] Tiers = { Tier.Normal, Tier.Rare, Tier.Epic, Tier.Legend };

    [MenuItem("Tools/Balance/SkillBalance.xlsx 검사 (업로드 전)")]
    public static void CheckMenu() => Debug.Log(Check(SourcePath));

    [MenuItem("Tools/Balance/SkillBalance.xlsx 값을 앱 내장 값에 반영")]
    public static void ImportMenu() => Debug.Log(Import(SourcePath));

    /// <summary>Reads and validates the workbook with the rules the game applies to the chart.</summary>
    public static Dictionary<string, float[]> Read(string path)
    {
        var table = BalanceXlsxTable.Read(File.ReadAllBytes(path), SheetName, 1, "Key", "Value", "Normal", "Rare", "Epic", "Legend");
        var rows = new JsonData(); rows.SetJsonType(JsonType.Array);
        foreach (var row in table.Rows)
        {
            var item = new JsonData();
            item["Key"] = row.Get("Key") ?? "";
            foreach (var column in Columns) item[column] = row.Get(column) ?? "";
            rows.Add(item);
        }
        return SkillBalance.Parse(rows);
    }

    public static string Check(string path)
    {
        var workbook = Read(path);
        var bundled = Bundled();
        var lines = new List<string> { "SkillBalance.xlsx 형식 통과: 키 " + workbook.Count + "개" };
        foreach (var pair in workbook)
        {
            string excel = Text(pair.Value);
            if (!bundled.TryGetValue(pair.Key, out var app)) lines.Add("앱 내장 값 없음: " + pair.Key);
            else if (Text(app) != excel) lines.Add("다름 " + pair.Key + ": 엑셀 " + excel + " / 앱 내장 " + Text(app));
        }
        if (lines.Count == 1) lines.Add("앱 내장 값과 전부 같음");
        return string.Join("\n", lines);
    }

    /// <summary>Writes workbook values into the bundled assets after the whole workbook passed validation.</summary>
    public static string Import(string path)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode before importing skill balance.");
        var workbook = Read(path);
        var hacking = Load<DroneHackingData>("DroneHacking.asset");
        var disigman = Load<DisigmanData>("Disigman.asset");
        var alphan = Load<AlphanSkillData>("AlphanActiveSkillData.asset");
        var basic = Load<SkillData>("DisigmanBasicAttack.asset");
        var skill = Load<SkillData>("DisigmanSkill.asset");
        Undo.RecordObjects(new UnityEngine.Object[] { hacking, disigman, alphan, basic, skill }, "Import SkillBalance.xlsx");

        hacking.Capacity = Mathf.RoundToInt(workbook[SkillBalance.HackingCapacity][0]);
        Write(hacking.AttacksToCharge, workbook[SkillBalance.HackingAttacksToCharge]);
        Write(hacking.StacksProduced, workbook[SkillBalance.HackingStacksProduced]);
        Write(hacking.MaxStacksConsumed, workbook[SkillBalance.HackingMaxStacksConsumed]);
        Write(hacking.BaseCoefficient, workbook[SkillBalance.HackingBaseCoefficient]);
        Write(hacking.CoefficientPerStack, workbook[SkillBalance.HackingCoefficientPerStack]);
        Write(disigman.AttacksToCharge, workbook[SkillBalance.DisigmanAttacksToCharge]);
        Write(disigman.SecondsRecovered, workbook[SkillBalance.DisigmanSecondsRecovered]);
        Write(Coefficient(basic, SkillBalance.DisigmanBasicCoefficient), workbook[SkillBalance.DisigmanBasicCoefficient]);
        Write(Coefficient(skill, SkillBalance.DisigmanSkillCoefficient), workbook[SkillBalance.DisigmanSkillCoefficient]);
        alphan.CooldownSeconds = workbook[SkillBalance.AlphanCooldownSeconds][0];
        alphan.RallyAttackCoefficient = workbook[SkillBalance.AlphanRallyAttackCoefficient][0];
        foreach (var asset in new UnityEngine.Object[] { hacking, disigman, alphan, basic, skill }) { EditorUtility.SetDirty(asset); AssetDatabase.SaveAssetIfDirty(asset); }
        return "SkillBalance.xlsx 값을 앱 내장 값에 반영함: 키 " + workbook.Count + "개";
    }

    /// <summary>Bundled value of every key: one number, or four numbers in tier order.</summary>
    public static Dictionary<string, float[]> Bundled()
    {
        var hacking = Load<DroneHackingData>("DroneHacking.asset");
        var disigman = Load<DisigmanData>("Disigman.asset");
        var alphan = Load<AlphanSkillData>("AlphanActiveSkillData.asset");
        var basic = Coefficient(Load<SkillData>("DisigmanBasicAttack.asset"), SkillBalance.DisigmanBasicCoefficient);
        var skill = Coefficient(Load<SkillData>("DisigmanSkill.asset"), SkillBalance.DisigmanSkillCoefficient);
        return new Dictionary<string, float[]>
        {
            { SkillBalance.HackingCapacity, new float[] { hacking.Capacity } },
            { SkillBalance.HackingAttacksToCharge, Tiers.Select(t => (float)hacking.AttacksToCharge.Get(t)).ToArray() },
            { SkillBalance.HackingStacksProduced, Tiers.Select(t => (float)hacking.StacksProduced.Get(t)).ToArray() },
            { SkillBalance.HackingMaxStacksConsumed, Tiers.Select(t => (float)hacking.MaxStacksConsumed.Get(t)).ToArray() },
            { SkillBalance.HackingBaseCoefficient, Tiers.Select(t => hacking.BaseCoefficient.Get(t)).ToArray() },
            { SkillBalance.HackingCoefficientPerStack, Tiers.Select(t => hacking.CoefficientPerStack.Get(t)).ToArray() },
            { SkillBalance.DisigmanAttacksToCharge, Tiers.Select(t => (float)disigman.AttacksToCharge.Get(t)).ToArray() },
            { SkillBalance.DisigmanSecondsRecovered, Tiers.Select(t => disigman.SecondsRecovered.Get(t)).ToArray() },
            { SkillBalance.DisigmanBasicCoefficient, Tiers.Select(basic.Bundled).ToArray() },
            { SkillBalance.DisigmanSkillCoefficient, Tiers.Select(skill.Bundled).ToArray() },
            { SkillBalance.AlphanCooldownSeconds, new[] { alphan.CooldownSeconds } },
            { SkillBalance.AlphanRallyAttackCoefficient, new[] { alphan.RallyAttackCoefficient } },
        };
    }

    /// <summary>The chart-linked value part that carries <paramref name="key"/> inside a skill asset's hit effects.</summary>
    private static SkillBalanceFloat Coefficient(SkillData data, string key)
    {
        foreach (var effect in data.hitEffects)
            if (effect is AtkCoefficientDamage damage && damage.coefficient is SkillBalanceFloat value && value.key == key) return value;
        throw new InvalidOperationException(data.name + ": no SkillBalanceFloat with key " + key);
    }

    private static T Load<T>(string file) where T : UnityEngine.Object
        => AssetDatabase.LoadAssetAtPath<T>(Folder + file) ?? throw new InvalidOperationException("Missing asset: " + Folder + file);

    private static void Write(PerTierFloat target, float[] v) { target.normal = v[0]; target.rare = v[1]; target.epic = v[2]; target.legend = v[3]; }
    private static void Write(PerTierInt target, float[] v)
    { target.normal = Mathf.RoundToInt(v[0]); target.rare = Mathf.RoundToInt(v[1]); target.epic = Mathf.RoundToInt(v[2]); target.legend = Mathf.RoundToInt(v[3]); }
    private static void Write(SkillBalanceFloat target, float[] v) { target.normal = v[0]; target.rare = v[1]; target.epic = v[2]; target.legend = v[3]; }

    public static string Text(float[] values) => string.Join("|", values.Select(N));
    public static string N(float value) => ((double)(decimal)value).ToString("0.######", CultureInfo.InvariantCulture);
}
